namespace Diaspora.Persistence.Transactions.Configurations;

/// <summary>
/// Table and schema names for the transaction subsystem.
/// <para>
/// The same <c>platform</c> schema the outbox uses. Both are platform infrastructure serving every
/// module, and neither belongs in a module's schema.
/// </para>
/// </summary>
public static class TransactionSchema
{
    public const string Name = "platform";

    public const string Receipts = "transaction_receipts";
}
