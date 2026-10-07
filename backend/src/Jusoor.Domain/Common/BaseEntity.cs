namespace Jusoor.Domain.Common;

/// <summary>
/// Base type for every domain entity. GUID primary keys are used platform-wide
/// so that IDs can be generated client-side (offline-safe, no round-trip needed
/// to know an entity's identity) and so that Postgres/pgvector correlation
/// across the future news pipeline never leaks sequential-integer enumeration
/// (see spec §22 — anti-enumeration).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>
/// Adds creation/modification auditing. Every entity that can be mutated by a
/// user (as opposed to pure reference data) should inherit this instead of
/// BaseEntity directly — the engineering plan calls out AuditLog as a
/// non-negotiable for Atmaen (Phase 2) and CMS (Phase 3); starting the
/// convention in Phase 0 means later phases don't retrofit it.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? LastModifiedAtUtc { get; set; }
    public string? LastModifiedByUserId { get; set; }
}
