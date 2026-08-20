using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Replaces a property house rules, renumbering them from zero.
/// <para>
/// Each rule gets a fresh caller-supplied key rather than reusing the old ones. The collection is
/// replaced wholesale, so reusing keys would make EF emit updates against rows that no longer
/// correspond to the text they carry.
/// </para>
/// </summary>
public sealed class SetHouseRulesCommandHandler
    : ICommandHandler<SetHouseRulesCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingListingReadContext _read;
    private readonly IGuidGenerator _guids;

    public SetHouseRulesCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingListingReadContext read,
        IGuidGenerator guids)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _read = read;
        _guids = guids;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        SetHouseRulesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(listing.Error);
        }

        var rules = command.Request.Rules
            .Select((rule, index) => HouseRule.Create(
                _guids.NewSequentialGuid(), rule.Text, index))
            .ToList();

        var applied = listing.Value.SetHouseRules(rules, command.Request.BaseRevision);

        if (applied.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await _read.ToResponseAsync(listing.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
