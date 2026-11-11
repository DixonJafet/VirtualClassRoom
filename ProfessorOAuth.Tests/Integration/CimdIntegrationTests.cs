using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using ProfessorOAuth.Mcp;
using ProfessorOAuth.Persistence;
using ProfessorOAuth.Services;

namespace ProfessorOAuth.Tests.Integration;

public class CimdIntegrationTests
{
    private const string ClientId = "https://client.example/metadata.json";
    private const string RedirectUri = "https://client.example/callback";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Fact]
    public async Task Discovery_AdvertisesCimdWhenEnabled()
    {
        await using var factory = CreateFactory(new MetadataHandler());
        using var client = CreateClient(factory);
        using var document = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration"));
        Assert.True(document.RootElement.GetProperty("client_id_metadata_document_supported").GetBoolean());
        var methods = document.RootElement.GetProperty("token_endpoint_auth_methods_supported");
        Assert.Equal("none", Assert.Single(methods.EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Authorization_InvalidMetadata_DoesNotRegisterClient()
    {
        var metadata = new MetadataHandler { ClientIdentifier = "https://wrong.example/metadata.json" };
        await using var factory = CreateFactory(metadata);
        using var client = CreateClient(factory);
        var response = await client.GetAsync(AuthorizationUrl(RedirectUri));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        Assert.Null(await applications.FindByClientIdAsync(ClientId));
    }

    [Fact]
    public async Task Authorization_RejectsMissingPkce()
    {
        await using var factory = CreateFactory(new MetadataHandler());
        using var client = CreateClient(factory);
        var url = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "classroom:read",
            ["resource"] = "https://classroomapi.runasp.net/mcp"
        });
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error:invalid_request", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        Assert.Null(await applications.FindByClientIdAsync(ClientId));
    }

    [Fact]
    public async Task Management_RequiresLogin()
    {
        await using var factory = CreateFactory(new MetadataHandler());
        using var client = CreateClient(factory);
        var response = await client.GetAsync("/connect/authorizations");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var login = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Authorization_FetchesMetadataAndRejectsChangedRedirect()
    {
        var metadata = new MetadataHandler();
        await using var factory = CreateFactory(metadata);
        using var client = CreateClient(factory);
        var first = await client.GetAsync(AuthorizationUrl(RedirectUri));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.StartsWith("/api/profes/oauth/login?", first.Headers.Location!.OriginalString);

        metadata.RedirectUri = "https://client.example/new-callback";
        var oldRedirect = await client.GetAsync(AuthorizationUrl(RedirectUri));
        Assert.Equal(HttpStatusCode.BadRequest, oldRedirect.StatusCode);

        var caseMismatch = await client.GetAsync(AuthorizationUrl("https://client.example/NEW-callback"));
        Assert.Equal(HttpStatusCode.BadRequest, caseMismatch.StatusCode);

        var newRedirect = await client.GetAsync(AuthorizationUrl(metadata.RedirectUri));
        Assert.Equal(HttpStatusCode.Redirect, newRedirect.StatusCode);
        Assert.StartsWith("/api/profes/oauth/login?", newRedirect.Headers.Location!.OriginalString);
        Assert.Equal(4, metadata.RequestCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Authorization_CodeAndRefreshFlow_UsesPkce(bool useCimd)
    {
        await using var factory = CreateFactory(new MetadataHandler(), useCimd);
        using var client = CreateClient(factory);
        var clientId = ClientId;
        if (!useCimd)
        {
            var registration = await client.PostAsJsonAsync("/connect/register", new
            {
                client_name = "Test DCR client",
                redirect_uris = new[] { RedirectUri },
                grant_types = new[] { "authorization_code", "refresh_token" },
                response_types = new[] { "code" },
                token_endpoint_auth_method = "none"
            });
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            using var registrationJson = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
            clientId = registrationJson.RootElement.GetProperty("client_id").GetString()!;
        }
        var authorizationUrl = AuthorizationUrl(RedirectUri, clientId);
        var authorization = await client.GetAsync(authorizationUrl);
        Assert.Equal(HttpStatusCode.Redirect, authorization.StatusCode);

        var login = await client.GetAsync(authorization.Headers.Location);
        var loginHtml = await login.Content.ReadAsStringAsync();
        var loginResponse = await client.PostAsync("/api/profes/oauth/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["returnUrl"] = authorizationUrl,
                ["email"] = "test@example.test",
                ["password"] = "test-only",
                ["__RequestVerificationToken"] = HiddenValue(loginHtml, "__RequestVerificationToken")
            }));
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var consent = await client.GetAsync(loginResponse.Headers.Location);
        var consentHtml = await consent.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, consent.StatusCode);
        Assert.Equal(
            "default-src 'none'; style-src 'unsafe-inline'; form-action 'self' https://client.example; base-uri 'none'; frame-ancestors 'none'",
            consent.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains(useCimd ? "Test CIMD client" : "Test DCR client", consentHtml);
        Assert.Contains("Maintain access when you are not signed in", consentHtml);
        var consentFields = QueryHelpers.ParseQuery(new Uri("https://test-oauth.azurewebsites.net" + authorizationUrl).Query)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        consentFields["consent"] = "approve";
        consentFields["__RequestVerificationToken"] = HiddenValue(consentHtml, "__RequestVerificationToken");
        foreach (var field in consentFields.Keys.Where(key => key != "consent"))
        {
            Assert.Equal(consentFields[field], HiddenValue(consentHtml, field));
        }
        var consentResponse = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(consentFields));
        Assert.Equal(HttpStatusCode.Redirect, consentResponse.StatusCode);
        var parameters = QueryHelpers.ParseQuery(consentResponse.Headers.Location!.Query);
        Assert.True(parameters.TryGetValue("code", out var code));

        if (useCimd)
        {
            var unsupported = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = "not-a-token"
                }));
            Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
            Assert.Contains("unsupported_grant_type", await unsupported.Content.ReadAsStringAsync());
        }

        var badVerifier = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code.ToString(),
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = new string('a', 43),
                ["resource"] = "https://classroomapi.runasp.net/mcp"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, badVerifier.StatusCode);

        var token = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code.ToString(),
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = Verifier,
                ["resource"] = "https://classroomapi.runasp.net/mcp"
            }));
        var tokenBody = await token.Content.ReadAsStringAsync();
        Assert.True(token.StatusCode == HttpStatusCode.OK, tokenBody);
        using var tokenJson = JsonDocument.Parse(tokenBody);
        var accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()!;
        using var accessPayload = JsonDocument.Parse(WebEncoders.Base64UrlDecode(accessToken.Split('.')[1]));
        var authorizationId = accessPayload.RootElement.GetProperty("mcp_authorization_id").GetString()!;
        Assert.Equal("test-professor", accessPayload.RootElement.GetProperty("sub").GetString());
        Assert.Equal("https://classroomapi.runasp.net/mcp", accessPayload.RootElement.GetProperty("aud").GetString());
        string otherAuthorizationId;
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorizationEntry = await manager.FindByIdAsync(authorizationId);
            Assert.NotNull(authorizationEntry);
            Assert.True(await manager.HasStatusAsync(authorizationEntry, OpenIddictConstants.Statuses.Valid));
            Assert.Contains("classroom:read", await manager.GetScopesAsync(authorizationEntry));
            var otherEntry = await manager.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                ApplicationId = await manager.GetApplicationIdAsync(authorizationEntry),
                Subject = "another-professor",
                Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent
            });
            otherAuthorizationId = (await manager.GetIdAsync(otherEntry))!;
        }
        var refresh = tokenJson.RootElement.GetProperty("refresh_token").GetString()!;

        var refreshForm = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["resource"] = "https://classroomapi.runasp.net/mcp"
        };
        var refreshed = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));
        var refreshedBody = await refreshed.Content.ReadAsStringAsync();
        Assert.True(refreshed.StatusCode == HttpStatusCode.OK, refreshedBody);
        using var refreshedJson = JsonDocument.Parse(refreshedBody);
        var newRefresh = refreshedJson.RootElement.GetProperty("refresh_token").GetString()!;
        Assert.NotEqual(refresh, newRefresh);
        var reused = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);

        var management = await client.GetAsync("/connect/authorizations");
        Assert.Equal(HttpStatusCode.OK, management.StatusCode);
        var managementHtml = await management.Content.ReadAsStringAsync();
        Assert.Equal(authorizationId, HiddenValue(managementHtml, "authorizationId"));
        Assert.DoesNotContain(otherAuthorizationId, managementHtml);
        var tokenValue = HiddenValue(managementHtml, "__RequestVerificationToken");
        var crossUser = await client.PostAsync("/connect/authorizations/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["authorizationId"] = otherAuthorizationId,
                ["__RequestVerificationToken"] = tokenValue
            }));
        Assert.Equal(HttpStatusCode.NotFound, crossUser.StatusCode);
        var noCsrf = await client.PostAsync("/connect/authorizations/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["authorizationId"] = authorizationId }));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);

        var revoked = await client.PostAsync("/connect/authorizations/revoke", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["authorizationId"] = authorizationId,
                ["__RequestVerificationToken"] = tokenValue
            }));
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorizationEntry = await manager.FindByIdAsync(authorizationId);
            Assert.NotNull(authorizationEntry);
            Assert.True(await manager.HasStatusAsync(authorizationEntry, OpenIddictConstants.Statuses.Revoked));
        }
        refreshForm["refresh_token"] = newRefresh;
        var revokedRefresh = await client.PostAsync("/connect/token", new FormUrlEncodedContent(refreshForm));
        Assert.Equal(HttpStatusCode.BadRequest, revokedRefresh.StatusCode);
    }

    private static string AuthorizationUrl(string redirect, string clientId = ClientId) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirect,
            ["response_type"] = "code",
            ["scope"] = "classroom:read offline_access",
            ["resource"] = "https://classroomapi.runasp.net/mcp",
            ["code_challenge"] = Challenge,
            ["code_challenge_method"] = "S256"
        });

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\" value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Missing {name}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://test-oauth.azurewebsites.net/")
        });

    private static WebApplicationFactory<Program> CreateFactory(MetadataHandler metadata, bool enableCimd = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            var databaseName = "CimdTests_" + Guid.NewGuid();
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:MySQLConnection", "Server=localhost;Database=test;User=test;Password=test;");
            builder.UseSetting("McpOAuth:Issuer", "https://test-oauth.azurewebsites.net/");
            builder.UseSetting("McpOAuth:ResourceUrl", "https://classroomapi.runasp.net/mcp");
            builder.UseSetting("McpOAuth:EnableCimd", enableCimd ? "true" : "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<OpenIddictDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<OpenIddictDbContext>>();
                services.AddDbContext<OpenIddictDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName);
                    options.UseOpenIddict();
                });
                services.RemoveAll<CimdMetadataFetcher>();
                services.AddSingleton(new CimdMetadataFetcher(new HttpClient(metadata)));
                services.RemoveAll<IProfessorCredentialService>();
                services.AddSingleton<IProfessorCredentialService, TestCredentials>();
            });
        });

    private sealed class TestCredentials : IProfessorCredentialService
    {
        public Task<ProfessorAccount?> AuthenticateAsync(string email, string password) =>
            Task.FromResult<ProfessorAccount?>(new ProfessorAccount("test-professor", "Test Professor"));
    }

    private sealed class MetadataHandler : HttpMessageHandler
    {
        public string RedirectUri { get; set; } = CimdIntegrationTests.RedirectUri;
        public string ClientIdentifier { get; set; } = ClientId;
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var body = JsonSerializer.Serialize(new
            {
                client_id = ClientIdentifier,
                client_name = "Test CIMD client",
                redirect_uris = new[] { RedirectUri },
                grant_types = new[] { "authorization_code", "refresh_token", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                response_types = new[] { "code" },
                token_endpoint_auth_method = "private_key_jwt",
                token_endpoint_auth_methods_supported = new[] { "none", "private_key_jwt" },
                jwks_uri = "https://client.example/oauth/jwks.json"
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
