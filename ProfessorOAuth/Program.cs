using System.Threading.RateLimiting;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using ProfessorOAuth.Certificates;
using ProfessorOAuth.Configuration;
using ProfessorOAuth.Health;
using ProfessorOAuth.Persistence;
using ProfessorOAuth.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("MySQLConnection")
    ?? builder.Configuration.GetConnectionString("MySQLConnnection")
    ?? builder.Configuration["MYSQL_ADDON_URI"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("A MySQL connection string is required.");
}

builder.Services.AddTransient(_ => new MySqlConnection(connectionString));
builder.Services.AddScoped<IProfessorCredentialService, ProfessorCredentialService>();
builder.Services.AddDbContext<OpenIddictDbContext>(options =>
{
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));
    options.UseOpenIddict();
});

var issuer = builder.Configuration["McpOAuth:Issuer"];
var resource = builder.Configuration["McpOAuth:ResourceUrl"];
var allowLoopbackHttp = builder.Environment.IsDevelopment();
if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) ||
    !IsAllowedOAuthUrl(issuerUri, allowLoopbackHttp) ||
    !Uri.TryCreate(resource, UriKind.Absolute, out var resourceUri) ||
    !IsAllowedOAuthUrl(resourceUri, allowLoopbackHttp))
{
    throw new InvalidOperationException(
        "McpOAuth:Issuer and McpOAuth:ResourceUrl must be absolute HTTPS URLs (loopback HTTP is allowed in Development).");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Configure ASP.NET Core Data Protection for cookie continuity across restarts and instances
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("ProfessorOAuth");

var blobContainerUri = builder.Configuration["DataProtection:BlobContainerUri"];
var keyVaultKeyUri = builder.Configuration["DataProtection:KeyVaultKeyUri"];

if (!string.IsNullOrWhiteSpace(blobContainerUri))
{
    var blobUri = new Uri(new Uri(blobContainerUri.TrimEnd('/') + "/"), "keys.xml");
    var tokenCredential = new DefaultAzureCredential();
    var dpBuilder = dataProtection.PersistKeysToAzureBlobStorage(blobUri, tokenCredential);

    if (!string.IsNullOrWhiteSpace(keyVaultKeyUri))
    {
        dpBuilder.ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyUri), tokenCredential);
    }
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "DataProtection:BlobContainerUri is required in non-Development environments to ensure cookie continuity.");
}

builder.Services.AddAntiforgery();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("McpClientRegistration", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("McpOAuthLogin", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Configure Key Vault options and certificate provider
builder.Services.Configure<KeyVaultOptions>(
    builder.Configuration.GetSection(KeyVaultOptions.SectionName));

OAuthCertificates? loadedCertificates = null;
IOAuthCertificateProvider? certificateProvider = null;

if (!builder.Environment.IsDevelopment() || !string.IsNullOrWhiteSpace(builder.Configuration["KeyVault:VaultUri"]))
{
    certificateProvider = new KeyVaultCertificateProvider(
        Microsoft.Extensions.Options.Options.Create(new KeyVaultOptions
        {
            VaultUri = builder.Configuration["KeyVault:VaultUri"],
            SigningCertificateName = builder.Configuration["KeyVault:SigningCertificateName"] ?? "openiddict-signing",
            EncryptionCertificateName = builder.Configuration["KeyVault:EncryptionCertificateName"] ?? "openiddict-encryption"
        }));

    loadedCertificates = await certificateProvider.LoadCertificatesAsync();
    builder.Services.AddSingleton(loadedCertificates);
    builder.Services.AddSingleton(certificateProvider);
}

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<OpenIddictDbContext>();
    })
    .AddServer(options =>
    {
        options.SetIssuer(issuerUri)
            .SetConfigurationEndpointUris("/.well-known/openid-configuration")
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token");
        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange();
        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.OfflineAccess,
            builder.Configuration["McpOAuth:ReadScope"] ?? "classroom:read",
            builder.Configuration["McpOAuth:WriteScope"] ?? "classroom:write");
        options.Configure(server =>
        {
            server.ClientAuthenticationMethods.Add("none");
            server.CodeChallengeMethods.Clear();
            server.CodeChallengeMethods.Add("S256");
        });
        options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handler =>
            handler.UseInlineHandler(context =>
            {
                context.Metadata["registration_endpoint"] =
                    new Uri(issuerUri, "connect/register").AbsoluteUri;
                return ValueTask.CompletedTask;
            }).SetOrder(int.MaxValue));
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15))
            .SetRefreshTokenLifetime(TimeSpan.FromDays(30))
            .DisableAccessTokenEncryption();

        if (loadedCertificates is not null)
        {
            options.AddSigningCertificate(loadedCertificates.SigningCertificate)
                .AddEncryptionCertificate(loadedCertificates.EncryptionCertificate);
        }
        else if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentSigningCertificate()
                .AddDevelopmentEncryptionCertificate()
                .DisableAccessTokenEncryption();
        }
        else
        {
            throw new InvalidOperationException("Key Vault certificates must be loaded in non-Development environments.");
        }

        var aspNetCore = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
        {
            aspNetCore.DisableTransportSecurityRequirement();
        }
    });

builder.Services.AddAuthentication()
    .AddCookie("McpOAuthCookie", options =>
    {
        options.Cookie.Name = "__Host-McpOAuthSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;
        options.LoginPath = "/api/profes/oauth/login";
    });

builder.Services.AddAuthorization();

builder.Services.AddHealthChecks()
    .AddCheck<KeyVaultCertificateHealthCheck>("certificates")
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Ensure clean disposal of Key Vault certificate material at shutdown
var appLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
if (certificateProvider is not null)
{
    appLifetime.ApplicationStopping.Register(() =>
    {
        certificateProvider.Dispose();
    });
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false, // Basic liveness ping
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { status = "Healthy" });
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true, // Full readiness check (certificates + database)
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString()
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapControllers();
app.Run();

static bool IsAllowedOAuthUrl(Uri uri, bool allowLoopbackHttp)
{
    return uri.Scheme == Uri.UriSchemeHttps ||
        (allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
}

public partial class Program { }
