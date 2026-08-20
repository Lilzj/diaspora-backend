using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Services;

/// <summary>
/// A time-bounded packaging of a service: a promotion, a package, a seasonal menu.
/// <para>
/// Separate from the service's own price because the two answer different questions and
/// overwriting one with the other loses information nobody can recover. "Box braids are £60–£120,
/// and £45 until the end of August" needs both numbers: the first is what the service costs,
/// the second is what it costs today, and a model that only stores the current price cannot tell
/// a member they are getting a discount, cannot restore the real price when the promotion ends,
/// and cannot show a struck-through original.
/// </para>
/// <para>
/// <b>Both bounds are optional and mean different things.</b> No start means "already running";
/// no end means "until further notice". An offering with neither is a permanent alternative
/// price, which is a legitimate thing (a members' rate) and not a modelling accident.
/// </para>
/// </summary>
public sealed class ServiceOffering : Entity<ServiceOfferingId>
{
    public const int MaxNameLength = 200;

    public const int MaxDescriptionLength = 1_000;

    private ServiceOffering(
        ServiceOfferingId id,
        ProviderServiceId serviceId,
        string name,
        ProviderPricing pricing,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt)
        : base(id)
    {
        ServiceId = serviceId;
        Name = name;
        Pricing = pricing;
        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    private ServiceOffering() { }

    /// <summary>
    /// The owning service, held explicitly rather than as a shadow property.
    /// <para>
    /// EF would supply a shadow foreign key for an owned collection, and reading it back would
    /// mean a string-keyed lookup at every call site. An explicit property is one column either
    /// way and is visible to the compiler.
    /// </para>
    /// </summary>
    public ProviderServiceId ServiceId { get; private set; } = default!;

    /// <summary>"Summer special", "Family bundle", "First consultation".</summary>
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public ProviderPricing Pricing { get; private set; } = ProviderPricing.Unspecified;

    /// <summary>Null means it is already running.</summary>
    public DateTimeOffset? StartsAt { get; private set; }

    /// <summary>Null means until further notice.</summary>
    public DateTimeOffset? EndsAt { get; private set; }

    /// <summary>Withdrawn by the provider without deleting it, so it can be run again.</summary>
    public bool IsActive { get; private set; } = true;

    internal static Result<ServiceOffering> Create(
        ServiceOfferingId id,
        ProviderServiceId serviceId,
        string? name,
        ProviderPricing pricing,
        DateTimeOffset? startsAt,
        DateTimeOffset? endsAt,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(pricing);

        var trimmedName = name?.Trim();

        if (string.IsNullOrEmpty(trimmedName))
        {
            return Result.Failure<ServiceOffering>(ProviderErrors.ServiceNameEmpty);
        }

        if (trimmedName.Length > MaxNameLength)
        {
            return Result.Failure<ServiceOffering>(ProviderErrors.ServiceNameTooLong);
        }

        if (startsAt is { } start && endsAt is { } end && end <= start)
        {
            return Result.Failure<ServiceOffering>(ProviderErrors.OfferingWindowInvalid);
        }

        return Result.Success(new ServiceOffering(id, serviceId, trimmedName, pricing, startsAt, endsAt)
        {
            Description = Trim(description)
        });
    }

    public void Withdraw() => IsActive = false;

    public void Reinstate() => IsActive = true;

    /// <summary>Whether this offering's price is the one a member should be quoted right now.</summary>
    public bool IsActiveAt(DateTimeOffset instant) =>
        IsActive
        && (StartsAt is null || StartsAt <= instant)
        && (EndsAt is null || EndsAt > instant);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, MaxDescriptionLength)];
}
