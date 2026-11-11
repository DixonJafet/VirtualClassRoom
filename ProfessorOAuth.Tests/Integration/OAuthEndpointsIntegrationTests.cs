using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProfessorOAuth.Persistence;

namespace ProfessorOAuth.Tests.Integration;

public class OAuthEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var databaseName = "OpenIddictTests_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:MySQLConnection", "Server=localhost;Database=test;User=test;Password=test;");
            builder.UseSetting("McpOAuth:Issuer", "https://test-oauth.azurewebsites.net/");
            builder.UseSetting("McpOAuth:ResourceUrl", "https://classroomapi.runasp.net/mcp");

            builder.ConfigureServices(services =>
            {
                // Replace EF Core with in-memory database for testing
                services.RemoveAll<DbContextOptions<OpenIddictDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<OpenIddictDbContext>>();
                services.AddDbContext<OpenIddictDbContext>(options =>
                {
                    options.UseInMemoryDatabase(databaseName);
                    options.UseOpenIddict();
                });
            });
        });
    }

    [Fact]
    public async Task GetHealth_Returns200WithHealthyStatus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetOpenIdConfiguration_ReturnsExpectedMetadataAndS256()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://test-oauth.azurewebsites.net/", root.GetProperty("issuer").GetString());
        Assert.True(root.TryGetProperty("authorization_endpoint", out var authEp));
        Assert.True(root.TryGetProperty("token_endpoint", out var tokenEp));
        Assert.True(root.TryGetProperty("jwks_uri", out var jwksUri));
        Assert.True(root.TryGetProperty("registration_endpoint", out var regEp));
        Assert.False(root.TryGetProperty("client_id_metadata_document_supported", out _));

        // Verify PKCE S256 is supported
        var codeChallengeMethods = root.GetProperty("code_challenge_methods_supported")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Contains("S256", codeChallengeMethods);

        // Verify scopes include classroom:read and classroom:write
        var scopes = root.GetProperty("scopes_supported")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        Assert.Contains("classroom:read", scopes);
        Assert.Contains("classroom:write", scopes);
    }

    [Fact]
    public async Task GetJwks_DoesNotContainPrivateKeyParameters()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/.well-known/jwks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.GetProperty("keys");

        Assert.True(keys.GetArrayLength() > 0);
        foreach (var key in keys.EnumerateArray())
        {
            // Verify public key components exist (e.g., kty, n, e)
            Assert.True(key.TryGetProperty("kty", out _));

            // CRITICAL: Verify NO private key fields exist!
            Assert.False(key.TryGetProperty("d", out _), "JWKS must never expose private key exponent 'd'.");
            Assert.False(key.TryGetProperty("p", out _), "JWKS must never expose private prime 'p'.");
            Assert.False(key.TryGetProperty("q", out _), "JWKS must never expose private prime 'q'.");
            Assert.False(key.TryGetProperty("dp", out _), "JWKS must never expose private key parameter 'dp'.");
            Assert.False(key.TryGetProperty("dq", out _), "JWKS must never expose private key parameter 'dq'.");
            Assert.False(key.TryGetProperty("qi", out _), "JWKS must never expose private key parameter 'qi'.");
        }
    }

    [Fact]
    public async Task DynamicClientRegistration_WithInvalidRedirectUri_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        // Registration with non-https and fragment URI (which is invalid per OAuth 2.0 / OpenID Connect)
        var invalidPayload = new
        {
            client_name = "Test Client",
            redirect_uris = new[] { "http://insecure-domain.com/callback#fragment" },
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" }
        };

        var response = await client.PostAsJsonAsync("/connect/register", invalidPayload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("https://classroomapi.runasp.net/mcp", true)]
    [InlineData("https://other.example/mcp", false)]
    public async Task Authorization_ValidatesRegisteredResource(string resource, bool allowed)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://test-oauth.azurewebsites.net/")
        });
        var registration = await client.PostAsJsonAsync("/connect/register", new
        {
            client_name = "Resource validation test",
            redirect_uris = new[] { "https://client.example/callback" },
            grant_types = new[] { "authorization_code" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        });
        var registrationBody = await registration.Content.ReadAsStringAsync();
        Assert.True(registration.StatusCode == HttpStatusCode.Created,
            $"Client registration returned {registration.StatusCode}: {registrationBody}");
        using var registered = JsonDocument.Parse(registrationBody);
        var clientId = registered.RootElement.GetProperty("client_id").GetString();
        var url = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = "https://client.example/callback",
            ["response_type"] = "code",
            ["scope"] = "classroom:read",
            ["resource"] = resource,
            ["code_challenge"] = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            ["code_challenge_method"] = "S256"
        });

        var response = await client.GetAsync(url);
        if (allowed)
        {
            Assert.True(response.StatusCode == HttpStatusCode.Redirect,
                $"Authorization returned {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            var location = response.Headers.Location;
            Assert.NotNull(location);
            Assert.StartsWith("/api/profes/oauth/login?", location.OriginalString);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("error:invalid_target", await response.Content.ReadAsStringAsync());
        }
    }
}
