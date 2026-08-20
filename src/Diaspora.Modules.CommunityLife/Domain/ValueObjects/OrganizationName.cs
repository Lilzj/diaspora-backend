using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// What an organization is called, and what it is called informally.
/// <para>
/// <b>The short name is not decoration.</b> "Nigerian Students Association, University of
/// Manchester" does not fit on a chip, and every card, every calendar row and every membership
/// list needs something that does. Without a place to put it, editors put it in the name and the
/// directory becomes unsearchable.
/// </para>
/// </summary>
public sealed class OrganizationName : ValueObject
{
    private OrganizationName(string value, string? shortName)
    {
        Value = value;
        ShortName = shortName;
    }

    private OrganizationName() { }

    public string Value { get; private set; } = string.Empty;

    /// <summary>An abbreviation or acronym. What a chip renders when the full name will not fit.</summary>
    public string? ShortName { get; private set; }

    /// <summary>What a card should render: the short name if there is one, the full name otherwise.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(ShortName) ? Value : ShortName;

    public static Result<OrganizationName> Create(string? value, string? shortName = null)
    {
        var normalized = Normalize(value);

        if (normalized is null)
        {
            return Result.Failure<OrganizationName>(CommunityLifeErrors.OrganizationNameInvalid);
        }

        var trimmedShort = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();

        if (trimmedShort is { Length: > CommunityLifeLimits.MaxDisplayName })
        {
            return Result.Failure<OrganizationName>(CommunityLifeErrors.OrganizationNameInvalid);
        }

        return Result.Success(new OrganizationName(normalized, trimmedShort));
    }

    /// <summary>The comparable form. Exposed so the duplicate check compares like with like.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = string.Join(
            ' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return collapsed.Length is >= CommunityLifeLimits.MinTitle
            and <= CommunityLifeLimits.MaxTitle
            ? collapsed
            : null;
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return ShortName;
    }
}
