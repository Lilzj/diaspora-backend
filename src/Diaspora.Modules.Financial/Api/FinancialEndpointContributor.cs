using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Maps B15's public URL space.
/// <para>
/// One group, one prefix, declared once in the module's metadata and asserted by an architecture
/// test — so a second financial surface cannot appear elsewhere in the URL space without somebody
/// noticing.
/// </para>
/// </summary>
public sealed class FinancialEndpointContributor : IEndpointContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(FinancialModuleMetadata.RoutePrefix)
            .WithTags("Financial");

        WalletEndpoints.Map(group);
        PaymentEndpoints.Map(group);
        CatalogEndpoints.Map(group);
        SubscriptionEndpoints.Map(group);
        GivingEndpoints.Map(group);
        FinancialOperationsEndpoints.Map(group);

        // B15.1. Money in, money out, money asked for back, points, and the provider's own voice.
        DepositEndpoints.Map(group);
        WithdrawalEndpoints.Map(group);
        RewardEndpoints.Map(group);
        EntitlementEndpoints.Map(group);
        WebhookEndpoints.Map(group);

        // B15.1 Giving Extension. The time series, recurring donations, and the read side of the
        // membership payment binding. The campaign routes live in another module's URL space — see
        // GivingExtensionEndpoints for why.
        GivingExtensionEndpoints.Map(group);

        // B16. `/campaigns` here is *advertising*; B15.1's fundraising campaigns are at
        // /api/v1/communities/campaigns. Two entities, one noun, two prefixes.
        AdCampaignEndpoints.Map(group);

        // Invoices for money the platform kept, and the register of saved payment methods —
        // empty, because nothing reusable is stored. See SavedPaymentMethods.cs.
        InvoiceEndpoints.Map(group);
    }
}
