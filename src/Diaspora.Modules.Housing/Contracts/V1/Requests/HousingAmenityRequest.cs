namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>One amenity a property claims.</summary>
public sealed record HousingAmenityRequest
{
    /// <summary>The catalogue key. Normalised on the way in.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// The qualification that stops the tick being a lie. A generator that runs four hours a night
    /// and a whole-house inverter are the same tick and a very different home.
    /// </summary>
    public string? Note { get; init; }
}
