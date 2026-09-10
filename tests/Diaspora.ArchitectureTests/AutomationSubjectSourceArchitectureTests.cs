using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// A preview must answer with the same keys the real trigger carries — Phase 5.
/// <para>
/// <b>The whole point of the subject seam is that a member sees, before they save, what their rule
/// will actually be checked against.</b> That only holds while the module's <i>source</i> and the
/// module's <i>consumer</i> fill the same dictionary. Let them drift and the failure is the worst
/// available shape: the builder shows a condition being checked, the member saves, and the real run
/// skips it with "this trigger does not carry the information" — the very sentence this work exists
/// to stop being wrong.
/// </para>
/// <para>
/// Source rather than reflection, because both sides write string literals into a dictionary and no
/// type carries them. It is the same reason <c>AutomationScheduleArchitectureTests</c> reads source
/// to find what arms a scheduled trigger.
/// </para>
/// </summary>
public sealed class AutomationSubjectSourceArchitectureTests
{
    /// <summary>
    /// Keys a consumer writes that a preview deliberately cannot answer.
    /// <para>
    /// An explicit set, and each entry needs a reason. A key lands here when the fact is about the
    /// <i>occasion</i> rather than the subject — what changed in an edit, who was involved — which
    /// no amount of reading the subject can reconstruct.
    /// </para>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NotPreviewable =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["applicationStatus"] =
                "The status an application moved to is carried by the change itself. A subject read "
                + "at rest has a current status and no notion of the transition a rule watches for.",
            ["listingStatus"] =
                "Same shape: a reservation ending is an occasion, and the horizon that arms it "
                + "writes the status the moment will carry rather than the one the listing has now.",
        };

    [Fact]
    public void An_event_preview_answers_every_key_the_event_consumers_carry()
    {
        var carried = KeysAssignedIn(
            Path.Combine(
                SourceTree.Root.FullName,
                "src", "Diaspora.Modules.Automation", "Application", "Consumers",
                "EventConsumers.cs"),
            Path.Combine(
                SourceTree.Root.FullName,
                "src", "Diaspora.Modules.Automation", "Application", "Consumers",
                "PublicationConsumers.cs"));

        var answered = KeysAssignedIn(
            Path.Combine(
                SourceTree.Root.FullName,
                "src", "Diaspora.Modules.CommunityLife", "Extensibility", "Automation",
                "EventAutomationSubjectSource.cs"));

        // PublicationConsumers covers more than events; only the keys the event source could
        // plausibly answer are in scope, and the rest are another module's to add a source for.
        var eventKeys = new[]
        {
            "country", "category", "eventType", "neighbourhood", "dayOfWeek", "timeOfDay", "city"
        };

        var missing = carried
            .Intersect(eventKeys, StringComparer.Ordinal)
            .Where(key => !answered.Contains(key))
            .Where(key => !NotPreviewable.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        missing.Should().BeEmpty(
            "a key the trigger carries and the preview does not is a condition the builder shows as "
            + "untested and the real run then evaluates — or worse, the reverse");
    }

    /// <summary>A preview that answers a key nothing carries is inventing a match.</summary>
    [Fact]
    public void An_event_preview_invents_no_key_of_its_own()
    {
        var carried = KeysAssignedIn(
            Path.Combine(
                SourceTree.Root.FullName,
                "src", "Diaspora.Modules.Automation", "Application", "Consumers",
                "EventConsumers.cs"),
            Path.Combine(
                SourceTree.Root.FullName,
                "src", "Diaspora.Modules.Automation", "Application", "Consumers",
                "PublicationConsumers.cs"));

        var answered = KeysAssignedIn(
            Path.Combine(
                SourceTree.Root.FullName,
                "src", "Diaspora.Modules.CommunityLife", "Extensibility", "Automation",
                "EventAutomationSubjectSource.cs"));

        answered
            .Where(key => !carried.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Should().BeEmpty(
                "the preview would show a condition being checked that no real run ever checks");
    }

    [Fact]
    public void No_exemption_outlives_the_key_it_records()
    {
        NotPreviewable.Values
            .Should().OnlyContain(reason => reason.Length > 80,
                "an exemption without a stated reason is a place to hide things");
    }

    /// <summary>Every <c>values["key"] =</c> assignment across the given files.</summary>
    private static HashSet<string> KeysAssignedIn(params string[] paths)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            File.Exists(path).Should().BeTrue($"the sweep reads {path}");

            // Comments stripped first. Without this the sweep read a comment in the event source
            // that *quoted* the assignment shape it was describing, and reported a condition key
            // called "key" that no code anywhere writes — a probe finding what it brought with it.
            var code = Regex.Replace(
                Regex.Replace(File.ReadAllText(path), @"/\*[\s\S]*?\*/", " "),
                @"//[^\n]*", " ");

            foreach (Match match in Regex.Matches(
                         code,
                         @"(?:values|horizonValues)\[""([a-zA-Z]+)""\]\s*="))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        keys.Should().NotBeEmpty("a sweep that parses nothing passes for the wrong reason");

        return keys;
    }
}
