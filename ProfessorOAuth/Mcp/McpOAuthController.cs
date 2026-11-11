using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace ProfessorOAuth.Mcp;

[ApiController]
[AllowAnonymous]
public sealed class McpOAuthController : ControllerBase
{
    private const string LoginCookieScheme = "McpOAuthCookie";

    private readonly IConfiguration _configuration;
    private readonly IOpenIddictApplicationManager _applications;
    private readonly IOpenIddictAuthorizationManager _authorizations;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<McpOAuthController> _logger;

    public McpOAuthController(
        IConfiguration configuration,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        IAntiforgery antiforgery,
        ILogger<McpOAuthController> logger)
    {
        _configuration = configuration;
        _applications = applications;
        _authorizations = authorizations;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    [HttpGet("~/connect/authorize")]
    [EnableRateLimiting("McpAuthorization")]
    public Task<IActionResult> Authorize()
    {
        return ProcessAuthorizationAsync(consentSubmitted: false);
    }

    [HttpPost("~/connect/authorize")]
    [EnableRateLimiting("McpAuthorization")]
    [Consumes("application/x-www-form-urlencoded")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AuthorizePost()
    {
        return ProcessAuthorizationAsync(consentSubmitted: true);
    }

    private async Task<IActionResult> ProcessAuthorizationAsync(bool consentSubmitted)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict authorization request is unavailable.");
        var authentication = await HttpContext.AuthenticateAsync(LoginCookieScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            if (consentSubmitted)
            {
                return Unauthorized("Your sign-in session expired. Restart authorization from your client.");
            }
            var returnUrl = Request.PathBase + Request.Path + Request.QueryString;
            return Redirect("/api/profes/oauth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        }

        var professorId = authentication.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(professorId))
        {
            await HttpContext.SignOutAsync(LoginCookieScheme);
            return Unauthorized();
        }

        var readScope = _configuration["McpOAuth:ReadScope"] ?? "classroom:read";
        var writeScope = _configuration["McpOAuth:WriteScope"] ?? "classroom:write";
        var requestedScopes = request.GetScopes();
        if (!requestedScopes.Contains(readScope, StringComparer.Ordinal))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        var resources = request.GetResources();
        var mcpResource = _configuration["McpOAuth:ResourceUrl"]!;
        if (resources.Any(resource => !string.Equals(resource, mcpResource, StringComparison.Ordinal)))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (!consentSubmitted)
        {
            return await ConsentPageAsync(request);
        }

        var form = await Request.ReadFormAsync();
        if (!string.Equals(form["consent"], "approve", StringComparison.Ordinal))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, professorId);
        identity.SetClaim(OpenIddictConstants.Claims.Name,
            authentication.Principal.FindFirst(ClaimTypes.Name)?.Value);
        identity.SetClaim(OpenIddictConstants.Claims.Email,
            authentication.Principal.FindFirst(ClaimTypes.Email)?.Value);
        identity.AddClaim(new Claim("mcp_access", "read"));
        if (requestedScopes.Contains(writeScope, StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim("mcp_access", "write"));
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(requestedScopes);
        principal.SetResources(mcpResource);
        var application = await _applications.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("The validated OAuth client was not found.");
        var applicationId = await _applications.GetIdAsync(application)
            ?? throw new InvalidOperationException("The validated OAuth client has no identifier.");
        var authorizationDescriptor = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = applicationId,
            Subject = professorId,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.Permanent
        };
        authorizationDescriptor.Scopes.UnionWith(requestedScopes);
        var authorization = await _authorizations.CreateAsync(authorizationDescriptor);
        var authorizationId = await _authorizations.GetIdAsync(authorization)
            ?? throw new InvalidOperationException("The OAuth authorization has no identifier.");
        principal.SetAuthorizationId(authorizationId);
        identity.SetClaim("mcp_authorization_id", authorizationId);
        principal.SetDestinations(static claim => claim.Type switch
        {
            OpenIddictConstants.Claims.Subject or "mcp_access" or "mcp_authorization_id" =>
                [OpenIddictConstants.Destinations.AccessToken],
            OpenIddictConstants.Claims.Name when claim.Subject?.HasScope(OpenIddictConstants.Scopes.Profile) == true =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Email when claim.Subject?.HasScope(OpenIddictConstants.Scopes.Email) == true =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            _ => []
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private async Task<IActionResult> ConsentPageAsync(OpenIddictRequest request)
    {
        var application = await _applications.FindByClientIdAsync(request.ClientId!);
        var clientName = application is null
            ? "MCP client"
            : await _applications.GetDisplayNameAsync(application) ?? "MCP client";
        var requestedScopes = request.GetScopes();
        var readScope = _configuration["McpOAuth:ReadScope"] ?? "classroom:read";
        var writeScope = _configuration["McpOAuth:WriteScope"] ?? "classroom:write";
        var permissions = new List<string>();
        if (requestedScopes.Contains(readScope, StringComparer.Ordinal))
        {
            permissions.Add("Read your classrooms, evaluations, and grades");
        }
        if (requestedScopes.Contains(writeScope, StringComparer.Ordinal))
        {
            permissions.Add("Create or edit classrooms, evaluations, and grades");
        }
        if (requestedScopes.Contains(OpenIddictConstants.Scopes.Profile, StringComparer.Ordinal))
        {
            permissions.Add("Read your name");
        }
        if (requestedScopes.Contains(OpenIddictConstants.Scopes.Email, StringComparer.Ordinal))
        {
            permissions.Add("Read your email address");
        }
        if (requestedScopes.Contains(OpenIddictConstants.Scopes.OfflineAccess, StringComparer.Ordinal))
        {
            permissions.Add("Maintain access when you are not signed in");
        }

        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        var encodedAction = HtmlEncoder.Default.Encode(Request.PathBase + Request.Path);
        var encodedClientName = HtmlEncoder.Default.Encode(clientName);
        var encodedClientId = HtmlEncoder.Default.Encode(request.ClientId ?? string.Empty);
        var encodedToken = HtmlEncoder.Default.Encode(tokens.RequestToken ?? string.Empty);
        var requestFields = string.Join(
            Environment.NewLine,
            Request.Query
                .Where(parameter => !string.Equals(parameter.Key, "consent", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parameter.Key, "__RequestVerificationToken", StringComparison.OrdinalIgnoreCase))
                .SelectMany(parameter => parameter.Value.Select(value =>
                    $"<input type=\"hidden\" name=\"{HtmlEncoder.Default.Encode(parameter.Key)}\" value=\"{HtmlEncoder.Default.Encode(value ?? string.Empty)}\">")));
        var permissionItems = string.Join(
            Environment.NewLine,
            permissions.Select(permission => $"<li>{HtmlEncoder.Default.Encode(permission)}</li>"));
        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Authorize {{encodedClientName}}</title>
              <style>
                body { font: 16px system-ui, sans-serif; max-width: 32rem; margin: 4rem auto; padding: 0 1rem; }
                button { font: inherit; margin: 1rem 1rem 0 0; padding: .65rem 1rem; cursor: pointer; }
              </style>
            </head>
            <body>
              <main>
                <h1>Authorize {{encodedClientName}}?</h1>
                <p>Client identifier: {{encodedClientId}}</p>
                <p>This client is requesting access to your MyClassRoom account:</p>
                <ul>{{permissionItems}}</ul>
                <form method="post" action="{{encodedAction}}">
                  <input type="hidden" name="__RequestVerificationToken" value="{{encodedToken}}">
                  {{requestFields}}
                  <button type="submit" name="consent" value="approve">Allow</button>
                  <button type="submit" name="consent" value="deny">Deny</button>
                </form>
                <p><a href="/connect/authorizations">Manage authorized clients</a></p>
              </main>
            </body>
            </html>
            """;

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var redirectUri = new Uri(request.RedirectUri
            ?? throw new InvalidOperationException("The validated OAuth redirect URI is unavailable."), UriKind.Absolute);
        var redirectOrigin = redirectUri.GetLeftPart(UriPartial.Authority);
        Response.Headers["Content-Security-Policy"] =
            $"default-src 'none'; style-src 'unsafe-inline'; form-action 'self' {redirectOrigin}; base-uri 'none'; frame-ancestors 'none'";
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenIddict token request is unavailable.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            return BadRequest(new
            {
                error = OpenIddictConstants.Errors.UnsupportedGrantType,
                error_description = "Only authorization_code and refresh_token grants are supported."
            });
        }

        var mcpResource = _configuration["McpOAuth:ResourceUrl"]!;
        if (request.GetResources().Any(resource =>
                !string.Equals(resource, mcpResource, StringComparison.Ordinal)))
        {
            return BadRequest(new
            {
                error = OpenIddictConstants.Errors.InvalidTarget,
                error_description = "The requested resource is not supported."
            });
        }

        var authentication = await HttpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            _logger.LogWarning("OpenIddict rejected a token exchange without a valid authorization principal.");
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (!authentication.Principal.GetResources().Contains(mcpResource, StringComparer.Ordinal))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return SignIn(
            authentication.Principal,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/register")]
    [EnableRateLimiting("McpClientRegistration")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<IActionResult> RegisterClient([FromBody] ClientRegistrationRequest request)
    {
        if (request.RedirectUris is not { Length: > 0 and <= 20 } ||
            request.RedirectUris.Any(uri => !IsAllowedRedirectUri(uri)))
        {
            return BadRequest(new
            {
                error = "invalid_redirect_uri",
                error_description = "Provide 1 to 20 HTTPS or loopback redirect URIs."
            });
        }

        var grantTypes = request.GrantTypes ?? ["authorization_code", "refresh_token"];
        var responseTypes = request.ResponseTypes ?? ["code"];
        if (!grantTypes.Contains("authorization_code", StringComparer.Ordinal) ||
            grantTypes.Any(grant => grant is not ("authorization_code" or "refresh_token")) ||
            grantTypes.Distinct(StringComparer.Ordinal).Count() != grantTypes.Length ||
            responseTypes.Length != 1 ||
            !string.Equals(responseTypes[0], "code", StringComparison.Ordinal) ||
            (request.TokenEndpointAuthMethod is not null &&
             !string.Equals(request.TokenEndpointAuthMethod, "none", StringComparison.Ordinal)))
        {
            return BadRequest(new
            {
                error = "invalid_client_metadata",
                error_description = "Only public authorization-code clients using the code response type are supported."
            });
        }

        var clientId = Guid.NewGuid().ToString("N");
        var readScope = _configuration["McpOAuth:ReadScope"] ?? "classroom:read";
        var writeScope = _configuration["McpOAuth:WriteScope"] ?? "classroom:write";
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = string.IsNullOrWhiteSpace(request.ClientName)
                ? "MCP client"
                : request.ClientName.Trim()[..Math.Min(request.ClientName.Trim().Length, 100)]
        };
        foreach (var uri in request.RedirectUris!)
        {
            descriptor.RedirectUris.Add(new Uri(uri, UriKind.Absolute));
        }

        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess,
            OpenIddictConstants.Permissions.Prefixes.Scope + readScope,
            OpenIddictConstants.Permissions.Prefixes.Scope + writeScope
        ]);
        if (grantTypes.Contains("refresh_token", StringComparer.Ordinal))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        }
        descriptor.Requirements.Add(
            OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        await _applications.CreateAsync(descriptor);

        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status201Created, new
        {
            client_id = clientId,
            client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            redirect_uris = request.RedirectUris,
            grant_types = grantTypes,
            response_types = responseTypes,
            token_endpoint_auth_method = "none",
            client_name = descriptor.DisplayName
        });
    }

    private static bool IsAllowedRedirectUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrWhiteSpace(uri.Host)) ||
            (uri.Scheme == Uri.UriSchemeHttp &&
             !string.IsNullOrWhiteSpace(uri.Host) &&
             (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
              System.Net.IPAddress.TryParse(uri.Host, out var address) &&
              System.Net.IPAddress.IsLoopback(address)));
    }

    public sealed class ClientRegistrationRequest
    {
        [JsonPropertyName("redirect_uris")]
        public string[]? RedirectUris { get; set; }

        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("grant_types")]
        public string[]? GrantTypes { get; set; }

        [JsonPropertyName("response_types")]
        public string[]? ResponseTypes { get; set; }

        [JsonPropertyName("token_endpoint_auth_method")]
        public string? TokenEndpointAuthMethod { get; set; }
    }
}
