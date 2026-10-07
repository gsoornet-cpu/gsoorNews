using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Persistence;
using Jusoor.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace Jusoor.Infrastructure.UnitTests.Scheduling;

public class SourceIngestionSweepJobTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var currentUserService = Substitute.For<ICurrentUserService>();
        // SourceIngestionSweepJob never touches Case/encrypted fields, so a bare
        // substitute with no configured behavior is enough — it only needs to
        // satisfy the constructor now that ApplicationDbContext requires it.
        var fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
        return new ApplicationDbContext(options, currentUserService, fieldEncryptionService);
    }

    private static Source NewSource(bool isActive) => new()
    {
        Name = "Test Source",
        Layer = SourceLayer.ProfessionalMedia,
        FeedUrl = "https://example.com/feed.xml",
        IsActive = isActive
    };

    // JobCancellationToken.Null is a real null in disguise — per Hangfire's own docs it exists
    // only to be parsed out of an enqueue-time expression tree, and Hangfire substitutes a
    // genuine, non-null IJobCancellationToken at execution time. Calling RunAsync directly (as a
    // unit test must) bypasses that substitution, so passing JobCancellationToken.Null previously
    // handed RunAsync an actual null and threw on its first line
    // (cancellationToken.ThrowIfCancellationRequested()). A substitute is a real, non-null
    // IJobCancellationToken whose ThrowIfCancellationRequested() is a safe no-op by default.
    private static IJobCancellationToken NotCancelledToken() => Substitute.For<IJobCancellationToken>();

    [Fact]
    public async Task RunAsync_should_enqueue_one_job_per_active_source_and_skip_inactive_ones()
    {
        await using var context = CreateContext();
        var active1 = NewSource(isActive: true);
        var active2 = NewSource(isActive: true);
        var inactive = NewSource(isActive: false);
        context.Sources.AddRange(active1, active2, inactive);
        await context.SaveChangesAsync(default);

        var backgroundJobClient = Substitute.For<IBackgroundJobClient>();
        backgroundJobClient.Create(Arg.Any<Job>(), Arg.Any<IState>()).Returns("job-id");

        var job = new SourceIngestionSweepJob(context, backgroundJobClient, NullLogger<SourceIngestionSweepJob>.Instance);

        await job.RunAsync(NotCancelledToken());

        // Enqueue<T>(...) is an extension method over IBackgroundJobClient.Create —
        // asserting on Create (the real interface method) is what NSubstitute
        // can actually intercept.
        backgroundJobClient.Received(2).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task RunAsync_should_enqueue_nothing_when_no_sources_are_active()
    {
        await using var context = CreateContext();
        context.Sources.Add(NewSource(isActive: false));
        await context.SaveChangesAsync(default);

        var backgroundJobClient = Substitute.For<IBackgroundJobClient>();
        var job = new SourceIngestionSweepJob(context, backgroundJobClient, NullLogger<SourceIngestionSweepJob>.Instance);

        await job.RunAsync(NotCancelledToken());

        backgroundJobClient.DidNotReceiveWithAnyArgs().Create(default!, default!);
    }
}