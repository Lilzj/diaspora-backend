using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Persistence;

public class StronglyTypedIdValueConverter<TId> : ValueConverter<TId, Guid>
    where TId : StronglyTypedId
{
    public StronglyTypedIdValueConverter()
        : base(
            id => id.Value,
            guid => (TId)Activator.CreateInstance(typeof(TId), guid)!)
    {
    }
}
