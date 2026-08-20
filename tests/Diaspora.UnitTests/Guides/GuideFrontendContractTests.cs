using System.Reflection;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// The vocabulary clients switch on, pinned so it cannot drift by accident.
/// <para>
/// Everything asserted here is a string a client compares as a literal: a status in a badge, a
/// block kind in a renderer's switch, an error code in a translation table, a sort key in a
/// query string, a route prefix in a base URL. <b>A serialised enum is a frozen contract, and a
/// default <c>ToString()</c> is a contract nobody decided on</b> — so the decision is made once,
/// in <see cref="GuideWireNames"/>, and asserted here.
/// </para>
/// <para>
/// When one of these fails the fix is almost never to change the test: it is either to restore
/// the backend name, or — if the change is deliberate — to update the client, the test and the
/// contract freeze together, in one reviewed change.
/// </para>
/// </summary>
public class GuideFrontendContractTests
{
    // ── Statuses and audience ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GuideStatus.Draft, "Draft")]
    [InlineData(GuideStatus.Published, "Published")]
    [InlineData(GuideStatus.Archived, "Archived")]
    public void Guide_statuses_travel_as_their_declared_names(GuideStatus status, string wire) =>
        GuideWireNames.ToWire(status).Should().Be(wire);

    [Theory]
    [InlineData(GuideVisibility.Public, "Public")]
    [InlineData(GuideVisibility.Unlisted, "Unlisted")]
    [InlineData(GuideVisibility.Private, "Private")]
    public void Visibilities_travel_as_their_declared_names(GuideVisibility visibility, string wire)
    {
        GuideWireNames.ToWire(visibility).Should().Be(wire);
        GuideWireNames.ParseVisibility(wire).Should().Be(visibility);
    }

    [Theory]
    [InlineData(GuideModerationState.Clear, "Clear")]
    [InlineData(GuideModerationState.Flagged, "Flagged")]
    [InlineData(GuideModerationState.Hidden, "Hidden")]
    public void Moderation_states_travel_as_their_declared_names(
        GuideModerationState state, string wire) =>
        GuideWireNames.ToWire(state).Should().Be(wire);

    // ── Block kinds: the renderer's switch ──────────────────────────────────────────────────

    [Theory]
    [InlineData(GuideBlockKind.Markdown, "Markdown")]
    [InlineData(GuideBlockKind.RichText, "RichText")]
    [InlineData(GuideBlockKind.Image, "Image")]
    [InlineData(GuideBlockKind.Video, "Video")]
    [InlineData(GuideBlockKind.Table, "Table")]
    [InlineData(GuideBlockKind.Links, "Links")]
    [InlineData(GuideBlockKind.Callout, "Callout")]
    [InlineData(GuideBlockKind.Warning, "Warning")]
    [InlineData(GuideBlockKind.Tip, "Tip")]
    [InlineData(GuideBlockKind.Attachment, "Attachment")]
    [InlineData(GuideBlockKind.CodeSnippet, "CodeSnippet")]
    public void Every_block_kind_the_renderer_switches_on_is_named_exactly(
        GuideBlockKind kind, string wire)
    {
        // A block kind the client does not recognise renders as nothing at all, which is a
        // paragraph of somebody's guide silently missing.
        GuideWireNames.ToWire(kind).Should().Be(wire);
        GuideWireNames.ParseBlockKind(wire).Should().Be(kind);
    }

    [Fact]
    public void No_block_kind_exists_that_the_contract_has_not_named()
    {
        // The [Theory] above is the contract; this is what stops a twelfth kind being added to
        // the enum and shipped without anybody deciding what clients should call it.
        Enum.GetValues<GuideBlockKind>().Should().HaveCount(11);
    }

    // ── Media roles ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GuideMediaRole.Hero, "Hero")]
    [InlineData(GuideMediaRole.Cover, "Cover")]
    [InlineData(GuideMediaRole.Gallery, "Gallery")]
    [InlineData(GuideMediaRole.Attachment, "Attachment")]
    [InlineData(GuideMediaRole.Embedded, "Embedded")]
    public void Media_roles_travel_as_their_declared_names(GuideMediaRole role, string wire)
    {
        GuideWireNames.ToWire(role).Should().Be(wire);
        GuideWireNames.ParseMediaRole(wire).Should().Be(role);
    }

    // ── Difficulty, votes, suggestions, verification ────────────────────────────────────────

    [Theory]
    [InlineData(GuideDifficulty.Beginner, "Beginner")]
    [InlineData(GuideDifficulty.Intermediate, "Intermediate")]
    [InlineData(GuideDifficulty.Advanced, "Advanced")]
    public void Difficulties_travel_as_their_declared_names(GuideDifficulty difficulty, string wire)
    {
        GuideWireNames.ToWire(difficulty).Should().Be(wire);
        GuideWireNames.ParseDifficulty(wire).Should().Be(difficulty);
    }

    [Theory]
    [InlineData(GuideVoteKind.Helpful, "Helpful")]
    [InlineData(GuideVoteKind.NotHelpful, "NotHelpful")]
    public void Vote_kinds_travel_as_their_declared_names(GuideVoteKind kind, string wire)
    {
        GuideWireNames.ToWire(kind).Should().Be(wire);
        GuideWireNames.ParseVoteKind(wire).Should().Be(kind);
    }

    [Theory]
    [InlineData(GuideSuggestionStatus.Open, "Open")]
    [InlineData(GuideSuggestionStatus.Accepted, "Accepted")]
    [InlineData(GuideSuggestionStatus.Rejected, "Rejected")]
    [InlineData(GuideSuggestionStatus.Withdrawn, "Withdrawn")]
    public void Suggestion_statuses_travel_as_their_declared_names(
        GuideSuggestionStatus status, string wire) =>
        GuideWireNames.ToWire(status).Should().Be(wire);

    [Theory]
    [InlineData(GuideVerificationState.None, "None")]
    [InlineData(GuideVerificationState.Pending, "Pending")]
    [InlineData(GuideVerificationState.Verified, "Verified")]
    [InlineData(GuideVerificationState.Revoked, "Revoked")]
    public void Verification_states_travel_as_their_declared_names(
        GuideVerificationState state, string wire) =>
        GuideWireNames.ToWire(state).Should().Be(wire);

    // ── Sort keys: lower-case kebab, because they live in query strings ─────────────────────

    [Theory]
    [InlineData(GuideSortOrder.Newest, "newest")]
    [InlineData(GuideSortOrder.MostHelpful, "most-helpful")]
    [InlineData(GuideSortOrder.RecentlyUpdated, "recently-updated")]
    [InlineData(GuideSortOrder.Popular, "popular")]
    [InlineData(GuideSortOrder.Trending, "trending")]
    public void Sort_keys_are_lower_case_kebab_and_round_trip(GuideSortOrder sort, string wire)
    {
        GuideWireNames.ToWire(sort).Should().Be(wire);
        GuideWireNames.ParseSort(wire).Should().Be(sort);
    }

    [Theory]
    [InlineData("mosthelpful", GuideSortOrder.MostHelpful)]
    [InlineData("helpful", GuideSortOrder.MostHelpful)]
    [InlineData("recentlyupdated", GuideSortOrder.RecentlyUpdated)]
    [InlineData("updated", GuideSortOrder.RecentlyUpdated)]
    [InlineData("TRENDING", GuideSortOrder.Trending)]
    [InlineData("  Popular  ", GuideSortOrder.Popular)]
    public void The_spellings_a_client_might_already_send_keep_working(
        string sent, GuideSortOrder expected) =>
        GuideWireNames.ParseSort(sent).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relevance")]
    public void An_unknown_sort_key_falls_back_rather_than_failing_a_browse(string? sent) =>
        GuideWireNames.ParseSort(sent).Should().Be(GuideSortOrder.Newest,
            "a browse screen that errors over a sort key is worse than one ordered differently");

    [Fact]
    public void The_ranking_keys_use_the_same_vocabulary_as_the_wire_sort_keys()
    {
        // One vocabulary, lower-case, frozen. Two would mean a client's saved sort key and the
        // registry's key needing a translation table nobody maintains.
        GuideRankingKeys.Newest.Should().Be(GuideWireNames.ToWire(GuideSortOrder.Newest));
        GuideRankingKeys.MostHelpful.Should().Be(GuideWireNames.ToWire(GuideSortOrder.MostHelpful));
        GuideRankingKeys.RecentlyUpdated.Should()
            .Be(GuideWireNames.ToWire(GuideSortOrder.RecentlyUpdated));
        GuideRankingKeys.Trending.Should().Be(GuideWireNames.ToWire(GuideSortOrder.Trending));
    }

    [Theory]
    [InlineData("not-a-kind")]
    [InlineData("42")]
    [InlineData(null)]
    public void An_unrecognised_value_parses_to_null_so_the_caller_owns_the_error(string? sent)
    {
        // The error codes differ per field, so the map deliberately does not choose one.
        GuideWireNames.ParseBlockKind(sent).Should().BeNull();
        GuideWireNames.ParseVisibility(sent).Should().BeNull();
        GuideWireNames.ParseDifficulty(sent).Should().BeNull();
        GuideWireNames.ParseMediaRole(sent).Should().BeNull();
        GuideWireNames.ParseVoteKind(sent).Should().BeNull();
    }

    // ── Error codes ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("guides.not-found")]
    [InlineData("guides.version.not-found")]
    [InlineData("guides.category.not-found")]
    [InlineData("guides.suggestion.not-found")]
    [InlineData("guides.draft.not-visible")]
    [InlineData("guides.hidden")]
    [InlineData("guides.deleted")]
    [InlineData("guides.not-authenticated")]
    [InlineData("guides.not-author")]
    [InlineData("guides.not-moderator")]
    [InlineData("guides.revision-conflict")]
    [InlineData("guides.not-publishable")]
    [InlineData("guides.already-published")]
    [InlineData("guides.not-published")]
    [InlineData("guides.not-archived")]
    [InlineData("guides.archived-immutable")]
    [InlineData("guides.not-deleted")]
    [InlineData("guides.schedule.in-past")]
    [InlineData("guides.not-scheduled")]
    [InlineData("guides.not-draft")]
    [InlineData("guides.title.invalid")]
    [InlineData("guides.slug.invalid")]
    [InlineData("guides.slug.taken")]
    [InlineData("guides.summary.too-long")]
    [InlineData("guides.language.invalid")]
    [InlineData("guides.country.invalid")]
    [InlineData("guides.seo.invalid")]
    [InlineData("guides.sections.limit")]
    [InlineData("guides.section.invalid")]
    [InlineData("guides.blocks.limit")]
    [InlineData("guides.block.invalid")]
    [InlineData("guides.structure.empty")]
    [InlineData("guides.tags.limit")]
    [InlineData("guides.tag.invalid")]
    [InlineData("guides.media.limit")]
    [InlineData("guides.media.invalid")]
    [InlineData("guides.provider-references.limit")]
    [InlineData("guides.provider-reference.invalid")]
    [InlineData("guides.reading-profile.invalid")]
    [InlineData("guides.moderation-note.invalid")]
    [InlineData("guides.revision-notes.invalid")]
    [InlineData("guides.snapshot.invalid")]
    [InlineData("guides.category.inactive")]
    [InlineData("guides.category.key-invalid")]
    [InlineData("guides.category.key-taken")]
    [InlineData("guides.verification.unavailable")]
    [InlineData("guides.verification.already-pending")]
    [InlineData("guides.verification.not-pending")]
    [InlineData("guides.verification.refused")]
    [InlineData("guides.vote.unchanged")]
    [InlineData("guides.bookmark.failed")]
    [InlineData("guides.report.failed")]
    [InlineData("guides.vote.self")]
    [InlineData("guides.suggestion.closed")]
    [InlineData("guides.suggestion.not-author")]
    [InlineData("guides.suggestion.self")]
    [InlineData("guides.translation.exists")]
    [InlineData("guides.translation.self")]
    [InlineData("guides.too-many")]
    public void Every_frozen_error_code_is_still_declared_verbatim(string code) =>
        DeclaredCodes().Should().Contain(code,
            "clients switch on these, support searches logs by them and translations key off them");

    [Fact]
    public void Every_error_the_module_can_return_carries_a_declared_code()
    {
        // A code assembled at a throw site is a code no translation table has, and no support
        // engineer can search for.
        var declared = DeclaredCodes();

        foreach (var error in DeclaredErrors())
        {
            declared.Should().Contain(error.Code);
        }
    }

    [Fact]
    public void Every_error_code_is_namespaced_to_this_module()
    {
        DeclaredCodes().Should().OnlyContain(code => code.StartsWith("guides.", StringComparison.Ordinal));
    }

    [Fact]
    public void No_two_failures_share_a_code()
    {
        var codes = DeclaredErrors().Select(error => error.Code).ToList();

        codes.Should().OnlyHaveUniqueItems(
            "a shared code makes two different failures indistinguishable to every client");
    }

    [Fact]
    public void A_stale_save_is_a_conflict_so_the_editor_can_offer_a_merge()
    {
        // The one status code that carries product meaning: a 409 tells the editor to reconcile,
        // where a 400 or a 500 tells it to give up and lose the author's work.
        GuideErrors.RevisionConflict.Type.Should().Be(ErrorType.Conflict);
        GuideErrors.RevisionConflict.Code.Should().Be(GuideErrorCodes.RevisionConflict);
    }

    // ── Namespacing of permissions, policies, features and routes ───────────────────────────

    [Fact]
    public void Every_permission_key_carries_the_module_prefix_with_a_colon()
    {
        Constants(typeof(GuidePermissions)).Should().OnlyContain(
            key => key.StartsWith("guides:", StringComparison.Ordinal),
            "permissions use colons, policies use dots, and the kernel relies on the difference");
    }

    [Fact]
    public void Every_policy_name_carries_the_module_prefix_with_a_dot()
    {
        Constants(typeof(GuidePolicies)).Should().OnlyContain(
            name => name.StartsWith("guides.", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_feature_gate_carries_the_module_prefix_with_a_dot()
    {
        Constants(typeof(GuideFeatures)).Should().OnlyContain(
            name => name.StartsWith("guides.", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_policy_has_a_permission_spelled_the_same_way_but_for_the_separator()
    {
        // The pairing is what makes a typo in either constant obvious rather than a route that
        // silently denies everybody forever.
        var permissions = Constants(typeof(GuidePermissions))
            .Select(key => key.Replace(':', '.'))
            .ToHashSet(StringComparer.Ordinal);

        Constants(typeof(GuidePolicies)).Should().BeSubsetOf(permissions);
    }

    [Fact]
    public void The_module_publishes_one_versioned_route_prefix()
    {
        GuidesModuleMetadata.RoutePrefix.Should().Be("/api/v1/guides",
            "a breaking change to the wire contract is a new prefix, not a mutation of this one");

        GuidesModuleMetadata.Name.Should().Be("Guides");
        GuidesModuleMetadata.Schema.Should().Be("guides");
    }

    private static HashSet<string> DeclaredCodes() =>
        Constants(typeof(GuideErrorCodes)).ToHashSet(StringComparer.Ordinal);

    private static List<Error> DeclaredErrors() =>
        typeof(GuideErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => (Error)field.GetValue(null)!)
            .ToList();

    private static List<string> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false }
                && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();
}
