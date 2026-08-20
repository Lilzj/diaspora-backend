using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Pipelines;

/// <summary>
/// One column on an employer's hiring board.
/// <para>
/// <b>The name is the employer's, the kind is the platform's.</b> An employer calls a stage
/// "Ward sign-off" or "Founder chat"; the platform needs to know it is an interview so that
/// "how many candidates are at interview" is answerable without parsing anyone's stage names,
/// and so a candidate's own timeline can render something they understand. Both, because
/// forcing employers into fixed stages makes the board useless to them, and having only free
/// text makes every cross-employer figure impossible.
/// </para>
/// <para>
/// <see cref="Key"/> is the stable handle an application stores. It never changes — renaming a
/// stage changes <see cref="Name"/> and leaves every application still pointing at the right
/// column.
/// </para>
/// </summary>
public sealed class HiringStage
{
    private HiringStage(
        string key, string name, HiringStageKind kind, int order, bool isRemovable)
    {
        Key = key;
        Name = name;
        Kind = kind;
        Order = order;
        IsRemovable = isRemovable;
    }

    private HiringStage() { }

    /// <summary>The stable handle applications point at. Never changes once created.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>What the employer calls it. Freely renameable.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>What the platform understands it to be.</summary>
    public HiringStageKind Kind { get; private set; }

    public int Order { get; private set; }

    /// <summary>
    /// False for the entry and hired stages, which every pipeline must keep — an application
    /// has to enter somewhere and a successful one has to end somewhere.
    /// </summary>
    public bool IsRemovable { get; private set; }

    /// <summary>Normalises free text to a stage key, or null when nothing usable survives.</summary>
    public static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var key = builder.ToString().Trim('-');

        return key.Length == 0
            ? null
            : key.Length <= JobLimits.MaxStageKey ? key : key[..JobLimits.MaxStageKey].TrimEnd('-');
    }

    internal static HiringStage? Create(
        string? key, string? name, HiringStageKind kind, int order, bool isRemovable = true)
    {
        var normalized = NormalizeKey(key ?? name);

        if (normalized is null || !Enum.IsDefined(kind))
        {
            return null;
        }

        var display = string.IsNullOrWhiteSpace(name) ? normalized : name.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            display = display[..JobLimits.MaxDisplayName].TrimEnd();
        }

        return new HiringStage(normalized, display, kind, order, isRemovable);
    }

    internal void Rename(string name) => Name = name;

    internal void Reorder(int order) => Order = order;
}
