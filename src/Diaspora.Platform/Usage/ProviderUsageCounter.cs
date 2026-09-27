namespace Diaspora.Platform.Usage;

/// <summary>
/// How many units of one vendor SKU this deployment has used in one billing month.
/// <para>
/// Primitive columns only, for the reason <c>TransactionReceipt</c> gives: the row is written by a
/// raw upsert that bypasses value converters, so every column has to mean the same thing whether
/// ADO.NET or EF wrote it.
/// </para>
/// </summary>
public sealed class ProviderUsageCounter
{
    /// <summary>Required by EF Core's materializer.</summary>
    private ProviderUsageCounter()
    {
        Id = string.Empty;
        Provider = string.Empty;
        Sku = string.Empty;
        Period = string.Empty;
    }

    /// <summary><c>{provider}:{sku}:{period}</c>. One column, so the upsert has one conflict target.</summary>
    public string Id { get; private set; }

    public string Provider { get; private set; }

    public string Sku { get; private set; }

    /// <summary>The billing month as <c>yyyy-MM</c>, in the vendor's billing time zone.</summary>
    public string Period { get; private set; }

    public int Used { get; private set; }

    /// <summary>The billing month a moment falls in, as the vendor reckons it.</summary>
    public static string PeriodOf(DateTimeOffset utcNow, string billingTimeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(billingTimeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, zone);
        return local.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string IdOf(string provider, string sku, string period) => $"{provider}:{sku}:{period}";
}
