namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// How a candidate wants to work. The candidate-side counterpart of
/// <see cref="WorkplaceType"/>.
/// <para>
/// <see cref="Flexible"/> is the default rather than an omission, because a blank preference
/// and "I do not mind" are different answers and only one of them should widen a candidate's
/// results.
/// </para>
/// </summary>
public enum RemotePreference
{
    /// <summary>No preference. Matches every workplace type.</summary>
    Flexible = 0,

    /// <summary>Wants to be on site.</summary>
    OnSiteOnly = 1,

    /// <summary>Wants a mix.</summary>
    HybridPreferred = 2,

    /// <summary>Wants fully remote work only.</summary>
    RemoteOnly = 3
}
