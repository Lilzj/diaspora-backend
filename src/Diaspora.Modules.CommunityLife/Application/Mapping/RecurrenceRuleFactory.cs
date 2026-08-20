using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Mapping;

/// <summary>
/// Turns a wire rule into a domain rule.
/// <para>
/// <b>The one place the client's <c>Custom</c> frequency is dealt with.</b> The wire has five
/// frequencies and the domain has four, because "custom" carries no expansion semantics of its own —
/// it means "weekly, on these weekdays", which is <see cref="RecurrenceFrequency.Weekly"/> with a
/// weekday list. Normalising it here rather than in the domain keeps the domain honest: a type that
/// knew about a word it cannot expand would be a type inviting somebody to expand it.
/// </para>
/// <para>
/// The normalised rule travels back on every response, so an editor sees what was stored rather than
/// what it sent.
/// </para>
/// </summary>
public static class RecurrenceRuleFactory
{
    /// <summary>The wire's word for a rule that is weekly with an explicit weekday list.</summary>
    private const string CustomFrequency = "Custom";

    public static Result<RecurrenceRule> Create(RecurrenceRuleRequest? request)
    {
        if (request is null)
        {
            return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceFrequencyInvalid);
        }

        var frequency = Normalize(request.Frequency);

        if (frequency is not { } resolved)
        {
            return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceFrequencyInvalid);
        }

        var endKind = CommunityLifeSharedMapper.ParseEnum<RecurrenceEndKind>(request.EndKind)
            ?? RecurrenceEndKind.Never;

        return RecurrenceRule.Create(
            resolved,
            request.Interval < 1 ? 1 : request.Interval,
            request.ByWeekday,
            request.ByMonthDay,
            endKind,
            request.Until,
            request.Count);
    }

    /// <summary>
    /// The domain frequency for a wire word, or null when the word is not one.
    /// <para>
    /// A <c>Custom</c> with no weekday list is still weekly — on whichever weekday the series starts,
    /// which is what an empty list already means.
    /// </para>
    /// </summary>
    private static RecurrenceFrequency? Normalize(string? frequency)
    {
        if (string.Equals(frequency?.Trim(), CustomFrequency, StringComparison.OrdinalIgnoreCase))
        {
            return RecurrenceFrequency.Weekly;
        }

        return CommunityLifeSharedMapper.ParseEnum<RecurrenceFrequency>(frequency);
    }
}
