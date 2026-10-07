namespace Jusoor.Domain.Enums;

/// <summary>
/// Spec §12's four privacy levels — except there are only three members
/// here, not four. Level 4 ("dقيق موقع دقيق" / precise location) is
/// explicitly rejected by the spec for the current product ("مرفوض
/// هندسياً إلا كوضع طوارئ مؤقت صريح لاحقاً... يُقيَّم في V2 فقط" — rejected
/// engineering-wise except as an explicit, temporary, non-persistent
/// emergency mode to be evaluated only in V2). Not modeling it at all,
/// rather than adding an unused enum member for it, is deliberate: an
/// unused "Precise" value sitting in this enum is an invitation for some
/// future caller to wire it up without re-deriving why it was rejected —
/// the absence itself is the safeguard.
/// </summary>
public enum DiasporaPrivacyLevel
{
    /// <summary>Level 0 — no location at all. The platform stays fully
    /// functional at this level; nothing about it degrades.</summary>
    NoLocation = 0,

    /// <summary>Level 1 — country only. The suggested default at
    /// registration, per spec — but not enforced as a required
    /// registration field here (see ProfileController's own comment on
    /// why RegisterCommand itself is untouched).</summary>
    CountryOnly = 1,

    /// <summary>Level 2 — region/state, optional.</summary>
    Region = 2,

    /// <summary>Level 3 — city, optional; spec: "enables full
    /// personalization and local alerts" (alerts themselves aren't built
    /// yet — see DiasporaProfile's own notes on what's deliberately out of
    /// scope this slice).</summary>
    City = 3
}
