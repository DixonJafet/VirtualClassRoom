using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MySql.Data.MySqlClient;
using ProfessorOAuth.Services;

namespace ProfessorOAuth.Mcp;

[ApiController]
[AllowAnonymous]
[Route("api/profes/oauth/login")]
public sealed class McpOAuthLoginController : ControllerBase
{
    private const string LoginCookieScheme = "McpOAuthCookie";
    private readonly IProfessorCredentialService _professors;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<McpOAuthLoginController> _logger;

    public McpOAuthLoginController(
        IProfessorCredentialService professors,
        IAntiforgery antiforgery,
        ILogger<McpOAuthLoginController> logger)
    {
        _professors = professors;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetLoginPage([FromQuery] string? returnUrl)
    {
        if (!IsAuthorizationReturnUrl(returnUrl))
        {
            return BadRequest("Invalid authorization request.");
        }

        return LoginPage(returnUrl!, null, StatusCodes.Status200OK);
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("McpOAuthLogin")]
    public async Task<IActionResult> SubmitLogin(
        [FromForm] string? returnUrl,
        [FromForm] string? email,
        [FromForm] string? password)
    {
        if (!IsAuthorizationReturnUrl(returnUrl))
        {
            return BadRequest("Invalid authorization request.");
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 ||
            string.IsNullOrEmpty(password) || password.Length > 1024)
        {
            return LoginPage(returnUrl!, "Enter a valid email address and password.",
                StatusCodes.Status400BadRequest);
        }

        ProfessorAccount? professor;
        try
        {
            professor = await _professors.AuthenticateAsync(email.Trim(), password);
        }
        catch (MySqlException exception)
        {
            _logger.LogError(exception, "Professor authentication failed during MCP OAuth sign-in.");
            return LoginPage(returnUrl!, "Sign-in is temporarily unavailable. Please try again.",
                StatusCodes.Status503ServiceUnavailable);
        }

        if (professor is null)
        {
            return LoginPage(returnUrl!, "The email or password is incorrect.",
                StatusCodes.Status401Unauthorized);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, professor.ProfessorId),
            new(ClaimTypes.Name, professor.Name ?? string.Empty)
        };
        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email.Trim()));
        }

        await HttpContext.SignInAsync(
            LoginCookieScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, LoginCookieScheme)),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            });

        return LocalRedirect(returnUrl!);
    }

    private ContentResult LoginPage(string returnUrl, string? error, int statusCode)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Content-Security-Policy"] =
            "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        Response.StatusCode = statusCode;

        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        var encodedReturnUrl = HtmlEncoder.Default.Encode(returnUrl);
        var encodedToken = HtmlEncoder.Default.Encode(tokens.RequestToken ?? string.Empty);
        var errorMarkup = error is null
            ? string.Empty
            : $"<p role=\"alert\">{HtmlEncoder.Default.Encode(error)}</p>";
        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Sign in to MyClassRoom</title>
              <style>
                body { font: 16px system-ui, sans-serif; max-width: 24rem; margin: 4rem auto; padding: 0 1rem; }
                label { display: block; margin-top: 1rem; }
                input, button { box-sizing: border-box; font: inherit; margin-top: .35rem; padding: .65rem; width: 100%; }
                button { margin-top: 1.25rem; cursor: pointer; }
                [role=alert] { color: #a00000; }
              </style>
            </head>
            <body>
              <main>
                <h1>Sign in to MyClassRoom</h1>
                <p>Use your existing professor account to continue.</p>
                {{errorMarkup}}
                <form method="post" action="/api/profes/oauth/login" autocomplete="on">
                  <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
                  <input type="hidden" name="__RequestVerificationToken" value="{{encodedToken}}">
                  <label for="email">Email</label>
                  <input id="email" name="email" type="email" autocomplete="username" required maxlength="320">
                  <label for="password">Password</label>
                  <input id="password" name="password" type="password" autocomplete="current-password" required maxlength="1024">
                  <button type="submit">Sign in</button>
                </form>
              </main>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    private bool IsAuthorizationReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl) &&
            returnUrl.StartsWith("/connect/authorize?", StringComparison.Ordinal);
    }
}
