using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// A single Atmaen ("اطمّن") safety case — spec §10, "the most important
/// feature on the platform... must be built to the highest privacy and
/// security standards." Deliberately NOT a public missing-persons database:
/// "the system does not create a public database of missing persons; it is
/// a fully private case-management system between the applicant and the
/// trusted response network" (spec's own words).
///
/// This entity covers steps 1–6 of the spec's 8-step flow only (country/city
/// selection, the concern description, minimum applicant/subject info, OTP
/// identity verification, and case creation itself). Steps 7–8 (assignment
/// to the response network, live status via SignalR) are explicitly a later
/// slice (step 3 of the approved execution order — "Atmaen Case core +
/// temporary CrisisEditor assignment flow") and this entity has no
/// assignment-related property yet on purpose: adding one now would mean
/// half-building that slice's own schema decision ahead of it.
///
/// Every sensitive field below is a plain string at the Domain level —
/// encryption is applied transparently at the persistence layer via EF Core
/// value converters (see ApplicationDbContext.OnModelCreating and
/// CaseConfiguration), per the approved Phase 2 decision (EF Core value
/// converters + Azure Key Vault + envelope encryption). Domain code must
/// never be made aware of how or whether a field is encrypted underneath it.
/// </summary>
public class Case : AuditableEntity
{
    public CaseStatus Status { get; private set; } = CaseStatus.NoVerifiedInformationYet;

    /// <summary>Spec §10 step 1 — "related to the Country table." Required.</summary>
    public Guid CountryId { get; private set; }

    /// <summary>Spec §10 step 2 — "related to the City table." Required. Stored
    /// alongside CountryId rather than derived solely through City's own
    /// Country relationship, mirroring Story.PrimaryCountryId/PrimaryCityId's
    /// existing denormalized-pair convention in this codebase.</summary>
    public Guid CityId { get; private set; }

    /// <summary>Spec §10 step 4 — "only the concerned person's name is
    /// mandatory." Encrypted at rest.</summary>
    public string SubjectName { get; private set; } = null!;

    /// <summary>Spec §10 step 4 — "phone/details are optional." This is
    /// optional information ABOUT the subject (the person of concern), not
    /// the applicant's own verification phone below — the two are distinct
    /// people and distinct fields. Encrypted at rest when present.</summary>
    public string? SubjectContactPhone { get; private set; }

    /// <summary>Spec §10 step 3 — the free-text concern description. Encrypted
    /// at rest. The spec also calls for automatic AI urgency classification
    /// at this same step; that is not part of this entity yet — deferred to
    /// whichever slice actually builds Case's create/read handlers, rather
    /// than adding a column here for a classifier that doesn't exist yet.</summary>
    public string ConcernDescription { get; private set; } = null!;

    /// <summary>Spec §10 step 5 — the APPLICANT's own phone number (i.e. the
    /// person submitting the report, not the subject), whose control of it is
    /// what the OTP check verifies. Encrypted at rest: under §22's
    /// need-to-know principle, knowing who reported a case is itself
    /// sensitive, independent of anything about the subject.</summary>
    public string ApplicantPhoneE164 { get; private set; } = null!;

    /// <summary>When the step-5 OTP check returned Approved (see
    /// IOtpService/OtpVerificationStatus). Deliberately NOT encrypted — a
    /// timestamp alone carries no identity or content information about
    /// either party, only that a verification succeeded and when.</summary>
    public DateTimeOffset OtpVerifiedAtUtc { get; private set; }

    /// <summary>Spec §10 step 7 — the CrisisEditor this Case is assigned to.
    /// Temporary Phase 2 mechanism (approved decision 3): a simple
    /// least-currently-assigned rotation among existing CrisisEditors,
    /// assigned once at creation — NOT the full §14 geographic
    /// correspondent/distribution network, which is explicitly deferred to
    /// Phase 3. Plain string (ApplicationUser.Id), not a hard EF foreign key
    /// — same convention AuditableEntity's own CreatedByUserId/
    /// LastModifiedByUserId already use for user references, so a user
    /// account can never be blocked from deletion by a Case's audit trail.</summary>
    public string? AssignedToCrisisEditorUserId { get; private set; }

    public DateTimeOffset? AssignedAtUtc { get; private set; }

    // EF Core requires a parameterless constructor for materialization;
    // private so application code can't construct an invalid Case by
    // skipping the factory below — same reasoning as every other
    // AuditableEntity/BaseEntity subtype in this codebase (Story,
    // HumanReviewDecision, ...).
    private Case() { }

    public static Case Create(
        Guid countryId,
        Guid cityId,
        string subjectName,
        string? subjectContactPhone,
        string concernDescription,
        string applicantPhoneE164,
        DateTimeOffset otpVerifiedAtUtc,
        DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
        {
            throw new ArgumentException("A Case must record the concerned person's name.", nameof(subjectName));
        }

        if (string.IsNullOrWhiteSpace(concernDescription))
        {
            throw new ArgumentException("A Case must record a description of the concern.", nameof(concernDescription));
        }

        if (string.IsNullOrWhiteSpace(applicantPhoneE164))
        {
            throw new ArgumentException(
                "A Case must record the applicant's verified phone number — this is what step 5's OTP check verifies.",
                nameof(applicantPhoneE164));
        }

        return new Case
        {
            CountryId = countryId,
            CityId = cityId,
            SubjectName = subjectName,
            SubjectContactPhone = string.IsNullOrWhiteSpace(subjectContactPhone) ? null : subjectContactPhone,
            ConcernDescription = concernDescription,
            ApplicantPhoneE164 = applicantPhoneE164,
            OtpVerifiedAtUtc = otpVerifiedAtUtc,
            Status = CaseStatus.NoVerifiedInformationYet,
            CreatedAtUtc = nowUtc,
            LastModifiedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Advances Status along the spec §10 vocabulary. Deliberately the only
    /// mutation method this entity exposes beyond Create: WHICH roles may
    /// cause which transitions (e.g. "only the assigned CrisisEditor, only
    /// for their own assigned Cases") is a Phase 3 RBAC / Case-workflow
    /// decision — approved in principle, not yet implemented — and does not
    /// belong inside the entity itself. This method only keeps the entity's
    /// own history truthful (status changed, with a timestamp) without
    /// inventing the authorization rule or workflow state machine around it.
    /// </summary>
    public void UpdateStatus(CaseStatus newStatus, DateTimeOffset nowUtc)
    {
        Status = newStatus;
        LastModifiedAtUtc = nowUtc;
    }

    /// <summary>
    /// Assigns (or reassigns) this Case to a specific CrisisEditor. Who is
    /// allowed to call this, and the actual rotation/load-balancing logic
    /// that picks which CrisisEditor to assign to, are Application-layer
    /// concerns (SubmitCaseCommand) — this method only enforces the entity's
    /// own invariant that an assignment must name someone.
    /// </summary>
    public void AssignTo(string crisisEditorUserId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(crisisEditorUserId))
        {
            throw new ArgumentException("Assigning a Case requires a CrisisEditor user id.", nameof(crisisEditorUserId));
        }

        AssignedToCrisisEditorUserId = crisisEditorUserId;
        AssignedAtUtc = nowUtc;
        LastModifiedAtUtc = nowUtc;
    }
}
