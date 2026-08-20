using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// The instruction a subtree needs when its root moves: rewrite every path starting with
/// <see cref="PreviousPath"/> to start with <see cref="NewPath"/> instead.
/// <para>
/// Returned by <see cref="AdministrativeArea.MoveUnder"/> rather than executed by it. The
/// aggregate owns its own consistency and nothing else's; descendants are separate
/// aggregates, and an aggregate that loaded a thousand of them to fix their paths would have
/// stopped being a consistency boundary and become a transaction.
/// </para>
/// </summary>
public sealed record AreaRelocation
{
    public required AreaId AreaId { get; init; }

    public required HierarchyPath PreviousPath { get; init; }

    public required HierarchyPath NewPath { get; init; }

    /// <summary>The country the subtree now sits in. Usually unchanged; not always.</summary>
    public required CountryId NewCountryId { get; init; }

    /// <summary>That country's ISO code, for the denormalised column on every descendant.</summary>
    public required Domain.ValueObjects.CountryCode NewCountryCode { get; init; }

    /// <summary>The prefix a descendant path currently starts with.</summary>
    public string PreviousDescendantPrefix => PreviousPath.DescendantPrefix;

    /// <summary>The prefix it must start with after the move.</summary>
    public string NewDescendantPrefix => NewPath.DescendantPrefix;
}
