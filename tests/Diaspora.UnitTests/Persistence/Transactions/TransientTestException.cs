namespace Diaspora.UnitTests.Persistence.Transactions;

/// <summary>
/// The failure the test execution strategy treats as transient.
/// <para>
/// A dedicated type rather than a real provider exception, because the point of these tests is the
/// platform's behaviour under <em>any</em> retrying strategy, not Npgsql's classification of any
/// particular SQLSTATE. Using a marker keeps the injection deterministic and keeps the suite off
/// Docker.
/// </para>
/// </summary>
public sealed class TransientTestException : Exception
{
    public TransientTestException() : base("Injected transient failure.") { }

    public TransientTestException(string message) : base(message) { }

    public TransientTestException(string message, Exception innerException)
        : base(message, innerException) { }
}
