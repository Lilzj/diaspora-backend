namespace Diaspora.Platform.Transactions;

/// <summary>
/// A row written inside a platform transaction so that, if the commit acknowledgement is lost, the
/// platform can ask the database whether the commit actually happened.
/// <para>
/// <b>This is the mechanism that makes commit success deterministically verifiable.</b> When a
/// retrying execution strategy sees a transient failure at commit time it cannot know whether the
/// transaction reached the server. EF Core's answer is a <c>verifySucceeded</c> delegate, and EF's
/// own connection-resiliency guidance lists "add state to the database" as the way to write one:
/// insert a row with a known identifier inside the transaction, then look for it afterwards. If it
/// is there the commit landed; if it is not, it did not. There is no third answer.
/// </para>
/// <para>
/// <b>The column types are deliberately primitive, and that is not carelessness.</b> The identifier
/// is a string and the timestamp is a tick count, because this row is written by a raw command that
/// bypasses the change tracker — see <c>DiasporaDbContext</c> for why it must — and a raw command
/// bypasses value converters with it. A <c>Guid</c> column is TEXT on SQLite and <c>uuid</c> on
/// PostgreSQL; a <c>DateTimeOffset</c> is converted to binary on SQLite and native on PostgreSQL.
/// Either would round-trip differently depending on whether EF or ADO.NET wrote it, and the one
/// component that must never be ambiguous about what it stored is the one that answers "did this
/// commit".
/// </para>
/// <para>
/// Receipts are worthless seconds after they are written — verification happens within one
/// execution-strategy retry window — and are purged on a short retention cycle.
/// </para>
/// </summary>
public sealed class TransactionReceipt
{
    /// <summary>Required by EF Core's materializer.</summary>
    private TransactionReceipt()
    {
        Id = string.Empty;
    }

    /// <summary>
    /// The identifier the verifying query looks for. Generated per transactional execution and
    /// reused across every retry of it, so the question stays "did <em>this</em> transaction
    /// commit" rather than "did any of them".
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    /// When the receipt was written, in UTC ticks. A tick count rather than a date because the
    /// retention sweep compares it in SQL and a raw insert cannot rely on a value converter.
    /// </summary>
    public long CreatedAtTicks { get; private set; }

    /// <summary>The write time, for a human reading the table. Not mapped.</summary>
    public DateTimeOffset CreatedAt => new(CreatedAtTicks, TimeSpan.Zero);

    /// <summary>
    /// A fresh receipt identifier. Format "D" — 36 lowercase characters, stable across providers
    /// and across the raw insert and the EF read that has to match it.
    /// </summary>
    public static string NewId() => Guid.NewGuid().ToString("D");
}
