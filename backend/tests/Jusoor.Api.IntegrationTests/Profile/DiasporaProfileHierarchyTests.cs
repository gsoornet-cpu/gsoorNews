using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Jusoor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jusoor.Api.IntegrationTests.Profile;

/// <summary>
/// Slice 11's Domain/Application tests all run against
/// TestApplicationDbContext, which is backed by EF's InMemory provider —
/// it does not enforce real unique indexes or FK constraints the way
/// Postgres does, so none of those 18 tests actually proved the
/// DiasporaProfile/MyPlace schema is correct at the database level. Same
/// gap Slice 10 deliberately closed with GeographyHierarchyTests; this
/// closes it here too, rather than letting "the build succeeded and other
/// tests passed" stand in for verifying the specific new schema.
/// </summary>
public class DiasporaProfileHierarchyTests : IClassFixture<JusoorApiFactory>
{
    private readonly JusoorApiFactory _factory;

    public DiasporaProfileHierarchyTests(JusoorApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DiasporaProfile_should_persist_and_be_readable_back()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var country = new Country { NameAr = "الإمارات", NameEn = "United Arab Emirates", IsoCode2 = "AE", Slug = $"uae-{Guid.NewGuid():N}" };
        var now = DateTimeOffset.UtcNow;
        var profile = DiasporaProfile.CreateDefault($"user-{Guid.NewGuid():N}", now);
        profile.UpdateLocation(DiasporaPrivacyLevel.CountryOnly, country.Id, null, null, now);

        context.Countries.Add(country);
        context.DiasporaProfiles.Add(profile);
        await context.SaveChangesAsync();

        using var verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await verifyContext.DiasporaProfiles.FirstAsync(p => p.Id == profile.Id);

        reloaded.PrivacyLevel.Should().Be(DiasporaPrivacyLevel.CountryOnly);
        reloaded.CountryId.Should().Be(country.Id);
    }

    [Fact]
    public async Task DiasporaProfile_should_reject_a_second_row_for_the_same_user_at_the_database_level()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = $"user-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        context.DiasporaProfiles.Add(DiasporaProfile.CreateDefault(userId, now));
        await context.SaveChangesAsync();

        // A second profile row for the same user, inserted directly (not
        // through the command's own "find-or-create" logic) — this is
        // exactly the real database constraint that logic relies on to be
        // safe under a race, and only a real unique index can prove it.
        using var secondScope = _factory.Services.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        secondContext.DiasporaProfiles.Add(DiasporaProfile.CreateDefault(userId, now));
        var act = async () => await secondContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task MyPlace_should_reject_following_the_same_city_twice_at_the_database_level()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var country = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "EG", Slug = $"egypt-{Guid.NewGuid():N}" };
        var city = new City { CountryId = country.Id, NameAr = "القاهرة", NameEn = "Cairo", Slug = $"cairo-{Guid.NewGuid():N}" };
        var userId = $"user-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        context.Countries.Add(country);
        context.Cities.Add(city);
        context.MyPlaces.Add(MyPlace.Create(userId, city.Id, now));
        await context.SaveChangesAsync();

        using var secondScope = _factory.Services.CreateScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        secondContext.MyPlaces.Add(MyPlace.Create(userId, city.Id, now));
        var act = async () => await secondContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task MyPlace_should_allow_the_same_city_to_be_followed_by_different_users()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // IsoCode2 carries a unique index, and every test in this class shares
        // one database (IClassFixture) — "EG" is already taken by
        // MyPlace_should_reject_following_the_same_city_twice..., so this test
        // needs its own code. Digit-containing codes are test-only (same
        // convention as GeographyHierarchyTests' "A1"/"B1"/"C1") and can never
        // collide with a real ISO 3166 alpha-2 code used by another test.
        var country = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "T1", Slug = $"egypt-{Guid.NewGuid():N}" };
        var city = new City { CountryId = country.Id, NameAr = "الإسكندرية", NameEn = "Alexandria", Slug = $"alex-{Guid.NewGuid():N}" };
        var now = DateTimeOffset.UtcNow;

        context.Countries.Add(country);
        context.Cities.Add(city);
        context.MyPlaces.Add(MyPlace.Create($"user-{Guid.NewGuid():N}", city.Id, now));
        context.MyPlaces.Add(MyPlace.Create($"user-{Guid.NewGuid():N}", city.Id, now));
        var act = async () => await context.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }
}
