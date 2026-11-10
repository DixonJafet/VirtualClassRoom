using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProfessorAPI.Mcp;

[ApiController]
[AllowAnonymous]
[Route("/.well-known/oauth-protected-resource")]
public sealed class McpOAuthMetadataController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public McpOAuthMetadataController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult GetMetadata()
    {
        var issuer = _configuration["McpOAuth:Issuer"];
        var resourceUrl = _configuration["McpOAuth:ResourceUrl"];
        if (string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(resourceUrl))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "MCP OAuth resource metadata is not configured."
            });
        }

        return Ok(new
        {
            resource = resourceUrl,
            authorization_servers = new[] { issuer.TrimEnd('/') + "/" },
            bearer_methods_supported = new[] { "header" },
            scopes_supported = new[]
            {
                _configuration["McpOAuth:ReadScope"] ?? "classroom:read",
                _configuration["McpOAuth:WriteScope"] ?? "classroom:write"
            }
        });
    }
}
