using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>Opens an upload session.</summary>
[Auditable]
[RequirePolicy(MediaPolicies.UploadImage)]
public sealed record StartUploadCommand : ICommand<UploadSessionResponse>
{
    public required StartUploadRequest Request { get; init; }
}
