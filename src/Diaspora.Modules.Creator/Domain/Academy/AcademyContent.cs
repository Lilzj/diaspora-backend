using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Academy;

/// <summary>A section of the Academy — "Start Here", "Monetization", "Community Safety".</summary>
public sealed class AcademySection : AggregateRoot<AcademySectionId>, IAuditableEntity
{
    private AcademySection(AcademySectionId id, string key, string title) : base(id)
    {
        Key = key;
        Title = title;
    }

    private AcademySection() { }

    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Icon { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static AcademySection Define(
        AcademySectionId id,
        string key,
        string title,
        int sortOrder,
        string? description = null,
        string? icon = null) =>
        new(id, key.Trim(), title.Trim())
        {
            SortOrder = sortOrder,
            Description = description?.Trim(),
            Icon = icon,
            IsPublished = true
        };

    public void Redefine(string title, string? description, string? icon, int sortOrder)
    {
        Title = title.Trim();
        Description = description?.Trim();
        Icon = icon;
        SortOrder = sortOrder;
        IsPublished = true;
    }

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
/// A course.
/// <para>
/// <b><see cref="ContentVersion"/> is CREATOR-GAP-006's content-versioning rule.</b> The register:
/// "A content-versioning rule, so progress survives a lesson being rewritten." A member who finished
/// version 3 stays finished when version 4 lands — their progress row records the version they
/// completed at, and the read serves both, so the app can say "this has been updated since you read
/// it" instead of the platform silently deciding either that they must do it again or that they
/// have read something they have not.
/// </para>
/// </summary>
public sealed class AcademyCourse : AggregateRoot<AcademyCourseId>, IAuditableEntity
{
    private readonly List<CourseTag> _tags = [];
    private readonly List<CoursePrerequisite> _prerequisites = [];

    private AcademyCourse(
        AcademyCourseId id, string slug, string title, string summary, CourseLevel level)
        : base(id)
    {
        Slug = slug;
        Title = title;
        Summary = summary;
        Level = level;
    }

    private AcademyCourse() { }

    /// <summary>Stable and unique. What a seeder updates a course by.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string? SectionKey { get; private set; }

    public CourseLevel Level { get; private set; }

    public int EstimatedMinutes { get; private set; }

    public string? CoverImageUrl { get; private set; }

    public string? Icon { get; private set; }

    public int LessonCount { get; private set; }

    public bool AwardsCertificate { get; private set; }

    public string? AwardsBadgeKey { get; private set; }

    /// <summary>Points the programme grants for finishing. Awarded through B15, never here.</summary>
    public int CompletionPoints { get; private set; }

    /// <summary>Bumped whenever the course or any of its lessons is rewritten. See the class summary.</summary>
    public int ContentVersion { get; private set; } = 1;

    public int SortOrder { get; private set; }

    public bool IsPublished { get; private set; }

    public IReadOnlyList<CourseTag> Tags => _tags.AsReadOnly();

    public IReadOnlyList<CoursePrerequisite> Prerequisites => _prerequisites.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static AcademyCourse Define(
        AcademyCourseId id,
        string slug,
        string title,
        string summary,
        CourseLevel level,
        int estimatedMinutes,
        int sortOrder,
        string? sectionKey = null,
        bool awardsCertificate = false,
        string? awardsBadgeKey = null,
        int completionPoints = 0,
        string? icon = null) =>
        new(id, slug.Trim(), title.Trim(), summary.Trim(), level)
        {
            EstimatedMinutes = estimatedMinutes,
            SortOrder = sortOrder,
            SectionKey = sectionKey,
            AwardsCertificate = awardsCertificate,
            AwardsBadgeKey = awardsBadgeKey,
            CompletionPoints = completionPoints,
            Icon = icon,
            IsPublished = true
        };

    public AcademyCourse WithTag(string tag)
    {
        _tags.Add(CourseTag.Create(tag, _tags.Count));
        return this;
    }

    public AcademyCourse RequiringCourse(string slug)
    {
        _prerequisites.Add(CoursePrerequisite.Create(slug));
        return this;
    }

    /// <summary>
    /// Rewrites the course and bumps its content version.
    /// <para>
    /// The bump is unconditional on a redefinition rather than computed from a diff. A version that
    /// only moved when somebody noticed a change would be a version nobody could rely on, and the
    /// cost of an unnecessary bump is a "this has been updated" badge that is a little eager.
    /// </para>
    /// </summary>
    public void Redefine(
        string title,
        string summary,
        CourseLevel level,
        int estimatedMinutes,
        int sortOrder,
        string? sectionKey,
        bool awardsCertificate,
        string? awardsBadgeKey,
        int completionPoints,
        string? icon,
        IEnumerable<CourseTag> tags,
        IEnumerable<CoursePrerequisite> prerequisites)
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(prerequisites);

        Title = title.Trim();
        Summary = summary.Trim();
        Level = level;
        EstimatedMinutes = estimatedMinutes;
        SortOrder = sortOrder;
        SectionKey = sectionKey;
        AwardsCertificate = awardsCertificate;
        AwardsBadgeKey = awardsBadgeKey;
        CompletionPoints = completionPoints;
        Icon = icon;
        IsPublished = true;
        ContentVersion++;

        _tags.Clear();
        _tags.AddRange(tags);

        _prerequisites.Clear();
        _prerequisites.AddRange(prerequisites);
    }

    public void SetLessonCount(int count) => LessonCount = Math.Max(0, count);

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

/// <summary>A tag on a course.</summary>
public sealed class CourseTag : ValueObject
{
    private CourseTag(string value, int sortOrder)
    {
        Value = value;
        SortOrder = sortOrder;
    }

    private CourseTag() { }

    public string Value { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static CourseTag Create(string value, int sortOrder) => new(value.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

/// <summary>A course the platform wants done first.</summary>
public sealed class CoursePrerequisite : ValueObject
{
    private CoursePrerequisite(string courseSlug) => CourseSlug = courseSlug;

    private CoursePrerequisite() { }

    public string CourseSlug { get; private set; } = string.Empty;

    public static CoursePrerequisite Create(string courseSlug) => new(courseSlug.Trim());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CourseSlug;
    }
}

/// <summary>
/// A lesson, as structured blocks.
/// <para>
/// <b>Blocks, never markup, and the register states the reason:</b> "Lessons are
/// <c>LessonBlock[]</c>, never HTML or markdown. A content service that could send markup could send
/// a script." The stored shape is a closed set of seven kinds; anything else is refused at the point
/// content is written, so the question of what a renderer does with an unknown kind never becomes a
/// question about what it does with a <c>&lt;script&gt;</c>.
/// </para>
/// <para>
/// <b>Its own aggregate rather than a collection on the course.</b> A course's blocks are the bulk
/// of the Academy's data, and listing courses should not load every paragraph of every lesson.
/// </para>
/// </summary>
public sealed class AcademyLesson : AggregateRoot<AcademyLessonId>, IAuditableEntity
{
    private readonly List<LessonBlock> _blocks = [];
    private readonly List<LessonBlockItem> _blockItems = [];

    private AcademyLesson(
        AcademyLessonId id, AcademyCourseId courseId, string key, string title, LessonKind kind)
        : base(id)
    {
        CourseId = courseId;
        Key = key;
        Title = title;
        Kind = kind;
    }

    private AcademyLesson() { }

    public AcademyCourseId CourseId { get; private set; } = default!;

    /// <summary>Unique inside its course. What a seeder updates a lesson by.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public LessonKind Kind { get; private set; }

    public int EstimatedMinutes { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>
    /// The quiz that finishes this lesson, when <see cref="Kind"/> is
    /// <see cref="LessonKind.Quiz"/>. Null otherwise, and a quiz lesson without one cannot be
    /// completed at all — which is the safe failure.
    /// </summary>
    public AcademyQuizId? QuizId { get; private set; }

    /// <summary>Bumped on every rewrite. See <see cref="AcademyCourse.ContentVersion"/>.</summary>
    public int ContentVersion { get; private set; } = 1;

    public bool IsPublished { get; private set; }

    public IReadOnlyList<LessonBlock> Blocks => _blocks.AsReadOnly();

    /// <summary>
    /// The lines belonging to list and checklist blocks, keyed back to their block's sort order.
    /// <para>
    /// <b>Flat on the lesson rather than nested inside the block, and that is a persistence decision
    /// made deliberately.</b> An owned collection inside an owned collection is a shape EF will
    /// accept and this platform has never shipped; two flat owned collections joined in the mapper
    /// are a shape it has shipped many times. The alternative — a JSON column — is what §27 of the
    /// phase brief forbids outright.
    /// </para>
    /// </summary>
    public IReadOnlyList<LessonBlockItem> BlockItems => _blockItems.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static AcademyLesson Define(
        AcademyLessonId id,
        AcademyCourseId courseId,
        string key,
        string title,
        LessonKind kind,
        int estimatedMinutes,
        int sortOrder) =>
        new(id, courseId, key.Trim(), title.Trim(), kind)
        {
            EstimatedMinutes = estimatedMinutes,
            SortOrder = sortOrder,
            IsPublished = true
        };

    /// <summary>Adds a block, with the lines it carries where it carries any.</summary>
    public AcademyLesson WithBlock(LessonBlock block, params string[] items)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(items);

        if (_blocks.Count >= CreatorLimits.MaxLessonBlocks)
        {
            return this;
        }

        _blocks.Add(block);

        var order = 0;

        foreach (var item in items.Take(CreatorLimits.MaxListItems))
        {
            _blockItems.Add(LessonBlockItem.Create(block.SortOrder, item, order));
            order++;
        }

        return this;
    }

    public AcademyLesson WithQuiz(AcademyQuizId quizId)
    {
        QuizId = quizId;
        return this;
    }

    public void Redefine(
        string title,
        LessonKind kind,
        int estimatedMinutes,
        int sortOrder,
        AcademyQuizId? quizId,
        IEnumerable<LessonBlock> blocks,
        IEnumerable<LessonBlockItem> blockItems)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(blockItems);

        Title = title.Trim();
        Kind = kind;
        EstimatedMinutes = estimatedMinutes;
        SortOrder = sortOrder;
        QuizId = quizId;
        IsPublished = true;
        ContentVersion++;

        _blocks.Clear();
        _blocks.AddRange(blocks.Take(CreatorLimits.MaxLessonBlocks));

        _blockItems.Clear();
        _blockItems.AddRange(blockItems);
    }

    /// <summary>The lines belonging to one block, in order.</summary>
    public IReadOnlyList<string> ItemsFor(int blockSortOrder) =>
        _blockItems
            .Where(item => item.BlockSortOrder == blockSortOrder)
            .OrderBy(item => item.SortOrder)
            .Select(item => item.Text)
            .ToList();

    /// <summary>
    /// Whether asserting completion is enough.
    /// <para>
    /// False for a quiz lesson, which is the whole of <c>creator.lesson.quiz_required</c>.
    /// </para>
    /// </summary>
    public bool CanBeCompletedByAssertion => Kind != LessonKind.Quiz;

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
/// One piece of lesson content.
/// <para>
/// A discriminated union flattened into columns, because EF maps columns and the alternative — a
/// JSON blob — is what §27 of the phase brief forbids in as many words: "Do not create a generic
/// 'CreatorData' JSON blob as a shortcut." Which columns are meaningful depends on
/// <see cref="Kind"/>, and the mapper reads only the ones that are.
/// </para>
/// </summary>
public sealed class LessonBlock : ValueObject
{
    private LessonBlock(LessonBlockKind kind, int sortOrder)
    {
        Kind = kind;
        SortOrder = sortOrder;
    }

    private LessonBlock() { }

    public LessonBlockKind Kind { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Heading, paragraph, callout, quote and action all carry text.</summary>
    public string? Text { get; private set; }

    /// <summary>Callout's title, quote's attribution, action's label.</summary>
    public string? Title { get; private set; }

    /// <summary>Callout only.</summary>
    public CalloutTone? Tone { get; private set; }

    /// <summary>List only: whether it is numbered.</summary>
    public bool IsOrdered { get; private set; }

    /// <summary>Action only: an in-app route. Never an external URL.</summary>
    public string? Route { get; private set; }

    public static LessonBlock Heading(string text, int sortOrder) =>
        new(LessonBlockKind.Heading, sortOrder) { Text = Bound(text) };

    public static LessonBlock Paragraph(string text, int sortOrder) =>
        new(LessonBlockKind.Paragraph, sortOrder) { Text = Bound(text) };

    /// <summary>The lines are added alongside, through <c>AcademyLesson.WithBlock</c>.</summary>
    public static LessonBlock List(bool ordered, int sortOrder) =>
        new(LessonBlockKind.List, sortOrder) { IsOrdered = ordered };

    public static LessonBlock Checklist(int sortOrder) =>
        new(LessonBlockKind.Checklist, sortOrder);

    public static LessonBlock Callout(CalloutTone tone, string? title, string text, int sortOrder) =>
        new(LessonBlockKind.Callout, sortOrder)
        {
            Tone = tone,
            Title = Bound(title),
            Text = Bound(text)
        };

    public static LessonBlock Quote(string text, string? attribution, int sortOrder) =>
        new(LessonBlockKind.Quote, sortOrder) { Text = Bound(text), Title = Bound(attribution) };

    public static LessonBlock Action(string label, string route, string? description, int sortOrder) =>
        new(LessonBlockKind.Action, sortOrder)
        {
            Title = Bound(label),
            Route = route.Trim(),
            Text = Bound(description)
        };

    private static string? Bound(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed[..Math.Min(trimmed.Length, CreatorLimits.MaxBlockTextLength)];
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return SortOrder;
    }
}

/// <summary>
/// One line of a list or checklist block.
/// <para>
/// Owned by the lesson rather than by the block, and keyed back to it by
/// <see cref="BlockSortOrder"/>. See <c>AcademyLesson.BlockItems</c> for why.
/// </para>
/// </summary>
public sealed class LessonBlockItem : ValueObject
{
    private LessonBlockItem(int blockSortOrder, string text, int sortOrder)
    {
        BlockSortOrder = blockSortOrder;
        Text = text;
        SortOrder = sortOrder;
    }

    private LessonBlockItem() { }

    /// <summary>Which block this line belongs to, by that block's position in the lesson.</summary>
    public int BlockSortOrder { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static LessonBlockItem Create(int blockSortOrder, string text, int sortOrder) =>
        new(blockSortOrder, text.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BlockSortOrder;
        yield return SortOrder;
        yield return Text;
    }
}
