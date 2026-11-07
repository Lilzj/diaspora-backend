using Diaspora.Contracts.Moderation.V1;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>Identifies one report on a moderation case.</summary>
public sealed record ModerationCaseReportId(Guid Value) : StronglyTypedId(Value)
{
    public static ModerationCaseReportId New() => new(Guid.NewGuid());

    public static ModerationCaseReportId From(Guid value) => new(value);
}

/// <summary>
/// One member's report, as it reached the case (7 Nov 2026).
/// <para>
/// The module keeps its own report row with the member's chosen reason; the case keeps who reported,
/// the reason mapped onto the platform's codes, and which module row to resolve once a decision is
/// made. One per reporter: a member who reports the same thing through two routes is one report.
/// </para>
/// </summary>
public sealed class ModerationCaseReport : Entity<ModerationCaseReportId>
{
    public const int MaxModuleReasonLength = 64;
    public const int MaxDetailLength = 1000;
    public const int MaxModuleLength = 32;

    public ModerationItemId ItemId { get; private set; } = default!;

    public UserId ReporterId { get; private set; } = default!;

    public ModerationReasonCode ReasonCode { get; private set; }

    public string ModuleReason { get; private set; } = default!;

    public string? Detail { get; private set; }

    public string Module { get; private set; } = default!;

    public Guid ModuleReportId { get; private set; }

    public DateTimeOffset FiledAt { get; private set; }

    private ModerationCaseReport() { }

    internal static ModerationCaseReport File(
        ModerationItemId itemId,
        UserId reporterId,
        ModerationReasonCode reasonCode,
        string moduleReason,
        string? detail,
        string module,
        Guid moduleReportId,
        DateTimeOffset filedAt) =>
        new()
        {
            Id = ModerationCaseReportId.New(),
            ItemId = itemId,
            ReporterId = reporterId,
            ReasonCode = reasonCode,
            ModuleReason = Clip(moduleReason, MaxModuleReasonLength) ?? "Other",
            Detail = Clip(detail, MaxDetailLength),
            Module = Clip(module, MaxModuleLength) ?? "unknown",
            ModuleReportId = moduleReportId,
            FiledAt = filedAt
        };

    private static string? Clip(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}
