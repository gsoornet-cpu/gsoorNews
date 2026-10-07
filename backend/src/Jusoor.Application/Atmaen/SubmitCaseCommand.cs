using FluentValidation;
using Jusoor.Application.Common.Exceptions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Atmaen;

public enum SubmitCaseOutcome
{
    Created,

    /// <summary>Step 5's check returned anything other than Approved —
    /// covers a wrong code, an expired code, and a denied/max-attempts
    /// result alike (see OtpVerificationStatus's own doc comment on why
    /// those are deliberately not distinguished to the caller).</summary>
    OtpNotVerified,

    /// <summary>CountryId or CityId doesn't exist — checked explicitly here
    /// rather than left to fail as a raw DbUpdateException at SaveChanges,
    /// mirroring UpdateProfileLocationCommand's existing precedent for
    /// validating referenced geography ids before writing.</summary>
    InvalidLocation,

    /// <summary>No account currently holds the CrisisEditor role — the
    /// temporary Phase 2 assignment mechanism has nothing to assign to.
    /// This is a real operational gap (the newsroom hasn't staffed the
    /// role yet), not an applicant error.</summary>
    NoCrisisEditorAvailable,

    ProviderUnavailable
}

public sealed record SubmitCaseResult(bool Succeeded, SubmitCaseOutcome Outcome, Guid? CaseId);

/// <summary>
/// Spec §10 steps 5–7 in one command: verify the OTP the applicant was sent
/// (RequestCaseVerificationCommand), and only on success create the Case and
/// assign it. There is deliberately no separate "verify, then create later"
/// two-step flow — Twilio Verify's own check is stateless per attempt (no
/// token to carry between calls), so verification and creation happen
/// atomically here rather than inventing an intermediate verified-session
/// concept this codebase doesn't otherwise have.
/// </summary>
public sealed record SubmitCaseCommand(
    Guid CountryId,
    Guid CityId,
    string SubjectName,
    string? SubjectContactPhone,
    string ConcernDescription,
    string ApplicantPhoneE164,
    string OtpCode) : IRequest<SubmitCaseResult>;

public class SubmitCaseCommandValidator : AbstractValidator<SubmitCaseCommand>
{
    public SubmitCaseCommandValidator()
    {
        RuleFor(x => x.CountryId).NotEmpty();
        RuleFor(x => x.CityId).NotEmpty();

        RuleFor(x => x.SubjectName)
            .NotEmpty().WithMessage("The concerned person's name is required.")
            .MaximumLength(200);

        RuleFor(x => x.SubjectContactPhone).MaximumLength(50);

        RuleFor(x => x.ConcernDescription)
            .NotEmpty().WithMessage("Please describe the concern.")
            .MinimumLength(10).WithMessage("Please provide a bit more detail — a single word isn't enough for the response network to act on.")
            .MaximumLength(4000);

        RuleFor(x => x.ApplicantPhoneE164)
            .NotEmpty()
            .Matches(@"^\+[1-9]\d{7,14}$")
            .WithMessage("Phone number must be in E.164 format, e.g. +201001234567.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Please enter the verification code sent to your phone.")
            .MaximumLength(10);
    }
}

public class SubmitCaseCommandHandler : IRequestHandler<SubmitCaseCommand, SubmitCaseResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IOtpService _otpService;
    private readonly IIdentityService _identityService;
    private readonly IDateTimeProvider _clock;

    public SubmitCaseCommandHandler(
        IApplicationDbContext context,
        IOtpService otpService,
        IIdentityService identityService,
        IDateTimeProvider clock)
    {
        _context = context;
        _otpService = otpService;
        _identityService = identityService;
        _clock = clock;
    }

    public async Task<SubmitCaseResult> Handle(SubmitCaseCommand request, CancellationToken cancellationToken)
    {
        OtpCheckResult checkResult;
        try
        {
            checkResult = await _otpService.CheckVerificationAsync(request.ApplicantPhoneE164, request.OtpCode, cancellationToken);
        }
        catch (OtpProviderException)
        {
            return new SubmitCaseResult(false, SubmitCaseOutcome.ProviderUnavailable, null);
        }

        if (checkResult.Status != OtpVerificationStatus.Approved)
        {
            return new SubmitCaseResult(false, SubmitCaseOutcome.OtpNotVerified, null);
        }

        var countryExists = await _context.Countries.AnyAsync(c => c.Id == request.CountryId, cancellationToken);
        var cityExists = await _context.Cities.AnyAsync(c => c.Id == request.CityId, cancellationToken);
        if (!countryExists || !cityExists)
        {
            return new SubmitCaseResult(false, SubmitCaseOutcome.InvalidLocation, null);
        }

        var crisisEditorIds = await _identityService.GetUserIdsInRoleAsync(NewsroomRole.CrisisEditor);
        if (crisisEditorIds.Count == 0)
        {
            return new SubmitCaseResult(false, SubmitCaseOutcome.NoCrisisEditorAvailable, null);
        }

        // Temporary Phase 2 assignment mechanism (approved decision 3):
        // assign to whichever current CrisisEditor has the fewest Cases
        // assigned to them overall (not filtered by open/closed status —
        // deciding which statuses count as "closed" is a real product
        // question this session was not asked to resolve, so the simplest
        // defensible rule is used instead: total historical count). This is
        // explicitly NOT spec §14's geographic distribution algorithm,
        // which remains deferred to Phase 3.
        var assignmentCounts = await _context.Cases
            .Where(c => c.AssignedToCrisisEditorUserId != null)
            .GroupBy(c => c.AssignedToCrisisEditorUserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countByUser = assignmentCounts.ToDictionary(x => x.UserId, x => x.Count);
        var assigneeUserId = crisisEditorIds.OrderBy(id => countByUser.GetValueOrDefault(id, 0)).First();

        var now = _clock.UtcNow;

        var @case = Case.Create(
            request.CountryId,
            request.CityId,
            request.SubjectName,
            request.SubjectContactPhone,
            request.ConcernDescription,
            request.ApplicantPhoneE164,
            otpVerifiedAtUtc: now,
            nowUtc: now);

        @case.AssignTo(assigneeUserId, now);

        _context.Cases.Add(@case);
        await _context.SaveChangesAsync(cancellationToken);

        return new SubmitCaseResult(true, SubmitCaseOutcome.Created, @case.Id);
    }
}
