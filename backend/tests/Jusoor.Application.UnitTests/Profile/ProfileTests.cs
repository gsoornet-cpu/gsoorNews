using FluentAssertions;
using Jusoor.Application.Common.Interfaces;
using Jusoor.Application.Profile;
using Jusoor.Application.UnitTests.TestSupport;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using NSubstitute;
using Xunit;

namespace Jusoor.Application.UnitTests.Profile;

public class ProfileTests
{
    private static IDateTimeProvider FixedClock(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return clock;
    }

    [Fact]
    public async Task GetMyProfileQuery_should_return_NoLocation_default_for_a_user_with_no_row_yet()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var handler = new GetMyProfileQueryHandler(context);

        var result = await handler.Handle(new GetMyProfileQuery("brand-new-user"), default);

        result.PrivacyLevel.Should().Be(nameof(DiasporaPrivacyLevel.NoLocation));
        result.CountryNameEn.Should().BeNull();
        result.Places.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyProfileQuery_should_resolve_the_selected_city_and_followed_places()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var country = new Country { NameAr = "الإمارات", NameEn = "United Arab Emirates", IsoCode2 = "AE", Slug = "uae" };
        var city = new City { CountryId = country.Id, Country = country, NameAr = "دبي", NameEn = "Dubai", Slug = "dubai" };
        var now = DateTimeOffset.UtcNow;
        var profile = DiasporaProfile.CreateDefault("user-1", now);
        profile.UpdateLocation(DiasporaPrivacyLevel.City, null, null, city.Id, now);

        context.Countries.Add(country);
        context.Cities.Add(city);
        context.DiasporaProfiles.Add(profile);
        context.MyPlaces.Add(MyPlace.Create("user-1", city.Id, now));
        await context.SaveChangesAsync(default);

        var handler = new GetMyProfileQueryHandler(context);
        var result = await handler.Handle(new GetMyProfileQuery("user-1"), default);

        result.PrivacyLevel.Should().Be(nameof(DiasporaPrivacyLevel.City));
        result.CityNameEn.Should().Be("Dubai");
        result.Places.Should().ContainSingle(p => p.CityNameEn == "Dubai" && p.CountryNameEn == "United Arab Emirates");
    }

    [Fact]
    public async Task UpdateProfileLocationCommand_should_reject_a_reference_to_a_nonexistent_city()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var handler = new UpdateProfileLocationCommandHandler(context, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(
            new UpdateProfileLocationCommand("user-1", DiasporaPrivacyLevel.City, null, null, Guid.NewGuid()), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(UpdateProfileLocationOutcome.InvalidLocationReference);
    }

    [Fact]
    public async Task UpdateProfileLocationCommand_should_create_a_profile_on_first_use_and_update_it_on_the_next()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var country = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "EG", Slug = "egypt" };
        context.Countries.Add(country);
        await context.SaveChangesAsync(default);

        var handler = new UpdateProfileLocationCommandHandler(context, FixedClock(DateTimeOffset.UtcNow));

        var first = await handler.Handle(
            new UpdateProfileLocationCommand("user-1", DiasporaPrivacyLevel.CountryOnly, country.Id, null, null), default);
        first.Succeeded.Should().BeTrue();
        context.DiasporaProfiles.Should().ContainSingle(p => p.UserId == "user-1" && p.CountryId == country.Id);

        var second = await handler.Handle(
            new UpdateProfileLocationCommand("user-1", DiasporaPrivacyLevel.NoLocation, null, null, null), default);
        second.Succeeded.Should().BeTrue();
        context.DiasporaProfiles.Should().ContainSingle(p => p.UserId == "user-1" && p.PrivacyLevel == DiasporaPrivacyLevel.NoLocation);
    }

    [Fact]
    public async Task AddMyPlaceCommand_should_reject_a_nonexistent_city()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var handler = new AddMyPlaceCommandHandler(context, FixedClock(DateTimeOffset.UtcNow));

        var result = await handler.Handle(new AddMyPlaceCommand("user-1", Guid.NewGuid()), default);

        result.Succeeded.Should().BeFalse();
        result.Outcome.Should().Be(AddMyPlaceOutcome.CityNotFound);
    }

    [Fact]
    public async Task AddMyPlaceCommand_should_be_idempotent_for_a_place_already_followed()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var country = new Country { NameAr = "مصر", NameEn = "Egypt", IsoCode2 = "EG", Slug = "egypt" };
        var city = new City { CountryId = country.Id, NameAr = "القاهرة", NameEn = "Cairo", Slug = "cairo" };
        context.Countries.Add(country);
        context.Cities.Add(city);
        await context.SaveChangesAsync(default);

        var handler = new AddMyPlaceCommandHandler(context, FixedClock(DateTimeOffset.UtcNow));
        await handler.Handle(new AddMyPlaceCommand("user-1", city.Id), default);
        var second = await handler.Handle(new AddMyPlaceCommand("user-1", city.Id), default);

        second.Succeeded.Should().BeTrue();
        second.Outcome.Should().Be(AddMyPlaceOutcome.AlreadyFollowing);
        context.MyPlaces.Should().ContainSingle(p => p.UserId == "user-1" && p.CityId == city.Id);
    }

    [Fact]
    public async Task RemoveMyPlaceCommand_should_remove_a_followed_place_and_report_not_following_otherwise()
    {
        await using var context = TestApplicationDbContext.CreateNew();
        var city = new City { CountryId = Guid.NewGuid(), NameAr = "القاهرة", NameEn = "Cairo", Slug = "cairo" };
        context.MyPlaces.Add(MyPlace.Create("user-1", city.Id, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(default);

        var handler = new RemoveMyPlaceCommandHandler(context);

        var removed = await handler.Handle(new RemoveMyPlaceCommand("user-1", city.Id), default);
        removed.Succeeded.Should().BeTrue();
        removed.Outcome.Should().Be(RemoveMyPlaceOutcome.Removed);

        var second = await handler.Handle(new RemoveMyPlaceCommand("user-1", city.Id), default);
        second.Succeeded.Should().BeFalse();
        second.Outcome.Should().Be(RemoveMyPlaceOutcome.NotFollowing);
    }
}
