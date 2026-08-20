using Diaspora.Modules.Providers.Domain.Availability;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// The availability engine.
/// <para>
/// Every test here is a case the client's <c>isOpenNow</c> boolean gets wrong: split shifts,
/// overnight opening, holidays, time zones, and providers for whom "open" is not a question.
/// </para>
/// </summary>
public class ProviderAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_split_shift_is_closed_between_its_periods()
    {
        var provider = Open();

        // A restaurant serving lunch and dinner. The client's single open/close pair says this
        // provider is open at four o'clock; it is not, and a member turns up to a locked door.
        SetHours(provider, DayOfWeek.Tuesday,
            (new TimeOnly(12, 0), new TimeOnly(15, 0)),
            (new TimeOnly(18, 0), new TimeOnly(23, 0)));

        // 2026-07-28 is a Tuesday.
        provider.IsOpenAt(At(13, 0)).Should().BeTrue();
        provider.IsOpenAt(At(16, 0)).Should().BeFalse();
        provider.IsOpenAt(At(19, 0)).Should().BeTrue();
    }

    [Fact]
    public void An_overnight_period_is_open_after_midnight()
    {
        var provider = Open();

        // A bar open 20:00–02:00. A closing time numerically before its opening time is a
        // midnight crossing, not an inverted range.
        SetHours(provider, DayOfWeek.Tuesday, (new TimeOnly(20, 0), new TimeOnly(2, 0)));

        provider.IsOpenAt(At(21, 0)).Should().BeTrue();
        provider.IsOpenAt(At(19, 0)).Should().BeFalse();
    }

    [Fact]
    public void Overlapping_periods_on_one_day_are_refused()
    {
        var provider = Open();

        var hours = provider.HoursFor(DayOfWeek.Tuesday, OperatingHoursId.New());

        hours.AddWindow(AvailabilityWindowId.New(), new TimeOnly(9, 0), new TimeOnly(17, 0))
            .IsSuccess.Should().BeTrue();

        // Almost always a typo — 9–5 entered twice, slightly differently. Merging silently would
        // produce a schedule the provider did not write and cannot see is wrong.
        var overlapping = hours.AddWindow(
            AvailabilityWindowId.New(), new TimeOnly(16, 0), new TimeOnly(18, 0));

        overlapping.IsFailure.Should().BeTrue();
        overlapping.Error.Should().Be(ProviderErrors.OperatingHoursOverlap);
    }

    [Fact]
    public void A_day_with_no_hours_recorded_answers_null_rather_than_closed()
    {
        var provider = Open();

        SetHours(provider, DayOfWeek.Monday, (new TimeOnly(9, 0), new TimeOnly(17, 0)));

        // Nothing said about Tuesday. Saying "Closed" would be a claim on the provider's behalf
        // that they never made.
        provider.IsOpenAt(At(13, 0)).Should().BeNull();
    }

    [Fact]
    public void An_explicit_closure_is_not_the_same_as_silence()
    {
        var provider = Open();

        var hours = provider.HoursFor(DayOfWeek.Tuesday, OperatingHoursId.New());
        hours.Close();

        provider.IsOpenAt(At(13, 0)).Should().BeFalse();
        hours.IsSpecified.Should().BeTrue();
        hours.Describe().Should().Be("Closed");
    }

    [Fact]
    public void A_dated_exception_beats_the_weekly_schedule()
    {
        var provider = Open();

        SetHours(provider, DayOfWeek.Tuesday, (new TimeOnly(9, 0), new TimeOnly(17, 0)));

        var holiday = AvailabilityException.Closure(
            AvailabilityExceptionId.New(),
            AvailabilityExceptionKind.Holiday,
            new DateOnly(2026, 7, 28),
            new DateOnly(2026, 7, 28),
            "Closed for Eid");

        provider.AddAvailabilityException(holiday.Value, new DateOnly(2026, 7, 28))
            .IsSuccess.Should().BeTrue();

        // Without this, a member is sent across London to a locked door on every public holiday.
        provider.IsOpenAt(At(13, 0)).Should().BeFalse();
    }

    [Fact]
    public void Overlapping_exceptions_are_refused()
    {
        var provider = Open();

        var first = AvailabilityException.Closure(
            AvailabilityExceptionId.New(),
            AvailabilityExceptionKind.Vacation,
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 14));

        var second = AvailabilityException.Closure(
            AvailabilityExceptionId.New(),
            AvailabilityExceptionKind.TemporaryClosure,
            new DateOnly(2026, 8, 10),
            new DateOnly(2026, 8, 20));

        provider.AddAvailabilityException(first.Value, new DateOnly(2026, 7, 28))
            .IsSuccess.Should().BeTrue();

        var clash = provider.AddAvailabilityException(second.Value, new DateOnly(2026, 7, 28));

        clash.IsFailure.Should().BeTrue();
        clash.Error.Should().Be(ProviderErrors.AvailabilityExceptionOverlap);
    }

    [Fact]
    public void Appointment_only_providers_answer_null_rather_than_closed()
    {
        var provider = Open();

        provider.ConfigureAvailability(
            AvailabilitySettings.Create(AvailabilityKind.AppointmentOnly, "Europe/London"))
            .IsSuccess.Should().BeTrue();

        // A consultant is neither open nor closed at ten on a Tuesday. The client's boolean has
        // to render one of them, and "Closed" costs them business.
        provider.IsOpenAt(At(10, 0)).Should().BeNull();
    }

    [Fact]
    public void An_always_open_provider_is_open_without_a_schedule()
    {
        var provider = Open();

        provider.ConfigureAvailability(
            AvailabilitySettings.Create(AvailabilityKind.AlwaysOpen, "UTC"))
            .IsSuccess.Should().BeTrue();

        provider.IsOpenAt(At(3, 0)).Should().BeTrue();
    }

    [Fact]
    public void Open_now_is_answered_in_the_providers_own_time_zone()
    {
        var provider = Open();

        // Mauritius is UTC+4. A shop open 09:00–17:00 local is open at 06:00 UTC and shut at
        // 04:00 UTC. Comparing a stored 09:00 against UtcNow shows it closed all morning.
        provider.ConfigureAvailability(
            AvailabilitySettings.Create(AvailabilityKind.BusinessHours, "Indian/Mauritius"))
            .IsSuccess.Should().BeTrue();

        SetHours(provider, DayOfWeek.Tuesday, (new TimeOnly(9, 0), new TimeOnly(17, 0)));

        provider.IsOpenAt(At(6, 0)).Should().BeTrue();
        provider.IsOpenAt(At(4, 0)).Should().BeFalse();
    }

    [Fact]
    public void An_unrecognised_time_zone_is_refused_rather_than_defaulted()
    {
        var provider = Open();

        // Falling back to UTC would produce hours wrong by a whole number of hours that look
        // entirely plausible.
        AvailabilitySettings.Create(AvailabilityKind.BusinessHours, "Middle/Earth")
            .Should().BeNull();

        var refused = provider.ConfigureAvailability(null);

        refused.IsFailure.Should().BeTrue();
        refused.Error.Should().Be(ProviderErrors.TimeZoneInvalid);
    }

    [Fact]
    public void A_paused_provider_is_closed_whatever_its_schedule_says()
    {
        var provider = Open();

        SetHours(provider, DayOfWeek.Tuesday, (new TimeOnly(9, 0), new TimeOnly(17, 0)));

        provider.Pause(Now).IsSuccess.Should().BeTrue();

        provider.IsOpenAt(At(13, 0)).Should().BeFalse();
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 7, 28, hour, minute, 0, TimeSpan.Zero);

    private static void SetHours(
        Provider provider, DayOfWeek day, params (TimeOnly Opens, TimeOnly Closes)[] windows)
    {
        var mapped = windows
            .Select(window => (AvailabilityWindowId.New(), window.Opens, window.Closes, (string?)null))
            .ToList();

        provider.SetDayHours(day, isClosed: false, mapped, OperatingHoursId.New())
            .IsSuccess.Should().BeTrue();
    }

    /// <summary>A published provider with a contact method, so it is Active and answerable.</summary>
    private static Provider Open()
    {
        var provider = ProviderOwnershipTests.Register();

        var phone = ProviderContactMethod.Create(
            ProviderContactMethodId.New(), ContactMethodKind.Phone, "+447700900456");

        provider.AddContactMethod(phone.Value).IsSuccess.Should().BeTrue();
        provider.Publish(Now).IsSuccess.Should().BeTrue();

        return provider;
    }
}
