using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Bookmarks;

/// <summary>
/// Saves or unsaves a provider, through Community.
/// <para>
/// The provider is loaded first purely to confirm it exists and is visible — saving a provider
/// that was deleted five minutes ago would create a bookmark the saved-collections screen can
/// never render, and B6 has no way to know that on its own.
/// </para>
/// </summary>
public sealed class SetProviderBookmarkCommandHandler : ICommandHandler<SetProviderBookmarkCommand>
{
    private readonly IProviderRepository _providers;
    private readonly ProviderBookmarkService _bookmarks;
    private readonly ProviderAuthorizationService _authorization;

    public SetProviderBookmarkCommandHandler(
        IProviderRepository providers,
        ProviderBookmarkService bookmarks,
        ProviderAuthorizationService authorization)
    {
        _providers = providers;
        _bookmarks = bookmarks;
        _authorization = authorization;
    }

    public async Task<Result> Handle(
        SetProviderBookmarkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var providerId = ProviderId.From(command.ProviderId);
        var provider = await _providers.GetForReadAsync(providerId, cancellationToken);

        if (provider is null || provider.IsDeleted)
        {
            return Result.Failure(ProviderErrors.NotFound);
        }

        return command.IsSaved
            ? await _bookmarks.SaveAsync(providerId, caller.Value, cancellationToken)
            : await _bookmarks.RemoveAsync(providerId, caller.Value, cancellationToken);
    }
}
