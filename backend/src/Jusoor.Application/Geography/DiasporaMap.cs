using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Application.Geography;

public static class DiasporaMapPrivacy
{
    // Approved privacy floor from PHASE2_DECISION_READINESS.md. Keep it centralized.
    public const int MinimumGroupSize = 25;
}

public sealed record DiasporaMapItemDto(Guid Id, Guid CountryId, string CountryNameAr, Guid? CityId, string? CityNameAr,
    decimal? Latitude, decimal? Longitude, int EstimatedPopulation, int? CommunitySignalCount,
    string SourceName, string SourceUrl, DateOnly SourcePublishedOn);

public sealed record GetPublicDiasporaMapQuery : IRequest<IReadOnlyList<DiasporaMapItemDto>>;

public sealed class GetPublicDiasporaMapQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicDiasporaMapQuery, IReadOnlyList<DiasporaMapItemDto>>
{
    public async Task<IReadOnlyList<DiasporaMapItemDto>> Handle(GetPublicDiasporaMapQuery request, CancellationToken cancellationToken)
    {
        var estimates = await context.DiasporaEstimates.AsNoTracking().Where(item => item.IsPublished)
            .OrderBy(item => item.CountryId).ThenBy(item => item.CityId).ToListAsync(cancellationToken);
        var countryIds = estimates.Select(item => item.CountryId).Distinct().ToArray();
        var cityIds = estimates.Where(item => item.CityId.HasValue).Select(item => item.CityId!.Value).Distinct().ToArray();
        var countriesWithCityEstimates = estimates.Where(item => item.CityId.HasValue).Select(item => item.CountryId).ToHashSet();
        if (estimates.Count == 0) return Array.Empty<DiasporaMapItemDto>();

        var countries = await context.Countries.AsNoTracking().Where(item => countryIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.NameAr, cancellationToken);
        var cities = await context.Cities.AsNoTracking().Where(item => cityIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => new { item.NameAr, item.Latitude, item.Longitude }, cancellationToken);

        // Counts are only computed for locations which have a public curated estimate.
        // Country-level disclosure includes all voluntarily shared levels within country;
        // city-level disclosure includes only profiles that explicitly chose City precision.
        var countrySignals = await context.DiasporaProfiles.AsNoTracking()
            .Where(profile => profile.PrivacyLevel != DiasporaPrivacyLevel.NoLocation && profile.CountryId.HasValue && countryIds.Contains(profile.CountryId.Value))
            .GroupBy(profile => profile.CountryId!.Value).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
        var citySignals = await context.DiasporaProfiles.AsNoTracking()
            .Where(profile => profile.PrivacyLevel == DiasporaPrivacyLevel.City && profile.CityId.HasValue && cityIds.Contains(profile.CityId.Value))
            .GroupBy(profile => profile.CityId!.Value).Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);

        return estimates.Where(item => countries.ContainsKey(item.CountryId) && (!item.CityId.HasValue || cities.ContainsKey(item.CityId.Value)))
            .Select(item =>
            {
                var city = item.CityId is { } cityId ? cities[cityId] : null;
                // Do not publish a country total alongside city-level cells. Doing so could
                // make suppressed small city groups derivable by subtraction (complementary suppression).
                var signalCount = item.CityId is { } id
                    ? citySignals.GetValueOrDefault(id)
                    : countriesWithCityEstimates.Contains(item.CountryId) ? 0 : countrySignals.GetValueOrDefault(item.CountryId);
                return new DiasporaMapItemDto(item.Id, item.CountryId, countries[item.CountryId], item.CityId,
                    city?.NameAr, city?.Latitude, city?.Longitude, item.EstimatedPopulation,
                    signalCount >= DiasporaMapPrivacy.MinimumGroupSize ? signalCount : null,
                    item.SourceName, item.SourceUrl, item.SourcePublishedOn);
            }).ToArray();
    }
}

public sealed record DiasporaEstimateAdminDto(Guid Id, Guid CountryId, string CountryNameAr, Guid? CityId, string? CityNameAr,
    int EstimatedPopulation, string SourceName, string SourceUrl, DateOnly SourcePublishedOn, bool IsPublished);
public sealed record GetDiasporaEstimatesQuery : IRequest<IReadOnlyList<DiasporaEstimateAdminDto>>;

public sealed class GetDiasporaEstimatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDiasporaEstimatesQuery, IReadOnlyList<DiasporaEstimateAdminDto>>
{
    public async Task<IReadOnlyList<DiasporaEstimateAdminDto>> Handle(GetDiasporaEstimatesQuery request, CancellationToken cancellationToken)
    {
        var rows = await context.DiasporaEstimates.AsNoTracking().OrderBy(item => item.CountryId).ThenBy(item => item.CityId).ToListAsync(cancellationToken);
        var countryIds = rows.Select(item => item.CountryId).Distinct().ToArray();
        var cityIds = rows.Where(item => item.CityId.HasValue).Select(item => item.CityId!.Value).Distinct().ToArray();
        var countries = await context.Countries.AsNoTracking().Where(item => countryIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.NameAr, cancellationToken);
        var cities = await context.Cities.AsNoTracking().Where(item => cityIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.NameAr, cancellationToken);
        return rows.Where(item => countries.ContainsKey(item.CountryId)).Select(item => new DiasporaEstimateAdminDto(item.Id, item.CountryId,
            countries[item.CountryId], item.CityId, item.CityId is { } cityId && cities.TryGetValue(cityId, out var name) ? name : null,
            item.EstimatedPopulation, item.SourceName, item.SourceUrl, item.SourcePublishedOn, item.IsPublished)).ToArray();
    }
}

public sealed record SaveDiasporaEstimateCommand(Guid? Id, Guid CountryId, Guid? CityId, int EstimatedPopulation,
    string SourceName, string SourceUrl, DateOnly SourcePublishedOn, bool IsPublished) : IRequest<DiasporaEstimateAdminDto>;

public sealed class SaveDiasporaEstimateCommandValidator : AbstractValidator<SaveDiasporaEstimateCommand>
{
    public SaveDiasporaEstimateCommandValidator()
    {
        RuleFor(item => item.CountryId).NotEmpty();
        RuleFor(item => item.EstimatedPopulation).GreaterThan(0).LessThanOrEqualTo(1_000_000_000);
        RuleFor(item => item.SourceName).NotEmpty().MaximumLength(200);
        RuleFor(item => item.SourceUrl).NotEmpty().MaximumLength(2048).Must(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
        RuleFor(item => item.SourcePublishedOn).Must(date => date <= DateOnly.FromDateTime(DateTime.UtcNow));
    }
}

public sealed class SaveDiasporaEstimateCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    : IRequestHandler<SaveDiasporaEstimateCommand, DiasporaEstimateAdminDto>
{
    public async Task<DiasporaEstimateAdminDto> Handle(SaveDiasporaEstimateCommand request, CancellationToken cancellationToken)
    {
        var country = await context.Countries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.CountryId, cancellationToken)
            ?? throw new ValidationException("الدولة المحددة غير موجودة.");
        string? cityName = null;
        if (request.CityId is { } cityId)
        {
            var city = await context.Cities.AsNoTracking().SingleOrDefaultAsync(item => item.Id == cityId && item.CountryId == request.CountryId, cancellationToken)
                ?? throw new ValidationException("المدينة لا تتبع الدولة المحددة.");
            cityName = city.NameAr;
        }
        var duplicateQuery = context.DiasporaEstimates.Where(item => item.CountryId == request.CountryId && item.CityId == request.CityId);
        if (request.Id is { } currentId) duplicateQuery = duplicateQuery.Where(item => item.Id != currentId);
        var duplicate = await duplicateQuery.AnyAsync(cancellationToken);
        if (duplicate) throw new ValidationException("يوجد تقدير مسجل بالفعل لهذا الموقع. عدّله من القائمة بدل إضافة نسخة مكررة.");
        if (request.IsPublished && (string.IsNullOrWhiteSpace(request.SourceName) || string.IsNullOrWhiteSpace(request.SourceUrl)))
            throw new ValidationException("لا يمكن نشر تقدير من دون مصدر موثق.");

        DiasporaEstimate entity;
        if (request.Id is { } id)
        {
            entity = await context.DiasporaEstimates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException("تقدير الخريطة غير موجود.");
            entity.Update(request.CountryId, request.CityId, request.EstimatedPopulation, request.SourceName, request.SourceUrl, request.SourcePublishedOn, request.IsPublished);
            entity.LastModifiedAtUtc = DateTimeOffset.UtcNow;
            entity.LastModifiedByUserId = currentUser.UserId;
        }
        else
        {
            entity = DiasporaEstimate.Create(request.CountryId, request.CityId, request.EstimatedPopulation, request.SourceName, request.SourceUrl, request.SourcePublishedOn, request.IsPublished);
            entity.CreatedAtUtc = DateTimeOffset.UtcNow;
            entity.CreatedByUserId = currentUser.UserId;
            context.DiasporaEstimates.Add(entity);
        }
        await context.SaveChangesAsync(cancellationToken);
        return new DiasporaEstimateAdminDto(entity.Id, country.Id, country.NameAr, request.CityId, cityName, entity.EstimatedPopulation, entity.SourceName, entity.SourceUrl, entity.SourcePublishedOn, entity.IsPublished);
    }
}

public sealed record DeleteDiasporaEstimateCommand(Guid Id) : IRequest<bool>;
public sealed class DeleteDiasporaEstimateCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteDiasporaEstimateCommand, bool>
{
    public async Task<bool> Handle(DeleteDiasporaEstimateCommand request, CancellationToken cancellationToken)
    {
        var row = await context.DiasporaEstimates.SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (row is null) return false;
        context.DiasporaEstimates.Remove(row);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
