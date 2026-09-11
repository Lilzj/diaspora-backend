using Diaspora.SharedKernel.Identity;

namespace Diaspora.Contracts.Automation.V1.Extensibility;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The Automation Platform's extension seams — B23.x Wave 12.
//
// **Why they are here and not in the Automation module.**
//
// Wave 11 declared two seams inside `Diaspora.Modules.Automation` and named the modules expected to
// implement them: `IAutomationScopeAuthority` for B6, B12 and B18, `IAutomationCalendarWriter` for
// B22. Every one of those modules composes *before* Automation — CommunityLife at 1100, Creator at
// 1400, Integrations at 2000, Automation at 2100 — and the platform's own guardrail,
// `No_module_depends_on_a_module_composed_after_it`, forbids any of them from referencing the
// assembly the interface lived in.
//
// So the seams were unimplementable by the modules they were written for. Nothing failed: no module
// tried, the shipped defaults answered honestly, and the capability simply stayed ungranted. It is
// the same shape this platform has recorded twice — PSR-1.2's "a seam is only as available as its
// assembly", and B13.3's single-implementation seam that could only ever have one user.
//
// `Diaspora.Contracts` is the one assembly every module already references and which references only
// the shared kernel, and it already hosts a cross-module seam for exactly this reason —
// `ITrustVerificationSubmission`. Declaring the automation seams here means an owning module
// implements and registers one without referencing the Automation module at all, and the dependency
// direction is unchanged: everyone points at contracts.
//
// **What did not move.** The implementations. `SelfOnlyScopeAuthority`, the resolver that picks
// between authorities, and the shipped calendar default all stay in the Automation module, because
// they are that module's behaviour rather than its contract.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Who an automation belongs to.
/// <para>
/// <c>Platform</c> is present so the model can describe one — an administrator's platform-wide
/// announcement rule — and the capability set refuses to let an ordinary member create one. Omitting
/// it would not prevent platform automations; it would prevent the platform from labelling one it
/// already had.
/// </para>
/// <para>
/// Declared here rather than in the module because it appears on every seam below, and an owning
/// module cannot answer "what may this member do in this scope" without being able to name the kind
/// of scope. Wave 12 moved it; the values and their numbers are unchanged, which matters because they
/// are persisted as strings and read back by name.
/// </para>
/// </summary>
public enum AutomationOwnerKind
{
    User = 0,
    Creator = 1,
    Provider = 2,
    Employer = 3,
    Landlord = 4,
    Seller = 5,
    Organization = 6,
    Community = 7,
    Platform = 8
}

/// <summary>
/// What one member may do within one owner scope.
/// </summary>
/// <param name="CanOwn">Whether they may create automations owned by this scope at all.</param>
/// <param name="CanManage">
/// Whether they may edit, pause and delete automations this scope owns that somebody else created.
/// A community's automations outlive the moderator who wrote them.
/// </param>
/// <param name="CanReachMembers">
/// Whether they may run an action that notifies this scope's members.
/// <para>
/// Separate from <see cref="CanOwn"/> because they are separately revocable and separately checked:
/// ownership is checked when the rule is written, and this is checked again in the moment before
/// every dispatch. A moderator who created a broadcast automation and then lost the role must stop
/// broadcasting, and configuration time is too early to notice.
/// </para>
/// </param>
/// <param name="Label">The scope's display name, for "this runs on behalf of".</param>
/// <param name="Capabilities">
/// The capability keys this scope grants — <c>automations.community</c> and its siblings. Read by the
/// capability endpoint and re-checked on every write.
/// </param>
/// <param name="ReachRefusal">
/// Why <paramref name="CanReachMembers"/> is false, when the reason is not the owner's standing.
/// <para>
/// <b>Added 11 Sep 2026 because one sentence was covering two facts.</b> The dispatcher refused a
/// broadcast with "Whoever set this up no longer has permission to notify these members" — true when
/// an administrator lost their role, and false when the scope simply cannot be addressed yet. The
/// owner of a community that staff have not opened was told they had lost a permission they held.
/// Null keeps the old sentence, which is still right for the case it was written for.
/// </para>
/// </param>
public readonly record struct AutomationScopeGrant(
    bool CanOwn,
    bool CanManage,
    bool CanReachMembers,
    string Label,
    IReadOnlyList<string> Capabilities,
    string? ReachRefusal = null)
{
    /// <summary>The safe answer whenever nothing has said otherwise.</summary>
    public static AutomationScopeGrant None(string label = "") =>
        new(false, false, false, label, []);

    /// <summary>A member over their own scope. The only grant the platform issues on its own authority.</summary>
    public static AutomationScopeGrant Self(string label) => new(true, true, true, label, []);
}

/// <summary>
/// Who a scope's automations may reach, as a count.
/// </summary>
/// <param name="EstimatedRecipients">
/// Null when the implementation cannot say, which is different from zero and is rendered differently.
/// </param>
/// <param name="Description">A sentence naming who this reaches. Never a list of members.</param>
/// <param name="IsExact">
/// False when the figure was banded. Small counts are suppressed rather than reported precisely —
/// "3 members match" in a small community identifies people.
/// </param>
public readonly record struct AutomationAudienceEstimate(
    int? EstimatedRecipients,
    string Description,
    bool IsExact)
{
    public static AutomationAudienceEstimate Unavailable(string description) =>
        new(null, description, false);
}

/// <summary>
/// The seam by which a module that owns a scope answers what its members may do with automation.
/// <para>
/// <b>The automation platform cannot answer the question and must not pretend to.</b> Whether
/// somebody is a moderator of a community, an administrator of an organization or a verified creator
/// is knowledge the owning module holds; an automation platform that decided would be a second,
/// divergent answer to a question that already has one, and the Phase 18 register says so explicitly:
/// "two capability sets over one community is two answers to one question".
/// </para>
/// <para>
/// <b>The shipped implementation is deny-by-default for every scope but the caller's own.</b> A module
/// that ships a real authority registers this interface and the resolver prefers it, because it names
/// the owner kinds it handles and the floor does not.
/// </para>
/// <para>
/// <b>Implementations must be cheap and must not throw.</b> This runs on the execution path, once per
/// automation per event, and a scope authority that failed would turn a revoked role into a run that
/// crashed rather than a run that was refused.
/// </para>
/// </summary>
public interface IAutomationScopeAuthority
{
    /// <summary>Which owner kinds this implementation answers for. Others fall through to the default.</summary>
    IReadOnlyList<AutomationOwnerKind> Handles { get; }

    /// <summary>What this member may do in this scope. Deny-by-default on anything unknown.</summary>
    Task<AutomationScopeGrant> EvaluateAsync(
        UserId member,
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Who an action in this scope would reach.
    /// <para>
    /// A count and prose. There is no variant that returns members: an audience preview that listed
    /// people would be an enumeration tool for a community, reachable by anybody who could create an
    /// automation.
    /// </para>
    /// </summary>
    Task<AutomationAudienceEstimate> EstimateAudienceAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The members an action in this scope would notify.
    /// <para>
    /// Returned in pages so a large community does not become one query and one list in memory. An
    /// implementation that cannot enumerate returns empty, and the engine records the action as
    /// skipped with a reason rather than claiming a broadcast happened.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserId>> ListRecipientsAsync(
        AutomationOwnerKind ownerKind,
        Guid ownerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The modules that answer a condition no trigger payload may answer — Wave 12.
/// <para>
/// Strings rather than type references, so the vocabulary takes no dependency on the assemblies that
/// implement the seam.
/// </para>
/// </summary>
public static class AutomationConditionSources
{
    /// <summary>The Financial Platform — a balance, and whether a subscription is live.</summary>
    public const string Entitlement = "financial";

    /// <summary>The Creator Platform — a member's creator standing.</summary>
    public const string Creator = "creator";

    /// <summary>The Analytics Platform — a member's own figures.</summary>
    public const string Metric = "analytics";
}

/// <summary>The condition keys an <see cref="IAutomationConditionSource"/> may be asked about.</summary>
public static class AutomationConditionKeys
{
    public const string WalletBalance = "walletBalance";
    public const string SubscriptionActive = "subscriptionActive";
    public const string CreatorStatus = "creatorStatus";
    public const string MetricAbove = "metricAbove";
    public const string MetricBelow = "metricBelow";
}

/// <summary>
/// What a member is being asked about, when the answer is not in the trigger's payload.
/// </summary>
/// <param name="Member">
/// The member whose state is being read.
/// <para>
/// <b>Always the automation's own owner, resolved server-side.</b> There is no path by which a rule,
/// a payload or a client can name whose balance is read: the engine passes the owner it authorized
/// the run as, and an implementation that accepted an identifier from anywhere else would turn a
/// condition into a cross-member read.
/// </para>
/// </param>
/// <param name="ConditionKeys">
/// The catalogue keys this rule actually asks about — a subset of what the source answers.
/// <para>
/// Passed so one round trip answers a rule with three conditions on it, and so a source can skip work
/// nobody asked for. A member's balance is not read because their rule checks their creator level.
/// </para>
/// </param>
/// <param name="SubjectId">The subject the trigger was about, where it had one. Usually irrelevant.</param>
public readonly record struct AutomationConditionQuery(
    UserId Member,
    IReadOnlyList<string> ConditionKeys,
    Guid? SubjectId);

/// <summary>
/// The seam by which a module that owns a figure answers a condition about it — Wave 12.
///
/// <para><b>Five conditions in the frontend's catalogue cannot be answered from a payload</b> —
/// <c>walletBalance</c>, <c>subscriptionActive</c>, <c>creatorStatus</c>, <c>metricAbove</c> and
/// <c>metricBelow</c>. Wave 11 shipped all five declared unevaluable, failing closed with a stated
/// reason, and recorded that each "waits on a read port from B15, B18 and B19". This is that port.</para>
///
/// <para><b>The direction is what makes it correct.</b> The automation platform does not read B15's
/// ledger, B18's creator state or B19's rollups. It declares a question; the module that owns the
/// answer implements this interface and is injected. Nothing in the automation assembly knows what a
/// wallet is, and an architecture test asserts it names no financial, creator or analytics type.</para>
///
/// <para><b>A payload can never satisfy one of these, and that is a security property rather than a
/// design preference.</b> Resolved answers are written into their own dictionary, which the condition
/// evaluator consults <i>instead of</i> the trigger's values for exactly these keys. If they shared a
/// dictionary, any module that publishes an event could decide whether a member's balance condition
/// matched by putting a number in a payload.</para>
///
/// <para><b>Implementations must be cheap, must not throw, and must fail closed.</b> This runs on the
/// execution path, once per automation per event. An implementation that cannot answer omits the key
/// rather than returning a default, and the run is skipped with the catalogue's stated reason —
/// because a condition optimistically matched is a member told something untrue.</para>
/// </summary>
public interface IAutomationConditionSource
{
    /// <summary>
    /// Which condition keys this source answers. Others fall through to any other source.
    /// <para>
    /// Two sources claiming one key is a configuration error rather than a merge: the resolver takes
    /// the first that answers, because two opinions about a balance is the same problem two capability
    /// sets over one community is.
    /// </para>
    /// </summary>
    IReadOnlyList<string> Answers { get; }

    /// <summary>
    /// The values for the keys asked about, in the evaluator's comparison form.
    /// <para>
    /// Money as a decimal string in the invariant culture; a boolean as <c>true</c> or <c>false</c>; a
    /// choice as its own key. A key the source cannot answer <b>must be omitted</b> — an entry with a
    /// null value and a missing entry mean the same thing here and both fail closed, but omitting is
    /// the one that cannot be mistaken for "the answer is nothing".
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<string, string?>> ReadAsync(
        AutomationConditionQuery request, CancellationToken cancellationToken = default);
}

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Subject state — Phase 5.
//
// **The defect this exists to close.** `POST /automations/simulate` accepts a `SampleSubjectId`,
// documented as "an optional real subject to evaluate against, so a dry run borrows real state". It
// borrowed none. The identifier was copied onto the trigger context and every other field was left
// at its default — including `Values`, which the context's own summary calls "the entire surface a
// condition can be answered from". So every payload-carried condition in every dry run resolved to
// the same sentence:
//
//     "This trigger does not carry the information the 'country' check needs."
//
// That sentence is false. Verified against the deployed platform on 10 Sep 2026: an event whose
// country is MU, a condition reading `country equals MU`, that event's own identifier passed as the
// sample subject — and the run came back SKIPPED with those words. The trigger carries `country`;
// `EventCreatedAutomationConsumer` puts it there, and the `event.startingSoon` horizon carries it
// too. The *simulation* carried nothing, and told the member it was the trigger's fault.
//
// **Why a seam rather than a read.** The Automation module cannot open Community Life's tables to
// find out what an event's country is, for the same reason it cannot open the ledger to find a
// balance — <see cref="IAutomationConditionSource"/> exists precisely so it does not have to. This
// is that argument applied to the other half of the dictionary: a module that owns a subject
// answers for it, and nothing in the automation assembly learns what an event is.
//
// **Why it is not <see cref="IAutomationConditionSource"/> with a wider key set.** The two answer
// different questions and must not share a dictionary. A condition source answers about *a member*
// — their balance, their creator standing — and its answers land in `ResolvedValues`, which the
// evaluator consults instead of the payload for exactly those keys, so that no module publishing an
// event can decide whether a balance condition matched. A subject source answers about *a thing*,
// and its answers are the payload: the same values the owning module's own consumer would have put
// there had the trigger really fired. Keeping them apart keeps that security property intact.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>What is being asked about, and by whom.</summary>
/// <param name="SubjectKind">
/// The kind, from the automation platform's closed set — <c>event</c>, <c>listing</c>, <c>job</c>,
/// <c>housing</c>, <c>guide</c>, <c>provider</c>. A source declares the kinds it answers.
/// </param>
/// <param name="SubjectId">The subject's own identifier, as the owning module issued it.</param>
/// <param name="Member">
/// Who is asking.
/// <para>
/// <b>Passed so an implementation can refuse, not so it can widen.</b> A preview is built from a
/// subject the member named, and the honest answer for one they cannot see is no answer at all — a
/// source that returned an unpublished listing's price would make this route a way to read drafts.
/// Implementations apply the same visibility their own read paths apply.
/// </para>
/// </param>
public readonly record struct AutomationSubjectQuery(
    string SubjectKind,
    Guid SubjectId,
    UserId Member);

/// <summary>
/// A subject's current state, in the shape a trigger over it would carry.
/// </summary>
public sealed record AutomationSubjectSnapshot
{
    /// <summary>Echoed back, so a caller holding several can tell them apart.</summary>
    public required string SubjectKind { get; init; }

    public required Guid SubjectId { get; init; }

    /// <summary>The subject's own title, for the preview's wording. Public field, always.</summary>
    public required string Title { get; init; }

    /// <summary>The moment the subject is about, where it has one — an event's start, a closing date.</summary>
    public DateTimeOffset? Moment { get; init; }

    /// <summary>The subject's own IANA zone, where it has one. §42: an event in Lagos stays in Lagos.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>
    /// The condition values a real trigger over this subject would carry.
    /// <para>
    /// <b>Keys are catalogue condition keys and the values are in the evaluator's comparison form</b>
    /// — the same dictionary the owning module's automation consumer builds from its own published
    /// contract. That is the point: a preview that derived them differently would be a second
    /// definition of what a rule matches, and the member would be shown one answer and get another.
    /// </para>
    /// <para>
    /// A key the subject genuinely has no value for is <b>omitted</b>, never nulled — the same rule
    /// <see cref="IAutomationConditionSource"/> states, for the same reason.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Values { get; init; }
}

/// <summary>
/// The seam by which the module that owns a subject describes it to a preview — Phase 5.
///
/// <para><b>Read-only, and never on the execution path.</b> A real run is driven by a published
/// contract and this is not consulted. It answers two things a member asked for directly: a dry run
/// against a subject they named, and the builder's "here is what this would be checked against".</para>
///
/// <para><b>Implementations must not throw and must fail closed.</b> A subject the member cannot see,
/// a subject that does not exist and a module having a bad minute all produce <c>null</c> — and the
/// caller says "this could not be previewed", which is true in all three cases. Inventing a default
/// would put a match in front of a member that the real run would not produce.</para>
/// </summary>
public interface IAutomationSubjectSource
{
    /// <summary>
    /// Which subject kinds this source answers.
    /// <para>
    /// Two sources claiming one kind is a configuration error rather than a merge, exactly as it is
    /// for a condition key: the resolver takes the first that answers.
    /// </para>
    /// </summary>
    IReadOnlyList<string> Answers { get; }

    /// <summary>The subject's state, or <c>null</c> where it cannot be described to this member.</summary>
    Task<AutomationSubjectSnapshot?> ReadAsync(
        AutomationSubjectQuery request, CancellationToken cancellationToken = default);
}
