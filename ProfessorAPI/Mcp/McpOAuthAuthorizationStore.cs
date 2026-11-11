using Dapper;
using MySql.Data.MySqlClient;

namespace ProfessorAPI.Mcp;

public interface IMcpOAuthAuthorizationStore
{
    Task<string?> FindActiveProfessorAsync(string subject, string authorizationId);
}

public sealed class McpOAuthAuthorizationStore(MySqlConnection connection) : IMcpOAuthAuthorizationStore
{
    public async Task<string?> FindActiveProfessorAsync(string subject, string authorizationId) =>
        await connection.QuerySingleOrDefaultAsync<string>(
            """
            SELECT p.professor_id
            FROM professors AS p
            INNER JOIN OpenIddictAuthorizations AS a
                ON BINARY a.Subject = BINARY p.professor_id
            WHERE p.professor_id = @subject AND BINARY p.professor_id = BINARY @subject
                AND a.Id = @authorizationId AND BINARY a.Id = BINARY @authorizationId
                AND a.Status = 'valid'
            LIMIT 1
            """,
            new { subject, authorizationId });
}
