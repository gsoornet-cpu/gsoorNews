using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Administration;

public sealed record EditorialStatusCountDto(string Status, int Count);
public sealed record EditorialActivityDto(string Title, string FromStatus, string ToStatus, string ActorRoles, DateTimeOffset OccurredAtUtc);
public sealed record EditorialMonitoringReportDto(
    int RangeDays,
    int TotalArticles,
    int CreatedInRange,
    int PublishedInRange,
    int UpdatedInRange,
    IReadOnlyList<EditorialStatusCountDto> StatusCounts,
    IReadOnlyList<EditorialActivityDto> RecentActivity);

public sealed record GetEditorialMonitoringReportQuery(int RangeDays) : IRequest<EditorialMonitoringReportDto>;

public sealed class GetEditorialMonitoringReportQueryHandler(IApplicationDbContext context, IDateTimeProvider clock)
    : IRequestHandler<GetEditorialMonitoringReportQuery, EditorialMonitoringReportDto>
{
    public async Task<EditorialMonitoringReportDto> Handle(GetEditorialMonitoringReportQuery request, CancellationToken cancellationToken)
    {
        if (request.RangeDays is not (7 or 30 or 90)) throw new ArgumentOutOfRangeException(nameof(request.RangeDays));
        var since = clock.UtcNow.AddDays(-request.RangeDays);
        var articles = context.EditorialArticles.AsNoTracking();
        var statuses = await articles.GroupBy(article => article.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var created = await articles.CountAsync(article => article.CreatedAtUtc >= since, cancellationToken);
        var published = await articles.CountAsync(article => article.PublishedAtUtc >= since, cancellationToken);
        var updated = await articles.CountAsync(article => article.LastModifiedAtUtc >= since, cancellationToken);
        var activityRows = await (from transition in context.EditorialArticleTransitions.AsNoTracking()
                                  join article in articles on transition.ArticleId equals article.Id
                                  orderby transition.OccurredAtUtc descending
                                  select new { article.Title, transition.FromStatus, transition.ToStatus, transition.ActorRoles, transition.OccurredAtUtc })
            .Take(30).ToListAsync(cancellationToken);
        var activity = activityRows.Select(row => new EditorialActivityDto(row.Title, row.FromStatus.ToString(),
            row.ToStatus.ToString(), row.ActorRoles, row.OccurredAtUtc)).ToArray();

        return new EditorialMonitoringReportDto(request.RangeDays, await articles.CountAsync(cancellationToken), created,
            published, updated, statuses.OrderBy(item => item.Status).Select(item => new EditorialStatusCountDto(item.Status.ToString(), item.Count)).ToArray(), activity);
    }
}
