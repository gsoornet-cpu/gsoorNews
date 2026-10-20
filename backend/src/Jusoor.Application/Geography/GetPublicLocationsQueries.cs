using Jusoor.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Geography;

public sealed record PublicCountryDto(Guid Id, string NameAr, string NameEn, string IsoCode2);
public sealed record PublicCityDto(Guid Id, Guid CountryId, string NameAr, string NameEn);

public sealed record GetPublicCountriesQuery : IRequest<IReadOnlyList<PublicCountryDto>>;

public sealed class GetPublicCountriesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicCountriesQuery, IReadOnlyList<PublicCountryDto>>
{
    public async Task<IReadOnlyList<PublicCountryDto>> Handle(GetPublicCountriesQuery request, CancellationToken cancellationToken)
        => await context.Countries.AsNoTracking()
            .OrderBy(country => country.NameAr)
            .Select(country => new PublicCountryDto(country.Id, country.NameAr, country.NameEn, country.IsoCode2))
            .ToListAsync(cancellationToken);
}

public sealed record GetPublicCitiesQuery(Guid CountryId) : IRequest<IReadOnlyList<PublicCityDto>>;

public sealed class GetPublicCitiesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicCitiesQuery, IReadOnlyList<PublicCityDto>>
{
    public async Task<IReadOnlyList<PublicCityDto>> Handle(GetPublicCitiesQuery request, CancellationToken cancellationToken)
        => await context.Cities.AsNoTracking()
            .Where(city => city.CountryId == request.CountryId)
            .OrderBy(city => city.NameAr)
            .Select(city => new PublicCityDto(city.Id, city.CountryId, city.NameAr, city.NameEn))
            .ToListAsync(cancellationToken);
}
