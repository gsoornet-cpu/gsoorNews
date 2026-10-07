using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Editorial;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

public sealed class PublishedVideoQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Hub_returns_only_published_articles_with_selected_ready_video_assets()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var readyVideo = Asset(EditorialMediaKind.Video, EditorialMediaStatus.Ready, "ready.mp4");
        var pendingVideo = Asset(EditorialMediaKind.Video, EditorialMediaStatus.PendingUpload, "pending.mp4");
        var image = Asset(EditorialMediaKind.Image, EditorialMediaStatus.Ready, "image.jpg");
        var included = Published("منشور بفيديو", Now);
        included.SetFeaturedVideoMediaAsset(readyVideo.Id);
        var draft = EditorialArticle.Create("مسودة بفيديو", null, "نص", "owner", null, null);
        draft.SetFeaturedVideoMediaAsset(readyVideo.Id);
        var pending = Published("فيديو غير جاهز", Now.AddMinutes(-1));
        pending.SetFeaturedVideoMediaAsset(pendingVideo.Id);
        var wrongKind = Published("ملف صورة", Now.AddMinutes(-2));
        wrongKind.SetFeaturedVideoMediaAsset(image.Id);
        var unselected = Published("مقال عادي", Now.AddMinutes(-3));

        context.EditorialMediaAssets.AddRange(readyVideo, pendingVideo, image);
        context.EditorialArticles.AddRange(included, draft, pending, wrongKind, unselected);
        await context.SaveChangesAsync(default);

        var result = await new GetPublishedVideosQueryHandler(context, new TestStorage())
            .Handle(new GetPublishedVideosQuery(), default);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = included.Id,
            Title = "منشور بفيديو",
            VideoUrl = $"https://media.test/{readyVideo.ObjectPath}",
            Credit = readyVideo.Credit,
            Caption = readyVideo.Caption,
            PublishedAtUtc = Now,
            Slug = included.Slug,
            Excerpt = included.Summary
        });
    }

    private static EditorialArticle Published(string title, DateTimeOffset publishedAt)
    {
        var article = EditorialArticle.Create(title, "ملخص", "نص الخبر", "owner", null, null);
        article.Publish("publisher", publishedAt);
        return article;
    }

    private static EditorialMediaAsset Asset(EditorialMediaKind kind, EditorialMediaStatus status, string name)
    {
        var now = Now;
        var asset = EditorialMediaAsset.CreatePending($"test/{name}", name, kind,
            kind == EditorialMediaKind.Video ? "video/mp4" : "image/jpeg", 1024,
            "وصف بديل", "هيئة تحرير جسور", "تعليق", null, null, "editor", now.AddHours(1));
        if (status == EditorialMediaStatus.Ready)
            asset.MarkReady(1024, kind == EditorialMediaKind.Video ? "video/mp4" : "image/jpeg", now);
        return asset;
    }

    private sealed class TestStorage : IEditorialMediaStorage
    {
        public string GetPublicUrl(string objectPath) => $"https://media.test/{objectPath}";
        public Task<SignedMediaUpload> CreateSignedUploadAsync(string objectPath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StoredMediaObject?> InspectPublicObjectAsync(string objectPath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteObjectAsync(string objectPath, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
