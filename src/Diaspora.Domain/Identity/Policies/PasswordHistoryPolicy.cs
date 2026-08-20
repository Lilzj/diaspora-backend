using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Policies;

/// <summary>
/// How many retired password hashes to retain, and whether reuse is refused.
/// </summary>
public sealed class PasswordHistoryPolicy : ValueObject
{
    public static readonly PasswordHistoryPolicy Default = new(depth: 5, enforced: true);

    public static readonly PasswordHistoryPolicy Disabled = new(depth: 0, enforced: false);

    public int Depth { get; }
    public bool Enforced { get; }

    private PasswordHistoryPolicy(int depth, bool enforced)
    {
        Depth = depth;
        Enforced = enforced;
    }

    public static PasswordHistoryPolicy Create(int depth, bool enforced)
    {
        if (depth < 0)
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "History depth cannot be negative.");

        return new PasswordHistoryPolicy(depth, enforced);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Depth;
        yield return Enforced;
    }
}
