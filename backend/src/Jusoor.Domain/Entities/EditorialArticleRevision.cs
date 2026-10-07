using Jusoor.Domain.Common;
using Jusoor.Domain.Enums;

namespace Jusoor.Domain.Entities;

/// <summary>
/// Append-only snapshot of an EditorialArticle's content AS IT WAS immediately
/// before an edit made while the article was Published (decision D4: "a
/// snapshot on every edit after first publication"). Storing the replaced
/// version means the history always answers "what did readers see before this
/// change?"; the current version is the article itself.
///
/// Same shape as <see cref="EditorialArticleTransition"/>: private setters, a
/// validating factory, no update or delete path, no foreign keys (the trail
/// must outlive whatever it describes).
/// </summary>
public class EditorialArticleRevision : BaseEntity
{
    public const int ReasonMaxLength = 1000;
    public const int ActorRolesMaxLength = 256;

    public Guid ArticleId { get; private set; }

    /// <summary>1-based, per article, assigned by the caller (count of earlier revisions + 1).</summary>
    public int RevisionNumber { get; private set; }

    public string Title { get; private set; } = null!;
    public string? AuthorName { get; private set; }
    public string? Summary { get; private set; }
    public string Body { get; private set; } = null!;

    /// <summary>
    /// Format of <see cref="Body"/> at the time of the snapshot. A snapshot of an
    /// article that pre-dates the rich-text slice is <see cref="ArticleBodyFormat.PlainText"/>,
    /// and stays so: the history never rewrites what readers actually saw.
    /// </summary>
    public ArticleBodyFormat BodyFormat { get; private set; } = ArticleBodyFormat.Html;
    public Guid? CountryId { get; private set; }
    public Guid? CityId { get; private set; }

    public string EditedByUserId { get; private set; } = null!;
    public string EditorRoles { get; private set; } = null!;
    public string? Reason { get; private set; }
    public DateTimeOffset EditedAtUtc { get; private set; }

    private EditorialArticleRevision() { }

    /// <summary>Captures the article's CURRENT (about-to-be-replaced) content.</summary>
    public static EditorialArticleRevision CaptureBeforeEdit(
        EditorialArticle article,
        int revisionNumber,
        string editedByUserId,
        IEnumerable<string> editorRoles,
        string? reason,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(article);

        if (article.Id == Guid.Empty)
        {
            throw new ArgumentException("A revision must reference a persisted article.", nameof(article));
        }

        if (revisionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber), "Revision numbers start at 1.");
        }

        if (string.IsNullOrWhiteSpace(editedByUserId))
        {
            throw new ArgumentException("A revision must record who edited the article.", nameof(editedByUserId));
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmedReason is not null && trimmedReason.Length > ReasonMaxLength)
        {
            throw new ArgumentException($"Reason cannot exceed {ReasonMaxLength} characters.", nameof(reason));
        }

        return new EditorialArticleRevision
        {
            ArticleId = article.Id,
            RevisionNumber = revisionNumber,
            Title = article.Title,
            AuthorName = article.AuthorName,
            Summary = article.Summary,
            Body = article.Body,
            BodyFormat = article.BodyFormat,
            CountryId = article.CountryId,
            CityId = article.CityId,
            EditedByUserId = editedByUserId,
            EditorRoles = JoinRoles(editorRoles, ActorRolesMaxLength),
            Reason = trimmedReason,
            EditedAtUtc = nowUtc
        };
    }

    internal static string JoinRoles(IEnumerable<string> roles, int maxLength)
    {
        var joined = string.Join(",", roles.Where(r => !string.IsNullOrWhiteSpace(r)).OrderBy(r => r, StringComparer.Ordinal));
        return joined.Length > maxLength ? joined[..maxLength] : joined;
    }
}
