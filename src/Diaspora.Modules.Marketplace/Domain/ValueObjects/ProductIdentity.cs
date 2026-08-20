using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// What the thing is, as a manufacturer would say it.
/// <para>
/// <b>All four fields are optional, and that is the point.</b> A sofa has no model number and a
/// bag of baby clothes has no brand; forcing either would make sellers type something false into
/// a field a buyer then filters on. Where they are present they are the strongest filter the
/// module has — "Samsung", "Galaxy S21" narrows a category far better than any free-text search
/// over a description.
/// </para>
/// <para>
/// <b><see cref="Sku"/> is the seller's own reference, never the platform's.</b> A shop pastes
/// its stock code so it can reconcile; nothing here parses it, indexes it for buyers or assumes
/// it is unique. A platform-assigned product identity would be a catalogue, which is a different
/// module and a much larger promise.
/// </para>
/// </summary>
public sealed class ProductIdentity : ValueObject
{
    private ProductIdentity(string? brand, string? model, string? sku, int? year)
    {
        Brand = brand;
        Model = model;
        Sku = sku;
        Year = year;
    }

    private ProductIdentity() { }

    public string? Brand { get; private set; }

    public string? Model { get; private set; }

    /// <summary>The seller's own stock reference. Never interpreted.</summary>
    public string? Sku { get; private set; }

    /// <summary>Year of manufacture. The filter a vehicle listing lives or dies on.</summary>
    public int? Year { get; private set; }

    /// <summary>Nothing stated. What most peer-to-peer listings carry.</summary>
    public static ProductIdentity Unspecified => new(null, null, null, null);

    /// <summary>Whether anything at all was stated.</summary>
    public bool IsSpecified =>
        Brand is not null || Model is not null || Sku is not null || Year is not null;

    public static Result<ProductIdentity> Create(
        string? brand, string? model, string? sku, int? year)
    {
        var cleanBrand = Clamp(brand);
        var cleanModel = Clamp(model);
        var cleanSku = Clamp(sku);

        if ((cleanBrand is null && !string.IsNullOrWhiteSpace(brand))
            || (cleanModel is null && !string.IsNullOrWhiteSpace(model))
            || (cleanSku is null && !string.IsNullOrWhiteSpace(sku)))
        {
            return Result.Failure<ProductIdentity>(MarketplaceErrors.ProductIdentityInvalid);
        }

        // A wide window rather than a clock-bound one: vintage furniture and classic cars are
        // ordinary listings on this platform, and a model year one ahead of the calendar is how
        // vehicles are sold everywhere.
        if (year is { } value && value is < 1800 or > 2200)
        {
            return Result.Failure<ProductIdentity>(MarketplaceErrors.ProductIdentityInvalid);
        }

        return Result.Success(new ProductIdentity(cleanBrand, cleanModel, cleanSku, year));
    }

    private static string? Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > MarketplaceLimits.MaxDisplayName ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Brand;
        yield return Model;
        yield return Sku;
        yield return Year;
    }
}
