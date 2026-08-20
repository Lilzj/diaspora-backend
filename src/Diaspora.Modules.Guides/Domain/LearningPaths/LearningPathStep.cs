using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.LearningPaths;

/// <summary>
/// One step of a curated onboarding sequence — "Step 3: find somewhere to live".
/// <para>
/// <b>A step points at a guide, at a whole knowledge category, or at both.</b> That flexibility is
/// the difference between a path that can be written and one that cannot. "Read the residence
/// permit guide" is a single guide. "Sort out your banking" is a category — there are four guides,
/// which one applies depends on whether you are employed or self-employed, and a curator who had
/// to pick one would be giving half the readers the wrong answer. Naming both is the common case:
/// a recommended starting guide, with the rest of the category behind it.
/// </para>
/// <para>
/// <b>The title and description are the step's own, not the guide's.</b> A guide is titled for
/// somebody searching for it; a step is titled for somebody being walked through a sequence, and
/// "Opening a bank account as a non-resident" reads very differently as a search result and as
/// step 4 of 7. Copying the guide's title would also make the path silently rewrite itself when an
/// author retitles.
/// </para>
/// </summary>
public sealed class LearningPathStep
{
    private LearningPathStep(
        Guid stepId,
        int position,
        string title,
        string? description,
        Guid? guideId,
        string? knowledgeCategoryKey,
        bool isOptional)
    {
        StepId = stepId;
        Position = position;
        Title = title;
        Description = description;
        GuideId = guideId;
        KnowledgeCategoryKey = knowledgeCategoryKey;
        IsOptional = isOptional;
    }

    private LearningPathStep() { }

    /// <summary>
    /// The step's identity, supplied by the caller so a client can track progress across a
    /// rearrangement. Position changes when an editor inserts a step; this does not.
    /// </summary>
    public Guid StepId { get; private set; }

    /// <summary>Contiguous from zero. Renumbered by the aggregate on every arrangement.</summary>
    public int Position { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The guide to start with, or null when the step is a whole subject.</summary>
    public Guid? GuideId { get; private set; }

    /// <summary>The knowledge category to explore, or null when the step is one guide.</summary>
    public string? KnowledgeCategoryKey { get; private set; }

    /// <summary>
    /// Whether the step counts towards completion.
    /// <para>
    /// Optional steps are how a path stays honest for the people it half applies to: "register your
    /// children for school" is essential for a family and irrelevant for a single graduate, and a
    /// path that told the graduate they were 6 of 7 done forever would be a path they abandoned.
    /// </para>
    /// </summary>
    public bool IsOptional { get; private set; }

    internal static LearningPathStep? TryCreate(
        Guid stepId,
        int position,
        string? title,
        string? description,
        Guid? guideId,
        string? knowledgeCategoryKey,
        bool isOptional)
    {
        var trimmedTitle = KnowledgeNormalization.Text(title, KnowledgeLimits.MaxStepTitle);

        if (trimmedTitle is null)
        {
            return null;
        }

        string? categoryKey = null;

        if (!string.IsNullOrWhiteSpace(knowledgeCategoryKey))
        {
            categoryKey = KnowledgeNormalization.Key(knowledgeCategoryKey);

            if (categoryKey is null)
            {
                return null;
            }
        }

        var guide = guideId is { } value && value != Guid.Empty ? value : (Guid?)null;

        // A step that points at nothing is a heading, and a heading in a numbered sequence is a
        // step a reader cannot complete. Refusing it here means no progress indicator on the
        // platform can ever divide by a denominator that includes one.
        if (guide is null && categoryKey is null)
        {
            return null;
        }

        return new LearningPathStep(
            stepId == Guid.Empty ? Guid.NewGuid() : stepId,
            position,
            trimmedTitle,
            KnowledgeNormalization.Text(description, KnowledgeLimits.MaxStepDescription),
            guide,
            categoryKey,
            isOptional);
    }

    internal void MoveTo(int position) => Position = position;
}
