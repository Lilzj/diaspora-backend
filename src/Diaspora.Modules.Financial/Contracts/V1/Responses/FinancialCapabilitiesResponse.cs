namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// What this deployment's Financial Platform can actually do.
/// <para>
/// <b>Modelled on Media's capabilities endpoint, and for the same reason.</b> B5 reports
/// <c>malwareScanningEnabled: false</c> so nobody has to infer from behaviour whether a seam has an
/// adapter behind it. Here the questions are which gateways are registered, whether conversion is
/// possible, and whether payouts are — and the honest answers today are "the simulator", "no" and
/// "no".
/// </para>
/// </summary>
public sealed record FinancialCapabilitiesResponse
{
    /// <summary>The adapters registered in this deployment, by name.</summary>
    public required IReadOnlyList<string> Gateways { get; init; }

    public required IReadOnlyList<string> SupportedMethods { get; init; }

    public required IReadOnlyList<CurrencyResponse> Currencies { get; init; }

    /// <summary>False everywhere today. The platform never converts.</summary>
    public required bool CurrencyConversionEnabled { get; init; }

    /// <summary>False everywhere today. No settlement engine ships in this phase.</summary>
    public required bool PayoutsEnabled { get; init; }

    /// <summary>Whether a fraud assessor beyond the shipped velocity rules is registered.</summary>
    public required bool FraudAssessmentEnabled { get; init; }

    /// <summary>Whether financial records are being projected to a search index.</summary>
    public required bool SearchProjectionEnabled { get; init; }
}
