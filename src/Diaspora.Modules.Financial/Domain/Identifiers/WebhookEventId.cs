using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>One callback from a payment provider, recorded exactly once.</summary>
public sealed record WebhookEventId(Guid Value) : StronglyTypedId(Value)
{
    public static WebhookEventId New() => new(Guid.NewGuid());

    public static WebhookEventId From(Guid value) => new(value);
}
