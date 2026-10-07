using FluentAssertions;
using Jusoor.Application.Editorial;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

/// <summary>
/// Encodes the editorial Role×Action×State rules of decision D1
/// (docs/decisions/PHASE3_EDITORIAL_DECISIONS.md). Every action includes deny
/// paths (wrong role, wrong owner) and wrong-state paths, not only allows.
/// The rows below are the same tables that were executed against the real
/// access class in the authoring sandbox.
/// </summary>
public class EditorialArticleAccessTests
{
    private const string Owner = "owner";
    private const string Other = "someone-else";

    private static EditorialArticle InState(EditorialArticleStatus status)
    {
        var article = EditorialArticle.Create("t", null, "b", Owner, null, null);
        switch (status)
        {
            case EditorialArticleStatus.InReview:
                article.SubmitForReview();
                break;
            case EditorialArticleStatus.Approved:
                article.SubmitForReview();
                article.Approve();
                break;
            case EditorialArticleStatus.Published:
                article.Publish("p", DateTimeOffset.UtcNow);
                break;
            case EditorialArticleStatus.Retracted:
                article.Publish("p", DateTimeOffset.UtcNow);
                article.Retract("chief", "retraction notice text", DateTimeOffset.UtcNow);
                break;
            case EditorialArticleStatus.Archived:
                article.Publish("p", DateTimeOffset.UtcNow);
                article.Archive("senior", DateTimeOffset.UtcNow);
                break;
        }

        return article;
    }

    private static string[] R(string role) => new[] { role };

    // ----- create -----
    [Theory]
    [InlineData(NewsroomRole.Reporter, true)]
    [InlineData(NewsroomRole.AiEditor, true)]
    [InlineData(NewsroomRole.CopyEditor, true)]
    [InlineData(NewsroomRole.SeniorEditor, true)]
    [InlineData(NewsroomRole.ManagingEditor, true)]
    [InlineData(NewsroomRole.EditorInChief, true)]
    [InlineData(NewsroomRole.SystemAdmin, false)]
    [InlineData(NewsroomRole.Researcher, false)]
    [InlineData(NewsroomRole.FactChecker, false)]
    [InlineData(NewsroomRole.SeoEditor, false)]
    [InlineData(NewsroomRole.CrisisEditor, false)]
    public void CanCreate_matches_the_matrix(string role, bool expected)
        => EditorialArticleAccess.CanCreate(R(role)).Should().Be(expected);

    [Fact]
    public void CanCreate_is_false_for_no_roles()
        => EditorialArticleAccess.CanCreate(Array.Empty<string>()).Should().BeFalse();

    // ----- view -----
    [Fact]
    public void Reporter_can_only_view_own_articles()
    {
        EditorialArticleAccess.CanView(R(NewsroomRole.Reporter), Owner, InState(EditorialArticleStatus.Draft)).Should().BeTrue();
        EditorialArticleAccess.CanView(R(NewsroomRole.Reporter), Other, InState(EditorialArticleStatus.Draft)).Should().BeFalse();
    }

    [Theory]
    [InlineData(NewsroomRole.Researcher)]
    [InlineData(NewsroomRole.CrisisEditor)]
    [InlineData(NewsroomRole.FactChecker)]
    [InlineData(NewsroomRole.SeoEditor)]
    public void Roles_without_cms_access_cannot_view_drafts(string role)
        => EditorialArticleAccess.CanView(R(role), Owner, InState(EditorialArticleStatus.Draft)).Should().BeFalse();

    [Theory]
    [InlineData(NewsroomRole.AiEditor)]
    [InlineData(NewsroomRole.CopyEditor)]
    [InlineData(NewsroomRole.SeniorEditor)]
    [InlineData(NewsroomRole.ManagingEditor)]
    [InlineData(NewsroomRole.EditorInChief)]
    [InlineData(NewsroomRole.SystemAdmin)]
    public void Editing_and_oversight_roles_can_view_any_article(string role)
        => EditorialArticleAccess.CanView(R(role), Other, InState(EditorialArticleStatus.InReview)).Should().BeTrue();

    // ----- transitions (D1) -----
    [Theory]
    [InlineData(EditorialAction.Submit, NewsroomRole.Reporter, true, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Submit, NewsroomRole.Reporter, false, EditorialArticleStatus.Draft, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Submit, NewsroomRole.Reporter, true, EditorialArticleStatus.InReview, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Submit, NewsroomRole.AiEditor, true, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Submit, NewsroomRole.CopyEditor, true, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Submit, NewsroomRole.CopyEditor, false, EditorialArticleStatus.Draft, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Submit, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Submit, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Submit, NewsroomRole.EditorInChief, false, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Submit, NewsroomRole.SystemAdmin, false, EditorialArticleStatus.Draft, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Submit, NewsroomRole.Researcher, true, EditorialArticleStatus.Draft, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.Reporter, true, EditorialArticleStatus.InReview, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.CopyEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.AiEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.CopyEditor, false, EditorialArticleStatus.Approved, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Approved, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.EditorInChief, false, EditorialArticleStatus.Approved, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Published, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.RequestRevision, NewsroomRole.SystemAdmin, false, EditorialArticleStatus.InReview, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Approve, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Approve, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Approve, NewsroomRole.EditorInChief, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Approve, NewsroomRole.CopyEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Approve, NewsroomRole.Reporter, true, EditorialArticleStatus.InReview, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Approve, NewsroomRole.SystemAdmin, false, EditorialArticleStatus.InReview, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Approve, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Approve, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Approved, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Publish, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Approved, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.Approved, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.EditorInChief, false, EditorialArticleStatus.Approved, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Publish, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Publish, NewsroomRole.EditorInChief, false, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.SystemAdmin, false, EditorialArticleStatus.Draft, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.SystemAdmin, false, EditorialArticleStatus.InReview, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Publish, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Published, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Publish, NewsroomRole.Reporter, true, EditorialArticleStatus.Approved, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Publish, NewsroomRole.CopyEditor, true, EditorialArticleStatus.InReview, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Publish, NewsroomRole.AiEditor, true, EditorialArticleStatus.Approved, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Publish, NewsroomRole.FactChecker, false, EditorialArticleStatus.Approved, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.EditorInChief, false, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.SystemAdmin, false, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.CopyEditor, false, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.Reporter, true, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Unpublish, NewsroomRole.ManagingEditor, false, EditorialArticleStatus.Approved, TransitionCheck.InvalidState)]
    public void CheckTransition_matches_the_D1_matrix(
        EditorialAction action, string role, bool actorIsOwner, EditorialArticleStatus status, TransitionCheck expected)
        => EditorialArticleAccess
            .CheckTransition(action, R(role), actorIsOwner ? Owner : Other, InState(status))
            .Should().Be(expected);

    [Fact]
    public void Any_matching_role_is_enough_for_a_multi_role_user()
        => EditorialArticleAccess
            .CheckTransition(EditorialAction.Approve, new[] { NewsroomRole.Reporter, NewsroomRole.ManagingEditor }, Other, InState(EditorialArticleStatus.InReview))
            .Should().Be(TransitionCheck.Allowed);

    [Theory]
    [InlineData(EditorialAction.Approve, EditorialArticleStatus.InReview)]
    [InlineData(EditorialAction.Publish, EditorialArticleStatus.Approved)]
    public void Senior_and_managing_editors_cannot_approve_or_publish_their_own_article(EditorialAction action, EditorialArticleStatus state)
    {
        EditorialArticleAccess.CheckTransition(action, R(NewsroomRole.SeniorEditor), Owner, InState(state))
            .Should().Be(TransitionCheck.Forbidden);
        EditorialArticleAccess.CheckTransition(action, R(NewsroomRole.ManagingEditor), Owner, InState(state))
            .Should().Be(TransitionCheck.Forbidden);
    }

    [Theory]
    [InlineData(EditorialAction.Approve, EditorialArticleStatus.InReview)]
    [InlineData(EditorialAction.Publish, EditorialArticleStatus.Approved)]
    public void Editor_in_chief_is_exempt_from_the_four_eyes_rule(EditorialAction action, EditorialArticleStatus state)
        => EditorialArticleAccess.CheckTransition(action, R(NewsroomRole.EditorInChief), Owner, InState(state))
            .Should().Be(TransitionCheck.Allowed);

    // ----- editing (D1) -----
    [Theory]
    [InlineData(NewsroomRole.Reporter, true, EditorialArticleStatus.Draft, true)]
    [InlineData(NewsroomRole.Reporter, false, EditorialArticleStatus.Draft, false)]
    [InlineData(NewsroomRole.Reporter, true, EditorialArticleStatus.InReview, false)]
    [InlineData(NewsroomRole.Reporter, true, EditorialArticleStatus.Approved, false)]
    [InlineData(NewsroomRole.Reporter, true, EditorialArticleStatus.Published, false)]
    [InlineData(NewsroomRole.CopyEditor, true, EditorialArticleStatus.Draft, true)]
    [InlineData(NewsroomRole.CopyEditor, false, EditorialArticleStatus.Draft, false)]
    [InlineData(NewsroomRole.CopyEditor, false, EditorialArticleStatus.InReview, true)]
    [InlineData(NewsroomRole.CopyEditor, false, EditorialArticleStatus.Approved, false)]
    [InlineData(NewsroomRole.CopyEditor, false, EditorialArticleStatus.Published, false)]
    [InlineData(NewsroomRole.AiEditor, false, EditorialArticleStatus.InReview, true)]
    [InlineData(NewsroomRole.AiEditor, false, EditorialArticleStatus.Draft, false)]
    [InlineData(NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Draft, true)]
    [InlineData(NewsroomRole.SeniorEditor, false, EditorialArticleStatus.InReview, true)]
    [InlineData(NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Approved, true)]
    [InlineData(NewsroomRole.SeniorEditor, false, EditorialArticleStatus.Published, true)]
    [InlineData(NewsroomRole.ManagingEditor, false, EditorialArticleStatus.Published, true)]
    [InlineData(NewsroomRole.EditorInChief, false, EditorialArticleStatus.Approved, true)]
    [InlineData(NewsroomRole.SystemAdmin, true, EditorialArticleStatus.Draft, false)]
    [InlineData(NewsroomRole.SystemAdmin, false, EditorialArticleStatus.InReview, false)]
    [InlineData(NewsroomRole.Researcher, true, EditorialArticleStatus.Draft, false)]
    [InlineData(NewsroomRole.FactChecker, true, EditorialArticleStatus.Draft, false)]
    public void CanEdit_matches_the_D1_matrix(string role, bool actorIsOwner, EditorialArticleStatus status, bool expected)
        => EditorialArticleAccess
            .CanEdit(R(role), actorIsOwner ? Owner : Other, InState(status))
            .Should().Be(expected);

    // ================= Slice 20b (Q9): Retracted / Archived =================

    private static readonly EditorialArticleStatus[] AllStatuses =
        Enum.GetValues<EditorialArticleStatus>();

    private static readonly EditorialAction[] NewActions =
    {
        EditorialAction.Retract, EditorialAction.Archive,
        EditorialAction.RestoreArchived, EditorialAction.ReinstateRetracted
    };

    private static readonly string[] SeniorUp =
    {
        NewsroomRole.SeniorEditor, NewsroomRole.ManagingEditor, NewsroomRole.EditorInChief
    };

    /// <summary>The plan's table (SLICE_20B_IMPLEMENTATION_PLAN §3), stated independently of the implementation.</summary>
    private static TransitionCheck ExpectedForNewAction(EditorialAction action, string role, EditorialArticleStatus status)
    {
        var allowedRoles = action == EditorialAction.ReinstateRetracted
            ? new[] { NewsroomRole.EditorInChief }
            : SeniorUp;
        var requiredState = action switch
        {
            EditorialAction.Retract => EditorialArticleStatus.Published,
            EditorialAction.Archive => EditorialArticleStatus.Published,
            EditorialAction.RestoreArchived => EditorialArticleStatus.Archived,
            _ => EditorialArticleStatus.Retracted
        };

        if (!allowedRoles.Contains(role))
        {
            return TransitionCheck.Forbidden; // role is checked before state
        }

        return status == requiredState ? TransitionCheck.Allowed : TransitionCheck.InvalidState;
    }

    public static IEnumerable<object[]> EveryRoleActionStateForTheNewActions()
    {
        foreach (var action in NewActions)
        foreach (var role in NewsroomRole.All)
        foreach (var status in AllStatuses)
        {
            yield return new object[] { action, role, status, ExpectedForNewAction(action, role, status) };
        }
    }

    [Theory]
    [MemberData(nameof(EveryRoleActionStateForTheNewActions))]
    public void New_actions_match_the_plan_for_every_role_and_state(
        EditorialAction action, string role, EditorialArticleStatus status, TransitionCheck expected)
    {
        // Owner = true so that "own items" can never be what grants or hides access.
        EditorialArticleAccess.CheckTransition(action, R(role), Owner, InState(status)).Should().Be(expected);
    }

    [Theory]
    [InlineData(EditorialAction.Retract, NewsroomRole.SeniorEditor, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Retract, NewsroomRole.ManagingEditor, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Retract, NewsroomRole.EditorInChief, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Retract, NewsroomRole.SystemAdmin, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Retract, NewsroomRole.CopyEditor, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Retract, NewsroomRole.Reporter, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Retract, NewsroomRole.SeniorEditor, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Retract, NewsroomRole.SeniorEditor, EditorialArticleStatus.Archived, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Retract, NewsroomRole.SeniorEditor, EditorialArticleStatus.Retracted, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.Archive, NewsroomRole.SeniorEditor, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.Archive, NewsroomRole.SystemAdmin, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.Archive, NewsroomRole.ManagingEditor, EditorialArticleStatus.Retracted, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.RestoreArchived, NewsroomRole.ManagingEditor, EditorialArticleStatus.Archived, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.RestoreArchived, NewsroomRole.SystemAdmin, EditorialArticleStatus.Archived, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.RestoreArchived, NewsroomRole.SeniorEditor, EditorialArticleStatus.Published, TransitionCheck.InvalidState)]
    [InlineData(EditorialAction.ReinstateRetracted, NewsroomRole.EditorInChief, EditorialArticleStatus.Retracted, TransitionCheck.Allowed)]
    [InlineData(EditorialAction.ReinstateRetracted, NewsroomRole.SeniorEditor, EditorialArticleStatus.Retracted, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.ReinstateRetracted, NewsroomRole.ManagingEditor, EditorialArticleStatus.Retracted, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.ReinstateRetracted, NewsroomRole.SystemAdmin, EditorialArticleStatus.Retracted, TransitionCheck.Forbidden)]
    [InlineData(EditorialAction.ReinstateRetracted, NewsroomRole.EditorInChief, EditorialArticleStatus.Archived, TransitionCheck.InvalidState)]
    public void New_actions_readable_table(
        EditorialAction action, string role, EditorialArticleStatus status, TransitionCheck expected)
        => EditorialArticleAccess.CheckTransition(action, R(role), Other, InState(status)).Should().Be(expected);

    [Fact]
    public void System_admin_gets_none_of_the_four_new_actions_in_any_state()
    {
        foreach (var action in NewActions)
        foreach (var status in AllStatuses)
        {
            EditorialArticleAccess.CheckTransition(action, R(NewsroomRole.SystemAdmin), Owner, InState(status))
                .Should().Be(TransitionCheck.Forbidden, because: $"{action} on {status}");
        }
    }

    [Fact]
    public void Reinstating_needs_the_editor_in_chief_role_even_for_a_multi_role_user()
    {
        var retracted = InState(EditorialArticleStatus.Retracted);

        EditorialArticleAccess.CheckTransition(EditorialAction.ReinstateRetracted,
            new[] { NewsroomRole.SeniorEditor, NewsroomRole.ManagingEditor }, Other, retracted).Should().Be(TransitionCheck.Forbidden);
        EditorialArticleAccess.CheckTransition(EditorialAction.ReinstateRetracted,
            new[] { NewsroomRole.Reporter, NewsroomRole.EditorInChief }, Other, retracted).Should().Be(TransitionCheck.Allowed);
    }

    [Fact]
    public void The_older_actions_are_never_allowed_on_retracted_or_archived_for_any_role()
    {
        var oldActions = new[]
        {
            EditorialAction.Submit, EditorialAction.RequestRevision, EditorialAction.Approve,
            EditorialAction.Publish, EditorialAction.Unpublish
        };

        foreach (var status in new[] { EditorialArticleStatus.Retracted, EditorialArticleStatus.Archived })
        foreach (var action in oldActions)
        foreach (var role in NewsroomRole.All)
        {
            EditorialArticleAccess.CheckTransition(action, R(role), Owner, InState(status))
                .Should().NotBe(TransitionCheck.Allowed, because: $"{role} {action} on {status}");
        }
    }

    [Fact]
    public void Role_lists_for_the_new_endpoints_exclude_system_admin_and_reinstate_is_chief_only()
    {
        EditorialArticleAccess.RetractionRoles.Should().BeEquivalentTo(SeniorUp);
        EditorialArticleAccess.RetractionRoles.Should().NotContain(NewsroomRole.SystemAdmin);
        EditorialArticleAccess.ReinstateRoles.Should().Equal(NewsroomRole.EditorInChief);
    }

    // ----- edit lock -----
    [Fact]
    public void Retracted_and_archived_articles_are_not_editable_by_anyone()
    {
        foreach (var status in new[] { EditorialArticleStatus.Retracted, EditorialArticleStatus.Archived })
        foreach (var role in NewsroomRole.All)
        foreach (var owner in new[] { Owner, Other })
        {
            EditorialArticleAccess.CanEdit(R(role), owner, InState(status))
                .Should().BeFalse(because: $"{role} (owner: {owner == Owner}) on {status}");
        }
    }

    [Theory]
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.Retracted, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.ManagingEditor, EditorialArticleStatus.Archived, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.EditorInChief, EditorialArticleStatus.Retracted, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.EditorInChief, EditorialArticleStatus.Archived, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.CopyEditor, EditorialArticleStatus.Archived, TransitionCheck.Forbidden)]
    [InlineData(NewsroomRole.Reporter, EditorialArticleStatus.Retracted, TransitionCheck.Forbidden)]
    [InlineData(NewsroomRole.SystemAdmin, EditorialArticleStatus.Archived, TransitionCheck.Forbidden)]
    [InlineData(NewsroomRole.Researcher, EditorialArticleStatus.Retracted, TransitionCheck.Forbidden)]
    // Unchanged behaviour on the older states:
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(NewsroomRole.Reporter, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    public void CheckEdit_reports_role_before_state(string role, EditorialArticleStatus status, TransitionCheck expected)
        => EditorialArticleAccess.CheckEdit(R(role), Owner, InState(status)).Should().Be(expected);

    // ----- corrections -----
    [Theory]
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.Published, TransitionCheck.Allowed)]
    [InlineData(NewsroomRole.ManagingEditor, EditorialArticleStatus.Archived, TransitionCheck.Allowed)]
    [InlineData(NewsroomRole.EditorInChief, EditorialArticleStatus.Archived, TransitionCheck.Allowed)]
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.Retracted, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.EditorInChief, EditorialArticleStatus.Retracted, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.Draft, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.InReview, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.SeniorEditor, EditorialArticleStatus.Approved, TransitionCheck.InvalidState)]
    [InlineData(NewsroomRole.SystemAdmin, EditorialArticleStatus.Archived, TransitionCheck.Forbidden)]
    [InlineData(NewsroomRole.SystemAdmin, EditorialArticleStatus.Retracted, TransitionCheck.Forbidden)]
    [InlineData(NewsroomRole.CopyEditor, EditorialArticleStatus.Archived, TransitionCheck.Forbidden)]
    [InlineData(NewsroomRole.Reporter, EditorialArticleStatus.Published, TransitionCheck.Forbidden)]
    public void CheckCorrection_accepts_published_and_archived_but_not_retracted(
        string role, EditorialArticleStatus status, TransitionCheck expected)
        => EditorialArticleAccess.CheckCorrection(R(role), InState(status)).Should().Be(expected);
}
