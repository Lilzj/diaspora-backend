using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Mentions;

/// <summary>
/// Turns a handle into something real.
/// <para>
/// The brief asks that mentions support members, organizations, providers and future entities.
/// This is how the last of those works without a release: whoever owns a new kind of mentionable
/// thing registers a resolver, and the same at-handle syntax reaches it.
/// </para>
/// <para>
/// Resolvers are tried in order and the first non-null answer wins, so ordering is a real
/// decision rather than a tidiness one — a handle that could be a member or an organization goes
/// to whichever resolver runs first. B6's member resolver runs last, so a module owning a
/// namespaced handle can claim it.
/// </para>
/// </summary>
public interface IMentionResolver
{
    MentionTargetType TargetType { get; }

    int Order => 0;

    /// <summary>
    /// Resolves handles in bulk. There is no singular form on purpose: a body with eight
    /// mentions must not become eight round trips through eight modules.
    /// </summary>
    Task<IReadOnlyDictionary<string, MentionTarget>> ResolveAsync(
        IReadOnlyCollection<string> handles, CancellationToken cancellationToken = default);
}
