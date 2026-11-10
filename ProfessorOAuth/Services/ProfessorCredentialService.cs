using Dapper;
using MySql.Data.MySqlClient;

namespace ProfessorOAuth.Services;

public sealed class ProfessorCredentialService(MySqlConnection connection) : IProfessorCredentialService
{
    public async Task<ProfessorAccount?> AuthenticateAsync(string email, string password)
    {
        var account = await connection.QuerySingleOrDefaultAsync<ProfessorAccountRecord>(
            """
            SELECT professor_id AS ProfessorId,
                   full_name AS Name,
                   password AS PasswordHash
            FROM professors
            WHERE email = @email
            LIMIT 1
            """,
            new { email });

        if (account is null || !BCrypt.Net.BCrypt.Verify(password, account.PasswordHash))
        {
            return null;
        }

        return new ProfessorAccount(account.ProfessorId, account.Name);
    }

    private sealed record ProfessorAccountRecord(
        string ProfessorId,
        string? Name,
        string PasswordHash);
}
