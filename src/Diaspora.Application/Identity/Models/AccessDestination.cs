namespace Diaspora.Application.Identity.Models;

/// <summary>
/// The surface a member should land on after authenticating. The Access Gateway maps
/// every access state onto exactly one of these.
/// </summary>
public enum AccessDestination
{
    /// <summary>Finish registration — the account is incomplete.</summary>
    RegistrationCompletion = 0,

    /// <summary>Enter the code sent by SMS.</summary>
    PhoneVerification = 1,

    /// <summary>Explore Diaspora with the Verification Center available.</summary>
    ExploreLimited = 2,

    /// <summary>Verification submitted; show progress.</summary>
    VerificationDashboard = 3,

    /// <summary>Under review by the Trust Engine.</summary>
    VerificationUnderReview = 4,

    /// <summary>Full application.</summary>
    FullApplication = 5,

    /// <summary>Suspension notice and appeal route.</summary>
    SuspensionScreen = 6,

    /// <summary>Hard lock; account recovery required.</summary>
    AccountRecovery = 7,

    /// <summary>Account closed.</summary>
    AccountDisabled = 8
}
