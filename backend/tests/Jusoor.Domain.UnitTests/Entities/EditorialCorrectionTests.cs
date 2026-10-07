using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class EditorialCorrectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly string[] Senior = { NewsroomRole.SeniorEditor };

    [Fact]
    public void Issue_should_record_kind_note_placement_issuer_and_time()
    {
        var articleId = Guid.NewGuid();

        var correction = EditorialCorrection.Issue(articleId, CorrectionKind.Correction, "  صُحّح اسم المدينة  ", true, "senior-1", Senior, Now);

        correction.ArticleId.Should().Be(articleId);
        correction.Kind.Should().Be(CorrectionKind.Correction);
        correction.Note.Should().Be("صُحّح اسم المدينة");
        correction.IsMajor.Should().BeTrue();
        correction.IssuedByUserId.Should().Be("senior-1");
        correction.IssuerRoles.Should().Be(NewsroomRole.SeniorEditor);
        correction.IssuedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Issue_should_reject_invalid_input()
    {
        var id = Guid.NewGuid();

        Action noArticle = () => EditorialCorrection.Issue(Guid.Empty, CorrectionKind.Notice, "n", false, "u", Senior, Now);
        Action badKind = () => EditorialCorrection.Issue(id, (CorrectionKind)99, "n", false, "u", Senior, Now);
        Action blankNote = () => EditorialCorrection.Issue(id, CorrectionKind.Notice, "  ", false, "u", Senior, Now);
        Action longNote = () => EditorialCorrection.Issue(
            id, CorrectionKind.Notice, new string('x', EditorialCorrection.NoteMaxLength + 1), false, "u", Senior, Now);
        Action noIssuer = () => EditorialCorrection.Issue(id, CorrectionKind.Notice, "n", false, " ", Senior, Now);

        noArticle.Should().Throw<ArgumentException>();
        badKind.Should().Throw<ArgumentException>();
        blankNote.Should().Throw<ArgumentException>();
        longNote.Should().Throw<ArgumentException>();
        noIssuer.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Kind_values_are_pinned_because_the_column_is_an_int()
    {
        ((int)CorrectionKind.Correction).Should().Be(1);
        ((int)CorrectionKind.Notice).Should().Be(2);
    }
}
