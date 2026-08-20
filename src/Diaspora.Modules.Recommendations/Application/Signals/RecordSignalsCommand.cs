using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Signals;

/// <summary>
/// Records what a client observed.
/// <para>
/// <b>Transactional and audited, unlike most write commands in this platform, and the reasoning is
/// asymmetric.</b> Transactional because a batch is one unit — half a scroll's impressions recorded
/// and half lost would skew a profile in a way nothing could detect. Audited because a signal is
/// behavioural data about a person and every write of it should be traceable, which is the standard
/// this module holds itself to rather than one the kernel imposes.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.SignalsRecord)]
[Transactional(Reason = "A batch of signals is one unit; a partial batch skews a profile silently.")]
[Auditable(Action = "recommendations.signals.record")]
public sealed record RecordSignalsCommand : ICommand<SignalsRecordedResponse>
{
    public required IReadOnlyList<RecordSignalRequest> Signals { get; init; }
}
