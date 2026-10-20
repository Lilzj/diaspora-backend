using Diaspora.Contracts.Community.V1.Publishing;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Community;

/// <summary>
/// Who a post is published as (20 Oct 2026): personal unless a registered authority grants the
/// named body, and refused — never quietly personal — when none does.
/// </summary>
public sealed class PublisherAuthorizationTests
{
    private static readonly UserId Author = UserId.From(Guid.NewGuid());

    [Fact]
    public async Task No_publisher_named_is_a_personal_post_whatever_is_registered()
    {
        var result = await new PublisherAuthorization([]).ResolveAsync(
            Author, null, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPersonal.Should().BeTrue();
    }

    [Fact]
    public async Task With_no_authority_registered_nobody_may_publish_as_anything()
    {
        var result = await new PublisherAuthorization([]).ResolveAsync(
            Author, Guid.NewGuid(), CancellationToken.None);

        result.Error.Should().Be(CommunityErrors.PostPublisherForbidden);
    }

    [Fact]
    public async Task Every_authority_refusing_is_a_refusal()
    {
        var result = await new PublisherAuthorization([new Fixed(null), new Fixed(null)]).ResolveAsync(
            Author, Guid.NewGuid(), CancellationToken.None);

        result.Error.Should().Be(CommunityErrors.PostPublisherForbidden);
    }

    [Fact]
    public async Task Any_authority_granting_publishes_as_the_body_with_its_kind_and_name()
    {
        var publisher = Guid.NewGuid();
        var granting = new Fixed(new PublisherGrant(PublisherKind.Community, "Lagos Circle"));

        var result = await new PublisherAuthorization([new Fixed(null), granting]).ResolveAsync(
            Author, publisher, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(PublishingIdentityType.Community);
        result.Value.OrganizationId.Should().Be(publisher);
        result.Value.DisplayName.Should().Be("Lagos Circle");
        granting.AskedFor.Should().Be((Author.Value, publisher));
    }

    [Fact]
    public async Task An_organisation_grant_is_stored_as_an_organisation()
    {
        var result = await new PublisherAuthorization(
                [new Fixed(new PublisherGrant(PublisherKind.Organization, "Association"))])
            .ResolveAsync(Author, Guid.NewGuid(), CancellationToken.None);

        result.Value.Type.Should().Be(PublishingIdentityType.Organization);
    }

    [Fact]
    public async Task An_empty_id_is_refused_without_asking()
    {
        var granting = new Fixed(new PublisherGrant(PublisherKind.Community, "Anything"));

        var result = await new PublisherAuthorization([granting]).ResolveAsync(
            Author, Guid.Empty, CancellationToken.None);

        result.Error.Should().Be(CommunityErrors.PostPublisherForbidden);
        granting.AskedFor.Should().BeNull();
    }

    private sealed class Fixed : IPublisherAuthority
    {
        private readonly PublisherGrant? _grant;

        public Fixed(PublisherGrant? grant) => _grant = grant;

        public (Guid Member, Guid Publisher)? AskedFor { get; private set; }

        public Task<PublisherGrant?> AuthorizeAsync(
            Guid memberId, Guid publisherId, CancellationToken cancellationToken = default)
        {
            AskedFor = (memberId, publisherId);
            return Task.FromResult(_grant);
        }
    }
}
