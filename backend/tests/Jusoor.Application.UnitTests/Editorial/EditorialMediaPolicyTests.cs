using FluentAssertions;
using Jusoor.Application.Editorial;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Application.UnitTests.Editorial;

public sealed class EditorialMediaPolicyTests
{
    [Theory]
    [InlineData("image.jpg", "image/jpeg", 10 * 1024 * 1024, true)]
    [InlineData("image.svg", "image/svg+xml", 100, false)]
    [InlineData("image.jpg", "image/png", 100, false)]
    [InlineData("video.mp4", "video/mp4", 100 * 1024 * 1024, true)]
    [InlineData("video.mp4", "video/mp4", 100 * 1024 * 1024 + 1, false)]
    [InlineData("brief.pdf", "application/pdf", 20 * 1024 * 1024, true)]
    [InlineData("brief.pdf", "application/pdf", 20 * 1024 * 1024 + 1, false)]
    public void Upload_policy_rejects_unsupported_extensions_mime_mismatches_and_oversize_files(
        string fileName, string contentType, long size, bool expected)
    {
        EditorialMediaPolicy.TryGetKind(fileName, contentType, size, out _).Should().Be(expected);
    }

    [Theory]
    [InlineData(EditorialMediaKind.Image, "image/jpeg", new byte[] { 0xff, 0xd8, 0xff, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true)]
    [InlineData(EditorialMediaKind.Image, "image/png", new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, false)]
    [InlineData(EditorialMediaKind.Video, "video/mp4", new byte[] { 0, 0, 0, 0, 102, 116, 121, 112, 0, 0, 0, 0 }, true)]
    [InlineData(EditorialMediaKind.Document, "application/pdf", new byte[] { 37, 80, 68, 70, 45, 0, 0, 0, 0, 0, 0, 0 }, true)]
    public void Content_signatures_must_match_the_declared_media_type(
        EditorialMediaKind kind, string contentType, byte[] prefix, bool expected)
    {
        EditorialMediaPolicy.HasExpectedSignature(kind, contentType, prefix).Should().Be(expected);
    }
}
