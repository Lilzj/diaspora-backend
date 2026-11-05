using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>
/// Reads a schedule off a create or update request, the same way for both. Nothing sent leaves
/// the posting as it is; part of one ("2 days" with no start) is refused rather than half-kept.
/// </summary>
internal static class JobScheduleRequest
{
    public static Result Apply(
        JobPosting posting,
        DateOnly? startsOn,
        int? lengthAmount,
        string? lengthUnit,
        DateTimeOffset now)
    {
        if (startsOn is null && lengthAmount is null && string.IsNullOrWhiteSpace(lengthUnit))
        {
            return Result.Success();
        }

        if (startsOn is not { } start
            || lengthAmount is not { } amount
            || JobWireNames.ParseLengthUnit(lengthUnit) is not { } unit)
        {
            return Result.Failure(JobErrors.ScheduleInvalid);
        }

        return posting.SetSchedule(start, amount, unit, now);
    }
}
