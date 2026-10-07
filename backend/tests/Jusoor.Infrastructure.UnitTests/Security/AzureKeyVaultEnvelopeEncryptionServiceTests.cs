using FluentAssertions;
using Jusoor.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Security;

/// <summary>
/// Covers only what's genuinely unit-testable without a live Azure Key Vault:
/// the fail-fast behavior when FieldEncryption configuration is missing.
///
/// NOT covered here, and NOT verified by this session: Encrypt/Decrypt
/// themselves. Every code path in AzureKeyVaultEnvelopeEncryptionService
/// beyond the constructor makes a real Key Vault call (WrapKey/UnwrapKey) —
/// there is no local/offline way to exercise AES-GCM + envelope wrapping
/// against a real KEK without live credentials and network access, both
/// unavailable in this sandbox (same restriction already noted for Twilio
/// and NuGet on every prior slice). A developer run against a real (even
/// just a personal/dev) Key Vault instance is needed before this class's
/// actual encryption/decryption round-trip can be called verified.
/// </summary>
public class AzureKeyVaultEnvelopeEncryptionServiceTests
{
    [Fact]
    public void Constructor_throws_when_FieldEncryption_configuration_section_is_missing()
    {
        var configuration = new ConfigurationBuilder().Build(); // no "FieldEncryption" section at all

        var act = () => new AzureKeyVaultEnvelopeEncryptionService(configuration);

        // Same fail-fast contract as JwtTokenService/TwilioOtpService: refuse
        // to start with no key-management configuration rather than run with
        // a default that would mean storing Case data unencrypted or with a
        // guessable key.
        act.Should().Throw<InvalidOperationException>();
    }
}
