using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Turns a wire content type and identifier into a reference, and checks somebody owns it.
/// <para>
/// The gate every social write passes through. Accepting a comment on a content type no module
/// has claimed produces a row that can never be rendered, moderated or notified about — so an
/// unclaimed type is refused rather than stored, and the refusal names the type.
/// </para>
/// <para>
/// Community's own two types are answered without consulting the registry, because Community is
/// the module that owns them and a self-lookup through a scoped registry on every comment is
/// waste.
/// </para>
/// </summary>
public sealed class ContentReferenceResolver
{
    private readonly CommunityContentSourceRegistry _sources;
    private readonly ICommunityPostRepository _posts;

    public ContentReferenceResolver(
        CommunityContentSourceRegistry sources, ICommunityPostRepository posts)
    {
        _sources = sources;
        _posts = posts;
    }

    /// <summary>Parses and validates. Fails when nothing owns the type.</summary>
    public Result<ContentReference> Resolve(string? contentType, Guid contentId)
    {
        var parsed = CommunityWireNames.ParseContentType(contentType);

        if (parsed.IsFailure)
        {
            return Result.Failure<ContentReference>(parsed.Error);
        }

        if (contentId == Guid.Empty)
        {
            return Result.Failure<ContentReference>(CommunityErrors.ContentNotFound);
        }

        return IsSupported(parsed.Value)
            ? Result.Success(ContentReference.Create(parsed.Value, contentId))
            : Result.Failure<ContentReference>(CommunityErrors.ContentTypeUnsupported);
    }

    public bool IsSupported(CommunityContentType type) =>
        type is CommunityContentType.Post or CommunityContentType.Comment
        || _sources.Find(type) is not null;

    public IReadOnlyList<CommunityContentType> SupportedTypes =>
        new[] { CommunityContentType.Post, CommunityContentType.Comment }
            .Concat(_sources.ClaimedTypes)
            .Distinct()
            .OrderBy(type => (int)type)
            .ToList();

    /// <summary>
    /// Describes a reference, using Community's own data for its own types and the owning
    /// module's content source for everything else.
    /// </summary>
    public async Task<ContentDescriptor?> DescribeAsync(
        ContentReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (reference.Type != CommunityContentType.Post)
        {
            return await _sources.DescribeAsync(reference, cancellationToken);
        }

        var post = await _posts.GetForReadAsync(
            CommunityPostId.From(reference.Id), cancellationToken);

        return post is null
            ? null
            : new ContentDescriptor
            {
                Reference = reference,
                AuthorId = post.AuthorId.Value,
                Title = post.Title ?? post.Body.Value,
                Exists = !post.IsDeleted,
                Visibility = post.Audience.Level,
                CountryCode = post.Location.CountryCode,
                AreaId = post.Location.AreaId,
                AcceptsComments = !post.CommentsClosed
            };
    }
}
