using Microsoft.EntityFrameworkCore;

namespace ProfessorOAuth.Persistence;

public sealed class OpenIddictDbContext(DbContextOptions<OpenIddictDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
        builder.Entity("OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken", token =>
        {
            token.Property<string>("Subject").HasMaxLength(255);
            token.Property<string>("Type").HasMaxLength(100);
        });
    }
}
