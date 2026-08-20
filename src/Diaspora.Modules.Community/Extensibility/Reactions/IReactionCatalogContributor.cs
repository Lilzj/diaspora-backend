using Diaspora.Modules.Community.Domain.Reactions;

namespace Diaspora.Modules.Community.Extensibility.Reactions;

/// <summary>
/// Contributes reaction kinds.
/// <para>
/// The seam that satisfies the brief's "no hardcoded Like". A module or a deployment adds a
/// reaction by contributing a definition; nothing in Community is edited, no enum grows a
/// member, and no migration runs. B6 contributes the four the client already renders plus a
/// generic like.
/// </para>
/// <para>
/// Contributed definitions are seeded once and then owned by the database, so a deployment can
/// disable or retune one without redeploying the contributor that introduced it. Removing a
/// contributor does <b>not</b> delete its definitions — three million reactions filed under a
/// key whose definition vanished would render as nothing at all.
/// </para>
/// </summary>
public interface IReactionCatalogContributor
{
    string Module { get; }

    IEnumerable<ReactionDefinition> GetDefinitions();
}
