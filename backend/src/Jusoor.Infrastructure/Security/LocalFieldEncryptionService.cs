using System.Security.Cryptography;
using System.Text;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Jusoor.Infrastructure.Security;

/// <summary>
/// Local-development-only field encryption.
///
/// This is intentionally separate from the production Azure Key Vault
/// implementation. It allows Atmaen to run locally without requiring an
/// Azure subscription or Key Vault.
///
/// The key is supplied through configuration and must never be committed
/// to source control.
/// </summary>
public sealed class LocalFieldEncryptionService : IFieldEncryptionService
{
    private const int KeySizeBytes = 32;   // AES-256
    private const int NonceSizeBytes = 12; // AES-GCM standard nonce
    private const int TagSizeBytes = 16;   // AES-GCM authentication tag

    private readonly byte[] _key;

    public LocalFieldEncryptionService(IConfiguration configuration)
    {
        var key = configuration["FieldEncryption:LocalKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "FieldEncryption:LocalKey is missing. " +
                "Set a local development encryption key.");
        }

        try
        {
            _key = Convert.FromBase64String(key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "FieldEncryption:LocalKey must be a Base64-encoded 32-byte key.",
                ex);
        }

        if (_key.Length != KeySizeBytes)
        {
            throw new InvalidOperationException(
                "FieldEncryption:LocalKey must decode to exactly 32 bytes.");
        }
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using (var aes = new AesGcm(_key, TagSizeBytes))
        {
            aes.Encrypt(
                nonce,
                plaintextBytes,
                ciphertext,
                tag);
        }

        // Format:
        // version (1 byte)
        // nonce
        // tag
        // ciphertext
        var result = new byte[
            1 +
            NonceSizeBytes +
            TagSizeBytes +
            ciphertext.Length];

        result[0] = 1;

        Buffer.BlockCopy(
            nonce,
            0,
            result,
            1,
            NonceSizeBytes);

        Buffer.BlockCopy(
            tag,
            0,
            result,
            1 + NonceSizeBytes,
            TagSizeBytes);

        Buffer.BlockCopy(
            ciphertext,
            0,
            result,
            1 + NonceSizeBytes + TagSizeBytes,
            ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        byte[] payload;

        try
        {
            payload = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Encrypted field is not valid Base64.",
                ex);
        }

        var minimumSize =
            1 +
            NonceSizeBytes +
            TagSizeBytes;

        if (payload.Length < minimumSize)
        {
            throw new InvalidOperationException(
                "Encrypted field payload is invalid or truncated.");
        }

        if (payload[0] != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported local encryption payload version: {payload[0]}.");
        }

        var nonce = payload[
            1..(1 + NonceSizeBytes)];

        var tag = payload[
            (1 + NonceSizeBytes)..(1 + NonceSizeBytes + TagSizeBytes)];

        var encryptedData = payload[
            (1 + NonceSizeBytes + TagSizeBytes)..];

        var plaintextBytes = new byte[encryptedData.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSizeBytes);

            aes.Decrypt(
                nonce,
                encryptedData,
                tag,
                plaintextBytes);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "Encrypted field could not be decrypted. " +
                "The local encryption key may have changed or the data may be corrupted.",
                ex);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
