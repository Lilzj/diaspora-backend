using Microsoft.Extensions.Options;

namespace Diaspora.UnitTests.Common;

/// <summary>
/// An <see cref="IOptionsMonitor{T}"/> over one fixed value.
/// <para>
/// The platform's own components take <c>IOptionsMonitor</c> rather than <c>IOptions</c> so a
/// deployment can change a setting without a restart. That is right in production and inconvenient
/// in a unit test, where <c>Options.Create</c> gives an <c>IOptions</c> and nothing gives a monitor.
/// This is the smallest honest bridge: the value never changes, and <see cref="OnChange"/> returns a
/// disposable nobody has to remember to dispose.
/// </para>
/// </summary>
public sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => NullSubscription.Instance;

    private sealed class NullSubscription : IDisposable
    {
        public static readonly NullSubscription Instance = new();

        public void Dispose()
        {
            // Nothing was subscribed, so there is nothing to unsubscribe.
        }
    }
}
