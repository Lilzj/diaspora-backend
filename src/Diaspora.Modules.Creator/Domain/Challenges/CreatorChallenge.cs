using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Challenges;

/// <summary>
/// A challenge the programme runs. Reference data with a lifecycle.
/// <para>
/// <b>The rules are rows, and §9 of the phase brief requires it:</b> "Do not hard-code challenge
/// rules into handlers. Challenge rules should be data-driven." There is no switch on a challenge
/// key anywhere in this module — a task names an action type from <c>CreatorActivityTypes</c>, a
/// target and a window, and the enrolment counts against exactly that.
/// </para>
/// <para>
/// <b>The state a client sees is not this row's alone.</b> A challenge is <c>Open</c> to somebody
/// who has not joined and <c>InProgress</c> to somebody who has; the row carries the programme's
/// half — published, running, cancelled — and the enrolment carries the member's. Resolving the two
/// in one place is <c>ChallengeStateResolver</c>, which is what makes the client's three-tab
/// <c>state</c> filter map deterministically instead of by coincidence.
/// </para>
/// </summary>
public sealed class CreatorChallenge : AggregateRoot<CreatorChallengeId>, IAuditableEntity
{
    private readonly List<ChallengeTask> _tasks = [];
    private readonly List<ChallengeRuleText> _rules = [];
    private readonly List<ChallengeCountry> _countries = [];

    private CreatorChallenge(
        CreatorChallengeId id,
        string key,
        string title,
        string description,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt) : base(id)
    {
        Key = key;
        Title = title;
        Description = description;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    private CreatorChallenge() { }

    /// <summary>Stable and unique. What a seeder updates a challenge by.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string? Icon { get; private set; }

    public string? Category { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public bool IsPublished { get; private set; }

    public bool IsCancelled { get; private set; }

    /// <summary>Whether this challenge ranks its participants.</summary>
    public bool HasLeaderboard { get; private set; }

    /// <summary>
    /// The lowest creator status that may join, as an eligibility floor.
    /// <para>
    /// Decided on the server on every join, and served as <c>isEligible</c> on the read so the
    /// client can grey a button without owning the rule.
    /// </para>
    /// </summary>
    public CreatorStatus MinimumStatus { get; private set; } = CreatorStatus.NotACreator;

    /// <summary>How many creators have joined. A counter, reconciled rather than recounted per read.</summary>
    public int ParticipantCount { get; private set; }

    // ── The reward ──────────────────────────────────────────────────────────────────────────

    public string RewardKind { get; private set; } = "points";

    public int RewardPoints { get; private set; }

    public string? RewardBadgeKey { get; private set; }

    public string RewardLabel { get; private set; } = string.Empty;

    public IReadOnlyList<ChallengeTask> Tasks => _tasks.AsReadOnly();

    /// <summary>Free text the platform authored. Rendered, never parsed.</summary>
    public IReadOnlyList<ChallengeRuleText> Rules => _rules.AsReadOnly();

    /// <summary>Countries this challenge runs in. Empty means everywhere.</summary>
    public IReadOnlyList<ChallengeCountry> Countries => _countries.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorChallenge Define(
        CreatorChallengeId id,
        string key,
        string title,
        string description,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string rewardLabel,
        int rewardPoints,
        bool hasLeaderboard = false,
        string? category = null,
        string? icon = null,
        CreatorStatus minimumStatus = CreatorStatus.NotACreator)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new CreatorChallenge(id, key.Trim(), title.Trim(), description.Trim(), startsAt, endsAt)
        {
            RewardLabel = rewardLabel,
            RewardPoints = rewardPoints,
            HasLeaderboard = hasLeaderboard,
            Category = category,
            Icon = icon,
            MinimumStatus = minimumStatus
        };
    }

    public CreatorChallenge WithTask(
        CreatorChallengeTaskId taskId,
        string label,
        string actionType,
        long target,
        int weight = 1,
        ChallengeTaskWindow window = ChallengeTaskWindow.SinceJoined,
        string? description = null,
        string? actionRoute = null,
        string? actionLabel = null)
    {
        _tasks.Add(ChallengeTask.Create(
            taskId, label, actionType, target, weight, window, description, actionRoute, actionLabel,
            _tasks.Count));

        return this;
    }

    public CreatorChallenge WithRule(string text)
    {
        _rules.Add(ChallengeRuleText.Create(text, _rules.Count));
        return this;
    }

    public CreatorChallenge InCountries(params string[] countryCodes)
    {
        ArgumentNullException.ThrowIfNull(countryCodes);

        foreach (var code in countryCodes.Where(code => !string.IsNullOrWhiteSpace(code)))
        {
            _countries.Add(ChallengeCountry.Create(code));
        }

        return this;
    }

    public CreatorChallenge Publish()
    {
        IsPublished = true;
        return this;
    }

    public void Cancel() => IsCancelled = true;

    /// <summary>Rewrites the definition in place, for a re-run of the seeder.</summary>
    public void Redefine(
        string title,
        string description,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string rewardLabel,
        int rewardPoints,
        bool hasLeaderboard,
        string? category,
        string? icon,
        CreatorStatus minimumStatus,
        IEnumerable<ChallengeTask> tasks,
        IEnumerable<ChallengeRuleText> rules,
        IEnumerable<ChallengeCountry> countries)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(countries);

        Title = title.Trim();
        Description = description.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        RewardLabel = rewardLabel;
        RewardPoints = rewardPoints;
        HasLeaderboard = hasLeaderboard;
        Category = category;
        Icon = icon;
        MinimumStatus = minimumStatus;
        IsCancelled = false;
        IsPublished = true;

        _tasks.Clear();
        _tasks.AddRange(tasks);

        _rules.Clear();
        _rules.AddRange(rules);

        _countries.Clear();
        _countries.AddRange(countries);
    }

    public void RecordJoin() => ParticipantCount++;

    /// <summary>The programme's own view of where this challenge is, before any member is considered.</summary>
    public CreatorChallengeState ProgrammeState(DateTimeOffset now)
    {
        if (IsCancelled)
        {
            return CreatorChallengeState.Cancelled;
        }

        if (!IsPublished || now < StartsAt)
        {
            return CreatorChallengeState.Upcoming;
        }

        return now > EndsAt ? CreatorChallengeState.Expired : CreatorChallengeState.Open;
    }

    /// <summary>Whether somebody may join right now, ignoring who they are.</summary>
    public bool IsJoinable(DateTimeOffset now) => ProgrammeState(now) == CreatorChallengeState.Open;

    public ChallengeTask? FindTask(CreatorChallengeTaskId taskId) =>
        _tasks.Find(task => task.TaskId == taskId);

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
}

/// <summary>
/// One task inside a challenge, as data.
/// <para>
/// <see cref="Window"/> is what stops a challenge paying for work done before it opened. The
/// register's example — "create 3 helpful guides" — is only meaningful if the three are new.
/// </para>
/// </summary>
public sealed class ChallengeTask : ValueObject
{
    private ChallengeTask(
        CreatorChallengeTaskId taskId,
        string label,
        string actionType,
        long target,
        int weight,
        ChallengeTaskWindow window,
        string? description,
        string? actionRoute,
        string? actionLabel,
        int sortOrder)
    {
        TaskId = taskId;
        Label = label;
        ActionType = actionType;
        Target = target;
        Weight = weight;
        Window = window;
        Description = description;
        ActionRoute = actionRoute;
        ActionLabel = actionLabel;
        SortOrder = sortOrder;
    }

    private ChallengeTask() { }

    /// <summary>Caller-supplied, so <c>ValueGeneratedNever()</c>. The client names it in a payload.</summary>
    public CreatorChallengeTaskId TaskId { get; private set; } = default!;

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>A member of <c>CreatorActivityTypes</c>.</summary>
    public string ActionType { get; private set; } = string.Empty;

    public long Target { get; private set; }

    public int Weight { get; private set; } = 1;

    public ChallengeTaskWindow Window { get; private set; }

    public string? ActionRoute { get; private set; }

    public string? ActionLabel { get; private set; }

    public int SortOrder { get; private set; }

    public static ChallengeTask Create(
        CreatorChallengeTaskId taskId,
        string label,
        string actionType,
        long target,
        int weight,
        ChallengeTaskWindow window,
        string? description,
        string? actionRoute,
        string? actionLabel,
        int sortOrder) =>
        new(taskId,
            label.Trim(),
            actionType.Trim(),
            Math.Max(1, target),
            Math.Max(1, weight),
            window,
            description?.Trim(),
            actionRoute,
            actionLabel,
            sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TaskId;
    }
}

/// <summary>One sentence of a challenge's rules. Text, ordered, never parsed.</summary>
public sealed class ChallengeRuleText : ValueObject
{
    private ChallengeRuleText(string text, int sortOrder)
    {
        Text = text;
        SortOrder = sortOrder;
    }

    private ChallengeRuleText() { }

    public string Text { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static ChallengeRuleText Create(string text, int sortOrder) => new(text.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SortOrder;
        yield return Text;
    }
}

/// <summary>A country a challenge runs in.</summary>
public sealed class ChallengeCountry : ValueObject
{
    private ChallengeCountry(string countryCode) => CountryCode = countryCode;

    private ChallengeCountry() { }

    public string CountryCode { get; private set; } = string.Empty;

    public static ChallengeCountry Create(string countryCode) =>
        new(countryCode.Trim().ToUpperInvariant());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
    }
}
