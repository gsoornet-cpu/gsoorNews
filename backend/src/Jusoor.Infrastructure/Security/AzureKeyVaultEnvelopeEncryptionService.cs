using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Jusoor.Infrastructure.Security;

/// <summary>
/// Implements the approved Phase 2 decision: "EF Core value converters +
/// Azure Key Vault + envelope encryption" for Atmaen Case fields.
///
/// Design: one AES-256 data-encryption key (DEK) is generated per field, per
/// encryption call — never reused across fields or rows, never persisted
/// unwrapped. The DEK wraps/unwraps via a single RSA key-encryption key (KEK)
/// held in Key Vault, using RSA-OAEP-256.
///
/// Per-FIELD DEKs rather than one per-ROW DEK shared across a Case's several
/// encrypted columns: EF Core value converters operate on one scalar
/// property in isolation with no access to sibling properties on the same
/// entity, so a shared-DEK-per-row design would require a
/// SaveChangesInterceptor instead of value converters — the mechanism the
/// approved decision actually named. The trade-off is more Key Vault
/// round-trips (one wrap/unwrap per encrypted field touched, not one per
/// Case row); accepted because Atmaen is a safety-critical, low-volume
/// workflow, not a high-traffic one. If volume ever makes this a real cost,
/// the fix is a SaveChangesInterceptor-based row-level DEK — a genuine
/// future option, not built now (YAGNI).
///
/// Rotation without re-encrypting historical rows: rotating the KEK means
/// creating a new version of the same Key Vault key. New encryptions use
/// whatever is "current"; old, still-enabled key versions remain available
/// in Key Vault to unwrap DEKs wrapped under them, so decrypting an old row
/// never requires touching it. Only if an old key VERSION is deliberately
/// disabled/deleted would old rows become unreadable — that is a Key Vault
/// operational policy, not something this class controls.
///
/// UNVERIFIED: this session had no NuGet access to confirm the exact method
/// signatures used below (KeyClient.GetCryptographyClient, WrapResult.KeyId,
/// AesGcm's two-argument constructor) against the actually-restored package
/// versions. The AesGcm(key, tagSizeInBytes) constructor specifically is
/// used deliberately, not just for style — on Linux/OpenSSL 3 (this
/// project's dev/CI environment, per its own WSL2 test logs), AesGcm's
/// older single-argument constructor is known to throw
/// PlatformNotSupportedException unless an explicit tag size is given.
/// </summary>
public class AzureKeyVaultEnvelopeEncryptionService : IFieldEncryptionService
{
    private const int DekSizeBytes = 32;   // AES-256
    private const int NonceSizeBytes = 12; // AES-GCM standard nonce size
    private const int TagSizeBytes = 16;   // AES-GCM standard (max) tag size

    private readonly FieldEncryptionOptions _options;
    private readonly TokenCredential _credential;
    private readonly KeyClient _keyClient;

    public AzureKeyVaultEnvelopeEncryptionService(IConfiguration configuration)
    {
        _options = configuration.GetSection(FieldEncryptionOptions.SectionName).Get<FieldEncryptionOptions>()
            ?? throw new InvalidOperationException(
                "FieldEncryption configuration section is missing. Refusing to start with no key management " +
                "configured for Atmaen Case data — see the approved Phase 2 decision (envelope encryption, Key Vault).");

        // Managed identity in Azure, developer credentials (Azure CLI /
        // Visual Studio / environment variables) locally. No secret of any
        // kind is read from configuration or committed to the repository —
        // this is the credential-level counterpart to "do not store
        // encryption keys alongside the encrypted database data."
        _credential = new DefaultAzureCredential();
        _keyClient = new KeyClient(new Uri(_options.KeyVaultUri), _credential);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var dek = RandomNumberGenerator.GetBytes(DekSizeBytes);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var ciphertextBytes = new byte[plaintextBytes.Length];
            var tag = new byte[TagSizeBytes];

            using (var aesGcm = new AesGcm(dek, TagSizeBytes))
            {
                aesGcm.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);
            }

            var cryptoClient = _keyClient.GetCryptographyClient(_options.KeyEncryptionKeyName);
            var wrapResult = cryptoClient.WrapKey(KeyWrapAlgorithm.RsaOaep256, dek);

            var envelope = new EncryptionEnvelope
            {
                KeyId = wrapResult.KeyId,
                WrappedDek = Convert.ToBase64String(wrapResult.EncryptedKey),
                Nonce = Convert.ToBase64String(nonce),
                Tag = Convert.ToBase64String(tag),
                Ciphertext = Convert.ToBase64String(ciphertextBytes)
            };

            return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(envelope));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var envelope = JsonSerializer.Deserialize<EncryptionEnvelope>(Convert.FromBase64String(ciphertext))
            ?? throw new InvalidOperationException("Encrypted field envelope could not be deserialized.");

        // Bound to the EXACT key version recorded at encryption time (see
        // EncryptionEnvelope.KeyId's own comment) — never "whatever Key
        // Vault currently considers the latest version of this key."
        var cryptoClient = new CryptographyClient(new Uri(envelope.KeyId), _credential);
        var dek = cryptoClient.UnwrapKey(KeyWrapAlgorithm.RsaOaep256, Convert.FromBase64String(envelope.WrappedDek)).Key;

        try
        {
            var ciphertextBytes = Convert.FromBase64String(envelope.Ciphertext);
            var plaintextBytes = new byte[ciphertextBytes.Length];

            using (var aesGcm = new AesGcm(dek, TagSizeBytes))
            {
                aesGcm.Decrypt(
                    Convert.FromBase64String(envelope.Nonce),
                    ciphertextBytes,
                    Convert.FromBase64String(envelope.Tag),
                    plaintextBytes);
            }

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}
