using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MySql.Data.MySqlClient;

namespace ProfessorOAuth.Health;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IConfiguration configuration, ILogger<DatabaseHealthCheck> logger)
    {
        _connectionString = configuration.GetConnectionString("MySQLConnection")
            ?? configuration.GetConnectionString("MySQLConnnection")
            ?? configuration["MYSQL_ADDON_URI"]
            ?? string.Empty;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Unhealthy("Database connection string is not configured.");
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var result = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1;", cancellationToken: cancellationToken));
            return result == 1
                ? HealthCheckResult.Healthy("Database is responsive.")
                : HealthCheckResult.Unhealthy("Database check returned unexpected result.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed.");
            return HealthCheckResult.Unhealthy("Database is unavailable.");
        }
    }
}

