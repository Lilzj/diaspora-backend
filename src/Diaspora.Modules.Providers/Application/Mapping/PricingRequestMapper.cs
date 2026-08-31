using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>
/// The inbound half of <see cref="PricingMapper"/> — a wire price becoming a domain one.
/// <para>
/// Static, unlike its outbound twin, because nothing here needs the formatter registry: reading
/// a price is arithmetic and parsing, and only rendering one needs a locale.
/// </para>
/// <para>
/// <b>Nothing is validated here.</b> Every rule — a <c>Quote</c> that carries a maximum, a range
/// whose ceiling is below its floor, a negative amount, an amount with no currency — lives in
/// <see cref="ProviderPricing.Create"/>, and this defers to it. A second copy of those checks in
/// the mapping layer is a second copy to drift, and the one that drifts is always the one the
/// domain does not enforce.
/// </para>
/// </summary>
public static class PricingRequestMapper
{
    /// <summary>
    /// Maps a supplied price, or <see cref="ProviderPricing.Unspecified"/> when the caller sent
    /// none.
    /// <para>
    /// Absent and unspecified are the same thing for a price, which is why this collapses them —
    /// unlike opening hours, where "closed" and "not stated" are genuinely different claims and
    /// <see cref="OperatingHoursRequest.IsClosed"/> keeps them apart.
    /// </para>
    /// </summary>
    public static Result<ProviderPricing> FromRequest(PricingRequest? request)
    {
        if (request is null)
        {
            return Result.Success(ProviderPricing.Unspecified);
        }

        // An omitted model is `Unspecified` rather than an error: a service whose price is "ask
        // us" is the common case for the small providers this platform exists to bring online,
        // and demanding they classify it would be a barrier for no gain.
        var model = ProviderWireNames.ParsePricingModel(
            string.IsNullOrWhiteSpace(request.Model) ? nameof(PricingModel.Unspecified) : request.Model);

        if (model.IsFailure)
        {
            return Result.Failure<ProviderPricing>(model.Error);
        }

        return ProviderPricing.Create(
            model.Value,
            request.MinAmount,
            request.MaxAmount,
            request.Currency,
            request.Unit,
            request.IsNegotiable);
    }
}
