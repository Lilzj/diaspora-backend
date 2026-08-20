using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// Everything a provider says about itself in prose and pictures.
/// <para>
/// Split from <see cref="Provider"/> rather than flattened into it because the two change on
/// completely different schedules and for different reasons. A provider's status, category,
/// visibility and verification move on operational events; its tagline, mission, cover photo and
/// founding year are edited by a human in a form. Keeping them apart means the profile editor
/// writes to one owned reference and cannot accidentally touch the aggregate's operational
/// state, and it means the two have separate permissions — an <c>Editor</c> may change this and
/// may not change status.
/// </para>
/// <para>
/// <b>Media is held as identifiers, never as URLs</b> (ADR-005). <see cref="LogoMediaId"/> and
/// <see cref="CoverMediaId"/> are what the client's <c>logo</c> and <c>coverImageUrl</c> become,
/// and the client resolves them through Media's link endpoint. That is the whole reason a
/// storage-provider migration is invisible to this module.
/// </para>
/// </summary>
public sealed class ProviderProfile : Entity<ProviderProfileId>
{
    public const int MaxTaglineLength = 160;

    public const int MaxMissionLength = 2_000;

    private ProviderProfile(ProviderProfileId id, ProviderDescription description)
        : base(id) =>
        Description = description;

    private ProviderProfile() { }

    /// <summary>The prose block. Never null; <see cref="ProviderDescription.Empty"/> when unset.</summary>
    public ProviderDescription Description { get; private set; } = ProviderDescription.Empty;

    /// <summary>One line under the name. "Authentic Ghanaian cuisine, fresh daily."</summary>
    public string? Tagline { get; private set; }

    /// <summary>The organization's purpose. The client's <c>CommunityOrganization.mission</c>.</summary>
    public string? Mission { get; private set; }

    /// <summary>When the provider started. Renders as "Serving the community since 2014".</summary>
    public DateOnly? FoundedOn { get; private set; }

    /// <summary>The card and header image. The client's <c>coverImageUrl</c>, as a reference.</summary>
    public MediaId? CoverMediaId { get; private set; }

    /// <summary>The provider's mark. The client's <c>logo</c>, as a reference.</summary>
    public MediaId? LogoMediaId { get; private set; }

    /// <summary>
    /// How quickly the provider usually replies, in hours. Null until enough contacts have been
    /// observed to say anything honest — a made-up "replies within an hour" is worse than silence.
    /// </summary>
    public int? TypicalResponseHours { get; private set; }

    public static ProviderProfile Create(ProviderProfileId id, ProviderDescription? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new ProviderProfile(id, description ?? ProviderDescription.Empty);
    }

    public Result Update(
        ProviderDescription description,
        string? tagline,
        string? mission,
        DateOnly? foundedOn,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(description);

        var trimmedTagline = Trim(tagline);

        if (trimmedTagline is { Length: > MaxTaglineLength })
        {
            return Result.Failure(ProviderErrors.TaglineTooLong);
        }

        var trimmedMission = Trim(mission);

        if (trimmedMission is { Length: > MaxMissionLength })
        {
            return Result.Failure(ProviderErrors.MissionTooLong);
        }

        if (foundedOn is { } founded && founded > today)
        {
            return Result.Failure(ProviderErrors.FoundedInFuture);
        }

        Description = description;
        Tagline = trimmedTagline;
        Mission = trimmedMission;
        FoundedOn = foundedOn;

        return Result.Success();
    }

    public void SetCover(MediaId? mediaId) => CoverMediaId = mediaId;

    public void SetLogo(MediaId? mediaId) => LogoMediaId = mediaId;

    /// <summary>
    /// Records an observed response time as a rolling mean.
    /// <para>
    /// A mean rather than the latest value, because one fast reply on a Tuesday is not a
    /// response time, and a client rendering "replies within an hour" from a sample of one is
    /// making a promise the provider never made.
    /// </para>
    /// </summary>
    public void ObserveResponseHours(int hours, int observationCount)
    {
        if (hours < 0 || observationCount <= 0)
        {
            return;
        }

        TypicalResponseHours = TypicalResponseHours is { } current && observationCount > 1
            ? (int)Math.Round(((current * (observationCount - 1)) + hours) / (double)observationCount)
            : hours;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
