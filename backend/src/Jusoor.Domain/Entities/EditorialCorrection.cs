using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// An append-only public editorial note on a published article (decision D4,
/// "تنويه صحفي / تصحيح"): when it was issued, its nature, and the editor's
/// note. A correction is never edited or deleted — if one was wrong, a newer
/// one is issued. <see cref="IsMajor"/> places the note at the top of the
/// article instead of the bottom (open question Q11: chosen by the issuing
/// editor). The issuer's id and roles are kept for the CMS audit view only and
/// are never part of a public DTO.
/// </summary>
public class EditorialCorrection : BaseEntity
{
    public const int NoteMaxLength = 2000;
    public const int IssuerRolesMaxLength = 256;

    public Guid ArticleId { get; private set; }
    public CorrectionKind Kind { get; private set; }
    public string Note { get; private set; } = null!;
    public bool IsMajor { get; private set; }
    public string IssuedByUserId { get; private set; } = null!;
    public string IssuerRoles { get; private set; } = null!;
    public DateTimeOffset IssuedAtUtc { get; private set; }

    private EditorialCorrection() { }

    public static EditorialCorrection Issue(
        Guid articleId,
        CorrectionKind kind,
        string note,
        bool isMajor,
        string issuedByUserId,
        IEnumerable<string> issuerRoles,
        DateTimeOffset nowUtc)
    {
        if (articleId == Guid.Empty)
        {
            throw new ArgumentException("A correction must reference an article.", nameof(articleId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException("Unknown correction kind.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("A correction must have a note.", nameof(note));
        }

        var trimmedNote = note.Trim();
        if (trimmedNote.Length > NoteMaxLength)
        {
            throw new ArgumentException($"Note cannot exceed {NoteMaxLength} characters.", nameof(note));
        }

        if (string.IsNullOrWhiteSpace(issuedByUserId))
        {
            throw new ArgumentException("A correction must record who issued it.", nameof(issuedByUserId));
        }

        return new EditorialCorrection
        {
            ArticleId = articleId,
            Kind = kind,
            Note = trimmedNote,
            IsMajor = isMajor,
            IssuedByUserId = issuedByUserId,
            IssuerRoles = EditorialArticleRevision.JoinRoles(issuerRoles, IssuerRolesMaxLength),
            IssuedAtUtc = nowUtc
        };
    }
}
