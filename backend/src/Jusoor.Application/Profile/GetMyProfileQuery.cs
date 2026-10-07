using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Profile.Contracts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Profile;

/// <summary>
/// Returns a real DTO even for a user with no DiasporaProfile row yet —
/// PrivacyLevel "NoLocation", everything else null — rather than 404 or
/// null. Spec §12 is explicit that level 0 is a fully-supported steady
/// state ("المنصة تبقى كاملة الوظائف"), not an error condition or an
/// unconfigured-account state, so a brand-new user reading their own
/// profile before ever touching it should see exactly what they'd see
/// after explicitly choosing NoLocation — because that IS what they've
/// chosen, by default. Nothing is written to the database by a read.
/// </summary>
public sealed record GetMyProfileQuery(string UserId) : IRequest<MyProfileDto>;

public class GetMyProfileQueryValidator : AbstractValidator<GetMyProfileQuery>
{
    public GetMyProfileQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, MyProfileDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyProfileQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MyProfileDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _context.DiasporaProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, cancellationToken);

        var country = profile?.CountryId is { } countryId
            ? await _context.Countries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == countryId, cancellationToken)
            : null;
        var region = profile?.RegionId is { } regionId
            ? await _context.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == regionId, cancellationToken)
            : null;
        var city = profile?.CityId is { } cityId
            ? await _context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken)
            : null;

        var places = await _context.MyPlaces
            .Where(p => p.UserId == request.UserId)
            .AsNoTracking()
            .OrderByDescending(p => p.FollowedAtUtc)
            .ToListAsync(cancellationToken);

        var placeCityIds = places.Select(p => p.CityId).ToList();
        var placeCities = await _context.Cities
            .Where(c => placeCityIds.Contains(c.Id))
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        var placeCountryIds = placeCities.Values.Select(c => c.CountryId).Distinct().ToList();
        var placeCountries = await _context.Countries
            .Where(c => placeCountryIds.Contains(c.Id))
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var placeDtos = places
            .Where(p => placeCities.ContainsKey(p.CityId)) // defensive: a City could theoretically be removed later
            .Select(p =>
            {
                var placeCity = placeCities[p.CityId];
                var placeCountry = placeCountries.GetValueOrDefault(placeCity.CountryId);
                return new MyPlaceDto(
                    placeCity.Id, placeCity.NameAr, placeCity.NameEn,
                    placeCountry?.NameAr, placeCountry?.NameEn, p.FollowedAtUtc);
            })
            .ToList();

        return new MyProfileDto(
            (profile?.PrivacyLevel ?? Domain.Enums.DiasporaPrivacyLevel.NoLocation).ToString(),
            country?.NameAr, country?.NameEn,
            region?.NameAr, region?.NameEn,
            city?.NameAr, city?.NameEn,
            placeDtos);
    }
}
