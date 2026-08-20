namespace Diaspora.IntegrationTests.Modules;

/// <summary>One delivery endpoint, as a client reads it. The token is never present.</summary>
internal sealed record DeviceShape
{
    public Guid Id { get; init; }

    public string Platform { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public bool HasToken { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// Whether this is the endpoint the request came from — B13.3. Derived from the presented token
    /// and never from anything the client sent, which is what makes it worth asserting.
    /// </summary>
    public bool IsCurrentDevice { get; init; }
}
