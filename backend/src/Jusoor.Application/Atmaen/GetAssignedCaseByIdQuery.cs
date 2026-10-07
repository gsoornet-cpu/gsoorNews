using FluentValidation;
using Jusoor.Application.Atmaen.Contracts;
using Jusoor.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Atmaen;

/// <summary>
/// Returns null — which the controller turns into a plain 404 — both when
/// the Case doesn't exist AND when it exists but is assigned to someone
/// else. The two are deliberately indistinguishable to the caller: the
/// assignment filter is in the WHERE clause itself, so an unassigned-to-you
/// Case is never loaded at all, let alone compared afterwards. This is the
/// approved "assignment must be an actual authorization boundary, not only a
/// UI filter" rule, using the same access-control-in-the-query pattern as
/// GetReviewQueueItemQuery.
/// </summary>
public sealed record GetAssignedCaseByIdQuery(Guid CaseId, string CrisisEditorUserId) : IRequest<CaseDetailDto?>;

public class GetAssignedCaseByIdQueryValidator : AbstractValidator<GetAssignedCaseByIdQuery>
{
    public GetAssignedCaseByIdQueryValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.CrisisEditorUserId).NotEmpty();
    }
}

public class GetAssignedCaseByIdQueryHandler : IRequestHandler<GetAssignedCaseByIdQuery, CaseDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetAssignedCaseByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CaseDetailDto?> Handle(GetAssignedCaseByIdQuery request, CancellationToken cancellationToken)
    {
        var @case = await _context.Cases
            .Where(c => c.Id == request.CaseId && c.AssignedToCrisisEditorUserId == request.CrisisEditorUserId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (@case is null)
        {
            return null;
        }

        var country = await _context.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == @case.CountryId, cancellationToken);
        var city = await _context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == @case.CityId, cancellationToken);

        return new CaseDetailDto(
            @case.Id,
            @case.Status,
            @case.SubjectName,
            @case.SubjectContactPhone,
            @case.ConcernDescription,
            @case.ApplicantPhoneE164,
            @case.OtpVerifiedAtUtc,
            @case.CreatedAtUtc,
            @case.AssignedAtUtc,
            country?.NameAr,
            country?.NameEn,
            city?.NameAr,
            city?.NameEn);
    }
}
