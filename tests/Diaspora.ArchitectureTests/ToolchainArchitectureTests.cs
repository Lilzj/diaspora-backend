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

    /// <summary>
    /// <b>The container builds from a named patch, not a moving tag.</b>
    /// </summary>
    /// <remarks>
    /// <c>sdk:10.0</c> served 10.0.404 when this was pinned, and would have served something else
    /// later: the same commit compiling against a different compiler and a different analyzer set,
    /// which is the finding in one line. The runtime image is numbered on its own scale
    /// (<c>aspnet:10.0.15</c>), so these two tags are not expected to match each other — only to
    /// be patches rather than bands.
    /// </remarks>
    [Fact]
    public void The_container_images_are_pinned_to_a_patch()
    {
        var floating = DockerImages()
            .Where(image => image.Tag.Count(character => character == '.') < 2)
            .Select(image => $"{image.Repository}:{image.Tag}")
            .ToList();

        floating.Should().BeEmpty(
            "a moving tag means the deployed artefact is not reproducible: the same commit builds "
            + "against a different SDK next month, and nothing records which one");
    }

    /// <summary>
    /// <b>Every build path names the same SDK.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three paths compile this solution: a developer machine through <c>global.json</c>, the CI
    /// runner, and <c>docker/Dockerfile</c>. On 13 November 2026 they were on 10.0.203, 10.0.100
    /// and 10.0.404 — three feature bands for one commit, which is what BARCH-003 meant by "the
    /// build is not reproducible".
    /// </para>
    /// <para>
    /// This rule previously exempted the developer machine, on the grounds that requiring an SDK
    /// is something to ask for rather than impose. It was asked for; the exemption is gone.
    /// Raising one of the three now fails here rather than quietly separating the build that
    /// gates a merge from the image that ships.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_build_path_names_the_same_sdk()
    {
        var container = DockerImages()
            .Where(image => image.Repository.EndsWith("/sdk", StringComparison.Ordinal))
            .Select(image => image.Tag)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        container.Should().ContainSingle("the build stage is the only SDK image");

        var runner = WorkflowValues("dotnet-version:");

        runner.Should().NotBeEmpty(
            "global-json-file alone installs the version field, which is a floor rather than a "
            + "toolchain, so the runner must name its SDK");

        runner.Distinct(StringComparer.Ordinal).Should().BeEquivalentTo(
            container,
            "raise docker/Dockerfile and ci.yml together, or CI stops proving anything about the "
            + "image that ships");

        DeclaredSdkVersion().Should().Be(
            container[0],
            "global.json is what a developer machine resolves against, and a floor below the two "
            + "automated paths lets it compile with a different compiler than either of them");
    }

    /// <summary>The SDK <c>global.json</c> names.</summary>
    private static string DeclaredSdkVersion()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(SourceTree.Root.FullName, "global.json")));

        return document.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
    }

    private static List<(string Repository, string Tag)> DockerImages()
    {
        const string prefix = "FROM mcr.microsoft.com/dotnet/";
        var images = new List<(string, string)>();

        foreach (var line in File.ReadAllLines(
            Path.Combine(SourceTree.Root.FullName, "docker", "Dockerfile")))
        {
            var trimmed = line.Trim();

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var reference = trimmed["FROM ".Length..].Split(' ')[0];
            var separator = reference.LastIndexOf(':');

            images.Add(separator < 0
                ? (reference, string.Empty)
                : (reference[..separator], reference[(separator + 1)..]));
        }

        return images;
    }

    private static List<string> WorkflowValues(string key)
    {
        return
        [
            .. File.ReadAllLines(
                    Path.Combine(SourceTree.Root.FullName, ".github", "workflows", "ci.yml"))
                .Select(line => line.Trim())
                .Where(line => line.StartsWith(key, StringComparison.Ordinal))
                .Select(line => line[key.Length..].Trim())
        ];
    }
}
