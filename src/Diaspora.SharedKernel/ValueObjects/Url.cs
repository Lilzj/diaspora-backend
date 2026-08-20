using Diaspora.SharedKernel.Primitives;

namespace Diaspora.SharedKernel.ValueObjects;

public sealed class Url : ValueObject
{
    public string Value { get; }

    private Url(string value)
    {
        Value = value;
    }

    public static Url Create(string value)
    {
        Guard.Against.NullOrWhiteSpace(value, nameof(value));
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException($"'{value}' is not a valid URL.", nameof(value));
        }
        return new Url(uri.ToString());
    }

    public static implicit operator string(Url url) => url.Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
