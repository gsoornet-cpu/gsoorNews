using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Geography;

public sealed record CityCatalogItem(Guid Id, Guid CountryId, string NameAr, string NameEn, string Slug, decimal? Latitude, decimal? Longitude);
public sealed record CountryCatalogItem(Guid Id, string NameAr, string NameEn, string IsoCode2, string Slug, int CityCount, IReadOnlyList<CityCatalogItem> Cities);
public sealed record GetCountryCatalogQuery : IRequest<IReadOnlyList<CountryCatalogItem>>;

public sealed class GetCountryCatalogQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCountryCatalogQuery, IReadOnlyList<CountryCatalogItem>>
{
    public async Task<IReadOnlyList<CountryCatalogItem>> Handle(GetCountryCatalogQuery request, CancellationToken cancellationToken)
    {
        var countries = await context.Countries.AsNoTracking().OrderBy(x => x.NameAr).ToListAsync(cancellationToken);
        var ids = countries.Select(x => x.Id).ToArray();
        var cities = await context.Cities.AsNoTracking().Where(x => ids.Contains(x.CountryId)).OrderBy(x => x.NameAr).ToListAsync(cancellationToken);
        return countries.Select(country => new CountryCatalogItem(country.Id, country.NameAr, country.NameEn, country.IsoCode2,
            country.Slug, cities.Count(city => city.CountryId == country.Id), cities.Where(city => city.CountryId == country.Id)
                .Select(city => new CityCatalogItem(city.Id, city.CountryId, city.NameAr, city.NameEn, city.Slug, city.Latitude, city.Longitude)).ToArray())).ToArray();
    }
}

public sealed record CreateCountryCommand(string NameAr, string NameEn, string IsoCode2) : IRequest<CountryCatalogItem>;

public sealed class CreateCountryCommandValidator : AbstractValidator<CreateCountryCommand>
{
    public CreateCountryCommandValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IsoCode2).NotEmpty().Length(2).Matches("^[A-Za-z]{2}$");
    }
}

public sealed class CreateCountryCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateCountryCommand, CountryCatalogItem>
{
    public async Task<CountryCatalogItem> Handle(CreateCountryCommand request, CancellationToken cancellationToken)
    {
        var code = request.IsoCode2.Trim().ToUpperInvariant();
        var exists = await context.Countries.AnyAsync(x => x.IsoCode2 == code, cancellationToken);
        if (exists) throw new ValidationException("رمز الدولة مسجل بالفعل.");
        var country = new Country { NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), IsoCode2 = code, Slug = code.ToLowerInvariant() };
        context.Countries.Add(country);
        await context.SaveChangesAsync(cancellationToken);
        return new CountryCatalogItem(country.Id, country.NameAr, country.NameEn, country.IsoCode2, country.Slug, 0, Array.Empty<CityCatalogItem>());
    }
}

public sealed record CreateCityCommand(Guid CountryId, string NameAr, string NameEn, string Slug, decimal? Latitude, decimal? Longitude) : IRequest<CityCatalogItem>;

public sealed class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(x => x.CountryId).NotEmpty();
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("أدخل خط العرض وخط الطول معًا أو اتركهما فارغين.");
    }
}

public sealed class CreateCityCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateCityCommand, CityCatalogItem>
{
    public async Task<CityCatalogItem> Handle(CreateCityCommand request, CancellationToken cancellationToken)
    {
        var countryExists = await context.Countries.AnyAsync(x => x.Id == request.CountryId, cancellationToken);
        if (!countryExists) throw new ValidationException("الدولة المحددة غير موجودة.");
        var slug = request.Slug.Trim().ToLowerInvariant();
        var duplicate = await context.Cities.AnyAsync(x => x.CountryId == request.CountryId && x.Slug == slug, cancellationToken);
        if (duplicate) throw new ValidationException("رابط المدينة مستخدم بالفعل داخل هذه الدولة.");
        var city = new City { CountryId = request.CountryId, NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), Slug = slug, Latitude = request.Latitude, Longitude = request.Longitude };
        context.Cities.Add(city);
        await context.SaveChangesAsync(cancellationToken);
        return new CityCatalogItem(city.Id, city.CountryId, city.NameAr, city.NameEn, city.Slug, city.Latitude, city.Longitude);
    }
}
