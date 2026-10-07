using Microsoft.AspNetCore.Identity;

namespace Jusoor.Infrastructure.Identity;

/// <summary>
/// Extends the default IdentityUser rather than building a parallel "user
/// profile" table in Phase 0 — that would duplicate what Identity already
/// does well (password hashing, lockout, security stamps) for no benefit
/// this early. §12's richer "diaspora profile" (privacy levels, saved
/// places) is a Phase 1/2 concern layered on top of this, not a reason to
/// avoid Identity now.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public required string DisplayName { get; set; }

    /// <summary>ISO 639-1 code, e.g. "ar" or "en". Defaults to Arabic per the
    /// engineering plan's recommendation to launch Arabic-only in MVP.</summary>
    public string PreferredLanguage { get; set; } = "ar";
}
