using FluentAssertions;
using Jusoor.Domain.Entities;
using Jusoor.Domain.Enums;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class DiasporaProfileTests
{
    [Fact]
    public void CreateDefault_should_start_at_NoLocation_with_nothing_set()
    {
        var profile = DiasporaProfile.CreateDefault("user-1", DateTimeOffset.UtcNow);

        profile.PrivacyLevel.Should().Be(DiasporaPrivacyLevel.NoLocation);
        profile.CountryId.Should().BeNull();
        profile.RegionId.Should().BeNull();
        profile.CityId.Should().BeNull();
    }

    [Fact]
    public void UpdateLocation_should_accept_a_consistent_City_level_selection()
    {
        var profile = DiasporaProfile.CreateDefault("user-1", DateTimeOffset.UtcNow);
        var cityId = Guid.NewGuid();

        profile.UpdateLocation(DiasporaPrivacyLevel.City, Guid.NewGuid(), Guid.NewGuid(), cityId, DateTimeOffset.UtcNow);

        profile.PrivacyLevel.Should().Be(DiasporaPrivacyLevel.City);
        profile.CityId.Should().Be(cityId);
    }

    [Fact]
    public void UpdateLocation_should_accept_City_level_with_only_a_city_and_no_region_or_country()
    {
        // Spec treats Region as independently optional — a user can go
        // straight to City-level without ever recording a Region.
        var profile = DiasporaProfile.CreateDefault("user-1", DateTimeOffset.UtcNow);
        var cityId = Guid.NewGuid();

        var act = () => profile.UpdateLocation(DiasporaPrivacyLevel.City, null, null, cityId, DateTimeOffset.UtcNow);

        act.Should().NotThrow();
        profile.CityId.Should().Be(cityId);
    }

    [Theory]
    [InlineData(DiasporaPrivacyLevel.NoLocation)]
    [InlineData(DiasporaPrivacyLevel.CountryOnly)]
    [InlineData(DiasporaPrivacyLevel.Region)]
    public void UpdateLocation_should_reject_disclosing_deeper_than_the_chosen_level(DiasporaPrivacyLevel level)
    {
        var profile = DiasporaProfile.CreateDefault("user-1", DateTimeOffset.UtcNow);

        // A CityId is always "deeper" than any of these three levels.
        var act = () => profile.UpdateLocation(level, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateLocation_should_reject_CountryOnly_with_no_country()
    {
        var profile = DiasporaProfile.CreateDefault("user-1", DateTimeOffset.UtcNow);

        var act = () => profile.UpdateLocation(DiasporaPrivacyLevel.CountryOnly, null, null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateLocation_should_reject_NoLocation_carrying_any_location_data()
    {
        var profile = DiasporaProfile.CreateDefault("user-1", DateTimeOffset.UtcNow);

        var act = () => profile.UpdateLocation(DiasporaPrivacyLevel.NoLocation, Guid.NewGuid(), null, null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
