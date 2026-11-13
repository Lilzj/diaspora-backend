using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The compiler the gate runs on is a decision, not a date (BARCH-003).
/// <para>
/// <b>Three settings compose into a build that fails whenever Microsoft ships a rule.</b>
/// <c>global.json</c> asked for the newest SDK in existence, prereleases of the next major
/// included; <c>AnalysisLevel</c> was <c>latest</c>, meaning every rule that SDK happens to carry;
/// and <c>TreatWarningsAsErrors</c> makes each new rule a build failure in Release. None of the
/// three is wrong alone.
/// </para>
/// <para>
/// <b>It stopped being theoretical on 13 November 2026.</b> A GitHub runner image picked up a
/// .NET 11 prerelease and master went red with eight <c>CA2026</c> errors on commit
/// <c>efeb175</c> — which had been green two days earlier, with no commit in between. The audit
/// had predicted the mechanism in those words: <i>"'CI is green' is not evidence the tree builds.
/// It is evidence the tree builds on ubuntu-latest's SDK inventory on that day."</i>
/// </para>
/// <para>
/// <b>What this rule does and does not claim.</b> It pins the policy, not the patch. The
/// toolchain can still differ by feature band between a developer machine, the runner and
/// <c>docker/Dockerfile</c>'s floating <c>sdk:10.0</c> tag — BARCH-003 items 3 and 4, still open.
/// What it refuses is the thing that broke the build: crossing a major version, and taking a
/// prerelease.
/// </para>
/// </summary>
public sealed class ToolchainArchitectureTests
{
    /// <summary>Roll-forward policies that can leave the major version the platform targets.</summary>
    private static readonly string[] CrossesMajor = ["latestMajor", "major"];

    /// <summary>
    /// <b>The SDK cannot roll forward onto a prerelease, or across a major version.</b>
    /// </summary>
    /// <remarks>
    /// Asserted on the committed file rather than on <c>Environment.Version</c>: the question is
    /// what every machine is told to use, not what this one happens to have. A test reading the
    /// running SDK would pass on a correctly-configured machine and say nothing about the runner.
    /// </remarks>
    [Fact]
    public void The_sdk_does_not_roll_forward_to_a_prerelease_or_a_new_major()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(SourceTree.Root.FullName, "global.json")));

        var sdk = document.RootElement.GetProperty("sdk");

        sdk.GetProperty("allowPrerelease").GetBoolean().Should().BeFalse(
            "a prerelease of the next major ships analyzer rules this codebase has never been "
            + "compiled against, and TreatWarningsAsErrors turns each of them into a build failure");

        var rollForward = sdk.GetProperty("rollForward").GetString();

        rollForward.Should().NotBeNullOrWhiteSpace();
        CrossesMajor.Should().NotContain(
            rollForward!,
            "rolling forward across a major version is how master went red on 13 November 2026 "
            + "with no commit: the runner image changed, and global.json had told it to");
    }

    /// <summary>
    /// <b>The analyzer surface is named, not inherited from whatever is installed.</b>
    /// </summary>
    /// <remarks>
    /// <c>AnalysisLevel</c> is the other half. With the SDK pinned to a major version the set of
    /// rules is already bounded, so this is belt and braces — but it is the half that says
    /// adopting new rules is a commit, and it costs one line to keep true.
    /// </remarks>
    [Fact]
    public void The_analysis_level_is_a_version_rather_than_latest()
    {
        var props = File.ReadAllText(
            Path.Combine(SourceTree.Root.FullName, "Directory.Build.props"));

        props.Should().NotContain(
            "<AnalysisLevel>latest</AnalysisLevel>",
            "\"latest\" means the rule set changes with the SDK rather than with a decision, "
            + "which is the opposite of what a gate is for");

        props.Should().Contain(
            "<AnalysisLevel>",
            "the property must still be set: its default follows the target framework, which "
            + "would reintroduce the same drift more quietly");
    }
}
