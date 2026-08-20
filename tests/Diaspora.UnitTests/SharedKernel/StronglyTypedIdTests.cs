using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.SharedKernel;

public class StronglyTypedIdTests
{
    [Fact]
    public void UserId_should_create_new_unique_id()
    {
        var id1 = UserId.New();
        var id2 = UserId.New();

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void UserId_should_roundtrip_guid()
    {
        var guid = Guid.NewGuid();
        var id = UserId.From(guid);

        ((Guid)id).Should().Be(guid);
    }

    [Fact]
    public void UserId_should_have_value_equality()
    {
        var guid = Guid.NewGuid();
        var id1 = UserId.From(guid);
        var id2 = UserId.From(guid);

        id1.Should().Be(id2);
    }

    [Fact]
    public void Different_id_types_should_not_be_equal()
    {
        var guid = Guid.NewGuid();
        var userId = UserId.From(guid);
        var listingId = ListingId.From(guid);

        userId.Should().NotBe(listingId);
    }
}
