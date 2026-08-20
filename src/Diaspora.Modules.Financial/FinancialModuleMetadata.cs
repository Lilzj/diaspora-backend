namespace Diaspora.Modules.Financial;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class FinancialModuleMetadata
{
    public const string Name = "Financial";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "financial";

    /// <summary>
    /// The one place the module's public URL space is declared, asserted by an architecture test.
    /// <para>
    /// <c>/financial</c> rather than <c>/payments</c>, <c>/billing</c> or <c>/wallet</c>. Payment is
    /// one capability of this platform among fourteen; naming the URL space after it would leave
    /// wallets, ledgers, subscriptions, donations, contributions and the monetization catalogue
    /// homeless. <c>/billing</c> means "what you owe", which is a subscription concern and not the
    /// other thirteen. B14 made the same call declining <c>/feed</c>.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/financial";
}
