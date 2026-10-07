namespace Jusoor.Domain.Enums;

/// <summary>
/// The nature of a public editorial note attached to a published article
/// (decision D4: "تنويه صحفي / تصحيح"). Stored as an int; add new kinds only
/// by appending new numbers.
/// </summary>
public enum CorrectionKind
{
    /// <summary>تصحيح — something previously stated was wrong and is now corrected.</summary>
    Correction = 1,

    /// <summary>تنويه — a clarification or editor's notice that does not admit an error.</summary>
    Notice = 2,

    /// <summary>توضيح — an explicit clarification of facts or context.</summary>
    Clarification = 3,

    /// <summary>تنويه المحرر — a newsroom note that is not a correction.</summary>
    EditorNote = 4,

    /// <summary>تحديث — a developing-story update, not an admission of error.</summary>
    Update = 5
}
