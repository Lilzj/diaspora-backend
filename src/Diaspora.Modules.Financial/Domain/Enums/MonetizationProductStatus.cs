namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Whether a catalogue entry can be bought.</summary>
public enum MonetizationProductStatus
{
    Draft = 1,
    Active = 2,

    /// <summary>Withdrawn from sale. Existing purchases are unaffected and still honoured.</summary>
    Retired = 3
}
