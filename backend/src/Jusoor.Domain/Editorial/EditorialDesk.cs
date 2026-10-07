namespace Jusoor.Domain.Editorial;

/// <summary>Presentation desks from the approved Final Demo. These are independent
/// of the newsroom-owned topical taxonomy (EditorialCategory).</summary>
public static class EditorialDesk
{
    public static readonly IReadOnlyDictionary<string, string> Catalog = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["mughtarib"] = "أخبار المغتربين",
        ["success"] = "قصة نجاح",
        ["egypt"] = "أخبار مصر",
        ["opportunities"] = "فرص استثمارية",
        ["official"] = "مع مسئول",
        ["events"] = "فعاليات",
        ["red"] = "خط أحمر",
        ["lamma"] = "اللمة الحلوة",
        ["secondgen"] = "الجيل الثاني",
        ["sports"] = "رياضة",
        ["arts"] = "فنون",
        ["articles"] = "مقالات رأي",
        ["various"] = "منوعات",
    };

    public static bool IsValid(string? slug) => slug is not null && Catalog.ContainsKey(slug);
}
