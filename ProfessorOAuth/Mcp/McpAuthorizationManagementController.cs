using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace ProfessorOAuth.Mcp;

[Authorize(AuthenticationSchemes = "McpOAuthCookie")]
public sealed class McpAuthorizationManagementController(
    IOpenIddictAuthorizationManager authorizations,
    IOpenIddictApplicationManager applications,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("~/connect/authorizations")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Unauthorized();
        }

        var token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
            ?? throw new InvalidOperationException("The antiforgery token was not created.");
        var entries = new StringBuilder();
        await foreach (var authorization in authorizations.FindBySubjectAsync(subject, cancellationToken))
        {
            if (!await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, cancellationToken) ||
                !string.Equals(await authorizations.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal))
            {
                continue;
            }

            var applicationId = await authorizations.GetApplicationIdAsync(authorization, cancellationToken);
            var application = applicationId is null ? null : await applications.FindByIdAsync(applicationId, cancellationToken);
            var name = application is null ? "Unknown client" : await applications.GetDisplayNameAsync(application, cancellationToken) ?? "MCP client";
            var clientId = application is null ? "" : await applications.GetClientIdAsync(application, cancellationToken) ?? "";
            var authorizationId = await authorizations.GetIdAsync(authorization, cancellationToken)
                ?? throw new InvalidOperationException("The OAuth authorization has no identifier.");
            var scopes = await authorizations.GetScopesAsync(authorization, cancellationToken);
            var encode = HtmlEncoder.Default;
            entries.Append($"""
                <li>
                  <strong>{encode.Encode(name)}</strong>
                  <p>Client identifier: {encode.Encode(clientId)}</p>
                  <p>Permissions: {encode.Encode(string.Join(", ", scopes))}</p>
                  <form method="post" action="/connect/authorizations/revoke">
                    <input type="hidden" name="__RequestVerificationToken" value="{encode.Encode(token)}">
                    <input type="hidden" name="authorizationId" value="{encode.Encode(authorizationId)}">
                    <button type="submit">Revoke access</button>
                  </form>
                </li>
                """);
        }

        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        return Content($$"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Authorized MCP clients</title>
            <style>body { font: 16px system-ui, sans-serif; max-width: 40rem; margin: 4rem auto; padding: 0 1rem; } li { margin: 1rem 0; }</style>
            </head><body><main><h1>Authorized MCP clients</h1>
            <p>Revoking access prevents new tokens. Existing access tokens may remain usable for up to 15 minutes.</p><ul>{{entries}}</ul>
            </main></body></html>
            """, "text/html; charset=utf-8");
    }

    [HttpPost("~/connect/authorizations/revoke")]
    [ValidateAntiForgeryToken]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Revoke([FromForm] string? authorizationId, CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Unauthorized();
        }
        if (string.IsNullOrWhiteSpace(authorizationId) || authorizationId.Length > 255)
        {
            return BadRequest("Invalid authorization identifier.");
        }

        var authorization = await authorizations.FindByIdAsync(authorizationId, cancellationToken);
        if (authorization is null ||
            !string.Equals(await authorizations.GetSubjectAsync(authorization, cancellationToken), subject, StringComparison.Ordinal))
        {
            return NotFound();
        }
        if (!await authorizations.TryRevokeAsync(authorization, cancellationToken))
        {
            return StatusCode(StatusCodes.Status409Conflict, "The authorization could not be revoked. Try again.");
        }
        return Redirect("/connect/authorizations");
    }
}
