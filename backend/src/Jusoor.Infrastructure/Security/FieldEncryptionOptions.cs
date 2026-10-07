namespace Jusoor.Infrastructure.Security;

/// <summary>
/// Unlike TwilioOptions, neither field here is a secret by itself — a Key
/// Vault URI and a key name reveal no credential on their own (authentication
/// to Key Vault is via DefaultAzureCredential, not a stored secret — see
/// AzureKeyVaultEnvelopeEncryptionService). They could technically live in
/// appsettings.json, but are kept out of it anyway, alongside every other
/// per-environment value (staging/production point at different vaults),
/// following this project's existing convention of environment-specific
/// values coming from configuration outside the committed appsettings files.
/// </summary>
public class FieldEncryptionOptions
{
    public const string SectionName = "FieldEncryption";

    /// <summary>e.g. "https://jusoor-vault.vault.azure.net/".</summary>
    public required string KeyVaultUri { get; set; }

    /// <summary>Name of the RSA key-encryption key (KEK) in Key Vault used to
    /// wrap/unwrap per-field data-encryption keys. The key's VERSION is not
    /// configured here — encryption always uses whatever is Key Vault's
    /// current version of this key; decryption uses whatever version is
    /// recorded in the individual field's own encrypted envelope. This is
    /// what makes rotating this key (creating a new version in Key Vault)
    /// not require touching any historical row.</summary>
    public required string KeyEncryptionKeyName { get; set; }
}
