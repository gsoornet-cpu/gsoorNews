using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Geography;

/// <summary>
/// Region/Neighborhood are plain lookup entities with no domain invariants
/// (same as Country/City, which have no dedicated unit tests either — see
/// their own entity files) — so what's actually worth verifying here isn't
/// business logic, it's that the EF configuration's relationships and
/// constraints behave correctly against a real database: something only an
/// integration test against real Postgres can actually prove.
/// </summary>
public class GeographyHierarchyTests : IClassFixture<JusoorApiFactory>
{
    private readonly JusoorApiFactory _factory;

    public GeographyHierarchyTests(JusoorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Full_four_tier_hierarchy_should_persist_and_be_navigable()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var country = new Country { NameAr = "الإمارات", NameEn = "United Arab Emirates", IsoCode2 = "AE", Slug = $"uae-{Guid.NewGuid():N}" };
        var region = new Region { CountryId = country.Id, Country = country, NameAr = "دبي", NameEn = "Dubai Emirate", Slug = "dubai-emirate" };
        var city = new City { CountryId = country.Id, RegionId = region.Id, NameAr = "دبي", NameEn = "Dubai", Slug = "dubai" };
        var neighborhood = new Neighborhood { CityId = city.Id, NameAr = "الجميرا", NameEn = "Jumeirah", Slug = "jumeirah" };

        context.Countries.Add(country);
        context.Regions.Add(region);
        context.Cities.Add(city);
        context.Neighborhoods.Add(neighborhood);
        await context.SaveChangesAsync();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var reloadedCity = await verifyContext.Cities
            .Include(c => c.Region)
            .Include(c => c.Neighborhoods)
            .FirstAsync(c => c.Id == city.Id);

        reloadedCity.Region!.NameEn.Should().Be("Dubai Emirate");
        reloadedCity.Neighborhoods.Should().ContainSingle(n => n.NameEn == "Jumeirah");
    }

    [Fact]
    public async Task City_should_not_require_a_region_to_be_created()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var country = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "EG", Slug = $"egypt-{Guid.NewGuid():N}" };
        var city = new City { CountryId = country.Id, NameAr = "القاهرة", NameEn = "Cairo", Slug = $"cairo-{Guid.NewGuid():N}" };

        context.Countries.Add(country);
        context.Cities.Add(city);
        var act = async () => await context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
        city.RegionId.Should().BeNull();
    }

    [Fact]
    public async Task Region_slug_should_only_be_required_to_be_unique_within_its_own_country()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var countryA = new Country { NameAr = "دولة أ", NameEn = "Country A", IsoCode2 = "A1", Slug = $"a-{Guid.NewGuid():N}" };
        var countryB = new Country { NameAr = "دولة ب", NameEn = "Country B", IsoCode2 = "B1", Slug = $"b-{Guid.NewGuid():N}" };
        var regionA = new Region { CountryId = countryA.Id, NameAr = "الوسط", NameEn = "Central", Slug = "central" };
        var regionB = new Region { CountryId = countryB.Id, NameAr = "الوسط", NameEn = "Central", Slug = "central" };

        context.Countries.AddRange(countryA, countryB);
        context.Regions.AddRange(regionA, regionB);
        var act = async () => await context.SaveChangesAsync();

        // Same slug, different countries — must NOT collide.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Region_slug_should_reject_a_duplicate_within_the_same_country()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var country = new Country { NameAr = "دولة ج", NameEn = "Country C", IsoCode2 = "C1", Slug = $"c-{Guid.NewGuid():N}" };
        var first = new Region { CountryId = country.Id, NameAr = "الشمال", NameEn = "North", Slug = "north" };
        var duplicate = new Region { CountryId = country.Id, NameAr = "الشمال ٢", NameEn = "North 2", Slug = "north" };

        context.Countries.Add(country);
        context.Regions.AddRange(first, duplicate);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
