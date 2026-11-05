using System.Globalization;
using System.Linq.Expressions;
using System.Text;

namespace Diaspora.SharedKernel.Search;

/// <summary>
/// The words of a search that say something, for matching without any AI (owner, 5 Nov 2026).
/// <para>
/// "Where to braid my hair in Grand Baie" was matched as one case-sensitive phrase, so a
/// sentence found nothing. A search is read here as its meaningful words, lower-cased, with the
/// words that only hold a sentence together ("where", "to", "my", "near", "me") left out, and a
/// result matches when any of them does. Words are compared lower-cased on both sides, because
/// PostgreSQL's <c>LIKE</c> is case-sensitive (SQLite's, which the tests run on, is not).
/// </para>
/// </summary>
public static class SearchWords
{
    /// <summary>Enough for any real search; a pasted paragraph is not one.</summary>
    public const int MaxWords = 6;

    private static readonly HashSet<string> Filler = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "of", "for", "to", "in", "on", "at", "by", "with", "from",
        "near", "nearby", "around", "close", "here", "there", "me", "my", "mine", "i", "im",
        "we", "us", "our", "you", "your", "where", "what", "which", "who", "how", "when", "can",
        "could", "would", "will", "want", "wants", "need", "needs", "looking", "look", "find",
        "get", "is", "are", "be", "do", "does", "some", "any", "please", "someone", "somebody",
        "anyone", "there's", "that", "this", "it", "its",

        // The kind of thing asked for, which the screen already knows, not the thing itself:
        // "providers near me" is every provider near me, not providers named "providers".
        "provider", "providers", "service", "services", "business", "businesses",
        "job", "jobs", "vacancy", "vacancies", "listing", "listings"
    };

    /// <summary>
    /// The meaningful words of <paramref name="text"/>, lower-cased, without accents folded
    /// away (an "é" stays an "é"), in order, without repeats. Empty when nothing is left, which a
    /// caller reads as "no text filter", never as "match nothing".
    /// </summary>
    public static IReadOnlyList<string> From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var words = new List<string>();
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length >= 2)
            {
                var word = current.ToString();

                if (!Filler.Contains(word) && !words.Contains(word))
                {
                    words.Add(word);
                }
            }

            current.Clear();
        }

        foreach (var character in text.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLower(character, CultureInfo.InvariantCulture));
            }
            else if (character is not '\'' and not '’')
            {
                Flush();
            }
        }

        Flush();

        return words.Count > MaxWords ? words.Take(MaxWords).ToList() : words;
    }

    /// <summary>
    /// Whether a word of <paramref name="phrase"/> starts with one of <paramref name="words"/>:
    /// "braid" finds "box braids" and "Hair Braiding", "car" does not find "childcare".
    /// </summary>
    public static bool StartsAWordOf(string? phrase, IReadOnlyCollection<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        if (string.IsNullOrWhiteSpace(phrase) || words.Count == 0)
        {
            return false;
        }

        var phraseWords = From(phrase);

        return phraseWords.Any(candidate => words.Any(word => candidate.StartsWith(word, StringComparison.Ordinal)));
    }

    /// <summary>
    /// One condition true when any of <paramref name="terms"/> is, as a single expression a query
    /// provider can translate (an <c>OR</c> of each, over one shared parameter).
    /// </summary>
    public static Expression<Func<T, bool>>? AnyOf<T>(IEnumerable<Expression<Func<T, bool>>> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var parameter = Expression.Parameter(typeof(T), "item");
        Expression? body = null;

        foreach (var term in terms)
        {
            var rebound = new Rebind(term.Parameters[0], parameter).Visit(term.Body);
            body = body is null ? rebound : Expression.OrElse(body, rebound);
        }

        return body is null ? null : Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
