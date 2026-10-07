using FluentAssertions;
using Jusoor.Domain.Entities;
using Xunit;

namespace Jusoor.Domain.UnitTests.Entities;

public class MyPlaceTests
{
    [Fact]
    public void Create_should_reject_missing_user()
    {
        var act = () => MyPlace.Create("", Guid.NewGuid(), DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_reject_an_empty_city_id()
    {
        var act = () => MyPlace.Create("user-1", Guid.Empty, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_should_succeed_for_a_valid_follow()
    {
        var cityId = Guid.NewGuid();
        var place = MyPlace.Create("user-1", cityId, DateTimeOffset.UtcNow);

        place.UserId.Should().Be("user-1");
        place.CityId.Should().Be(cityId);
    }
}
