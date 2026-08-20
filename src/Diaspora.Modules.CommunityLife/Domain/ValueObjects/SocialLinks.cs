using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// An organization's presence elsewhere.
/// <para>
/// <b>Named columns rather than a bag of key-value pairs</b>, and the reason is the client: it
/// renders a fixed row of icons, and a schema that could hold an arbitrary network would produce
/// links the client cannot draw. When a network matters enough to render, it gets a column here —
/// an additive change — and until then it belongs in the description.
/// </para>
/// <para>
/// Every value is validated as an absolute http or https URL. Scheme-checked rather than merely
/// parsed, because <c>javascript:</c> parses perfectly well as a URI and this is text a client puts
/// straight into an anchor.
/// </para>
/// </summary>
public sealed class SocialLinks : ValueObject
{
    private SocialLinks(
        string? facebook,
        string? instagram,
        string? twitter,
        string? youTube,
        string? linkedIn,
        string? tikTok)
    {
        Facebook = facebook;
        Instagram = instagram;
        Twitter = twitter;
        YouTube = youTube;
        LinkedIn = linkedIn;
        TikTok = tikTok;
    }

    private SocialLinks() { }

    public static SocialLinks None => new(null, null, null, null, null, null);

    public string? Facebook { get; private set; }

    public string? Instagram { get; private set; }

    public string? Twitter { get; private set; }

    public string? YouTube { get; private set; }

    public string? LinkedIn { get; private set; }

    public string? TikTok { get; private set; }

    public bool IsEmpty =>
        Facebook is null
        && Instagram is null
        && Twitter is null
        && YouTube is null
        && LinkedIn is null
        && TikTok is null;

    public static Result<SocialLinks> Create(
        string? facebook = null,
        string? instagram = null,
        string? twitter = null,
        string? youTube = null,
        string? linkedIn = null,
        string? tikTok = null)
    {
        string?[] candidates = [facebook, instagram, twitter, youTube, linkedIn, tikTok];
        var cleaned = new string?[candidates.Length];

        for (var index = 0; index < candidates.Length; index++)
        {
            var normalized = Normalize(candidates[index]);

            if (normalized.IsFailure)
            {
                return Result.Failure<SocialLinks>(normalized.Error);
            }

            cleaned[index] = normalized.Value;
        }

        return Result.Success(new SocialLinks(
            cleaned[0], cleaned[1], cleaned[2], cleaned[3], cleaned[4], cleaned[5]));
    }

    private static Result<string?> Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success<string?>(null);
        }

        var trimmed = value.Trim();

        if (trimmed.Length > CommunityLifeLimits.MaxUrl
            || !ContactDetails.IsAbsoluteHttpUrl(trimmed))
        {
            return Result.Failure<string?>(CommunityLifeErrors.SocialLinkInvalid);
        }

        return Result.Success<string?>(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Facebook;
        yield return Instagram;
        yield return Twitter;
        yield return YouTube;
        yield return LinkedIn;
        yield return TikTok;
    }
}
