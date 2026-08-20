using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>Pins or unpins a post. A pinned post keeps whatever status it had.</summary>
[Auditable]
public sealed record PinPostCommand : ICommand
{
    public required Guid PostId { get; init; }

    public required bool Pinned { get; init; }
}
