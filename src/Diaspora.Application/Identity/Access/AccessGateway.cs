using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Access;

/// <summary>
/// The centralised Access Gateway.
/// <para>
/// After every successful authentication, exactly one component decides where the member
/// goes and what they may do: this one. Controllers return its verdict verbatim. That is
/// the whole point — the routing rules for nine access states, each with its own message
/// and required actions, are the kind of thing that rots the instant it is copied into a
/// second place.
/// </para>
/// <para>
/// Pure and synchronous. Everything it needs is already on the loaded aggregate, so
/// resolving a decision costs no I/O.
/// </para>
/// </summary>
public sealed class AccessGateway : IAccessGateway
{
    private readonly IFeatureAccessResolver _featureResolver;
    private readonly IPermissionResolver _permissionResolver;

    public AccessGateway(IFeatureAccessResolver featureResolver, IPermissionResolver permissionResolver)
    {
        _featureResolver = featureResolver;
        _permissionResolver = permissionResolver;
    }

    public AccessDecision Resolve(User user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var state = user.AccessState;
        var features = _featureResolver.Resolve(state, user.Role);
        var permissions = _permissionResolver.Resolve(user.Role, state);

        var (destination, message, actions) = RouteFor(user, state);

        // A suspended or closed member is told why, in the words staff used and the reason they
        // chose (7 Nov 2026). The screen said only "suspended pending review".
        var staffRestriction = state == AccessState.Suspended || state == AccessState.Disabled;
        var reasonCode = staffRestriction && ModerationReasons.TryParse(user.StatusReasonCode, out var code)
            ? code
            : (ModerationReasonCode?)null;

        return new AccessDecision
        {
            AccessState = state,
            AccountStatus = state.ToAccountStatus(),
            Destination = destination,
            Message = message,
            RequiredActions = actions,
            Features = features,
            Permissions = permissions,
            RestrictedUntil = state == AccessState.Suspended ? user.SuspendedUntil : null,
            ReasonCode = reasonCode?.ToString(),
            ReasonLabel = reasonCode is { } known ? ModerationReasons.Label(known) : null,
            Note = staffRestriction ? user.StatusReason : null
        };
    }

    private static (AccessDestination Destination, string Message, IReadOnlyList<string> Actions) RouteFor(
        User user,
        AccessState state)
    {
        if (state == AccessState.RegistrationStarted)
        {
            return (
                AccessDestination.RegistrationCompletion,
                "Your registration is not finished yet.",
                ["Request a verification code for your phone number."]);
        }

        if (state == AccessState.PhoneVerificationPending)
        {
            return (
                AccessDestination.PhoneVerification,
                "Enter the code we sent to your phone to activate your account.",
                ["Enter the 6-digit verification code."]);
        }

        if (state == AccessState.LimitedAccess)
        {
            return (
                AccessDestination.ExploreLimited,
                "Welcome to Diaspora. Explore freely — verify your account to start contributing.",
                ["Visit the Verification Center to request Diaspora verification."]);
        }

        if (state == AccessState.VerificationPending)
        {
            return (
                AccessDestination.VerificationDashboard,
                "Your verification request has been received and is queued for review.",
                ["Keep an eye on your verification dashboard for updates."]);
        }

        if (state == AccessState.VerificationUnderReview)
        {
            return (
                AccessDestination.VerificationUnderReview,
                "Your verification is being reviewed.",
                ["No action needed. We will notify you when the review is complete."]);
        }

        if (state == AccessState.Verified)
        {
            return (
                AccessDestination.FullApplication,
                "You are Diaspora verified. All features are available.",
                []);
        }

        if (state == AccessState.Suspended)
        {
            var message = user.SuspendedUntil is { } until
                ? $"Your account is suspended until {until:u}."
                : "Your account is suspended pending review.";

            return (
                AccessDestination.SuspensionScreen,
                message,
                ["Review the suspension notice.", "Submit an appeal if you believe this is a mistake."]);
        }

        if (state == AccessState.Locked)
        {
            return (
                AccessDestination.AccountRecovery,
                "Your account is locked for security reasons.",
                ["Complete account recovery to restore access."]);
        }

        if (state == AccessState.Disabled)
        {
            return (
                AccessDestination.AccountDisabled,
                "This account has been disabled.",
                ["Contact Diaspora support if you would like it reopened."]);
        }

        // Unreachable while every AccessState is handled above; an architecture test
        // asserts that, so falling through here means a state was added without routing.
        throw new InvalidOperationException(
            $"The Access Gateway has no route for access state '{state.Name}'.");
    }
}
