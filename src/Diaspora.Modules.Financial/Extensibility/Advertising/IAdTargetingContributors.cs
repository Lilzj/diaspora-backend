using Diaspora.Modules.Financial.Domain.Advertising;

namespace Diaspora.Modules.Financial.Extensibility.Advertising;

/// <summary>
/// What one targeting dimension offers, contributed by the module that owns the vocabulary.
/// <para>
/// <b>Contribution rather than a lookup, because this module references no other module.</b> The
/// Financial project takes a ProjectReference on SharedKernel, Contracts, Platform, Application and
/// Persistence and on nothing else — its own csproj says so — so it cannot ask B18 for a community
/// list or Location for a country one. It defines the shape; whoever owns the data implements it;
/// DI does the rest. That is the same inversion <c>IOrganizationTypeContributor</c> and
/// <c>IReferenceDataSeeder</c> already use.
/// </para>
/// <para>
/// <b>A dimension with no provider is reported as having no catalogue, not as having no options.</b>
/// Those are different sentences on a targeting screen: one means "pick from this empty list", the
/// other means "type what you want, the platform cannot enumerate these".
/// </para>
/// </summary>
public interface IAdTargetingOptionProvider
{
    /// <summary>The dimension this fills.</summary>
    AdTargetingDimension Dimension { get; }

    /// <summary>
    /// The selectable values.
    /// <para>
    /// <paramref name="query"/> is a free-text narrowing, because a country or community list is
    /// longer than a picker can render and paging a picker is worse than searching one.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdTargetingOption>> GetOptionsAsync(
        string? query, int take, CancellationToken cancellationToken = default);
}

/// <summary>One selectable value. The label is already localised by whoever produced it.</summary>
public sealed record AdTargetingOption(string Value, string Label);

/// <summary>
/// How many people one dimension of a selection could reach, contributed by the module that can
/// actually count them.
/// <para>
/// <b>Nobody is obliged to answer, and that is the honest part.</b> §29 forbids inventing an
/// authoritative metric, and most dimensions genuinely cannot be sized — the platform does not know
/// how many members hold an interest, and counting members by country would mean querying identity
/// data to price an advertisement. A dimension with no sizer contributes nothing and the estimate
/// says which dimensions it was actually able to use.
/// </para>
/// </summary>
public interface IAdAudienceSizer
{
    AdTargetingDimension Dimension { get; }

    /// <summary>
    /// Bounds for the values selected on this dimension, or null when it cannot say.
    /// <para>
    /// A <em>range</em> rather than a number, because a union of overlapping groups has no single
    /// answer: somebody in three of the selected communities is one person, and the platform cannot
    /// tell how many people are in more than one. The lower bound is the largest single group — the
    /// fewest people the union can contain — and the upper is the sum.
    /// </para>
    /// </summary>
    Task<AdAudienceBounds?> EstimateAsync(
        AdAudience audience, CancellationToken cancellationToken = default);
}

/// <summary>
/// What one sizer could say.
/// <para>
/// <see cref="Basis"/> is rendered to the advertiser verbatim. An estimate somebody cannot explain
/// is an estimate nobody should spend against.
/// </para>
/// </summary>
public sealed record AdAudienceBounds(long Lower, long Upper, string Basis);
