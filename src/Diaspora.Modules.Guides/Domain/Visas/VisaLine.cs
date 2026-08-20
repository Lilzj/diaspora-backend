using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Visas;

/// <summary>
/// One requirement or one required document on a visa advisory.
/// <para>
/// <b>A sentence, not a structure.</b> The temptation is to model a requirement as a typed
/// constraint — minimum balance, minimum passport validity, an enumerated document type — and every
/// attempt runs aground on the first advisory that says "sufficient funds, assessed at the discretion
/// of the officer". Official requirements are prose written by lawyers, and a structured model would
/// either refuse to hold them or hold a lossy summary of them beside a free-text field nobody trusts.
/// </para>
/// <para>
/// What is structured is the two things a client renders differently: which list the line belongs to,
/// and where it sits in that list. Everything else is transcribed and shown as written.
/// </para>
/// </summary>
public sealed class VisaLine
{
    private VisaLine(VisaLineKind kind, int position, string text)
    {
        Kind = kind;
        Position = position;
        Text = text;
    }

    private VisaLine() { }

    /// <summary>Which list this belongs to. Half of the row's identity.</summary>
    public VisaLineKind Kind { get; private set; }

    /// <summary>Position within its list, contiguous from zero. The other half of the identity.</summary>
    public int Position { get; private set; }

    public string Text { get; private set; } = string.Empty;

    internal static VisaLine? TryCreate(VisaLineKind kind, int position, string? text)
    {
        var trimmed = IntelligenceNormalization.Text(text, IntelligenceLimits.MaxVisaLine);

        return trimmed is null || !Enum.IsDefined(kind)
            ? null
            : new VisaLine(kind, position, trimmed);
    }
}
