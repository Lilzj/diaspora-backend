namespace Diaspora.Domain.Abstractions;

public interface IHasOwner
{
    Guid OwnerId { get; }
}
