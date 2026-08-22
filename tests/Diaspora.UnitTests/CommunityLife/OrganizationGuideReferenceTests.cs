using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// The organization guide collection, and the gap that kept it empty.
/// <para>
/// <b>Every piece of this path existed and none of it was connected.</b>
/// <see cref="Organization.SetGuideReferences"/> with its limit and de-duplication,
/// <see cref="OrganizationGuideReference"/>, the <c>community_life_organization_guides</c> table, its
/// persistence configuration, and <c>CommunityLifeGuideResolver.ForOrganizationAsync</c> — all
/// present, and neither the domain method nor the resolver method had a single caller anywhere in
/// the solution. No request contract carried a guide identifier for an organization either.
/// </para>
/// <para>
/// So <c>OrganizationResponse.Guides</c> was a field that could never be non-empty for any
/// organization on the platform, and the client's Guides tab could only ever render its empty state
/// — which reads as "this organization has published none" when the truth was "the platform cannot
/// record any". The two sibling aggregates were wired: <c>CreateEventCommandHandler</c> sets guides
/// on an event and <c>PublishHousingListingCommandHandler</c> on a listing. Organizations were the
/// one of three left unconnected.
/// </para>
/// <para>
/// Found by seeding an organization through the real API and noticing that a profile whose event
/// linked two real guides still served <c>guides: []</c>.
/// </para>
/// </summary>
public sealed class OrganizationGuideReferenceTests
{
    private static Organization Draft()
    {
        var organization = Organization.Draft(
            OrganizationId.From(Guid.NewGuid()),
            UserId.From(Guid.NewGuid()),
            OrganizationName.Create("Yaba Nigerian Association").Value,
            CommunityLifeSlug.Create("yaba-nigerian-association").Value,
            CommunityLifeDescription.Create("A community body.").Value,
            "place-of-worship",
            CommunityLifeLocationContext.Create("NG", null, "Lagos").Value,
            DateTimeOffset.UtcNow);

        organization.IsSuccess.Should().BeTrue();
        return organization.Value;
    }

    private static OrganizationGuideReference Reference(Guid? id = null, string title = "A guide") =>
        OrganizationGuideReference.Create(id ?? Guid.NewGuid(), title, "a-guide");

    [Fact]
    public void An_organization_starts_with_no_guides()
    {
        Draft().GuideReferences.Should().BeEmpty();
    }

    [Fact]
    public void Guides_can_be_attached()
    {
        // The assertion the platform could not make before this wave: an
        // organization's guide collection is reachable.
        var organization = Draft();
        var first = Reference(title: "Getting your documents in order");
        var second = Reference(title: "Finding a room in your first month");

        var applied = organization.SetGuideReferences([first, second]);

        applied.IsSuccess.Should().BeTrue();
        organization.GuideReferences.Should().HaveCount(2);
        organization.GuideReferences.Select(reference => reference.Title)
            .Should().Contain("Getting your documents in order");
    }

    [Fact]
    public void Setting_guides_replaces_rather_than_appends()
    {
        var organization = Draft();
        organization.SetGuideReferences([Reference(title: "First")]);

        organization.SetGuideReferences([Reference(title: "Second")]);

        organization.GuideReferences.Should().HaveCount(1);
        organization.GuideReferences[0].Title.Should().Be("Second");
    }

    [Fact]
    public void An_empty_list_clears_the_collection()
    {
        var organization = Draft();
        organization.SetGuideReferences([Reference(), Reference()]);

        organization.SetGuideReferences([]);

        organization.GuideReferences.Should().BeEmpty();
    }

    [Fact]
    public void The_same_guide_twice_is_stored_once()
    {
        // De-duplication is the domain's, and a caller that sends a duplicate is
        // not refused — it simply cannot produce two rows pointing at one guide.
        var organization = Draft();
        var guideId = Guid.NewGuid();

        organization.SetGuideReferences([Reference(guideId), Reference(guideId, "A later title")]);

        organization.GuideReferences.Should().HaveCount(1);
        organization.GuideReferences[0].GuideId.Should().Be(guideId);
    }

    [Fact]
    public void More_than_the_limit_is_refused()
    {
        var organization = Draft();
        var tooMany = Enumerable
            .Range(0, CommunityLifeLimits.MaxGuideReferences + 1)
            .Select(_ => Reference())
            .ToList();

        var applied = organization.SetGuideReferences(tooMany);

        applied.IsFailure.Should().BeTrue();
        // And nothing is stored: a refused write leaves the previous list alone
        // rather than half-applying it.
        organization.GuideReferences.Should().BeEmpty();
    }

    [Fact]
    public void Exactly_the_limit_is_allowed()
    {
        // The boundary, because an off-by-one here silently costs an organization
        // its tenth guide.
        var organization = Draft();
        var atLimit = Enumerable
            .Range(0, CommunityLifeLimits.MaxGuideReferences)
            .Select(_ => Reference())
            .ToList();

        organization.SetGuideReferences(atLimit).IsSuccess.Should().BeTrue();
        organization.GuideReferences.Should().HaveCount(CommunityLifeLimits.MaxGuideReferences);
    }

    [Fact]
    public void An_archived_organization_refuses_the_write()
    {
        // `SetGuideReferences` goes through `EnsureEditable`, so the archived rule
        // applies here as it does to every other edit.
        var organization = Draft();
        organization.Archive(DateTimeOffset.UtcNow, UserId.From(Guid.NewGuid()), "closed");

        var applied = organization.SetGuideReferences([Reference()]);

        applied.IsFailure.Should().BeTrue();
    }
}
