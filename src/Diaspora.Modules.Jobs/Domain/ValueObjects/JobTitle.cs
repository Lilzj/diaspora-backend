using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// The role a posting is advertising.
/// <para>
/// Whitespace is collapsed rather than merely trimmed, because job titles arrive from
/// spreadsheet paste and applicant-tracking exports full of double spaces and stray tabs, and
/// "Senior  Nurse" and "Senior Nurse" must be the same string for search, for the slug, and for
/// the duplicate check an employer's twentieth posting runs against.
/// </para>
/// </summary>
public sealed class JobTitle : ValueObject
{
    private JobTitle(string value) => Value = value;

    private JobTitle() { }

    public string Value { get; private set; } = string.Empty;

    public static Result<JobTitle> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<JobTitle>(JobErrors.TitleInvalid);
        }

        var collapsed = string.Join(' ', value.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return collapsed.Length >= JobLimits.MinTitle && collapsed.Length <= JobLimits.MaxTitle
            ? Result.Success(new JobTitle(collapsed))
            : Result.Failure<JobTitle>(JobErrors.TitleInvalid);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
