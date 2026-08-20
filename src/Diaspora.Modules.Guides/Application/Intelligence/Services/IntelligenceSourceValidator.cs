using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Modules.Guides.Observability;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// Runs every registered source validator over a notice about to be published.
/// <para>
/// <b>A veto, not a vote.</b> Every validator sees the source and any single rejection refuses it.
/// That is deliberately not a majority — a source policy is a list of things that must not happen,
/// and a deployment that has forbidden something means it whatever else agrees.
/// </para>
/// <para>
/// <b>A validator that throws is treated as abstaining, and logged.</b> An editorial team must not be
/// unable to publish a cyclone warning because somebody's allow-list service is down. Failing open is
/// the right trade here precisely because the real gate is elsewhere: a notice cannot be published
/// unless its authority is verified, and no configuration can switch that off.
/// </para>
/// <para>
/// <b>The rejection reason is carried into the error.</b> An editor told "that source is not one this
/// deployment accepts" and nothing else has no way to fix it; told "sources must be on a .gov.mu
/// domain" they can. The reason comes from whichever validator refused, verbatim.
/// </para>
/// </summary>
public sealed class IntelligenceSourceValidator
{
    private readonly IReadOnlyList<IIntelligenceSourceValidator> _validators;
    private readonly ILogger<IntelligenceSourceValidator> _logger;

    public IntelligenceSourceValidator(
        IEnumerable<IIntelligenceSourceValidator> validators,
        ILogger<IntelligenceSourceValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(validators);

        _validators = [.. validators.OrderBy(validator => validator.Order)];
        _logger = logger;
    }

    /// <summary>The validators in force, by module. For the capabilities endpoint.</summary>
    public IReadOnlyList<string> ValidatorNames =>
        [.. _validators.Select(validator => validator.Module)];

    public async Task<Result> ValidateAsync(
        OfficialNotice notice,
        IssuingAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(authority);

        if (_validators.Count == 0)
        {
            return Result.Success();
        }

        var request = new SourceValidationRequest
        {
            CountryCode = notice.CountryCode,
            AuthorityType = authority.AuthorityType,
            AuthorityName = authority.Name,
            AuthorityIsVerified = authority.IsVerified,
            SourceUrl = notice.SourceUrl,
            SourceHost = IntelligenceNormalization.Host(notice.SourceUrl),
            ReferenceNumber = notice.ReferenceNumber
        };

        foreach (var validator in _validators)
        {
            var verdict = await EvaluateAsync(validator, request, cancellationToken);

            if (!verdict.IsRejected)
            {
                continue;
            }

            IntelligenceTelemetry.SourcesRejected.Add(
                1, new KeyValuePair<string, object?>("module", validator.Module));

            return Result.Failure(
                verdict.Reason is null
                    ? IntelligenceErrors.SourceRejected
                    : IntelligenceErrors.SourceRejected with { Description = verdict.Reason });
        }

        return Result.Success();
    }

    private async Task<SourceValidationVerdict> EvaluateAsync(
        IIntelligenceSourceValidator validator,
        SourceValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await validator.ValidateAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A broken validator must not block an urgent publication.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "The {Module} source validator failed and was treated as abstaining.",
                validator.Module);

            return SourceValidationVerdict.Abstain();
        }
#pragma warning restore CA1031
    }
}
