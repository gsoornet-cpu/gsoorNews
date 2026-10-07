namespace Jusoor.Domain.Enums;

/// <summary>
/// The 8 case-status labels spec §10 mandates verbatim ("الصياغة الإلزامية" —
/// the mandatory phrasing) for the Atmaen ("اطمّن") case-tracking flow. Order
/// below follows the spec's own listing, which is roughly the expected
/// progression of a case, though nothing in this enum enforces that a Case
/// must move through these in order — the actual allowed-transition rules
/// are Phase 3 RBAC / Case-workflow scope (steps 3 and 8 of the approved
/// execution order), not decided here.
///
/// Spec's own governing rule for whatever UI renders these: never phrase
/// this as a guarantee ("we confirm your relative is fine") — always
/// probabilistic ("this is the latest our network has verified so far").
/// That's a UI-copy rule, not something this enum can enforce, but recorded
/// here since it's the reason these labels exist as neutral status
/// descriptions rather than reassurances.
/// </summary>
public enum CaseStatus
{
    /// <summary>لا معلومة موثقة بعد — No verified information yet.</summary>
    NoVerifiedInformationYet = 1,

    /// <summary>جارِ التحقق — Verification in progress.</summary>
    VerificationInProgress = 2,

    /// <summary>محاولة تواصل جارية — A contact attempt is in progress.</summary>
    ContactAttemptInProgress = 3,

    /// <summary>تواصل تم تأكيده — Contact has been confirmed.</summary>
    ContactConfirmed = 4,

    /// <summary>الهوية مؤكَّدة — Identity confirmed.</summary>
    IdentityConfirmed = 5,

    /// <summary>تم إبلاغ الأسرة — The family has been informed.</summary>
    FamilyInformed = 6,

    /// <summary>معلومة رسمية متاحة — Official information is available.</summary>
    OfficialInformationAvailable = 7,

    /// <summary>تعذّر التحقق — Verification could not be completed.</summary>
    VerificationFailed = 8
}
