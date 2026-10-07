using FluentValidation;
using Jusoor.Application.Atmaen.Contracts;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Stories.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Atmaen;

/// <summary>
/// A CrisisEditor's own queue — only Cases assigned to CrisisEditorUserId,
/// filtered in the WHERE clause itself so another CrisisEditor's Cases are
/// never even loaded into memory here (the same "the query is itself an
/// access-control boundary" pattern GetReviewQueueItemQuery established).
/// Reuses Stories.Contracts.PagedResult&lt;T&gt; rather than a second paging
/// wrapper. CrisisEditorUserId comes from the authenticated principal at the
/// controller, never from the request.
/// </summary>
public sealed record GetMyAssignedCasesQuery(string CrisisEditorUserId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<CaseSummaryDto>>;

public class GetMyAssignedCasesQueryValidator : AbstractValidator<GetMyAssignedCasesQuery>
{
    public GetMyAssignedCasesQueryValidator()
    {
        RuleFor(x => x.CrisisEditorUserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}

public class GetMyAssignedCasesQueryHandler : IRequestHandler<GetMyAssignedCasesQuery, PagedResult<CaseSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMyAssignedCasesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<CaseSummaryDto>> Handle(GetMyAssignedCasesQuery request, CancellationToken cancellationToken)
    {
        var baseQuery = _context.Cases
            .Where(c => c.AssignedToCrisisEditorUserId == request.CrisisEditorUserId)
            .AsNoTracking();

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // Oldest-first (FIFO), same fairness reasoning as GetReviewQueueQuery:
        // a queue that surfaces the newest arrivals first lets older cases
        // starve — and for a safety-critical workflow like Atmaen, an
        // older unresolved case is exactly the one that most needs
        // attention, not the least.
        var cases = await baseQuery
            .OrderBy(c => c.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var countryIds = cases.Select(c => c.CountryId).Distinct().ToList();
        var cityIds = cases.Select(c => c.CityId).Distinct().ToList();
        var countries = await _context.Countries.Where(c => countryIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);
        var cities = await _context.Cities.Where(c => cityIds.Contains(c.Id)).AsNoTracking().ToDictionaryAsync(c => c.Id, cancellationToken);

        var items = cases.Select(c =>
        {
            var country = countries.GetValueOrDefault(c.CountryId);
            var city = cities.GetValueOrDefault(c.CityId);
            return new CaseSummaryDto(
                c.Id,
                c.Status,
                c.SubjectName,
                c.CreatedAtUtc,
                c.AssignedAtUtc,
                country?.NameAr,
                country?.NameEn,
                city?.NameAr,
                city?.NameEn);
        }).ToList();

        return new PagedResult<CaseSummaryDto>(items, request.Page, request.PageSize, totalCount);
    }
}
