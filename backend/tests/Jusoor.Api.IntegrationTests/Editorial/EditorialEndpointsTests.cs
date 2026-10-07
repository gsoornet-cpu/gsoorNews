using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Hangfire;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Identity;
using Jusoor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Editorial;

/// <summary>
/// End-to-end coverage of the demo-critical path (create → save → edit →
/// publish → public feed) against real Postgres, plus the authorization
/// boundaries of the approved Role×Action matrix: both allow and deny.
/// </summary>
public class EditorialEndpointsTests : IClassFixture<JusoorApiFactory>
{
    private readonly JusoorApiFactory _factory;

    public EditorialEndpointsTests(JusoorApiFactory factory) => _factory = factory;

    private sealed record Actor(HttpClient Client, string UserId);
    private sealed class NotCancelledJobToken : IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;
        public void ThrowIfCancellationRequested() { }
    }
    private sealed class FixedClock(DateTimeOffset utcNow) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private async Task<Actor> NewActorAsync(string? role)
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "P@ssword1";

        await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Test User" });

        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            userId = user!.Id;
            if (role is not null)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());

        return new Actor(client, userId);
    }

    private static object ArticleBody(string title = "عنوان تجريبي للخبر", string body = "نص الخبر الكامل هنا.")
        => new { title, summary = "ملخص قصير", body };

    private static async Task<Guid> CreateDraftAsync(Actor actor, string title = "عنوان تجريبي للخبر")
    {
        var response = await actor.Client.PostAsJsonAsync("/api/v1/cms/articles", ArticleBody(title));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// The normal route to publication under decision D1: a Draft goes InReview
    /// first (the publisher submits it on the author's behalf), then is published.
    /// Only the Editor-in-Chief and SystemAdmin may publish straight from Draft.
    /// </summary>
    private static async Task<HttpResponseMessage> PublishViaReviewAsync(Actor publisher, Guid id)
    {
        (await publisher.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return await publisher.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null);
    }

    private async Task<List<EditorialArticleTransition>> TrailAsync(Guid articleId)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.EditorialArticleTransitions.AsNoTracking()
            .Where(t => t.ArticleId == articleId).OrderBy(t => t.OccurredAtUtc).ToListAsync();
    }

    private static async Task<bool> PublicFeedContainsAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync("/api/v1/news?pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("id").GetGuid() == id);
    }

    // ---------------- authentication / role gate ----------------

    [Fact]
    public async Task Cms_endpoints_should_reject_anonymous_callers()
    {
        var anonymous = _factory.CreateClient();

        (await anonymous.GetAsync("/api/v1/cms/articles")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/cms/articles", ArticleBody())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsync($"/api/v1/cms/articles/{Guid.NewGuid()}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(NewsroomRole.Researcher)]
    [InlineData(NewsroomRole.CrisisEditor)]
    [InlineData(NewsroomRole.FactChecker)]
    [InlineData(NewsroomRole.SeoEditor)]
    public async Task Users_without_a_cms_role_should_be_forbidden(string? role)
    {
        var actor = await NewActorAsync(role);

        (await actor.Client.GetAsync("/api/v1/cms/articles")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await actor.Client.PostAsJsonAsync("/api/v1/cms/articles", ArticleBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- the demo flow ----------------

    [Fact]
    public async Task Managing_editor_can_publish_a_reporters_article_and_the_public_can_then_read_it()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var visitor = _factory.CreateClient();

        var id = await CreateDraftAsync(reporter, "خبر العرض التجريبي");

        // Draft: persisted, visible in the CMS, invisible to the public.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = await context.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == id);
            saved.Status.Should().Be(EditorialArticleStatus.Draft);
            saved.OwnerUserId.Should().Be(reporter.UserId);
        }
        (await editor.Client.GetAsync($"/api/v1/cms/articles/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublicFeedContainsAsync(visitor, id)).Should().BeFalse();
        (await visitor.GetAsync($"/api/v1/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Edit the draft.
        var update = await reporter.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}",
            new { title = "خبر العرض بعد التعديل", summary = "ملخص", body = "نص معدّل" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        // Publish.
        var publish = await PublishViaReviewAsync(editor, id);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        // Public: visible in the feed and by id, with the edited content and no internal fields.
        (await PublicFeedContainsAsync(visitor, id)).Should().BeTrue();
        var detailResponse = await visitor.GetAsync($"/api/v1/news/{id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("title").GetString().Should().Be("خبر العرض بعد التعديل");
        detail.GetProperty("body").GetString().Should().Be("نص معدّل");
        detail.TryGetProperty("ownerUserId", out _).Should().BeFalse();
        detail.TryGetProperty("status", out _).Should().BeFalse();

        // Unpublish removes it from the public again.
        (await editor.Client.PostAsync($"/api/v1/cms/articles/{id}/unpublish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await visitor.GetAsync($"/api/v1/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PublicFeedContainsAsync(visitor, id)).Should().BeFalse();
    }

    [Fact]
    public async Task Publishing_twice_should_return_conflict()
    {
        var editor = await NewActorAsync(NewsroomRole.EditorInChief);
        var id = await CreateDraftAsync(editor);

        (await editor.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await editor.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------- Reporter: own items only, never publishes ----------------

    [Fact]
    public async Task Reporter_can_create_but_cannot_publish_and_the_draft_stays_private()
    {
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter);

        var publish = await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null);

        publish.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PublicFeedContainsAsync(_factory.CreateClient(), id)).Should().BeFalse();
    }

    [Fact]
    public async Task Reporter_cannot_see_or_edit_another_reporters_article()
    {
        var owner = await NewActorAsync(NewsroomRole.Reporter);
        var stranger = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(owner, "خبر خاص بالمراسل الأول");

        (await stranger.Client.GetAsync($"/api/v1/cms/articles/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("اختراق")))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await stranger.Client.GetFromJsonAsync<JsonElement>("/api/v1/cms/articles?pageSize=50");
        list.GetProperty("items").EnumerateArray().Should().NotContain(i => i.GetProperty("id").GetGuid() == id);

        var unchanged = await owner.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}");
        unchanged.GetProperty("title").GetString().Should().Be("خبر خاص بالمراسل الأول");
    }

    [Fact]
    public async Task Reporter_can_edit_their_own_draft_but_not_after_it_is_published()
    {
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var manager = await NewActorAsync(NewsroomRole.ManagingEditor);
        var id = await CreateDraftAsync(reporter);

        (await reporter.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("عنوان معدّل من المراسل")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await PublishViaReviewAsync(manager, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await reporter.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("تعديل بعد النشر")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var live = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        live.GetProperty("title").GetString().Should().Be("عنوان معدّل من المراسل");
    }

    // ---------------- SeniorEditor: reviews and publishes any article in the workflow ----------------

    [Fact]
    public async Task Senior_editor_can_approve_and_publish_any_article_in_review_but_not_a_draft()
    {
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var reviewedId = await CreateDraftAsync(reporter);
        var draftId = await CreateDraftAsync(reporter);
        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{reviewedId}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await senior.Client.PostAsync($"/api/v1/cms/articles/{draftId}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await senior.Client.PostAsync($"/api/v1/cms/articles/{reviewedId}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await senior.Client.PostAsync($"/api/v1/cms/articles/{reviewedId}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var visitor = _factory.CreateClient();
        (await PublicFeedContainsAsync(visitor, reviewedId)).Should().BeTrue();
        (await PublicFeedContainsAsync(visitor, draftId)).Should().BeFalse();
    }

    [Fact]
    public async Task Copy_editor_can_edit_only_articles_in_review_and_can_never_publish()
    {
        var copyEditor = await NewActorAsync(NewsroomRole.CopyEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter);

        // A reporter's draft is the reporter's until it is submitted.
        (await copyEditor.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("تدقيق لغوي")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await copyEditor.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("تدقيق لغوي")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await copyEditor.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------- SystemAdmin ----------------

    [Fact]
    public async Task System_admin_can_publish_but_cannot_create_or_edit()
    {
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter);

        (await admin.Client.PostAsJsonAsync("/api/v1/cms/articles", ArticleBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("تعديل"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/publish", new { reason = "Emergency legal publication" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------- validation ----------------

    [Fact]
    public async Task Create_should_reject_invalid_input_with_400_and_persist_nothing()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);

        (await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = "  ", body = "نص" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = "عنوان", body = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = new string('ع', 301), body = "نص" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = "عنوان", body = "نص", cityId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = "عنوان", body = "نص", countryId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var list = await editor.Client.GetFromJsonAsync<JsonElement>("/api/v1/cms/articles?pageSize=50");
        list.GetProperty("items").EnumerateArray()
            .Should().NotContain(i => i.GetProperty("ownerUserId").GetString() == editor.UserId);
    }

    [Fact]
    public async Task Create_with_a_real_country_and_matching_city_should_succeed_and_show_in_the_public_dto()
    {
        Guid countryId, cityId;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var country = new Country { NameAr = "الإمارات", NameEn = "UAE", IsoCode2 = "A1", Slug = $"uae-{Guid.NewGuid():N}" };
            var city = new City { CountryId = country.Id, NameAr = "دبي", NameEn = "Dubai", Slug = $"dubai-{Guid.NewGuid():N}" };
            context.Countries.Add(country);
            context.Cities.Add(city);
            await context.SaveChangesAsync();
            countryId = country.Id;
            cityId = city.Id;
        }

        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var create = await reporter.Client.PostAsJsonAsync("/api/v1/cms/articles",
            new { title = "خبر من دبي", body = "نص", countryId, cityId });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await PublishViaReviewAsync(editor, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        detail.GetProperty("cityNameAr").GetString().Should().Be("دبي");
        detail.GetProperty("countryNameEn").GetString().Should().Be("UAE");
    }

    [Fact]
    public async Task List_should_reject_an_unknown_status_filter_and_an_oversized_page()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);

        (await editor.Client.GetAsync("/api/v1/cms/articles?status=Banana")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await editor.Client.GetAsync("/api/v1/cms/articles?pageSize=1000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------- public API ----------------

    [Fact]
    public async Task Public_news_should_return_404_for_an_unknown_id_and_400_for_an_oversized_page()
    {
        var visitor = _factory.CreateClient();

        (await visitor.GetAsync($"/api/v1/news/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await visitor.GetAsync("/api/v1/news?pageSize=1000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------- SEO foundation (Slice 18) ----------------

    [Fact]
    public async Task Public_sitemap_should_list_only_published_articles_and_expose_no_internal_fields()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var visitor = _factory.CreateClient();
        var publishedId = await CreateDraftAsync(reporter, "خبر منشور في الخريطة");
        var draftId = await CreateDraftAsync(reporter, "مسودة لا تظهر في الخريطة");
        (await PublishViaReviewAsync(editor, publishedId)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Anonymous, and not swallowed by the {id:guid} route.
        var response = await visitor.GetAsync("/api/v1/news/sitemap");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();

        var published = entries.Single(e => e.GetProperty("id").GetGuid() == publishedId);
        published.GetProperty("title").GetString().Should().Be("خبر منشور في الخريطة");
        published.GetProperty("updatedAtUtc").GetDateTimeOffset()
            .Should().BeOnOrAfter(published.GetProperty("publishedAtUtc").GetDateTimeOffset());
        entries.Any(e => e.GetProperty("id").GetGuid() == draftId).Should().BeFalse();
        foreach (var internalField in new[] { "body", "summary", "ownerUserId", "status", "publishedByUserId" })
        {
            published.TryGetProperty(internalField, out _).Should().BeFalse($"'{internalField}' must not be in the public sitemap feed");
        }

        // Unpublishing removes it again.
        (await editor.Client.PostAsync($"/api/v1/cms/articles/{publishedId}/unpublish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await (await visitor.GetAsync("/api/v1/news/sitemap")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray();
        after.Any(e => e.GetProperty("id").GetGuid() == publishedId).Should().BeFalse();
    }

    [Fact]
    public async Task Editing_a_published_article_should_advance_updated_at_but_not_published_at()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var visitor = _factory.CreateClient();
        var id = await CreateDraftAsync(reporter, "خبر سيُحدَّث");
        (await PublishViaReviewAsync(editor, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var before = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        var publishedAt = before.GetProperty("publishedAtUtc").GetDateTimeOffset();
        before.GetProperty("updatedAtUtc").GetDateTimeOffset().Should().BeOnOrAfter(publishedAt);

        var update = await editor.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}",
            new { title = "خبر سيُحدَّث", summary = "ملخص", body = "نص بعد التحديث", editReason = "تصحيح سياق الخبر" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        after.GetProperty("publishedAtUtc").GetDateTimeOffset().Should().Be(publishedAt);
        after.GetProperty("updatedAtUtc").GetDateTimeOffset().Should().BeAfter(publishedAt);
    }

    // ---------------- editorial workflow (Slice 19, decision D1) ----------------

    [Fact]
    public async Task Workflow_should_take_an_article_through_review_to_publication_and_persist_an_audit_trail()
    {
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var copyEditor = await NewActorAsync(NewsroomRole.CopyEditor);
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var managing = await NewActorAsync(NewsroomRole.ManagingEditor);
        var id = await CreateDraftAsync(reporter, "خبر يمر بسير العمل");

        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        // Once submitted the article is locked for its author.
        (await reporter.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("تعديل بعد التسليم")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var revision = await copyEditor.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/request-revision", new { reason = "أضف المصدر" });
        revision.StatusCode.Should().Be(HttpStatusCode.OK);
        (await revision.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Draft");

        // Back with the author, who may edit and resubmit.
        (await reporter.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("نسخة بعد المراجعة")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await senior.Client.PostAsync($"/api/v1/cms/articles/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var visitor = _factory.CreateClient();
        (await PublicFeedContainsAsync(visitor, id)).Should().BeFalse(); // approved is still not public
        (await managing.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublicFeedContainsAsync(visitor, id)).Should().BeTrue();

        var trail = await TrailAsync(id);
        trail.Select(t => $"{t.FromStatus}>{t.ToStatus}").Should().Equal(
            "Draft>InReview", "InReview>Draft", "Draft>InReview", "InReview>Approved", "Approved>Published");
        trail[1].Reason.Should().Be("أضف المصدر");
        trail[1].ActorUserId.Should().Be(copyEditor.UserId);
        trail[1].ActorRoles.Should().Be(NewsroomRole.CopyEditor);
        trail[4].ActorUserId.Should().Be(managing.UserId);
    }

    [Fact]
    public async Task Request_revision_without_a_reason_should_return_400_and_change_nothing()
    {
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var id = await CreateDraftAsync(reporter);
        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await senior.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/request-revision", new { reason = "  " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await senior.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/request-revision", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await TrailAsync(id)).Should().ContainSingle(); // only the submit
    }

    [Fact]
    public async Task Workflow_endpoints_should_enforce_authentication_and_role_gates()
    {
        var anonymous = _factory.CreateClient();
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var copyEditor = await NewActorAsync(NewsroomRole.CopyEditor);
        var researcher = await NewActorAsync(NewsroomRole.Researcher);
        var id = await CreateDraftAsync(reporter);
        var someId = Guid.NewGuid();

        foreach (var path in new[] { "submit", "approve" })
        {
            (await anonymous.PostAsync($"/api/v1/cms/articles/{someId}/{path}", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        (await anonymous.PostAsJsonAsync($"/api/v1/cms/articles/{someId}/request-revision", new { reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Policy gates (coarse): a Reporter cannot approve or request revisions at all;
        // a CopyEditor cannot approve; a Researcher is outside the CMS.
        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reporter.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/request-revision", new { reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await copyEditor.Client.PostAsync($"/api/v1/cms/articles/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await researcher.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Resource gate: another reporter cannot even see (so cannot submit) this draft.
        var otherReporter = await NewActorAsync(NewsroomRole.Reporter);
        (await otherReporter.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await TrailAsync(id)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_managing_editor_cannot_publish_a_draft_but_the_editor_in_chief_can()
    {
        var managing = await NewActorAsync(NewsroomRole.ManagingEditor);
        var chief = await NewActorAsync(NewsroomRole.EditorInChief);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter);

        (await managing.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PublicFeedContainsAsync(_factory.CreateClient(), id)).Should().BeFalse();

        (await chief.Client.PostAsync($"/api/v1/cms/articles/{id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublicFeedContainsAsync(_factory.CreateClient(), id)).Should().BeTrue();
        (await TrailAsync(id)).Should().ContainSingle().Which.ActorRoles.Should().Be(NewsroomRole.EditorInChief);
    }

    // ---------------- revision history & corrections (Slice 20, decision D4) ----------------

    [Fact]
    public async Task Approved_article_can_be_scheduled_cancelled_and_published_by_the_idempotent_worker()
    {
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var visitor = _factory.CreateClient();
        var id = await CreateDraftAsync(reporter, "خبر مجدول للنشر");

        (await senior.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await senior.Client.PostAsync($"/api/v1/cms/articles/{id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var dueAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var schedule = await senior.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/schedule",
            new { scheduledPublishAtUtc = dueAt, reason = "التوقيت المعتمد للنشر" });
        schedule.StatusCode.Should().Be(HttpStatusCode.OK);
        var scheduled = await schedule.Content.ReadFromJsonAsync<JsonElement>();
        scheduled.GetProperty("status").GetString().Should().Be("Scheduled");
        scheduled.GetProperty("scheduledPublishAtUtc").GetDateTimeOffset().Should().Be(dueAt);
        (await PublicFeedContainsAsync(visitor, id)).Should().BeFalse();

        // Cancellation is a real transition back to Approved; scheduling it
        // again proves the worker consumes the persisted timestamp, not a
        // transient in-memory Hangfire payload.
        var cancel = await senior.Client.PostAsync($"/api/v1/cms/articles/{id}/cancel-schedule", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancel.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Approved");

        dueAt = DateTimeOffset.UtcNow.AddMinutes(5);
        (await senior.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/schedule",
            new { scheduledPublishAtUtc = dueAt, reason = "إعادة الجدولة" })).StatusCode.Should().Be(HttpStatusCode.OK);
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger<Jusoor.Infrastructure.Scheduling.ScheduledEditorialPublicationJob>();
            var job = new Jusoor.Infrastructure.Scheduling.ScheduledEditorialPublicationJob(
                context, new FixedClock(dueAt.AddSeconds(1)), logger);
            await job.RunAsync(new NotCancelledJobToken());
            // A second pass models Hangfire's at-least-once retry and must be a no-op.
            await job.RunAsync(new NotCancelledJobToken());
        }

        (await PublicFeedContainsAsync(visitor, id)).Should().BeTrue();
        var trail = await TrailAsync(id);
        trail.Select(t => (t.FromStatus, t.ToStatus)).Should().ContainInOrder(
            (EditorialArticleStatus.Draft, EditorialArticleStatus.InReview),
            (EditorialArticleStatus.InReview, EditorialArticleStatus.Approved),
            (EditorialArticleStatus.Approved, EditorialArticleStatus.Scheduled),
            (EditorialArticleStatus.Scheduled, EditorialArticleStatus.Approved),
            (EditorialArticleStatus.Approved, EditorialArticleStatus.Scheduled),
            (EditorialArticleStatus.Scheduled, EditorialArticleStatus.Published));
        trail.Last().ActorUserId.Should().Be(Jusoor.Infrastructure.Scheduling.ScheduledEditorialPublicationJob.SystemActorId);
        trail.Count.Should().Be(6);
    }

    [Fact]
    public async Task Editing_a_published_article_should_persist_a_revision_and_expose_it_through_the_history_endpoints()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter, "العنوان قبل التعديل");
        (await PublishViaReviewAsync(editor, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var update = await editor.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}",
            new { title = "العنوان بعد التعديل", summary = "ملخص", body = "نص بعد التعديل", editReason = "تصحيح خطأ مطبعي" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await editor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}/history");
        var revisions = history.GetProperty("revisions").EnumerateArray().ToList();
        revisions.Should().ContainSingle();
        revisions[0].GetProperty("revisionNumber").GetInt32().Should().Be(1);
        revisions[0].GetProperty("title").GetString().Should().Be("العنوان قبل التعديل");
        revisions[0].GetProperty("reason").GetString().Should().Be("تصحيح خطأ مطبعي");
        revisions[0].GetProperty("editedByUserId").GetString().Should().Be(editor.UserId);
        revisions[0].TryGetProperty("body", out _).Should().BeFalse(); // the list never carries article text
        history.GetProperty("transitions").GetArrayLength().Should().Be(2); // Draft>InReview, InReview>Published

        var snapshot = await editor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}/revisions/1");
        snapshot.GetProperty("title").GetString().Should().Be("العنوان قبل التعديل");
        snapshot.GetProperty("body").GetString().Should().Be("نص الخبر الكامل هنا.");

        (await editor.Client.GetAsync($"/api/v1/cms/articles/{id}/revisions/2")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.EditorialArticleRevisions.AsNoTracking().CountAsync(r => r.ArticleId == id)).Should().Be(1);
    }

    [Fact]
    public async Task Editing_a_draft_should_not_create_a_revision()
    {
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter);

        (await reporter.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", ArticleBody("مسودة معدّلة"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var history = await reporter.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}/history");
        history.GetProperty("revisions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task A_senior_editor_can_issue_a_correction_that_the_public_article_then_shows_without_issuer_data()
    {
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var visitor = _factory.CreateClient();
        var id = await CreateDraftAsync(reporter, "خبر يحتاج تصحيحاً");
        (await PublishViaReviewAsync(senior, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}")).GetProperty("corrections").GetArrayLength().Should().Be(0);

        var issued = await senior.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/corrections",
            new { kind = "Correction", note = "صُحّح اسم المدينة في الفقرة الثانية", isMajor = true });
        issued.StatusCode.Should().Be(HttpStatusCode.Created);

        var pub = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        var corrections = pub.GetProperty("corrections").EnumerateArray().ToList();
        corrections.Should().ContainSingle();
        corrections[0].GetProperty("kind").GetString().Should().Be("Correction");
        corrections[0].GetProperty("note").GetString().Should().Be("صُحّح اسم المدينة في الفقرة الثانية");
        corrections[0].GetProperty("isMajor").GetBoolean().Should().BeTrue();
        corrections[0].TryGetProperty("issuedByUserId", out _).Should().BeFalse();
        corrections[0].TryGetProperty("issuerRoles", out _).Should().BeFalse();

        var history = await senior.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}/history");
        var cmsRow = history.GetProperty("corrections").EnumerateArray().Single();
        cmsRow.GetProperty("issuedByUserId").GetString().Should().Be(senior.UserId);
        cmsRow.GetProperty("issuerRoles").GetString().Should().Be(NewsroomRole.SeniorEditor);
    }

    [Fact]
    public async Task Corrections_should_enforce_authentication_roles_state_and_input_validation()
    {
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var copy = await NewActorAsync(NewsroomRole.CopyEditor);
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);
        var live = await CreateDraftAsync(reporter, "خبر منشور");
        (await PublishViaReviewAsync(senior, live)).StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await CreateDraftAsync(senior, "مسودة");
        var url = (Guid id) => $"/api/v1/cms/articles/{id}/corrections";
        var body = new { kind = "Correction", note = "تصحيح" };

        (await _factory.CreateClient().PostAsJsonAsync(url(live), body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        foreach (var actor in new[] { reporter, copy, admin })
        {
            (await actor.Client.PostAsJsonAsync(url(live), body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await senior.Client.PostAsJsonAsync(url(draft), body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await senior.Client.PostAsJsonAsync(url(Guid.NewGuid()), body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await senior.Client.PostAsJsonAsync(url(live), new { kind = "Rumour", note = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await senior.Client.PostAsJsonAsync(url(live), new { kind = "Notice", note = "تنويه قديم" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await senior.Client.PostAsJsonAsync(url(live), new { kind = "Correction", note = "   " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.EditorialCorrections.AsNoTracking().CountAsync(c => c.ArticleId == live || c.ArticleId == draft)).Should().Be(0);
    }

    [Fact]
    public async Task Retraction_archive_restore_and_reinstate_should_enforce_roles_and_public_visibility()
    {
        var senior = await NewActorAsync(NewsroomRole.SeniorEditor);
        var admin = await NewActorAsync(NewsroomRole.SystemAdmin);
        var chief = await NewActorAsync(NewsroomRole.EditorInChief);
        var visitor = _factory.CreateClient();
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var id = await CreateDraftAsync(reporter, "خبر محفوظ في سجل الاختبار");
        (await PublishViaReviewAsync(senior, id)).StatusCode.Should().Be(HttpStatusCode.OK);
        var url = $"/api/v1/cms/articles/{id}";

        (await admin.Client.PostAsJsonAsync($"{url}/archive", new { reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await senior.Client.PostAsJsonAsync($"{url}/archive", new { reason = "حفظ تاريخي" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await PublicFeedContainsAsync(visitor, id)).Should().BeFalse();
        var archived = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        archived.GetProperty("isArchived").GetBoolean().Should().BeTrue();

        (await senior.Client.PostAsJsonAsync($"{url}/restore-archive", new { reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await senior.Client.PostAsJsonAsync($"{url}/retract", new
        {
            publicNotice = "سُحب الخبر لوجود خطأ جوهري في المعلومات المنشورة.",
            internalReason = "تحقق المحرر من المصدر وتصحيح الوقائع"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var gone = await visitor.GetAsync($"/api/v1/news/{id}");
        gone.StatusCode.Should().Be(HttpStatusCode.Gone);
        gone.Headers.CacheControl!.NoStore.Should().BeTrue();
        var notice = await gone.Content.ReadFromJsonAsync<JsonElement>();
        notice.GetProperty("notice").GetString().Should().Contain("خطأ جوهري");
        notice.TryGetProperty("body", out _).Should().BeFalse();

        (await senior.Client.PostAsJsonAsync($"{url}/reinstate", new { reason = "مراجعة" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await chief.Client.PostAsJsonAsync($"{url}/reinstate", new { reason = "تم التحقق وإعادة المحتوى للمراجعة" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await visitor.GetAsync($"/api/v1/news/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var trail = await TrailAsync(id);
        trail.Select(t => t.ToStatus).Should().ContainInOrder(
            EditorialArticleStatus.InReview, EditorialArticleStatus.Published,
            EditorialArticleStatus.Archived, EditorialArticleStatus.Published,
            EditorialArticleStatus.Retracted, EditorialArticleStatus.Draft);
    }

    [Fact]
    public async Task History_should_require_authentication_hide_other_reporters_articles_and_show_the_return_reason_to_the_author()
    {
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var other = await NewActorAsync(NewsroomRole.Reporter);
        var copy = await NewActorAsync(NewsroomRole.CopyEditor);
        var id = await CreateDraftAsync(reporter, "خبر للمراجعة");
        (await reporter.Client.PostAsync($"/api/v1/cms/articles/{id}/submit", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await copy.Client.PostAsJsonAsync($"/api/v1/cms/articles/{id}/request-revision", new { reason = "أضف المصدر" })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await _factory.CreateClient().GetAsync($"/api/v1/cms/articles/{id}/history")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await other.Client.GetAsync($"/api/v1/cms/articles/{id}/history")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.GetAsync($"/api/v1/cms/articles/{id}/revisions/1")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var history = await reporter.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}/history");
        var transitions = history.GetProperty("transitions").EnumerateArray().ToList();
        transitions.Select(t => $"{t.GetProperty("fromStatus").GetString()}>{t.GetProperty("toStatus").GetString()}")
            .Should().Equal("Draft>InReview", "InReview>Draft");
        transitions[1].GetProperty("reason").GetString().Should().Be("أضف المصدر");
    }

    // ---------------- rich text (Slice 21, decision D2) ----------------

    [Fact]
    public async Task Rich_text_should_be_sanitized_before_it_is_stored_and_the_public_should_receive_only_the_clean_html()
    {
        var editor = await NewActorAsync(NewsroomRole.EditorInChief);
        var visitor = _factory.CreateClient();
        const string hostile =
            "<h2>عنوان فرعي</h2><p onclick=\"steal()\" style=\"color:red\">نص <a href=\"javascript:alert(1)\">سيئ</a> "
            + "<a href=\"https://example.com/\" target=\"_blank\">جيد</a></p><script>alert(1)</script><img src=x onerror=alert(1)>";

        var created = await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = "خبر منسّق", body = hostile });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = dto.GetProperty("id").GetGuid();

        // What the API hands back is what was stored: clean.
        var returned = dto.GetProperty("body").GetString()!;
        returned.Should().Contain("<h2>عنوان فرعي</h2>");
        returned.Should().Contain("href=\"https://example.com/\"").And.Contain("rel=\"noopener noreferrer\"");
        returned.Should().NotContainEquivalentOf("<script").And.NotContain("onclick").And.NotContain("style=")
            .And.NotContain("javascript:").And.NotContain("<img").And.NotContain("onerror").And.NotContain("target=");

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await context.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == id);
            stored.Body.Should().Be(returned);
            stored.BodyFormat.Should().Be(ArticleBodyFormat.Html);
        }

        (await PublishViaReviewAsync(editor, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        detail.GetProperty("body").GetString().Should().Be(returned);

        // The feed shows a plain-text teaser, never markup.
        var feed = await visitor.GetFromJsonAsync<JsonElement>("/api/v1/news?pageSize=50");
        var item = feed.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        item.GetProperty("excerpt").GetString().Should().Be("عنوان فرعي نص سيئ جيد");
    }

    [Fact]
    public async Task A_body_that_is_empty_once_sanitized_should_be_rejected_with_400_and_leave_the_article_unchanged()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var id = await CreateDraftAsync(editor);

        foreach (var emptyish in new[] { "<p></p>", "<p><br></p>", "<script>alert(1)</script>", "<img src=x onerror=alert(1)>" })
        {
            (await editor.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}", new { title = "عنوان", body = emptyish }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, because: emptyish);
            (await editor.Client.PostAsJsonAsync("/api/v1/cms/articles", new { title = "عنوان", body = emptyish }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest, because: emptyish);
        }

        var article = await editor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}");
        article.GetProperty("body").GetString().Should().Be("نص الخبر الكامل هنا.");
    }

    [Fact]
    public async Task A_legacy_plain_text_article_should_be_served_as_encoded_html_and_converted_when_it_is_next_saved()
    {
        var editor = await NewActorAsync(NewsroomRole.ManagingEditor);
        var reporter = await NewActorAsync(NewsroomRole.Reporter);
        var visitor = _factory.CreateClient();
        var id = await CreateDraftAsync(reporter);

        // Rewind the row to how every article looked before Slice 21: plain text, format 0.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await context.EditorialArticles.SingleAsync(a => a.Id == id);
            context.Entry(row).Property(nameof(EditorialArticle.Body)).CurrentValue = "سطر <b>أول</b>\nتابع\n\n<script>alert(1)</script>";
            context.Entry(row).Property(nameof(EditorialArticle.BodyFormat)).CurrentValue = ArticleBodyFormat.PlainText;
            await context.SaveChangesAsync();
        }

        const string expected = "<p>سطر &lt;b&gt;أول&lt;/b&gt;<br>تابع</p><p>&lt;script&gt;alert(1)&lt;/script&gt;</p>";
        var cms = await editor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}");
        cms.GetProperty("body").GetString().Should().Be(expected);

        (await PublishViaReviewAsync(editor, id)).StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/news/{id}");
        detail.GetProperty("body").GetString().Should().Be(expected).And.NotContain("<script").And.NotContain("<b>");

        // Saving converts it; the snapshot of the replaced version stays the plain text readers saw.
        (await editor.Client.PutAsJsonAsync($"/api/v1/cms/articles/{id}",
            new { title = "عنوان", body = "<p>نص جديد</p>", editReason = "تحويل التنسيق" })).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await context.EditorialArticles.AsNoTracking().SingleAsync(a => a.Id == id)).BodyFormat.Should().Be(ArticleBodyFormat.Html);
            (await context.EditorialArticleRevisions.AsNoTracking().SingleAsync(r => r.ArticleId == id)).BodyFormat.Should().Be(ArticleBodyFormat.PlainText);
        }

        var revision = await editor.Client.GetFromJsonAsync<JsonElement>($"/api/v1/cms/articles/{id}/revisions/1");
        revision.GetProperty("body").GetString().Should().Be(expected);
    }
}
