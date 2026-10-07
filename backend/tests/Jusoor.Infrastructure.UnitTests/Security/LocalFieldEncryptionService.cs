using System.Security.Cryptography;
using System.Text;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Jusoor.Infrastructure.Security;

public sealed class LocalFieldEncryptionService : IFieldEncryptionService
{
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public LocalFieldEncryptionService(IConfiguration configuration)
    {
        var key = configuration["FieldEncryption:LocalKey"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "FieldEncryption:LocalKey is missing. " +
                "Set a random 32-byte Base64 key in the Development environment.");
        }

        try
        {
            _key = Convert.FromBase64String(key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "FieldEncryption:LocalKey must be a valid Base64 string.",
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

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);

        aes.Encrypt(
            nonce,
            plaintextBytes,
            ciphertext,
            tag);

        var envelope = new byte[
            NonceSizeBytes +
            TagSizeBytes +
            ciphertext.Length];

        Buffer.BlockCopy(nonce, 0, envelope, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, envelope, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(
            ciphertext,
            0,
            envelope,
            NonceSizeBytes + TagSizeBytes,
            ciphertext.Length);

        return Convert.ToBase64String(envelope);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        var envelope = Convert.FromBase64String(ciphertext);

        if (envelope.Length < NonceSizeBytes + TagSizeBytes)
        {
            throw new InvalidOperationException(
                "Encrypted field envelope is invalid.");
        }

        var nonce = envelope[..NonceSizeBytes];
        var tag = envelope[
            NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];

        var encryptedData = envelope[
            (NonceSizeBytes + TagSizeBytes)..];

        var plaintext = new byte[encryptedData.Length];

        using var aes = new AesGcm(_key, TagSizeBytes);

        aes.Decrypt(
            nonce,
            encryptedData,
            tag,
            plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
