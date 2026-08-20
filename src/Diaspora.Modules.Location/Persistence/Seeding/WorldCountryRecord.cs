namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>One row of the shipped country table.</summary>
/// <param name="Code">ISO 3166-1 alpha-2.</param>
/// <param name="Name">English display name, matching the frontend's bundled list.</param>
/// <param name="CallingCode">E.164, with the leading plus.</param>
public sealed record WorldCountryRecord(string Code, string Name, string CallingCode);
