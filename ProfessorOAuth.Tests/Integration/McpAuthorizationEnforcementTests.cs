extern alias Api;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Api::ProfessorAPI.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using OpenIddict.Abstractions;

namespace ProfessorOAuth.Tests.Integration;

public class McpAuthorizationEnforcementTests
{
    [Theory]
    [InlineData(true, true, true, HttpStatusCode.OK)]
    [InlineData(true, false, true, HttpStatusCode.Forbidden)]
    [InlineData(false, true, true, HttpStatusCode.Unauthorized)]
    [InlineData(true, true, false, HttpStatusCode.Unauthorized)]
    public async Task McpEndpoint_EnforcesActiveGrantAndReadScope(
        bool activeGrant, bool readScope, bool validToken, HttpStatusCode expected)
    {
        var store = new TestAuthorizationStore { Active = activeGrant };
        using var server = CreateServer(store);
        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new string('a', 32) + ".b.c");
        if (!readScope) request.Headers.Add("X-Test-No-Read", "1");
        if (!validToken) request.Headers.Add("X-Test-Invalid", "1");

        var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (validToken)
        {
            Assert.Equal("professor-1", store.Subject);
            Assert.Equal("grant-1", store.AuthorizationId);
        }
    }

    [Fact]
    public async Task McpEndpoint_WithoutBearer_ReturnsUnauthorized()
    {
        using var server = CreateServer(new TestAuthorizationStore { Active = true });
        using var client = server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/mcp")).StatusCode);
    }

    [Fact]
    public async Task McpEndpoint_WithoutGrantClaim_ReturnsUnauthorized()
    {
        var store = new TestAuthorizationStore { Active = true };
        using var server = CreateServer(store);
        using var client = server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new string('a', 32) + ".b.c");
        request.Headers.Add("X-Test-No-Grant", "1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        Assert.Null(store.AuthorizationId);
    }

    private static TestServer CreateServer(TestAuthorizationStore store)
    {
        var builder = new WebHostBuilder()
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["McpOAuth:Issuer"] = "https://issuer.example/" }))
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton<IMcpOAuthAuthorizationStore>(store);
                services.AddTransient(_ => new MySqlConnection("Server=localhost;Database=test;User=test;Password=test"));
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, FakeOpenIddictHandler>("OpenIddict.Validation.AspNetCore", _ => { })
                    .AddScheme<AuthenticationSchemeOptions, McpTokenAuthenticationHandler>("McpBearer", _ => { });
                services.AddAuthorization(options => options.AddPolicy("McpReadOnly", policy =>
                {
                    policy.AddAuthenticationSchemes("McpBearer");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("mcp_access", "read");
                }));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                    endpoints.MapGet("/mcp", context => context.Response.WriteAsync("ok"))
                        .RequireAuthorization("McpReadOnly"));
            });
        return new TestServer(builder);
    }

    private sealed class TestAuthorizationStore : IMcpOAuthAuthorizationStore
    {
        public bool Active { get; set; }
        public string? Subject { get; private set; }
        public string? AuthorizationId { get; private set; }

        public Task<string?> FindActiveProfessorAsync(string subject, string authorizationId)
        {
            Subject = subject;
            AuthorizationId = authorizationId;
            return Task.FromResult(Active ? subject : null);
        }
    }

    private sealed class FakeOpenIddictHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Test-Invalid"))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test token."));
            }
            var identity = new ClaimsIdentity(Scheme.Name);
            identity.SetClaim(OpenIddictConstants.Claims.Subject, "professor-1");
            if (!Request.Headers.ContainsKey("X-Test-No-Grant"))
            {
                identity.SetClaim("mcp_authorization_id", "grant-1");
            }
            if (!Request.Headers.ContainsKey("X-Test-No-Read"))
            {
                identity.SetScopes("classroom:read");
            }
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
