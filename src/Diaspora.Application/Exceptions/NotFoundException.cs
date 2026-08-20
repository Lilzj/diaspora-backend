namespace Diaspora.Application.Exceptions;

public sealed class NotFoundException : Exception
{
    public string EntityType { get; }
    public string EntityId { get; }

    public NotFoundException(string entityType, string entityId)
        : base($"{entityType} with ID '{entityId}' was not found.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public static NotFoundException For<T>(object id) =>
        new(typeof(T).Name, id.ToString() ?? string.Empty);
}
