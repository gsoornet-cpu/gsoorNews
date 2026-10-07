using Jusoor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Jusoor.Api.IntegrationTests;

public class JusoorApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("jusoor_test")
        .WithUsername("jusoor")
        .WithPassword("jusoor_test_only")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // triggers the auto-migrate/seed path in Program.cs

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                // Test-only signing key, never used outside this process.
                // Real environments get this from Key Vault, never from a
                // committed file — see JwtTokenService's comment.
                ["Jwt:SigningKey"] = "test-only-signing-key-not-for-any-real-environment-000000",
                ["Jwt:Issuer"] = "jusoor-api-test",
                ["Jwt:Audience"] = "jusoor-clients-test",
                // Added for the Case-entity slice: ApplicationDbContext now
                // takes a constructor-injected IFieldEncryptionService, which
                // Program.cs's Development auto-migrate path (see the comment
                // above on UseEnvironment) constructs at host startup for
                // EVERY integration test class, not just ones that touch
                // Case — so without this, every integration test would fail
                // at startup, not just new Case-related ones.
                // AzureKeyVaultEnvelopeEncryptionService's constructor only
                // throws if this section is missing entirely; it does not
                // validate that the URI/key actually exist, and merely
                // constructing Azure.Identity/KeyClient objects makes no
                // network call by itself — an actual Key Vault call only
                // happens if something in a test really encrypts/decrypts a
                // Case field, which no test does yet (Case's own
                // create/read handlers are a later slice). These values are
                // therefore placeholders sufficient for startup, not
                // evidence that real Key Vault connectivity has been
                // exercised — flagged so a future reader doesn't assume
                // otherwise from their presence here.
                ["FieldEncryption:KeyVaultUri"] = "https://jusoor-test-placeholder.vault.azure.net/",
                ["FieldEncryption:KeyEncryptionKeyName"] = "test-placeholder-key",
                // Infrastructure's DependencyInjection currently registers
                // LocalFieldEncryptionService in BOTH branches of the
                // UseLocalFieldEncryption check, and that service throws at
                // construction without a key. Supplying a test-only key here
                // makes the suite hermetic: it no longer depends on
                // FieldEncryption__LocalKey happening to be set in the
                // developer's shell. Base64 of a 32-byte ASCII string,
                // never used outside this process.
                ["UseLocalFieldEncryption"] = "true",
                ["FieldEncryption:LocalKey"] = "dGVzdC1vbmx5LTMyLWJ5dGUta2V5LW5vdC1yZWFsISE="
            });
        });
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        // Host first, container second. The host runs a real Hangfire server
        // that de-registers itself from Postgres on shutdown; stopping the
        // container first (what this used to do — xUnit then disposed the
        // host afterwards via IDisposable) made that shutdown talk to a dead
        // database.
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}