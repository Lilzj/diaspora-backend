using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.ValueObjects;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

/// <summary>
/// The right to erasure, at the aggregate.
/// <para>
/// Anonymisation rather than deletion is the whole design, and these tests are what stop somebody
/// "simplifying" it later into a hard delete that would take two hundred and sixteen foreign keys
/// with it.
/// </para>
/// </summary>
public class UserErasureTests
{
    private static PhoneNumber Tombstone(long value = 1) =>
        PhoneNumber.Create($"+999{value:D11}");

    private static FullName TombstoneName() => FullName.Create("Deleted member");

    [Fact]
    public void Erasure_replaces_everything_that_named_a_person()
    {
        var user = IdentityTestData.VerifiedUser();
        var originalPhone = user.PhoneNumber.Value;

        var result = user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.FullName.Value.Should().Be("Deleted member");
        user.PhoneNumber.Value.Should().NotBe(originalPhone);
        user.PhoneNumber.Value.Should().StartWith("+999");
        user.Email.Should().BeNull();
        user.Avatar.Should().BeNull();
    }

    [Fact]
    public void Erasure_keeps_the_row_so_nothing_pointing_at_it_breaks()
    {
        // The reason this is anonymisation and not deletion. A listing has a seller, a message has an
        // author, a ledger entry has an actor — all by user id. Removing the row would either fail on
        // a foreign key or cascade into other members' content.
        var user = IdentityTestData.VerifiedUser();
        var id = user.Id;

        user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        user.Id.Should().Be(id);
    }

    [Fact]
    public void Erasure_closes_the_account_and_soft_deletes_it()
    {
        var user = IdentityTestData.VerifiedUser();

        user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        user.AccessState.Should().Be(AccessState.Disabled);
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        user.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public void Erasure_rotates_the_security_stamp_so_issued_tokens_stop_working()
    {
        var user = IdentityTestData.VerifiedUser();
        var before = user.Security.SecurityStamp;

        user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        user.Security.SecurityStamp.Should().NotBe(before);
    }

    [Fact]
    public void Erasure_clears_the_password_history()
    {
        // A password is reused across services often enough that keeping the hashes of somebody who
        // has left is a liability with no remaining purpose.
        var user = IdentityTestData.VerifiedUser();

        user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        user.PasswordHistory.Should().BeEmpty();
    }

    [Fact]
    public void Erasure_announces_itself()
    {
        var user = IdentityTestData.VerifiedUser();

        user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        user.DomainEvents.Should().Contain(e => e is UserAnonymizedDomainEvent);
    }

    [Fact]
    public void Erasing_twice_is_not_an_error_and_does_not_announce_twice()
    {
        // A request that was interrupted and retried must get the same answer as one that completed,
        // and a second pass must not rotate a stamp that already invalidated everything.
        var user = IdentityTestData.VerifiedUser();
        user.Anonymize(Tombstone(1), TombstoneName(), IdentityTestData.Now);
        var stampAfterFirst = user.Security.SecurityStamp;
        var phoneAfterFirst = user.PhoneNumber.Value;

        var second = user.Anonymize(Tombstone(2), TombstoneName(), IdentityTestData.Now);

        second.IsSuccess.Should().BeTrue();
        user.Security.SecurityStamp.Should().Be(stampAfterFirst);
        user.PhoneNumber.Value.Should().Be(phoneAfterFirst);
        user.DomainEvents.Count(e => e is UserAnonymizedDomainEvent).Should().Be(1);
    }

    [Fact]
    public void An_already_disabled_account_can_still_be_erased()
    {
        // Closing an account and erasing it are different acts, and a member who did the first is
        // entitled to the second.
        var user = IdentityTestData.VerifiedUser();
        user.Disable("Member closed their account.", IdentityTestData.Now);

        var result = user.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        result.IsSuccess.Should().BeTrue();
        user.FullName.Value.Should().Be("Deleted member");
        user.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public void An_erased_account_is_identifiable_as_one()
    {
        // `IsAnonymized` exists so a caller can refuse to reactivate an account whose profile is
        // tombstones. Without it, Reactivate would hand back a member with no name and a phone
        // number nobody can dial.
        var untouched = IdentityTestData.VerifiedUser();
        var erased = IdentityTestData.VerifiedUser();
        erased.Anonymize(Tombstone(), TombstoneName(), IdentityTestData.Now);

        untouched.IsAnonymized.Should().BeFalse();
        erased.IsAnonymized.Should().BeTrue();
    }
}
