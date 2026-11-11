using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProfessorOAuth.Persistence;

namespace ProfessorOAuth.Tests.Integration;

public class OpenIddictSchemaTests
{
    [Fact]
    public void ClientIdMigration_GeneratesCaseSensitiveExpandedColumn()
    {
        var options = new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseMySql("Server=localhost;Database=test;User=test;Password=test",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .UseOpenIddict()
            .Options;
        using var context = new OpenIddictDbContext(options);

        var application = context.Model.FindEntityType(
            "OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication");
        var clientId = application!.FindProperty("ClientId");
        Assert.Equal(512, clientId!.GetMaxLength());

        var sql = context.GetService<IMigrator>().GenerateScript(
            "20261109004246_OpenIddictSchema",
            "20261111052329_ExpandClientIdForMetadataDocuments");
        Assert.Contains("varchar(512)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("utf8mb4_bin", sql, StringComparison.OrdinalIgnoreCase);
    }
}
