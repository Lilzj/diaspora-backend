using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// How a claim that a body is official gets checked.
/// <para>
/// <b>A seam rather than a direct call to Trust, and the reason is that this is the one place a
/// deployment plausibly has a better answer than a review queue.</b> A government content programme
/// confirming its own ministry in a second, or a deployment with an accredited-missions list from its
/// foreign office, beats a human reviewer reading a website — and both are exactly the sort of thing
/// that exists in one deployment and nowhere else. The shipped Trust-backed provider registers last
/// so either can get there first.
/// </para>
/// <para>
/// <b>This is the gate the word "official" rests on.</b> A notice cannot be published unless its
/// authority is verified, so whatever answers here decides what the platform is willing to present as
/// government information. That is why the outcome cannot be a grant: however a claim is raised, the
/// decision arrives as a published Trust event and lands through one projection.
/// </para>
/// </summary>
public interface IAuthorityVerificationProvider
{
    /// <summary>The module contributing. Appears in diagnostics.</summary>
    string Module { get; }

    /// <summary>Lower runs first. The shipped Trust provider sits at 1000, deliberately last.</summary>
    int Order => 100;

    /// <summary>Whether this provider handles claims for the country given.</summary>
    bool Handles(string countryCode);

    Task<Result<AuthorityVerificationOutcome>> SubmitAsync(
        AuthorityVerificationRequest request, CancellationToken cancellationToken = default);
}
