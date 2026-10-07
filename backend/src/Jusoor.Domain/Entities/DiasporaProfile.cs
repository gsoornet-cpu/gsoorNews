using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Spec §12's "بروفايل المهجر" (diaspora profile) — currently just the
/// privacy-level-gated location disclosure. Deliberately a separate Domain
/// entity, not fields bolted onto Infrastructure's ApplicationUser: Domain
/// must not depend on Infrastructure (ApplicationUser lives there), and
/// ApplicationUser's own comment already anticipated this split ("§12's
/// richer diaspora profile... is a concern layered on top of this").
/// UserId is a plain string — the same "no cross-boundary FK onto
/// AspNetUsers" precedent as AuditableEntity.CreatedByUserId and
/// HumanReviewDecision.ReviewedByUserId, not a new pattern.
///
/// Deliberately does NOT include (this slice): followed topics (spec
/// mentions "المواضيع المتابَعة" but no Topic/Category taxonomy exists
/// anywhere in the Domain model to follow — Slice 8 hit this same gap
/// building the homepage), notification preferences (spec's own wording
/// is a generic "تفضيلات الإشعارات" with no specified channels/categories,
/// and no delivery mechanism exists to make a stored preference mean
/// anything yet), or "حالة الاغتراب" / expat status (mentioned once in the
/// spec with no defined value set — building an enum for it now would mean
/// inventing the values). All three are real gaps, not oversights, and are
/// recorded in the dashboard rather than guessed at here.
/// </summary>
public class DiasporaProfile : BaseEntity
{
    public string UserId { get; private set; } = null!;

    public DiasporaPrivacyLevel PrivacyLevel { get; private set; } = DiasporaPrivacyLevel.NoLocation;

    // At most one of these is meaningfully set at a time, gated by
    // PrivacyLevel — see UpdateLocation's own invariant. Country/Region/
    // City are peers here (not a strict "must fill shallower tiers first"
    // chain): spec explicitly treats Region and City as independently
    // optional ("المنطقة/الولاية: اختياري", "المدينة: اختياري"), so a user
    // can go straight from NoLocation to City-level without ever
    // separately recording a Region.
    public Guid? CountryId { get; private set; }
    public Guid? RegionId { get; private set; }
    public Guid? CityId { get; private set; }

    public DateTimeOffset LastUpdatedAtUtc { get; private set; }

    private DiasporaProfile() { }

    public static DiasporaProfile CreateDefault(string userId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("A DiasporaProfile must belong to a user.", nameof(userId));
        }

        // Level 0 by default — spec is explicit that the platform stays
        // fully functional with no location at all; this is a real,
        // supported steady-state, not a placeholder waiting to be filled in.
        return new DiasporaProfile { UserId = userId, PrivacyLevel = DiasporaPrivacyLevel.NoLocation, LastUpdatedAtUtc = nowUtc };
    }

    /// <summary>
    /// The one real invariant this entity enforces: a user can never end up
    /// disclosing more precision than their own chosen PrivacyLevel allows.
    /// Validating the referenced Country/Region/City ids actually exist is
    /// the Application layer's job (it has DB access; this method
    /// deliberately doesn't) — this only enforces the shape/consistency
    /// rule that belongs to the entity itself.
    /// </summary>
    public void UpdateLocation(DiasporaPrivacyLevel level, Guid? countryId, Guid? regionId, Guid? cityId, DateTimeOffset nowUtc)
    {
        switch (level)
        {
            case DiasporaPrivacyLevel.NoLocation when countryId is not null || regionId is not null || cityId is not null:
                throw new ArgumentException("NoLocation must not carry a country, region, or city.");
            case DiasporaPrivacyLevel.CountryOnly when regionId is not null || cityId is not null:
                throw new ArgumentException("CountryOnly must not carry a region or city.");
            case DiasporaPrivacyLevel.CountryOnly when countryId is null:
                throw new ArgumentException("CountryOnly requires a country.");
            case DiasporaPrivacyLevel.Region when cityId is not null:
                throw new ArgumentException("Region-level privacy must not carry a city.");
            case DiasporaPrivacyLevel.Region when regionId is null:
                throw new ArgumentException("Region-level privacy requires a region.");
            case DiasporaPrivacyLevel.City when cityId is null:
                throw new ArgumentException("City-level privacy requires a city.");
        }

        PrivacyLevel = level;
        CountryId = countryId;
        RegionId = regionId;
        CityId = cityId;
        LastUpdatedAtUtc = nowUtc;
    }
}
