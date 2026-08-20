using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Mentions;

/// <summary>
/// Finds at-handles in a body and asks the registered resolvers to claim them.
/// <para>
/// Unclaimed handles are returned as unresolved rather than dropped, so the body renders exactly
/// what somebody wrote. An email address in a body is the case this has to get right, and the
/// scanner does: a handle immediately preceded by a word character is skipped.
/// </para>
/// </summary>
public sealed class MentionExtractor
{
    private readonly IReadOnlyList<IMentionResolver> _resolvers;

    public MentionExtractor(IEnumerable<IMentionResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);

        _resolvers = resolvers
            .OrderBy(resolver => resolver.Order)
            .ThenBy(resolver => resolver.GetType().FullName, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<MentionCandidate> Scan(string? body, int max)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var candidates = new List<MentionCandidate>();

        for (var index = 0; index < body.Length && candidates.Count < max; index++)
        {
            if (body[index] != '@')
            {
                continue;
            }

            // An at-sign after a word character is an email address, not a mention.
            if (index > 0 && (char.IsLetterOrDigit(body[index - 1]) || body[index - 1] == '.'))
            {
                continue;
            }

            var end = index + 1;

            while (end < body.Length &&
                   (char.IsLetterOrDigit(body[end]) || body[end] is '_' or '.' or '-'))
            {
                end++;
            }

            // A trailing dot or hyphen is punctuation, not part of the handle.
            while (end > index + 1 && body[end - 1] is '.' or '-')
            {
                end--;
            }

            var length = end - index;

            if (length <= 1)
            {
                continue;
            }

            candidates.Add(new MentionCandidate
            {
                Handle = body[(index + 1)..end],
                StartOffset = index,
                Length = length
            });

            index = end - 1;
        }

        return candidates;
    }

    /// <summary>Scans and resolves. Every candidate comes back, resolved or not.</summary>
    public async Task<IReadOnlyList<(MentionCandidate Candidate, MentionTarget Target)>> ExtractAsync(
        string? body,
        int max = CommunityLimits.MaxMentionsPerPost,
        CancellationToken cancellationToken = default)
    {
        var candidates = Scan(body, max);

        if (candidates.Count == 0)
        {
            return [];
        }

        var resolved = new Dictionary<string, MentionTarget>(StringComparer.OrdinalIgnoreCase);
        var outstanding = candidates
            .Select(candidate => candidate.Handle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var resolver in _resolvers)
        {
            if (outstanding.Count == 0)
            {
                break;
            }

            var answers = await resolver.ResolveAsync(outstanding, cancellationToken);

            foreach (var (handle, target) in answers)
            {
                if (target.IsResolved)
                {
                    resolved[handle] = target;
                }
            }

            outstanding = outstanding.Where(handle => !resolved.ContainsKey(handle)).ToList();
        }

        return candidates
            .Select(candidate => (
                Candidate: candidate,
                Target: resolved.TryGetValue(candidate.Handle, out var target)
                    ? target
                    : MentionTarget.Unresolved(candidate.Handle)))
            .ToList();
    }
}
