namespace Diaspora.Persistence.Outbox.Configurations;

/// <summary>
/// Table and schema names for the platform outbox.
/// <para>
/// Its own schema, beside the modules' rather than inside any of them. The outbox belongs to
/// the platform and holds every module's events; putting it in <c>identity</c> because identity
/// happened to publish first is exactly the kind of accident the schema-ownership check exists
/// to prevent.
/// </para>
/// </summary>
public static class OutboxSchema
{
    public const string Name = "platform";

    public const string Messages = "outbox_messages";

    public const string DeadLetters = "outbox_dead_letters";

    public const string DeliveryAttempts = "outbox_delivery_attempts";
}
