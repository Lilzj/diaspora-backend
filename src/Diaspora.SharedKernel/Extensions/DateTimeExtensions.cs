namespace Diaspora.SharedKernel.Extensions;

public static class DateTimeExtensions
{
    public static long ToUnixTimestamp(this DateTimeOffset dateTime) =>
        dateTime.ToUnixTimeSeconds();

    public static DateTimeOffset FromUnixTimestamp(long timestamp) =>
        DateTimeOffset.FromUnixTimeSeconds(timestamp);

    public static DateTimeOffset StartOfDay(this DateTimeOffset dateTime) =>
        new(dateTime.Date, dateTime.Offset);

    public static DateTimeOffset EndOfDay(this DateTimeOffset dateTime) =>
        new(dateTime.Date.AddDays(1).AddTicks(-1), dateTime.Offset);
}
