namespace Jusoor.Application.Common.Interfaces;

/// <summary>
/// Encrypts/decrypts a single string field for at-rest storage. Deliberately
/// provider-agnostic at this layer — same reasoning as IOtpService: the
/// concrete mechanism (EF Core value converters + Azure Key Vault + envelope
/// encryption, per the approved Phase 2 decision) lives entirely in
/// Infrastructure, and Application/Domain code must never need to know it
/// exists, let alone which key-management service backs it.
///
/// Implementations MUST be safe to hold as a long-lived singleton (no
/// per-request/per-entity state) — this interface is resolved once, at
/// EF Core model-build time, and reused for the lifetime of the process; see
/// ApplicationDbContext.OnModelCreating's comment on why that constrains the
/// implementation's lifetime, not just its registration.
///
/// Never logs, returns, or otherwise exposes plaintext except as this
/// interface's own return value — callers are responsible for not logging
/// what they get back.
/// </summary>
public interface IFieldEncryptionService
{
    /// <summary>Returns an opaque, self-contained encoded blob — never partial
    /// or streaming output. Safe to store directly in a single database
    /// column of unbounded text length (the blob is larger than the
    /// plaintext, by construction — see the Infrastructure implementation's
    /// own comment on why no fixed max-length applies to columns using this).</summary>
    string Encrypt(string plaintext);

    /// <summary>Reverses <see cref="Encrypt"/>. Must accept ciphertext produced
    /// by any historical key version this service's key-encryption key has
    /// ever used, not only the current one — this is what makes key rotation
    /// possible without re-encrypting historical rows (approved decision).</summary>
    string Decrypt(string ciphertext);
}
