using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Editorial;

/// <summary>
/// Slice 20b, step 4 (decision Q9), over real HTTP against real Postgres: what an
/// anonymous visitor gets for a Published, Archived and Retracted article, and that the
/// database itself refuses a Retracted row without a public notice.
///
/// Articles are seeded through the domain methods (the same ones the CMS commands call),
/// not through CMS endpoints: the retract/archive endpoints are step 5 and do not exist
/// yet. Everything asserted here is the PUBLIC contract, which is independent of how a
/// state was reached.
/// </summary>
public class PublicNewsPostPublicationEndpointsTests : IClassFixture<JusoorApiFactory>
{
    private const string BodyMarker = "BODY-MARKER-must-never-leak";
    private const string SummaryMarker = "SUMMARY-MARKER-must-never-leak";
    private const string Notice = "تم سحب هذا الخبر لعدم دقة المعلومات الواردة فيه.";

    private readonly JusoorApiFactory _factory;

    public PublicNewsPostPublicationEndpointsTests(JusoorApiFactory factory) => _factory = factory;

    private async Task<Guid> SeedAsync(string title, Action<EditorialArticle>? after = null)
    {
        var article = EditorialArticle.Create(title, SummaryMarker, $"<p>{BodyMarker}</p>", "seed-owner", null, null);
        article.Publish("seed-publisher", DateTimeOffset.UtcNow.AddHours(-3));
        after?.Invoke(article);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.EditorialArticles.Add(article);
        await context.SaveChangesAsync();
        return article.Id;
    }

    private Task<Guid> SeedPublishedAsync(string title) => SeedAsync(title);

    private Task<Guid> SeedArchivedAsync(string title)
        => SeedAsync(title, a => a.Archive("seed-chief", DateTimeOffset.UtcNow.AddHours(-1)));

    private Task<Guid> SeedRetractedAsync(string title)
        => SeedAsync(title, a => a.Retract("seed-chief", Notice, DateTimeOffset.UtcNow.AddHours(-1)));

    // ---------------- detail ----------------

    [Fact]
    public async Task A_published_article_should_answer_200_and_not_be_flagged_archived()
    {
        var id = await SeedPublishedAsync("خبر منشور");

        var response = await _factory.CreateClient().GetAsync($"/api/v1/news/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        json.GetProperty("archivedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        json.GetProperty("body").GetString().Should().Contain(BodyMarker);
    }

    [Fact]
    public async Task An_archived_article_should_still_answer_200_with_its_text_and_the_archived_flag()
    {
        var id = await SeedArchivedAsync("خبر مؤرشف");

        var response = await _factory.CreateClient().GetAsync($"/api/v1/news/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        json.GetProperty("archivedAtUtc").GetDateTimeOffset().Should().BeBefore(DateTimeOffset.UtcNow);
        json.GetProperty("publishedAtUtc").GetDateTimeOffset().Should().BeBefore(json.GetProperty("archivedAtUtc").GetDateTimeOffset());
        json.GetProperty("body").GetString().Should().Contain(BodyMarker);
    }

    [Fact]
    public async Task A_retracted_article_should_answer_410_with_only_the_notice_and_never_cache_it()
    {
        var id = await SeedRetractedAsync("خبر مسحوب");

        var response = await _factory.CreateClient().GetAsync($"/api/v1/news/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(BodyMarker).And.NotContain(SummaryMarker);

        var json = JsonDocument.Parse(raw).RootElement;
        json.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("id", "title", "publishedAtUtc", "retractedAtUtc", "notice");
        json.GetProperty("id").GetGuid().Should().Be(id);
        json.GetProperty("title").GetString().Should().Be("خبر مسحوب");
        json.GetProperty("notice").GetString().Should().Be(Notice);
        json.GetProperty("retractedAtUtc").GetDateTimeOffset().Should().BeAfter(json.GetProperty("publishedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task An_unknown_id_should_still_answer_404_without_the_410_cache_header()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/news/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.CacheControl?.NoStore.Should().NotBe(true); // no-store is specific to the 410 contract
    }

    // ---------------- feed ----------------

    [Fact]
    public async Task The_public_feed_should_contain_the_published_article_but_neither_the_archived_nor_the_retracted_one()
    {
        var published = await SeedPublishedAsync("في الموجز");
        var archived = await SeedArchivedAsync("مؤرشف خارج الموجز");
        var retracted = await SeedRetractedAsync("مسحوب خارج الموجز");

        var response = await _factory.CreateClient().GetAsync("/api/v1/news?pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        ids.Should().Contain(published);
        ids.Should().NotContain(archived);
        ids.Should().NotContain(retracted);
    }

    // ---------------- sitemap ----------------

    [Fact]
    public async Task The_sitemap_should_list_published_and_archived_with_the_flag_and_never_the_retracted_one()
    {
        var published = await SeedPublishedAsync("خريطة منشور");
        var archived = await SeedArchivedAsync("خريطة مؤرشف");
        var retracted = await SeedRetractedAsync("خريطة مسحوب");

        var response = await _factory.CreateClient().GetAsync("/api/v1/news/sitemap");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        entries.Single(e => e.GetProperty("id").GetGuid() == published).GetProperty("isArchived").GetBoolean().Should().BeFalse();
        entries.Single(e => e.GetProperty("id").GetGuid() == archived).GetProperty("isArchived").GetBoolean().Should().BeTrue();
        entries.Any(e => e.GetProperty("id").GetGuid() == retracted).Should().BeFalse();
        foreach (var internalField in new[] { "body", "summary", "notice", "retractionNotice", "status", "ownerUserId" })
        {
            entries.First().TryGetProperty(internalField, out _).Should().BeFalse($"'{internalField}' must not be in the sitemap feed");
        }
    }

    // ---------------- database guard ----------------

    [Fact]
    public async Task The_database_should_refuse_a_retracted_row_without_a_public_notice()
    {
        var id = await SeedRetractedAsync("سيحاول أحدهم مسح الإشعار");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var act = async () => await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"EditorialArticles\" SET \"RetractionNotice\" = NULL WHERE \"Id\" = {id}");

        (await act.Should().ThrowAsync<Exception>())
            .Which.ToString().Should().Contain("CK_EditorialArticles_RetractedHasNotice");

        // And the row is untouched: still retracted, notice intact.
        var response = await _factory.CreateClient().GetAsync($"/api/v1/news/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task The_database_should_allow_a_null_notice_on_every_non_retracted_row()
    {
        // The constraint is "Status <> 6 OR notice IS NOT NULL": it must not trip on the
        // ordinary states, whose notice is legitimately null.
        var published = await SeedPublishedAsync("لا إشعار 1");
        var archived = await SeedArchivedAsync("لا إشعار 2");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.EditorialArticles.AsNoTracking()
            .CountAsync(a => (a.Id == published || a.Id == archived) && a.RetractionNotice == null);

        count.Should().Be(2);
    }
}
