using Diaspora.Api.Hosting;
using Diaspora.Api.RateLimiting;
using Diaspora.Application.Identity.Commands.EmailChange;
using Diaspora.Application.Identity.Commands.EmailVerification;
using MediatR;

namespace Diaspora.Api.Identity;

/// <summary>
/// The page a verification email links to.
/// <para>
/// <b>Opening the link does not verify; pressing the button does.</b> Mail security scanners
/// (Outlook Safe Links, Gmail's proxy, corporate gateways) fetch every link in a message before a
/// person sees it. A GET that verified would be completed by a robot, and an address would count
/// as proven by an inbox nobody read. So the GET only shows a button, and the POST it submits is
/// what confirms.
/// </para>
/// <para>
/// Served by the API rather than the website for the reason the Stripe return pages are: it is
/// the one host the deployment already proves it owns, and the confirmation needs no session.
/// </para>
/// </summary>
internal static class EmailConfirmationPages
{
    public const string Path = "/email/confirm";

    /// <summary>The page an email change link opens. The same rule: the button changes, the link does not.</summary>
    public const string ChangePath = "/email/change";

    private const string AppLink = "diaspora://settings/integrations/email";

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet(Path, (string? token) => string.IsNullOrWhiteSpace(token)
                ? Page("This link is incomplete",
                    "Open the most recent verification email from Diaspora and use its button.",
                    form: null)
                : Page("Confirm your email address",
                    "Press the button to confirm this is your email address for Diaspora.",
                    form: token, formAction: Path, buttonLabel: "Confirm my email"))
            .AllowAnonymous()
            .ExcludeFromDescription()
            .RequireRateLimiting(RateLimitingRegistration.RecoveryPolicy);

        app.MapPost(Path, async (HttpRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var token = form["token"].ToString();

                var result = await sender.Send(
                    new ConfirmEmailVerificationCommand { Token = token }, cancellationToken);

                var outcome = result.IsSuccess ? result.Value.Outcome : "Invalid";

                return outcome switch
                {
                    "Verified" => Page("Email confirmed",
                        "Thank you. Your email address is now verified. You can return to Diaspora.", null, AppLink),
                    "AlreadyVerified" => Page("Already confirmed",
                        "This email address was already verified. Nothing else is needed.", null, AppLink),
                    "AddressChanged" => Page("This link is for an old address",
                        "The email address on your account has changed since this link was sent. Ask for a new link from Diaspora.",
                        null, AppLink),
                    _ => Page("This link has expired or is not valid",
                        "Verification links work for 12 hours. Ask for a new one from Diaspora.", null, AppLink)
                };
            })
            // The signed token is the credential. A forged cross-site post would have to carry a
            // valid token for the victim's own address, which only that mailbox has.
            .DisableAntiforgery()
            .AllowAnonymous()
            .ExcludeFromDescription()
            .RequireRateLimiting(RateLimitingRegistration.RecoveryPolicy);

        MapChange(app);
    }

    private static void MapChange(IEndpointRouteBuilder app)
    {
        app.MapGet(ChangePath, (string? token) => string.IsNullOrWhiteSpace(token)
                ? Page("This link is incomplete",
                    "Open the most recent email from Diaspora about changing your email address and use its button.",
                    form: null)
                : Page("Confirm your new email address",
                    "Press the button to make this the email address you sign in to Diaspora with. Other devices signed in to your account will be signed out.",
                    form: token, formAction: ChangePath, buttonLabel: "Confirm my new email"))
            .AllowAnonymous()
            .ExcludeFromDescription()
            .RequireRateLimiting(RateLimitingRegistration.RecoveryPolicy);

        app.MapPost(ChangePath, async (HttpRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var form = await request.ReadFormAsync(cancellationToken);
                var token = form["token"].ToString();

                var result = await sender.Send(new ConfirmEmailChangeCommand { Token = token }, cancellationToken);

                var outcome = result.IsSuccess ? result.Value.Outcome : EmailChangeOutcomes.Invalid;

                return outcome switch
                {
                    EmailChangeOutcomes.Changed => Page("Email address changed",
                        "This is now the email address you sign in to Diaspora with. Other devices were signed out.",
                        null, AppLink),
                    EmailChangeOutcomes.AlreadyChanged => Page("Already changed",
                        "This email address is already the one on your account. Nothing else is needed.", null, AppLink),
                    EmailChangeOutcomes.AddressTaken => Page("This address is already in use",
                        "Another Diaspora account signs in with this email address, so it cannot be moved to yours. Nothing was changed.",
                        null, AppLink),
                    _ => Page("This link has expired or is not valid",
                        "Email change links work for 2 hours, and stop working if the account's password or email address changes. Ask for a new one from Diaspora.",
                        null, AppLink)
                };
            })
            // The signed token is the credential, and it was only ever sent to the new mailbox. A
            // forged cross-site post would have to carry one.
            .DisableAntiforgery()
            .AllowAnonymous()
            .ExcludeFromDescription()
            .RequireRateLimiting(RateLimitingRegistration.RecoveryPolicy);
    }

    private static HostPage Page(
        string heading,
        string message,
        string? form,
        string? appLink = null,
        string formAction = Path,
        string buttonLabel = "Confirm my email") =>
        form is not null ? HostPage.Form(heading, message, formAction, form, buttonLabel)
        : appLink is not null ? HostPage.Link(heading, message, appLink, "Open Diaspora", handOff: false)
        : HostPage.Message(heading, message);
}
