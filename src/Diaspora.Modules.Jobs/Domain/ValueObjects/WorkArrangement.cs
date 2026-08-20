using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// How and where the work is performed, and what the employer will do to get someone there.
/// <para>
/// <b>Visa sponsorship and relocation assistance live here rather than as loose booleans,</b>
/// because on a diaspora platform they are the same decision as the workplace type: all three
/// answer "can I, specifically, do this job from where I am?". Grouping them means a card can
/// render the honest answer in one line, and means the two filters candidates care about most
/// sit next to the column they qualify.
/// </para>
/// <para>
/// <b>Sponsorship is the employer's own statement and nothing more.</b> B9 never verifies it,
/// and the module contains no immigration logic of any kind — international tax and background
/// checks are explicitly out of scope. What the platform can do is let a candidate filter on the
/// claim and let Trust verify the employer making it.
/// </para>
/// </summary>
public sealed class WorkArrangement : ValueObject
{
    private WorkArrangement(
        WorkplaceType workplaceType,
        bool offersVisaSponsorship,
        bool offersRelocationAssistance,
        int? onSiteDaysPerWeek,
        string? remotePolicy)
    {
        WorkplaceType = workplaceType;
        OffersVisaSponsorship = offersVisaSponsorship;
        OffersRelocationAssistance = offersRelocationAssistance;
        OnSiteDaysPerWeek = onSiteDaysPerWeek;
        RemotePolicy = remotePolicy;
    }

    private WorkArrangement() { }

    public WorkplaceType WorkplaceType { get; private set; }

    /// <summary>The employer's claim that it will sponsor a work visa. Never verified here.</summary>
    public bool OffersVisaSponsorship { get; private set; }

    /// <summary>The employer's claim that it will help with relocation costs.</summary>
    public bool OffersRelocationAssistance { get; private set; }

    /// <summary>Days in the office. Meaningful only for a hybrid arrangement.</summary>
    public int? OnSiteDaysPerWeek { get; private set; }

    /// <summary>
    /// Free text qualifying a remote arrangement — "within the EU", "must overlap 09:00–13:00
    /// UTC". The single most useful sentence on a remote posting and the one most often absent.
    /// </summary>
    public string? RemotePolicy { get; private set; }

    /// <summary>What a posting starts as: on site, no sponsorship, no relocation.</summary>
    public static WorkArrangement OnSite =>
        new(WorkplaceType.OnSite, false, false, null, null);

    public static Result<WorkArrangement> Create(
        WorkplaceType workplaceType,
        bool offersVisaSponsorship = false,
        bool offersRelocationAssistance = false,
        int? onSiteDaysPerWeek = null,
        string? remotePolicy = null)
    {
        if (!Enum.IsDefined(workplaceType))
        {
            return Result.Failure<WorkArrangement>(JobErrors.WorkplaceInvalid);
        }

        if (onSiteDaysPerWeek is < 0 or > 7)
        {
            return Result.Failure<WorkArrangement>(JobErrors.WorkplaceInvalid);
        }

        // A day count only means something for hybrid. Dropping it elsewhere is kinder than
        // rejecting the save: the client sends the whole form back and the field is stale.
        var days = workplaceType == WorkplaceType.Hybrid ? onSiteDaysPerWeek : null;

        var policy = string.IsNullOrWhiteSpace(remotePolicy) ? null : remotePolicy.Trim();

        if (policy is { Length: > 1_000 })
        {
            return Result.Failure<WorkArrangement>(JobErrors.WorkplaceInvalid);
        }

        // Relocation assistance for a fully remote role is a contradiction the employer did not
        // mean; on-site days for one likewise. Normalise rather than refuse.
        var relocation = workplaceType == WorkplaceType.Remote
            ? false
            : offersRelocationAssistance;

        return Result.Success(new WorkArrangement(
            workplaceType, offersVisaSponsorship, relocation, days, policy));
    }

    /// <summary>Whether a candidate could do this from another country without moving.</summary>
    public bool IsRemote => WorkplaceType == WorkplaceType.Remote;

    /// <summary>Whether this posting is reachable by someone who needs sponsorship to move.</summary>
    public bool SupportsRelocation => OffersVisaSponsorship || OffersRelocationAssistance;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return WorkplaceType;
        yield return OffersVisaSponsorship;
        yield return OffersRelocationAssistance;
        yield return OnSiteDaysPerWeek;
        yield return RemotePolicy;
    }
}
