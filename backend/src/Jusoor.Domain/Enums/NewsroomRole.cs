namespace Jusoor.Domain.Enums;

/// <summary>
/// Newsroom roles as defined in spec §25. These are seeded as ASP.NET Identity
/// roles during Phase 0 so that Phase 3's RBAC work has a stable, spec-derived
/// set of role names to build permissions against — but no permission matrix
/// is enforced yet. Enforcing these against endpoints before Phase 3's
/// Role×Action permission matrix exists would mean inventing the matrix twice.
/// </summary>
public static class NewsroomRole
{
    public const string Reporter = "Reporter";                 // مراسل
    public const string Researcher = "Researcher";              // باحث
    public const string AiEditor = "AiEditor";                  // محرّر ذكاء اصطناعي
    public const string CopyEditor = "CopyEditor";               // محرّر لغوي
    public const string SeniorEditor = "SeniorEditor";            // محرّر أول
    public const string ManagingEditor = "ManagingEditor";          // مدير التحرير
    public const string EditorInChief = "EditorInChief";           // رئيس التحرير
    public const string FactChecker = "FactChecker";             // مدقّق حقائق
    public const string SeoEditor = "SeoEditor";                // محرّر سيو
    public const string CrisisEditor = "CrisisEditor";            // محرّر أزمات
    public const string SystemAdmin = "SystemAdmin";             // مسؤول نظام

    public static readonly IReadOnlyList<string> All = new[]
    {
        Reporter, Researcher, AiEditor, CopyEditor, SeniorEditor,
        ManagingEditor, EditorInChief, FactChecker, SeoEditor,
        CrisisEditor, SystemAdmin
    };
}
