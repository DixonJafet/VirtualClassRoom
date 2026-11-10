using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProfessorOAuth.Persistence;

public sealed class OpenIddictDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OpenIddictDbContext>
{
    public OpenIddictDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MySQLConnection") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__MySQLConnnection") ??
            Environment.GetEnvironmentVariable("MYSQL_ADDON_URI");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__MySQLConnection or MYSQL_ADDON_URI before running EF migrations.");
        }

        var options = new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .UseOpenIddict()
            .Options;

        return new OpenIddictDbContext(options);
    }
}
