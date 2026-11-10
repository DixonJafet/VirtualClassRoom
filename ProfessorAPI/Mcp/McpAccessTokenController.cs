using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MySql.Data.MySqlClient;

namespace ProfessorAPI.Mcp;

[ApiController]
[Route("api/profes/mcp")]
[Authorize]
public sealed class McpAccessTokenController : ControllerBase
{
    private readonly MySqlConnection _connection;

    public McpAccessTokenController(MySqlConnection connection)
    {
        _connection = connection;
    }

    [HttpPost("token")]
    public async Task<IActionResult> CreateToken([FromBody] CreateMcpTokenRequest request)
    {
        var professorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(professorId))
        {
            return Unauthorized();
        }

        if (request.AccessLevel is not ("read" or "write"))
        {
            return BadRequest(new { error = "access_level must be 'read' or 'write'." });
        }

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var expiresAt = DateTime.UtcNow.AddDays(365);

        await _connection.ExecuteAsync(
            """
            INSERT INTO mcp_access_tokens (professor_id, token_hash, access_level, expires_at)
            VALUES (@professorId, @tokenHash, @accessLevel, @expiresAt)
            """,
            new { professorId, tokenHash, accessLevel = request.AccessLevel, expiresAt });

        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            access_token = token,
            token_type = "Bearer",
            access_level = request.AccessLevel,
            expires_at_utc = expiresAt,
            mcp_url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/mcp"
        });
    }

    public sealed class CreateMcpTokenRequest
    {
        public string AccessLevel { get; set; } = "write";
    }

    [HttpDelete("token")]
    public async Task<IActionResult> RevokeTokens()
    {
        var professorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(professorId))
        {
            return Unauthorized();
        }

        var revokedCount = await _connection.ExecuteAsync(
            "DELETE FROM mcp_access_tokens WHERE professor_id = @professorId",
            new { professorId });

        return Ok(new { revoked_count = revokedCount });
    }
}
