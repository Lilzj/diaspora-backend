using Diaspora.Contracts.Moderation.V1;

namespace Diaspora.Modules.Trust.Application.Moderation;

/// <summary>
/// Every module's <see cref="IModerationTarget"/>, by content type (7 Nov 2026).
/// <para>
/// Each module registers its own; Trust never references a module. A case about a content type no
/// module claims cannot be decided, and says so, rather than recording a decision nothing carries out.
/// </para>
/// </summary>
public sealed class ModerationTargetRegistry
{
    private readonly Dictionary<string, IModerationTarget> _targets;

    public ModerationTargetRegistry(IEnumerable<IModerationTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        _targets = new Dictionary<string, IModerationTarget>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in targets)
        {
            // Last registration wins, as DI does: a module replacing its own target in a test host
            // must not be shadowed by the one it replaced.
            _targets[target.ContentType] = target;
        }
    }

    public IModerationTarget? Find(string? contentType) =>
        contentType is not null && _targets.TryGetValue(contentType, out var target) ? target : null;

    public IReadOnlyCollection<string> ContentTypes => _targets.Keys;
}
