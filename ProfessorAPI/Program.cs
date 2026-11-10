using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using ProfessorAPI.Binding;
using System.Text;
using ProfessorAPI.Connexions;
using ProfessorAPI.Interface;
using ProfessorAPI.Controllers;
using ProfessorAPI.Mcp;
using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.Server;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<BlobService>();
builder.Services.AddTransient<FileController>();
builder.Services.AddControllers(options =>
{
    options.ModelBinderProviders.Insert(0, new FromFormJsonModelBinderProvider());
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

var mysqlConnString = builder.Configuration.GetConnectionString("MySQLConnnection")
    ?? builder.Configuration.GetConnectionString("MySQLConnection")
    ?? builder.Configuration["MYSQL_ADDON_URI"];

builder.Services.AddTransient(sp => new MySqlConnection(mysqlConnString));

if (string.IsNullOrWhiteSpace(mysqlConnString))
{
    throw new InvalidOperationException("A MySQL connection string is required.");
}

builder.Services.AddScoped<InterProfessors, ProfessorsController>();
builder.Services.AddScoped<FileController>();
builder.Services.AddScoped<MyJWT>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AnalyticsTools>();
builder.Services.AddScoped<ClassroomResources>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<AnalyticsTools>()
    .WithResources<ClassroomResources>()
    .WithPrompts<AnalyticsPrompts>();

var configuredOrigins = builder.Configuration["AllowedCorsOrigins"]?
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? new[] { "https://virtual-class-room-peach.vercel.app", "https://localhost:5173", "https://10.0.0.5:5173" };
var allowedOrigins = configuredOrigins
    .Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
    .Append("https://virtual-class-room-peach.vercel.app")
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options => { 
    options.AddPolicy(name: "Policy", 
        policy =>
        { 
            policy.WithOrigins(allowedOrigins)
                  .AllowCredentials()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }); 
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpReadOnly", policy =>
    {
        policy.AddAuthenticationSchemes("McpBearer");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("mcp_access", "read");
    });
});

var mcpIssuer = builder.Configuration["McpOAuth:Issuer"];
var mcpResource = builder.Configuration["McpOAuth:ResourceUrl"];
var allowLoopbackHttp = builder.Environment.IsDevelopment();
var mcpOAuthEnabled = !string.IsNullOrWhiteSpace(mcpIssuer);
if (mcpOAuthEnabled)
{
    if (!Uri.TryCreate(mcpIssuer, UriKind.Absolute, out var issuerUri) ||
        !IsAllowedOAuthUrl(issuerUri, allowLoopbackHttp) ||
        !Uri.TryCreate(mcpResource, UriKind.Absolute, out var resourceUri) ||
        !IsAllowedOAuthUrl(resourceUri, allowLoopbackHttp))
    {
        throw new InvalidOperationException(
            "When configured, McpOAuth:Issuer and McpOAuth:ResourceUrl must be absolute HTTPS URLs (loopback HTTP is allowed in Development).");
    }

    builder.Services.AddOpenIddict()
        .AddValidation(options =>
        {
            options.SetIssuer(issuerUri);
            options.AddAudiences(resourceUri.AbsoluteUri);
            options.UseSystemNetHttp();
            options.UseAspNetCore();
        });
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Bearer";
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme = "Bearer";
})
    .AddJwtBearer("Bearer", opt =>
{
    var jwtKey = builder.Configuration.GetSection("JWTkey").GetValue<string>("key")
        ?? "new-jwt-key-for-classroom-api-in-project";
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

    opt.RequireHttpsMetadata = false;
    opt.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateAudience = false,
        ValidateIssuer = false,
        IssuerSigningKey = signingKey,
    };
    opt.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Cookies["SchoolWebToken506"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            if (context.Principal?.HasClaim("mcp_access", "read") == true)
            {
                context.Fail("MCP access tokens cannot be used with the regular API.");
            }

            return Task.CompletedTask;
        }
    };
})
    .AddScheme<AuthenticationSchemeOptions, McpTokenAuthenticationHandler>("McpBearer", _ => { });

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Policy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization("McpReadOnly");
app.Run();

static bool IsAllowedOAuthUrl(Uri uri, bool allowLoopbackHttp)
{
    return uri.Scheme == Uri.UriSchemeHttps ||
        (allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
}
