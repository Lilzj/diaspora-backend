using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Announcements;

/// <summary>
/// A published post from an organization to the people it is entitled to address.
/// <para>
/// <b>The scope is the whole point, and it is what B9's community updates do not have.</b> A B9 post
/// is authored by a member to a country audience; there is no scope on it, so an organization cannot
/// address its own members and no audience-resolution rule exists to reuse. This aggregate adds
/// exactly that — a scope, a scoped identifier, and a rule the server applies on both the read and
/// the fan-out — and adds nothing else, because everything else about publishing a post already
/// exists somewhere on this platform.
/// </para>
/// <para>
/// <b>Delivery is B13's and this module writes none of it.</b> Publishing raises a domain event; the
/// relay turns it into an integration event carrying the scope; B13 resolves who that reaches and
/// delivers through the channels and preferences it already owns. No Firebase call, no email vendor,
/// no SignalR frame is composed here, and an architecture test asserts the module names none of them.
/// </para>
/// <para>
/// <b>Media by reference, never by URL.</b> B5's rule since it shipped: the identifier of an asset,
/// and the consumer asks Media for the rendition it wants.
/// </para>
/// </summary>
public sealed class CommunityAnnouncement
    : AggregateRoot<CommunityAnnouncementId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<AnnouncementMediaReference> _media = [];

    private CommunityAnnouncement(
        CommunityAnnouncementId id,
        AnnouncementScope scope,
        Guid? scopeId,
        Guid? organizationId,
        UserId publishedBy,
        string title,
        string body,
        bool isPinned,
        DateTimeOffset now) : base(id)
    {
        Scope = scope;
        ScopeId = scopeId;
        OrganizationId = organizationId;
        PublishedByUserId = publishedBy;
        Title = title;
        Body = body;
        IsPinned = isPinned;
        PublishedAt = now;
        CreatedAt = now;
    }

    private CommunityAnnouncement() { }

    public AnnouncementScope Scope { get; private set; }

    /// <summary>
    /// What the scope names — an organization, a B4 area, a B12 event, a B15 cause.
    /// <para>
    /// Null only on <see cref="AnnouncementScope.Community"/>, which addresses a country rather than
    /// an entity. Every other scope requires one, and the factory refuses without it.
    /// </para>
    /// </summary>
    public Guid? ScopeId { get; private set; }

    /// <summary>
    /// The organization that published it, when one did.
    /// <para>
    /// Distinct from <see cref="ScopeId"/>, and the difference matters on an event announcement: the
    /// scope names the event, and this names the body answerable for the words. Both are needed to
    /// authorize an edit.
    /// </para>
    /// </summary>
    public Guid? OrganizationId { get; private set; }

    public UserId PublishedByUserId { get; private set; } = default!;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>The country an unscoped community announcement is addressed to.</summary>
    public string? CountryCode { get; private set; }

    public bool IsPinned { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>B15's idempotency primitive. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<AnnouncementMediaReference> Media => _media.AsReadOnly();

    // ── Auditing and deletion ───────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public static Result<CommunityAnnouncement> Publish(
        CommunityAnnouncementId id,
        AnnouncementScope scope,
        Guid? scopeId,
        Guid? organizationId,
        UserId publishedBy,
        string title,
        string body,
        string? countryCode,
        bool isPinned,
        DateTimeOffset now,
        IEnumerable<Guid>? mediaIds = null,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(publishedBy);

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<CommunityAnnouncement>(
                CommunityProgrammeErrors.Validation("An announcement needs a title."));
        }

        // Community is the one scope addressed to a country rather than to an entity. Every other
        // scope must name what it is addressed to, and an empty Guid is not naming one.
        if (scope != AnnouncementScope.Community && scopeId.GetValueOrDefault() == Guid.Empty)
        {
            return Result.Failure<CommunityAnnouncement>(
                CommunityProgrammeErrors.AnnouncementScopeInvalid);
        }

        var announcement = new CommunityAnnouncement(
            id,
            scope,
            scopeId,
            organizationId,
            publishedBy,
            MembershipProgramme.Clip(title, CommunityProgrammeLimits.MaxNameLength)!,
            MembershipProgramme.Clip(body, CommunityProgrammeLimits.MaxDescriptionLength)
                ?? string.Empty,
            isPinned,
            now)
        {
            CountryCode = MembershipProgramme.Clip(
                countryCode, CommunityProgrammeLimits.MaxCountryLength)?.ToUpperInvariant(),
            IdempotencyKey = MembershipProgramme.Clip(
                idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength)
        };

        if (mediaIds is not null)
        {
            foreach (var mediaId in mediaIds
                .Where(mediaId => mediaId != Guid.Empty)
                .Distinct()
                .Take(CommunityProgrammeLimits.MaxMediaReferences))
            {
                announcement._media.Add(AnnouncementMediaReference.Create(mediaId));
            }
        }

        announcement.RaiseDomainEvent(new CommunityAnnouncementPublishedDomainEvent
        {
            AnnouncementId = id,
            Scope = scope,
            ScopeId = scopeId,
            OrganizationId = organizationId,
            Title = announcement.Title,
            PublishedBy = publishedBy,
            PublishedAt = now
        });

        return Result.Success(announcement);
    }

    public void Pin(bool isPinned, DateTimeOffset now)
    {
        IsPinned = isPinned;
        Revision++;
        ModifiedAt = now;
    }

    // ── Auditing and deletion ───────────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}

/// <summary>
/// A B5 asset attached to an announcement.
/// <para>
/// An identifier and nothing else — never a URL. B5 owns the bytes and the renditions, and a stored
/// URL is a stored decision about size, format and CDN that goes wrong the first time any of the
/// three changes.
/// </para>
/// </summary>
public sealed class AnnouncementMediaReference
{
    private AnnouncementMediaReference(Guid id, Guid mediaId)
    {
        Id = id;
        MediaId = mediaId;
    }

    private AnnouncementMediaReference() { }

    public Guid Id { get; private set; }

    public Guid MediaId { get; private set; }

    public static AnnouncementMediaReference Create(Guid mediaId) => new(Guid.NewGuid(), mediaId);
}
