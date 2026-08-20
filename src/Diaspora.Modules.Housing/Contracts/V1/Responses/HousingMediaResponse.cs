namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>An attached asset, by reference. Never a URL and never bytes.</summary>
public sealed record HousingMediaResponse
{
    /// <summary>B5's asset identifier. The client resolves it through Media.</summary>
    public required Guid MediaId { get; init; }

    public required string Role { get; init; }

    public required int Order { get; init; }

    public string? Caption { get; init; }
}
