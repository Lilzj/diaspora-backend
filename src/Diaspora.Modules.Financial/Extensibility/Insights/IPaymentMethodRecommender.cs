using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Insights;

/// <summary>
/// Suggests how a particular member is most likely to pay successfully.
/// <para>
/// Declared with no implementation. In several of the markets this platform serves the honest answer
/// is mobile money rather than a card, and ordering the payment sheet by what actually works for
/// somebody in their country is worth more than any model.
/// </para>
/// </summary>
public interface IPaymentMethodRecommender
{
    Task<IReadOnlyList<PaymentMethodKind>> RecommendAsync(
        Guid userId, string currency, string? countryCode, CancellationToken cancellationToken = default);
}
