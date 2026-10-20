namespace Diaspora.Email;

/// <summary>
/// The web address an email button uses to open a screen in the app.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>{AppBaseUrl}{route}</c>.</b> That is what the button used to be, and the API host
/// serves no page at <c>/profile/trust-center</c> or any other in-app route, so a member who tapped
/// it on a phone got a 404 in the browser. Every in-app route is now wrapped as
/// <c>{AppBaseUrl}/open/{route}</c>: the Android app claims that prefix as an App Link and opens the
/// screen directly, and anywhere it does not (no app, a desktop mail client, an unverified link)
/// the API answers with a small page that hands off to <c>diaspora://{route}</c> and offers a button.
/// </para>
/// <para>
/// The page refuses any route that is not on the client's route table, so this cannot be bent into
/// an open redirect. See <c>AppOpenPages</c> in the API.
/// </para>
/// </remarks>
public static class AppOpenLink
{
    /// <summary>The path prefix the landing page is served under, and the App Link claims.</summary>
    public const string PathPrefix = "/open";

    /// <summary>
    /// <c>{baseUrl}/open/{route without its leading slash}</c>, or null when either half is missing.
    /// </summary>
    /// <example><c>("https://diaspora-api.fly.dev", "/profile/trust-center")</c> →
    /// <c>https://diaspora-api.fly.dev/open/profile/trust-center</c>.</example>
    public static string? Build(string? baseUrl, string? route)
    {
        if (string.IsNullOrWhiteSpace(route) || string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        var path = route.Trim().TrimStart('/');

        if (path.Length == 0)
        {
            return null;
        }

        return $"{baseUrl.Trim().TrimEnd('/')}{PathPrefix}/{path}";
    }
}
