using Jusoor.Domain.Common;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Spec §12's "أماكني" (My Places) — deliberately replaces a "family list"
/// with places only: "المستخدم يتابع أماكن فقط... دون إلزامه بإدخال أسماء
/// أو صفة القرابة" (the user follows places only, without being required
/// to enter names or a relationship). No name, no relationship, no person
/// — just a City. That omission is the safety feature the spec describes,
/// not a missing field.
///
/// Scoped to City for this slice: both of the spec's own examples ("القاهرة،
/// تورونتو" — Cairo, Toronto) are cities, and nothing in §12 asks for
/// following a bare Country or Region. Extending to that later is a small,
/// additive change if a real need for it shows up — not a reason to build
/// it speculatively now.
/// </summary>
public class MyPlace : BaseEntity
{
    public string UserId { get; private set; } = null!;
    public Guid CityId { get; private set; }
    public DateTimeOffset FollowedAtUtc { get; private set; }

    private MyPlace() { }

    public static MyPlace Create(string userId, Guid cityId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("A MyPlace must belong to a user.", nameof(userId));
        }

        if (cityId == Guid.Empty)
        {
            throw new ArgumentException("A MyPlace must reference a real city.", nameof(cityId));
        }

        return new MyPlace { UserId = userId, CityId = cityId, FollowedAtUtc = nowUtc };
    }
}
