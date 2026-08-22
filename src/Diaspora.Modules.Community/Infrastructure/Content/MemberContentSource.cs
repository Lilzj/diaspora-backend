using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Members;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Infrastructure.Content;

/// <summary>
/// Makes a member reportable — BG-05a.
/// <para>
/// <b>The last unreportable subject on the platform, and the one with the clearest harm.</b> A
/// member could report a guide, a listing, a property, an event, a post, a comment, a poll, an
/// organisation, a vacancy and a direct message. They could not report the <em>person</em>. Somebody
/// being harassed across several surfaces had to file a complaint about each message and each post
/// separately, and no single case ever described the pattern — which is what moderation of a
/// persistent harasser actually needs.
/// </para>
///
/// <para>
/// ── <b>Why this class lives here and not in Trust</b> ──
/// </para>
/// <para>
/// It was written in Trust first. It worked: <c>POST /community/reports contentType=Member</c>
/// answered 201 and a duplicate was refused. The architecture tests then failed it on two counts and
/// both were right — Trust may not depend on Identity's domain (ADR-002), and Trust composes at 200
/// while Community composes at 500, so the module graph's direction forbade the reference. It was
/// reverted rather than the rules weakened, and the constraint stood open as a documented blocker.
/// </para>
/// <para>
/// The blocker was never "a member cannot be described". It was "nothing that may talk to Community
/// can prove a member exists". <see cref="ICommunityMemberDirectory"/> is Community's own answer,
/// fed by <c>UserRegisteredIntegrationEvent</c> — a published contract, not a domain reference — so
/// this source reads a table this module owns and reaches into nothing.
/// </para>
///
/// <para>
/// ── <b>The rules that keep a reporting seam from becoming something else</b> ──
/// </para>
/// <list type="number">
/// <item>
/// <b>Never commentable.</b> A public comment thread hanging off a person is not a moderation
/// feature, it is a pillory. <see cref="ContentDescriptor.AcceptsComments"/> is false and there is
/// no configuration that turns it on.
/// </item>
/// <item>
/// <b><see cref="ContentDescriptor.AuthorId"/> is the member themselves.</b> Community refuses a
/// report whose author is the caller, so this is also what stops somebody reporting their own
/// account — which is not a moderation case, and would otherwise open one.
/// </item>
/// <item>
/// <b>The name, and nothing else.</b> No phone number, no email, no country beyond what a public
/// profile already shows. The moderation queue needs to render whom the case is about; everything
/// past that would make this seam a member-lookup route by another name.
/// </item>
/// </list>
///
/// <para>
/// ── <b>How this differs from the message source, deliberately</b> ──
/// </para>
/// <para>
/// <c>MessageContentSource</c> resolves only for a participant and batch-resolves nothing, because a
/// private message must not be readable by identifier. A member is not private in that way: their
/// existence and display name are already public on their profile, so resolving here for any caller
/// leaks nothing that <c>GET /users/{id}</c> does not. That is why
/// <see cref="DescribeManyAsync"/> genuinely resolves and a page of member reports renders with
/// names rather than raw identifiers.
/// </para>
/// <para>
/// <b>Moderation is not applied through this seam.</b> Suspending, restricting or banning a member
/// is Identity's and Trust's to do, through their own audited paths and their own permissions. B6
/// records the decision and publishes it, which routes the case to the people who hold those
/// permissions; a content source that could disable an account would be a second, unaudited route to
/// the most consequential action on the platform.
/// </para>
/// </summary>
public sealed class MemberContentSource : ICommunityContentSource
{
    private readonly ICommunityMemberDirectory _members;

    public MemberContentSource(ICommunityMemberDirectory members) => _members = members;

    public CommunityContentType ContentType => CommunityContentType.Member;

    public async Task<ContentDescriptor?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        if (contentId == Guid.Empty)
        {
            return null;
        }

        var member = await _members.FindAsync(UserId.From(contentId), cancellationToken);

        return member is null ? null : Describe(member);
    }

    public async Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentIds);

        var members = await _members.FindManyAsync(contentIds, cancellationToken);

        return members.ToDictionary(entry => entry.Key, entry => Describe(entry.Value));
    }

    private static ContentDescriptor Describe(CommunityMember member) => new()
    {
        Reference = ContentReference.Create(CommunityContentType.Member, member.UserId.Value),

        // The subject is the author. See rule 2 on the class.
        AuthorId = member.UserId.Value,

        Title = member.DisplayName,

        // A member who deleted their account is anonymised rather than removed, and a report filed
        // about what they did before that is still a real case.
        Exists = true,
        Visibility = VisibilityLevel.Public,
        AcceptsComments = false,
        CountryCode = member.CountryCode
    };
}
