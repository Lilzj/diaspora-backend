using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Identifiers;

/// <summary>An issued invoice. Not its number — the number is what a person reads.</summary>
public sealed record InvoiceId(Guid Value) : StronglyTypedId(Value)
{
    public static InvoiceId New() => new(Guid.NewGuid());

    public static InvoiceId From(Guid value) => new(value);
}
