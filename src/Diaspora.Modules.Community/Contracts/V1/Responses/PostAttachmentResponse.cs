namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// One attachment.
/// <para>
/// Carries <c>mediaId</c> and <b>no URL</b>. That absence is the contract, exactly as it is on
/// Media's own reference response: a client resolves bytes through Media, and no business module
/// is able to hand out a storage address.
/// </para>
/// </summary>
public sealed record PostAttachmentResponse
{
    public required Guid AttachmentId { get; init; }

    public required string Kind { get; init; }

    public Guid? MediaId { get; init; }

    public required string Role { get; init; }

    public required int Position { get; init; }

    public string? Caption { get; init; }

    public string? LinkUrl { get; init; }

    public string? LinkTitle { get; init; }

    public string? LinkDescription { get; init; }

    public string? LinkSiteName { get; init; }

    public Guid? LinkImageMediaId { get; init; }
}
