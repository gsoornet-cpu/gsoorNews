using FluentAssertions;
using Hangfire;
using Jusoor.Application.Ingestion;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Scheduling;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Jusoor.Infrastructure.UnitTests.Scheduling;

public class IngestSourceJobTests
{
    [Fact]
    public async Task RunAsync_should_dispatch_IngestSourceCommand_for_the_given_source()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IngestSourceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new IngestSourceResult(true, SourceFetchOutcome.Success, 0, 0, 0, null));

        var job = new IngestSourceJob(sender, NullLogger<IngestSourceJob>.Instance);
        var sourceId = Guid.NewGuid();

        // JobCancellationToken.Null is a real null in disguise — Hangfire's docs are explicit
        // that it exists only to be parsed out of an enqueue-time expression tree, and that
        // Hangfire substitutes a genuine, non-null IJobCancellationToken at execution time.
        // Calling RunAsync directly (as a unit test must) bypasses that substitution, so passing
        // JobCancellationToken.Null here previously handed RunAsync an actual null and blew up on
        // its first line (cancellationToken.ThrowIfCancellationRequested()). A substitute is a
        // real, non-null IJobCancellationToken whose ThrowIfCancellationRequested() is a safe
        // no-op by default, which is what these tests actually want to simulate.
        var cancellationToken = Substitute.For<IJobCancellationToken>();

        await job.RunAsync(sourceId, cancellationToken);

        await sender.Received(1).Send(
            Arg.Is<IngestSourceCommand>(c => c.SourceId == sourceId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_should_not_throw_when_ingestion_reports_a_handled_failure()
    {
        // A returned Succeeded=false (e.g. Timeout, HttpError) is already a
        // handled, logged outcome inside IngestSourceCommandHandler — this
        // job must log it and move on, not treat it as a Hangfire-retryable
        // exception. Only a genuinely thrown exception should trigger
        // Hangfire's own retry policy.
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IngestSourceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new IngestSourceResult(false, SourceFetchOutcome.Timeout, 0, 0, 0, "timed out"));

        var job = new IngestSourceJob(sender, NullLogger<IngestSourceJob>.Instance);
        var cancellationToken = Substitute.For<IJobCancellationToken>();

        var act = async () => await job.RunAsync(Guid.NewGuid(), cancellationToken);

        await act.Should().NotThrowAsync();
    }
}