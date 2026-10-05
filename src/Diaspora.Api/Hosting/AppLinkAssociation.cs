namespace Diaspora.Api.Hosting;

/// <summary>
/// <c>AppLinks:Android</c>: which app may open this host's links directly.
/// <para>
/// <b>Only release signing keys belong here, never the debug key.</b> The debug keystore in a fresh
/// React Native project is shared by every such project. Listing its fingerprint would let anyone
/// build a debug app named <c>com.diaspora.app</c>, have Android verify it, and receive the links
/// this host serves: email verification tokens and payment returns. A developer's emulator is
/// approved locally with <c>adb shell pm set-app-links-user-selection</c> instead.
/// </para>
/// </summary>
public sealed class AppLinkOptions
{
    public const string SectionName = "AppLinks:Android";

    public string PackageName { get; set; } = string.Empty;

    /// <summary>SHA-256 certificate fingerprints, colon-separated upper-case hex.</summary>
    public List<string> Sha256Fingerprints { get; set; } = [];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PackageName) && Sha256Fingerprints.Count > 0;
}

/// <summary>
/// Serves <c>/.well-known/assetlinks.json</c>, the Digital Asset Links statement Android reads to
/// verify that this host lets the Diaspora app open its links (App Links, <c>autoVerify</c>).
/// <para>
/// On the API's host because that is where the links point: the Stripe Checkout return pages and
/// the email confirmation page are both served here, and this is the one domain the deployment
/// already proves it owns. A 404 when unconfigured, so an unconfigured deployment vouches for no app.
/// </para>
/// </summary>
internal static class AppLinkAssociation
{
    public const string Path = "/.well-known/assetlinks.json";

    public static void Map(WebApplication app)
    {
        var options = app.Configuration.GetSection(AppLinkOptions.SectionName).Get<AppLinkOptions>() ?? new AppLinkOptions();

        app.MapGet(Path, () =>
            {
                if (!options.IsConfigured) return Results.NotFound();

                object[] statement =
                [
                    new
                    {
                        relation = new[] { "delegate_permission/common.handle_all_urls" },
                        target = new
                        {
                            @namespace = "android_app",
                            package_name = options.PackageName,
                            sha256_cert_fingerprints = options.Sha256Fingerprints
                        }
                    }
                ];

                // Exactly application/json: Android's verifier refuses anything else, including a
                // charset suffix on some versions.
                return Results.Json(statement, contentType: "application/json");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}
