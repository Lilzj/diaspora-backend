using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One piece of evidence against a member, counted toward their risk (RWD-14, CS-4b).
/// <para>
/// <b>Evidence, kept only while it can count.</b> A rejection counts for thirty days and an upheld
/// report for seven (<see cref="RiskPolicy"/>); the risk sweep removes a signal once it is older than
/// the longest window, so this table is never a member's history. Only evidence that can count is
/// recorded at all: an honest rejection, or a report on the reporter's own content, stores nothing.
/// </para>
/// <para>
/// <b>Keyed by where it came from.</b> <see cref="SourceKey"/> is unique, so a redelivered rejection
/// or report finds its row and is counted once.
/// </para>
/// <para>
/// Staff-facing, like the assessment it feeds: no member route reads it.
/// </para>
/// </summary>
public sealed class RiskSignal : AggregateRoot<RiskSignalId>
{
    public const int MaxSourceKeyLength = 128;
    public const int MaxContentKeyLength = 200;

    public UserId UserId { get; private set; } = default!;

    public RiskSignalKind Kind { get; private set; }

    /// <summary><c>verification:{id}</c> or <c>report:{module}:{id}</c>. Unique.</summary>
    public string SourceKey { get; private set; } = default!;

    /// <summary>The verification a rejection was for. A later grant of that type answers it.</summary>
    public VerificationType? VerificationType { get; private set; }

    /// <summary>
    /// The content an upheld report was about, as <c>{module}/{type}/{id}</c>. A spike counts content,
    /// not reports: three reports on one post are one bad post.
    /// </summary>
    public string? ContentKey { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    private RiskSignal() { }

    private RiskSignal(
        RiskSignalId id,
        UserId userId,
        RiskSignalKind kind,
        string sourceKey,
        DateTimeOffset occurredAt) : base(id)
    {
        UserId = userId;
        Kind = kind;
        SourceKey = sourceKey;
        OccurredAt = occurredAt;
    }

    /// <summary>A verification of the member's that a reviewer rejected for a fraud-like reason.</summary>
    public static RiskSignal VerificationRejected(
        UserId member, Guid verificationId, VerificationType type, DateTimeOffset rejectedAt)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new RiskSignal(
            RiskSignalId.New(),
            member,
            RiskSignalKind.VerificationRejected,
            $"verification:{verificationId:N}",
            rejectedAt)
        {
            VerificationType = type
        };
    }

    /// <summary>A report upheld against a piece of the member's content.</summary>
    public static RiskSignal ReportUpheld(
        UserId contentOwner,
        string module,
        string contentType,
        Guid contentId,
        Guid reportId,
        DateTimeOffset upheldAt)
    {
        ArgumentNullException.ThrowIfNull(contentOwner);

        var source = Guard.Against.NullOrWhiteSpace(module, nameof(module)).Trim();
        var type = Guard.Against.NullOrWhiteSpace(contentType, nameof(contentType)).Trim();

        return new RiskSignal(
            RiskSignalId.New(),
            contentOwner,
            RiskSignalKind.ReportUpheld,
            Truncate($"report:{source}:{reportId:N}", MaxSourceKeyLength),
            upheldAt)
        {
            ContentKey = Truncate($"{source}/{type}/{contentId:N}", MaxContentKeyLength)
        };
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
