namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// How a deployment decides which sources it will publish official information from.
/// <para>
/// <b>The brief asks for source validation and authority validation, and this is where a deployment's
/// answer lives rather than this module's.</b> What counts as an acceptable source is a jurisdictional
/// question with no universal answer: one deployment publishes only from <c>.gov</c> hosts, another
/// serves a country whose immigration department posts on a commercial platform, and a third accepts
/// a printed circular with a gazette number and no URL at all. A rule hard-coded here would be wrong
/// for two of the three.
/// </para>
/// <para>
/// <b>Registered as a collection and evaluated as a veto.</b> Every registered validator sees every
/// source; any single rejection refuses it. That is deliberately not a vote — a source policy is a
/// list of things that must not happen, and a deployment that has forbidden something means it
/// whatever else agrees.
/// </para>
/// <para>
/// <b>A validator that throws is treated as abstaining, and logged.</b> An editorial team must not be
/// unable to publish a cyclone warning because somebody's allow-list service is down; failing open is
/// the right trade here precisely because the authority verification requirement is the real gate and
/// this is the additional one.
/// </para>
/// </summary>
public interface IIntelligenceSourceValidator
{
    /// <summary>The module contributing. Appears in diagnostics and in the rejection reason.</summary>
    string Module { get; }

    /// <summary>
    /// Lower runs first. Order affects only which rejection reason an editor is shown first, since
    /// any rejection is decisive.
    /// </summary>
    int Order => 100;

    Task<SourceValidationVerdict> ValidateAsync(
        SourceValidationRequest request, CancellationToken cancellationToken = default);
}
