using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Intelligence.Notices;

/// <summary>
/// Opens a draft official notice.
/// <para>
/// Gated at <see cref="IntelligencePermissions.ManageNotices"/>, which moderators hold: transcribing
/// a circular is editorial work of the kind they already do. Making it visible is not, and has its
/// own permission.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.ManageNotices)]
[Auditable]
public sealed record CreateOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required CreateOfficialNoticeRequest Request { get; init; }
}

/// <summary>
/// Rewrites a notice.
/// <para>
/// <b>One command where the aggregate has eight operations, and it is <c>[Transactional]</c> for
/// exactly that reason.</b> A single editing screen sends content, classification, dates, sourcing
/// and four collections together; each is a separate rule on the aggregate, and the handler applies
/// them in sequence. Without the attribute a failure partway through would leave a notice whose
/// category moved and whose dates did not — a state the editor never asked for and cannot see.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.ManageNotices)]
[Auditable]
[Transactional(Reason =
    "Content, classification, dates, sourcing and four collections are separate aggregate " +
    "operations applied from one editing screen. A partial apply leaves a notice nobody asked for.")]
public sealed record UpdateOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }

    public required UpdateOfficialNoticeRequest Request { get; init; }
}

/// <summary>Sends a completed draft for verification.</summary>
[Authorize(Permission = IntelligencePermissions.ManageNotices)]
[Auditable]
public sealed record SubmitOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>
/// A verifier's decision on a submitted notice.
/// <para>
/// Administrators only. Confirming that a ministry really said something is the platform asserting it
/// on its own authority, and the person who transcribed it approving their own transcription is a
/// verification step that verifies nothing.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.VerifyNotices)]
[Auditable]
public sealed record VerifyOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }

    public required VerifyOfficialNoticeRequest Request { get; init; }
}

/// <summary>
/// Makes a verified notice visible.
/// <para>
/// <b>The command that runs the source validators and checks the authority.</b> Both gates sit here
/// rather than on the create, because both can change between drafting and publishing — an authority
/// verified overnight, a deployment's source policy tightened this morning — and the moment that
/// matters is the moment somebody is about to be told.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.PublishNotices)]
[Auditable]
public sealed record PublishOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>
/// Replaces a live notice with a later one.
/// <para>
/// <b><c>[Transactional]</c>, because it writes two aggregates.</b> The old notice moves to superseded
/// and the replacement is read and checked; a failure between them would leave a notice pointing at a
/// replacement that was never confirmed published.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.PublishNotices)]
[Auditable]
[Transactional(Reason =
    "Supersession reads the replacement and writes the superseded notice. A partial apply leaves a " +
    "notice pointing at something that may not be publishable.")]
public sealed record SupersedeOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }

    public required SupersedeOfficialNoticeRequest Request { get; init; }
}

/// <summary>Withdraws a notice. The soft delete; restorable.</summary>
[Authorize(Permission = IntelligencePermissions.ManageNotices)]
[Auditable]
public sealed record ArchiveOfficialNoticeCommand : ICommand
{
    public required Guid OfficialNoticeId { get; init; }
}

/// <summary>
/// Returns an archived notice to the desk.
/// <para>
/// It comes back as a draft, never as published — restoring is a decision to look at something again,
/// and republishing it unread would put a withdrawn official statement back in front of members
/// without anybody confirming it is still true.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.ManageNotices)]
[Auditable]
public sealed record RestoreOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }
}

/// <summary>
/// Records that somebody checked a notice is still true, without changing a word of it.
/// <para>
/// The single most useful operation on an ageing knowledge base and the one nobody adds until they
/// have one. A two-year-old indefinite notice nobody has looked at is a different thing from one
/// confirmed last week, and only this distinguishes them.
/// </para>
/// </summary>
[Authorize(Permission = IntelligencePermissions.ManageNotices)]
[Auditable]
public sealed record ReviewOfficialNoticeCommand : ICommand<OfficialNoticeResponse>
{
    public required Guid OfficialNoticeId { get; init; }
}

/// <summary>
/// Records one thing a reader did with a notice.
/// <para>
/// <b>Anonymous, and it has to be.</b> There is no permission on this command: reading official
/// information requires no account, so counting a read cannot either. The signal carries no reader
/// identity at any layer — not here, not in the sink contract, not in the schema.
/// </para>
/// <para>
/// <b>A command rather than a side effect of the GET.</b> A read that incremented a counter would
/// count every prefetch, every crawler and every retry, and would make the detail endpoint
/// un-cacheable on the screens that most need caching.
/// </para>
/// <para>
/// <b>Deliberately not <c>[Auditable]</c>.</b> Every other command in this phase is, because every
/// other one is somebody making an editorial decision worth a trail. This one fires on every open of
/// every notice by everybody — auditing it would write millions of rows recording that anonymous
/// readers read things, which is both useless and the closest this module could come to building the
/// behavioural log its analytics design exists to make impossible.
/// </para>
/// </summary>
[Transactional(Reason =
    "The notice's own counter and the country's counter are written by two saves — the handler's " +
    "and the analytics sink's. Either alone leaves a most-viewed list and a country total that " +
    "disagree about the same read.")]
[AllowAnonymous(
    "Deliberately anonymous, and the counter is the reason: readers who share a visa guide with a " +
    "relative are frequently not signed in, and a figure that counted only members would " +
    "understate the one number that measures whether the knowledge base works. Bounded by the " +
    "transport limiter rather than by a token (BSEC-002); BSEC-004 is the finding that asked for " +
    "the bound.")]
public sealed record RecordNoticeActivityCommand : ICommand
{
    public required Guid OfficialNoticeId { get; init; }

    public required RecordNoticeActivityRequest Request { get; init; }
}
