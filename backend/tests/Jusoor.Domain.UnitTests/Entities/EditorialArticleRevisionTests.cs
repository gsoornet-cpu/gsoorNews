using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class EditorialArticleRevisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    private static EditorialArticle PublishedArticle()
    {
        var article = EditorialArticle.Create("العنوان الأصلي", "الملخص الأصلي", "النص الأصلي", "rep-1", null, null);
        article.Publish("pub-1", Now.AddHours(-1));
        return article;
    }

    [Fact]
    public void CaptureBeforeEdit_should_copy_the_current_content_and_record_who_when_and_why()
    {
        var article = PublishedArticle();

        var revision = EditorialArticleRevision.CaptureBeforeEdit(
            article, 1, "senior-1", new[] { NewsroomRole.SeniorEditor }, "  تصحيح اسم  ", Now);

        revision.ArticleId.Should().Be(article.Id);
        revision.RevisionNumber.Should().Be(1);
        revision.Title.Should().Be("العنوان الأصلي");
        revision.Summary.Should().Be("الملخص الأصلي");
        revision.Body.Should().Be("النص الأصلي");
        revision.EditedByUserId.Should().Be("senior-1");
        revision.EditorRoles.Should().Be(NewsroomRole.SeniorEditor);
        revision.Reason.Should().Be("تصحيح اسم");
        revision.EditedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void A_captured_revision_should_not_change_when_the_article_is_edited_afterwards()
    {
        var article = PublishedArticle();
        var revision = EditorialArticleRevision.CaptureBeforeEdit(article, 1, "senior-1", new[] { NewsroomRole.SeniorEditor }, null, Now);

        article.UpdateContent("عنوان جديد", null, "نص جديد", null, null);

        revision.Title.Should().Be("العنوان الأصلي");
        revision.Body.Should().Be("النص الأصلي");
    }

    [Fact]
    public void CaptureBeforeEdit_should_normalise_a_blank_reason_to_null_and_order_roles_stably()
    {
        var revision = EditorialArticleRevision.CaptureBeforeEdit(
            PublishedArticle(), 2, "u-1", new[] { NewsroomRole.SeniorEditor, NewsroomRole.CopyEditor }, "   ", Now);

        revision.Reason.Should().BeNull();
        revision.EditorRoles.Should().Be("CopyEditor,SeniorEditor");
    }

    [Fact]
    public void CaptureBeforeEdit_should_reject_invalid_input()
    {
        var article = PublishedArticle();
        var roles = new[] { NewsroomRole.SeniorEditor };

        Action zeroNumber = () => EditorialArticleRevision.CaptureBeforeEdit(article, 0, "u", roles, null, Now);
        Action noActor = () => EditorialArticleRevision.CaptureBeforeEdit(article, 1, " ", roles, null, Now);
        Action longReason = () => EditorialArticleRevision.CaptureBeforeEdit(
            article, 1, "u", roles, new string('x', EditorialArticleRevision.ReasonMaxLength + 1), Now);
        Action nullArticle = () => EditorialArticleRevision.CaptureBeforeEdit(null!, 1, "u", roles, null, Now);

        zeroNumber.Should().Throw<ArgumentOutOfRangeException>();
        noActor.Should().Throw<ArgumentException>();
        longReason.Should().Throw<ArgumentException>();
        nullArticle.Should().Throw<ArgumentNullException>();
    }
}
