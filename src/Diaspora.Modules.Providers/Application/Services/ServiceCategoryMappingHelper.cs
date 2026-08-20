using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Categories;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// A thin forwarder from <see cref="ProviderReadContext"/> to
/// <see cref="ServiceCategoryMapper"/>.
/// <para>
/// Exists purely to keep the dependency arrow pointing one way. The read context lives in
/// <c>Application.Services</c> and the mappers in <c>Application.Mapping</c>, and
/// <see cref="ServiceCategoryMapper"/> already imports the services namespace for
/// <c>ProviderWireNames</c>. Calling it directly from the context would close the cycle, which
/// compiles today and becomes an untangleable knot the moment either side grows.
/// </para>
/// </summary>
internal static class ServiceCategoryMappingHelper
{
    internal static ProviderCategoryResponse ToSummary(
        ServiceCategory category, string? languageCode) =>
        ServiceCategoryMapper.ToSummary(category, languageCode);

    internal static ProviderCategoryResponse Unknown(Guid categoryId) =>
        ServiceCategoryMapper.Unknown(categoryId);
}
