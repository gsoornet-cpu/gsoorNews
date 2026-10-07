using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Profile;

public enum UpdateProfileLocationOutcome
{
    Updated,
    InvalidLocationReference
}

public sealed record UpdateProfileLocationResult(bool Succeeded, UpdateProfileLocationOutcome Outcome);

public sealed record UpdateProfileLocationCommand(
    string UserId,
    DiasporaPrivacyLevel PrivacyLevel,
    Guid? CountryId,
    Guid? RegionId,
    Guid? CityId) : IRequest<UpdateProfileLocationResult>;

public class UpdateProfileLocationCommandValidator : AbstractValidator<UpdateProfileLocationCommand>
{
    public UpdateProfileLocationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PrivacyLevel).IsInEnum();

        // Mirrors DiasporaProfile.UpdateLocation's own invariant so the
        // person gets a clear 400 with a real message instead of a generic
        // 500 from the domain method's ArgumentException — the domain
        // check stays in place regardless (defense in depth), this is
        // purely about the quality of the error the caller sees.
        RuleFor(x => x).Custom((command, context) =>
        {
            switch (command.PrivacyLevel)
            {
                case DiasporaPrivacyLevel.NoLocation when command.CountryId is not null || command.RegionId is not null || command.CityId is not null:
                    context.AddFailure("NoLocation must not include a country, region, or city.");
                    break;
                case DiasporaPrivacyLevel.CountryOnly when command.CountryId is null:
                    context.AddFailure("CountryOnly requires a country.");
                    break;
                case DiasporaPrivacyLevel.CountryOnly when command.RegionId is not null || command.CityId is not null:
                    context.AddFailure("CountryOnly must not include a region or city.");
                    break;
                case DiasporaPrivacyLevel.Region when command.RegionId is null:
                    context.AddFailure("Region-level privacy requires a region.");
                    break;
                case DiasporaPrivacyLevel.Region when command.CityId is not null:
                    context.AddFailure("Region-level privacy must not include a city.");
                    break;
                case DiasporaPrivacyLevel.City when command.CityId is null:
                    context.AddFailure("City-level privacy requires a city.");
                    break;
            }
        });
    }
}

public class UpdateProfileLocationCommandHandler : IRequestHandler<UpdateProfileLocationCommand, UpdateProfileLocationResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _clock;

    public UpdateProfileLocationCommandHandler(IApplicationDbContext context, IDateTimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<UpdateProfileLocationResult> Handle(UpdateProfileLocationCommand request, CancellationToken cancellationToken)
    {
        // Validate every referenced id actually exists before touching the
        // domain entity — the same "Application layer owns DB-backed
        // validation, the entity owns shape invariants" split
        // DiasporaProfile.UpdateLocation's own comment describes.
        if (request.CountryId is { } countryId && !await _context.Countries.AnyAsync(c => c.Id == countryId, cancellationToken))
        {
            return new UpdateProfileLocationResult(false, UpdateProfileLocationOutcome.InvalidLocationReference);
        }
        if (request.RegionId is { } regionId && !await _context.Regions.AnyAsync(r => r.Id == regionId, cancellationToken))
        {
            return new UpdateProfileLocationResult(false, UpdateProfileLocationOutcome.InvalidLocationReference);
        }
        if (request.CityId is { } cityId && !await _context.Cities.AnyAsync(c => c.Id == cityId, cancellationToken))
        {
            return new UpdateProfileLocationResult(false, UpdateProfileLocationOutcome.InvalidLocationReference);
        }

        var now = _clock.UtcNow;
        var profile = await _context.DiasporaProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);
        if (profile is null)
        {
            profile = DiasporaProfile.CreateDefault(request.UserId, now);
            _context.DiasporaProfiles.Add(profile);
        }

        profile.UpdateLocation(request.PrivacyLevel, request.CountryId, request.RegionId, request.CityId, now);

        await _context.SaveChangesAsync(cancellationToken);

        return new UpdateProfileLocationResult(true, UpdateProfileLocationOutcome.Updated);
    }
}
