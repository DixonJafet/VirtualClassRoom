using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using OpenIddict.Abstractions;

namespace ProfessorAPI.Mcp;

public sealed class McpTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string OpenIddictValidationScheme = "OpenIddict.Validation.AspNetCore";
    private readonly MySqlConnection _connection;
    private readonly IConfiguration _configuration;

    public McpTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        MySqlConnection connection,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _connection = connection;
        _configuration = configuration;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization["Bearer ".Length..].Trim();
        if (token.Length is < 32 or > 16_384)
        {
            return AuthenticateResult.Fail("Invalid MCP access token.");
        }

        if (token.Count(character => character == '.') == 2)
        {
            if (string.IsNullOrWhiteSpace(_configuration["McpOAuth:Issuer"]))
            {
                return AuthenticateResult.Fail("MCP OAuth is not configured on this API.");
            }

            var validation = await Context.AuthenticateAsync(OpenIddictValidationScheme);
            if (!validation.Succeeded || validation.Principal is null)
            {
                Logger.LogInformation("OpenIddict rejected an MCP OAuth access token.");
                return AuthenticateResult.Fail("Invalid or unauthorized MCP OAuth access token.");
            }

            var principal = validation.Principal;
            var readScope = _configuration["McpOAuth:ReadScope"] ?? "classroom:read";
            var writeScope = _configuration["McpOAuth:WriteScope"] ?? "classroom:write";
            var scopes = principal.GetScopes();
            var subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
            if (string.IsNullOrWhiteSpace(subject) ||
                !scopes.Contains(readScope, StringComparer.Ordinal))
            {
                return AuthenticateResult.Fail("The OAuth token is missing the required MCP identity or read scope.");
            }

            var professor = await _connection.QuerySingleOrDefaultAsync<ProfessorIdentity>(
                """
                SELECT professor_id AS ProfessorId
                FROM professors
                WHERE professor_id = @subject
                LIMIT 1
                """,
                new { subject });
            if (string.IsNullOrWhiteSpace(professor?.ProfessorId))
            {
                return AuthenticateResult.Fail("The OAuth subject is not linked to a professor account.");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, professor.ProfessorId),
                new("mcp_access", "read")
            };
            if (scopes.Contains(writeScope, StringComparer.Ordinal))
            {
                claims.Add(new Claim("mcp_access", "write"));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        if (token.Length > 256)
        {
            return AuthenticateResult.Fail("Invalid MCP access token.");
        }

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var tokenAccess = await _connection.QuerySingleOrDefaultAsync<McpTokenAccess>(
            """
            SELECT professor_id AS ProfessorId, access_level AS AccessLevel
            FROM mcp_access_tokens
            WHERE token_hash = @tokenHash AND expires_at > UTC_TIMESTAMP()
            LIMIT 1
            """,
            new { tokenHash });

        if (string.IsNullOrEmpty(tokenAccess?.ProfessorId) ||
            (tokenAccess.AccessLevel != "read" && tokenAccess.AccessLevel != "write"))
        {
            return AuthenticateResult.Fail("Invalid or expired MCP access token.");
        }

        var legacyClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, tokenAccess.ProfessorId),
            new("mcp_access", "read")
        };
        if (tokenAccess.AccessLevel == "write")
        {
            legacyClaims.Add(new Claim("mcp_access", "write"));
        }

        var legacyIdentity = new ClaimsIdentity(legacyClaims, Scheme.Name);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(legacyIdentity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        var metadataUrl = _configuration["McpOAuth:MetadataUrl"];
        if (!string.IsNullOrWhiteSpace(_configuration["McpOAuth:Issuer"]) &&
            !string.IsNullOrWhiteSpace(metadataUrl))
        {
            Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{metadataUrl}\"";
        }

        return Task.CompletedTask;
    }

    private sealed record McpTokenAccess(string ProfessorId, string AccessLevel);
    private sealed record ProfessorIdentity(string ProfessorId);
}
