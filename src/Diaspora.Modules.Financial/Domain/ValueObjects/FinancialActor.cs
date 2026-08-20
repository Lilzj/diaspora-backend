using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// Who caused a financial action.
/// <para>
/// <b>A type as well as an identifier, because "no user id" has three different meanings.</b> A
/// system sweep, an unauthenticated gateway callback and a staff member acting through an
/// impersonation path all produce different rows and must be told apart afterwards. A nullable
/// identifier alone would collapse them into one, which is exactly the ambiguity an audit trail
/// exists to remove.
/// </para>
/// </summary>
public sealed class FinancialActor : ValueObject
{
    private FinancialActor(FinancialActorType actorType, Guid? userId, string? label)
    {
        ActorType = actorType;
        UserId = userId;
        Label = label;
    }

    private FinancialActor() { }

    public FinancialActorType ActorType { get; private set; }

    /// <summary>Null for a system or gateway actor. Never invented.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>The sweep name, the gateway name, or the staff role. Free text, for humans.</summary>
    public string? Label { get; private set; }

    public static FinancialActor Member(Guid userId) =>
        new(FinancialActorType.Member, userId, null);

    public static FinancialActor Staff(Guid userId, string? role = null) =>
        new(FinancialActorType.Staff, userId, Trim(role));

    public static FinancialActor System(string component) =>
        new(FinancialActorType.System, null, Trim(component));

    public static FinancialActor Gateway(string provider) =>
        new(FinancialActorType.Gateway, null, Trim(provider));

    /// <summary>
    /// A fresh instance with the same values.
    /// <para>
    /// <b>Not tidiness — this is the platform's owned-type sharing trap.</b> One posting writes a
    /// transaction and several ledger entries, and every one of them owns an audit stamp with an
    /// actor inside it. Handing the same instance to all of them makes EF track it against whichever
    /// owner it saw first and write nulls into the others' NOT NULL columns. The handbook records
    /// this as a cached static; a value threaded through a request has exactly the same shape.
    /// </para>
    /// </summary>
    public FinancialActor Copy() => new(ActorType, UserId, Label);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ActorType;
        yield return UserId;
        yield return Label;
    }
}
