using Jusoor.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Jusoor.Infrastructure.Persistence;

/// <summary>
/// Without this, `dotnet ef migrations add` falls back to discovering
/// ApplicationDbContext by partially executing Program.cs's top-level
/// statements — which, in this app, include auto-migrate and role-seeding
/// against a real Postgres connection. That's fragile (fails if Docker
/// isn't running) and slow. This factory builds just the DbContext, reading
/// the same appsettings/env-var configuration Program.cs uses, with a
/// no-op ICurrentUserService since design-time tooling has no request.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            // Port 5433, matching docker-compose.yml's host-side mapping —
            // see that file's comment for why it's not 5432.
            ?? "Host=localhost;Port=5433;Database=jusoor;Username=jusoor;Password=jusoor_dev_only";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(
            optionsBuilder.Options,
            new DesignTimeCurrentUserService(),
            new DesignTimeFieldEncryptionService());
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public string? UserId => null;
        public bool IsAuthenticated => false;
        public IReadOnlyList<string> Roles => Array.Empty<string>();
    }

    /// <summary>
    /// Same reasoning as DesignTimeCurrentUserService above, applied to
    /// Case's field encryption: scaffolding a migration only needs EF Core to
    /// know these properties map to string-typed columns — it never actually
    /// persists or reads real data. Requiring live Azure Key Vault
    /// connectivity just to run `dotnet ef migrations add` would be a design-
    /// time-tooling regression, not a security improvement, so this
    /// passthrough deliberately does not encrypt anything.
    /// </summary>
    private sealed class DesignTimeFieldEncryptionService : IFieldEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string ciphertext) => ciphertext;
    }
}
