namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>
/// A new post.
/// <para>
/// Everything but the body is optional, because the client's composer collects a message and a
/// category and nothing else. A request that omits the audience gets <c>public</c>; one that
/// omits the expiry gets the category default the client already renders.
/// </para>
/// </summary>
public sealed record CreatePostRequest
{
    public required string Body { get; init; }

    public string? Title { get; init; }

    /// <summary>A <c>PostCategory</c> name. The client's PascalCase spelling is accepted.</summary>
    public string? Category { get; init; }

    /// <summary><c>low</c>, <c>normal</c>, <c>urgent</c> or <c>emergency</c>.</summary>
    public string? Urgency { get; init; }

    /// <summary>A <c>VisibilityLevel</c> name. Defaults to <c>public</c>.</summary>
    public string? Visibility { get; init; }

    /// <summary>Required when the visibility is <c>country</c>.</summary>
    public string? AudienceCountryCode { get; init; }

    /// <summary>Required when the visibility is <c>neighborhood</c>.</summary>
    public Guid? AudienceAreaId { get; init; }

    /// <summary>Required when the visibility is <c>custom</c>.</summary>
    public Guid? VisibilityRuleId { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? CityName { get; init; }

    public string? AreaName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>
    /// How far around the pin the alert applies, in metres, from 25 to 50,000. Needs a latitude and
    /// a longitude. Leave it out for an alert at a point.
    /// </summary>
    public int? AffectedRadiusMetres { get; init; }

    /// <summary>Hours until expiry. Clamped; defaults to the category lifetime.</summary>
    public int? ExpiresInHours { get; init; }

    /// <summary>Post as an organization. Omitted means as yourself.</summary>
    public Guid? PublishAsOrganizationId { get; init; }

    public IReadOnlyList<AttachmentRequest>? Attachments { get; init; }

    public PollRequest? Poll { get; init; }

    /// <summary>False saves a draft. True is the default, because most posts are published.</summary>
    public bool Publish { get; init; } = true;
}
