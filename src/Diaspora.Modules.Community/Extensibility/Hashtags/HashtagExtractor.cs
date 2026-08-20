using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;

namespace Diaspora.Modules.Community.Extensibility.Hashtags;

/// <summary>
/// Finds the tags in a body and runs the processor pipeline over them.
/// <para>
/// The scanner is deliberately simple and deliberately unicode-aware: a hash followed by letters
/// or digits, in any script. A regular expression would be shorter and would either exclude
/// every non-Latin script or need a character class nobody can review — and this platform's
/// members write in scripts a naive pattern gets wrong.
/// </para>
/// <para>
/// Duplicates within one body collapse to the first occurrence, and the whole set is capped.
/// Both matter for the same reason: the count that feeds trending must be uses-by-distinct-post,
/// or one member repeating a tag forty times becomes a trend.
/// </para>
/// </summary>
public sealed class HashtagExtractor
{
    private readonly IReadOnlyList<IHashtagProcessor> _processors;

    public HashtagExtractor(IEnumerable<IHashtagProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);

        _processors = processors
            .OrderBy(processor => processor.Order)
            .ThenBy(processor => processor.GetType().FullName, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<HashtagCandidate> Extract(string? body, int max = CommunityLimits.MaxHashtagsPerPost)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        IReadOnlyList<HashtagCandidate> found = Scan(body);

        foreach (var processor in _processors)
        {
            found = processor.Process(body, found);
        }

        return found
            .Where(candidate => candidate.Slug.Length >= HashtagSlug.MinLength)
            .GroupBy(candidate => candidate.Slug, StringComparer.Ordinal)
            .Select(group => group.OrderBy(candidate => candidate.StartOffset).First())
            .OrderBy(candidate => candidate.StartOffset)
            .Take(max)
            .ToList();
    }

    private static List<HashtagCandidate> Scan(string body)
    {
        var candidates = new List<HashtagCandidate>();

        for (var index = 0; index < body.Length; index++)
        {
            if (body[index] != '#')
            {
                continue;
            }

            // A hash immediately after a letter or digit is part of a word (C#, F#), not a tag.
            if (index > 0 && char.IsLetterOrDigit(body[index - 1]))
            {
                continue;
            }

            var end = index + 1;

            while (end < body.Length && (char.IsLetterOrDigit(body[end]) || body[end] == '_'))
            {
                end++;
            }

            var length = end - index;

            if (length <= 1)
            {
                continue;
            }

            var raw = body[(index + 1)..end];

            candidates.Add(new HashtagCandidate
            {
                Raw = raw,
                Slug = HashtagSlug.Normalise(raw),
                StartOffset = index,
                Length = length
            });

            index = end - 1;
        }

        return candidates;
    }
}
