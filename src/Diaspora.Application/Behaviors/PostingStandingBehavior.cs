using System.Globalization;
using System.Reflection;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Platform.Services.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Marks a command that puts a member's content up in an area: creating or publishing a job, a
/// listing, a home, a guide, a post, an event (7 Nov 2026). A member under a strike's posting block in
/// that area is refused, with the date it ends.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PostsInAreaAttribute : Attribute
{
    public PostsInAreaAttribute(string area)
    {
        Area = area;
    }

    /// <summary>One of <see cref="ModerationAreas"/>.</summary>
    public string Area { get; }
}

/// <summary>
/// Refuses a member's post in an area where a strike blocks them (7 Nov 2026).
/// <para>
/// A first strike blocks the area where it happened for seven days, a second for thirty, and nothing
/// else. The check asks <see cref="IPostingStanding"/>, which Trust answers; with no implementation
/// registered (a host without Trust), nobody is blocked.
/// </para>
/// </summary>
public sealed class PostingStandingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUser _currentUser;
    private readonly IServiceProvider _services;

    public PostingStandingBehavior(ICurrentUser currentUser, IServiceProvider services)
    {
        _currentUser = currentUser;
        _services = services;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var area = typeof(TRequest).GetCustomAttribute<PostsInAreaAttribute>()?.Area;

        if (area is null || _currentUser.UserId is not { } userId)
        {
            return await next();
        }

        var standing = _services.GetService<IPostingStanding>();

        if (standing is null)
        {
            return await next();
        }

        var block = await standing.GetBlockAsync(userId.Value, area, cancellationToken);

        if (block is not null)
        {
            throw new Exceptions.ForbiddenException(
                $"You cannot post in {area} until "
                + block.Until.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
                + ", because of a strike for content that was removed. Your other areas are not affected.");
        }

        return await next();
    }
}
