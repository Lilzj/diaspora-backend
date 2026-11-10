using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The pre-launch audit's seventy findings, and the rules that stop one being skipped.
/// <para>
/// <b>The audit itself is not committed.</b> <c>.gitignore</c> excludes <c>docs/</c> entirely, so
/// the nine reports of 9 November 2026 exist only in a working tree. That is the repository's
/// convention and this suite does not change it — the reports are evidence, they do not change, and
/// a reader who needs the detail opens them. What cannot live there is the <i>state</i>: which
/// findings are closed, on what proof, and what is still owed. A remediation programme whose only
/// record is deleted by <c>git clean -xdf</c> has no record.
/// </para>
/// <para>
/// So <c>PrelaunchFindings.csv</c> sits beside this file, tracked, and is read from the source tree
/// rather than copied as content — the same technique <see cref="SourceTree"/> uses, and for the
/// same reason: an edit to the ledger is visible to the next test run with no build step in between.
/// </para>
/// <para>
/// <b>These are plain <c>[Fact]</c>s, never <see cref="DocumentationFactAttribute"/>.</b> That is
/// the whole point and it is worth stating, because this repository has already paid for the
/// alternative: nineteen rules once read <c>docs/</c> and so passed vacuously on every clean
/// checkout — "every one asserting a file exists that the checkout was never going to contain, and
/// nothing they guard was checked" (<see cref="LocalDocumentation"/>). Eight rules are still gated
/// that way today. A ledger guard that skipped in CI would be worse than no guard, because it would
/// report success.
/// </para>
/// <para>
/// <b>Closure needs a test, not a sentence.</b> XC-007 — "documentation used as mechanism" — is
/// named in the audit as the root cause behind a third of its own findings: BSEC-003 is a comment
/// claiming a guardrail nobody wrote, BSEC-001 was two comments asserting the opposite of the
/// behaviour. Remediation that closed findings on prose would reproduce the defect it was fixing, so
/// <see cref="A_closed_backend_finding_names_a_test_that_exists"/> resolves the named test against
/// the source tree and fails when it does not exist.
/// </para>
/// <para>
/// <b>What this suite cannot check.</b> Thirty-three of the seventy findings live in the frontend
/// repository, which this assembly cannot see. Their evidence is recorded with a <c>frontend:</c>
/// prefix so that it reads as unverified-here rather than as a resolved reference, and it is checked
/// for shape only. That half needs an equivalent guard in its own repository; FSEC-007 and
/// FARCH-001 — no linter, no circular-dependency detection, no bundle analysis — are the reason it
/// does not have one yet.
/// </para>
/// </summary>
public sealed class PrelaunchRemediationArchitectureTests
{
    private const string LedgerPath = "tests/Diaspora.ArchitectureTests/PrelaunchFindings.csv";

    /// <summary>
    /// The audit's identifier space, declared here rather than counted from the ledger.
    /// <para>
    /// This is the anti-skip mechanism. Deleting an inconvenient row is the realistic way a finding
    /// goes missing — not forgetfulness, and not a decision anyone would defend out loud. Counting
    /// the rows and comparing them to themselves would accept that silently. These numbers come from
    /// each report's own findings table and from the index's severity summary (70 findings, 9 HIGH),
    /// and changing one is a deliberate act in tracked code.
    /// </para>
    /// </summary>
    private static readonly (string Prefix, int Count)[] Families =
    [
        ("BSEC", 13),
        ("BSOLID", 8),
        ("BBL", 7),
        ("BARCH", 9),
        ("FSEC", 11),
        ("FSOLID", 8),
        ("FBL", 7),
        ("FARCH", 7)
    ];

    private static readonly string[] States =
        ["OPEN", "FIXED", "VERIFIED", "ACCEPTED", "SUPERSEDED"];

    private static readonly string[] Severities =
        ["HIGH", "MEDIUM-HIGH", "MEDIUM", "LOW-HIGH", "LOW", "INFORMATIONAL"];

    private static readonly Lazy<List<Finding>> Ledger = new(LoadLedger);

    private static readonly Lazy<Dictionary<string, HashSet<string>>> DeclaredTests =
        new(LoadDeclaredTests);

    // ─── The ledger holds every finding, and only those findings ────────────────────────────

    /// <summary>
    /// Every identifier the audit issued has exactly one row, and the ledger invents none.
    /// </summary>
    [Fact]
    public void Every_finding_the_audit_raised_has_exactly_one_row()
    {
        var expected = Families
            .SelectMany(family => Enumerable
                .Range(1, family.Count)
                .Select(n => $"{family.Prefix}-{n:D3}"))
            .ToHashSet(StringComparer.Ordinal);

        var actual = Ledger.Value.Select(finding => finding.Id).ToList();

        actual.Should().OnlyHaveUniqueItems("a finding tracked twice can be closed once and left open once");

        expected.Except(actual).Should().BeEmpty(
            "a finding with no row is a finding nobody is accounting for — restore the row rather "
            + "than the count, and if the audit genuinely retired it, record it as SUPERSEDED with "
            + "a date and a reason");

        actual.Except(expected).Should().BeEmpty(
            "{0} describes seventy findings and this ledger tracks something it does not define",
            LedgerPath);

        actual.Should().HaveCount(70);
    }

    /// <summary>
    /// The nine HIGH findings and all eight launch blockers are still accounted for.
    /// <para>
    /// Severity is the field most likely to be quietly softened under schedule pressure, and the
    /// blocker numbering is what the launch decision is actually made against.
    /// </para>
    /// </summary>
    [Fact]
    public void The_nine_high_findings_and_all_eight_launch_blockers_are_present()
    {
        Ledger.Value.Where(finding => finding.Severity == "HIGH")
            .Should().HaveCount(9, "the index records nine HIGH findings across eight distinct issues");

        Ledger.Value
            .Where(finding => finding.Blocker is not null)
            .Select(finding => finding.Blocker!.Value)
            .Distinct()
            .OrderBy(blocker => blocker)
            .Should().Equal([1, 2, 3, 4, 5, 6, 7, 8],
                "the eight numbered launch blockers in the index's section 4");
    }

    /// <summary>Every field carries a value this suite and a human can both read.</summary>
    [Fact]
    public void Every_row_is_well_formed()
    {
        var ledger = Ledger.Value;

        ledger.Where(finding => !States.Contains(finding.State, StringComparer.Ordinal))
            .Select(finding => $"{finding.Id}={finding.State}")
            .Should().BeEmpty("a state outside {0} is not a state anything can act on",
                string.Join("/", States));

        ledger.Where(finding => !Severities.Contains(finding.Severity, StringComparer.Ordinal))
            .Select(finding => $"{finding.Id}={finding.Severity}")
            .Should().BeEmpty("severity comes from the owning report's findings table");

        ledger.Where(finding => finding.Wave is < 0 or > 4)
            .Select(finding => $"{finding.Id}={finding.Wave}")
            .Should().BeEmpty("the roadmap has five waves, 0 to 4");

        ledger.Where(finding => finding.Repo is not ("backend" or "frontend"))
            .Select(finding => $"{finding.Id}={finding.Repo}")
            .Should().BeEmpty("the audit covered two repositories and a finding belongs to one");

        ledger.Where(finding => finding.Title.Length < 20)
            .Select(finding => finding.Id)
            .Should().BeEmpty("a row nobody can read without opening the report will not be read");
    }

    // ─── Closure requires evidence ──────────────────────────────────────────────────────────

    /// <summary>
    /// An OPEN finding claims nothing. The reverse of the closure rule, and the cheaper half: a row
    /// carrying evidence while still OPEN is a closure somebody forgot to finish recording.
    /// </summary>
    [Fact]
    public void An_open_finding_carries_no_evidence_and_no_date()
    {
        Ledger.Value
            .Where(finding => finding.State == "OPEN")
            .Where(finding => finding.Evidence.Length > 0 || finding.Decided.Length > 0)
            .Select(finding => finding.Id)
            .Should().BeEmpty(
                "this row is OPEN but already names evidence or a date — either the fix landed and "
                + "the state was never advanced, or the evidence belongs to a different finding");
    }

    /// <summary>
    /// Anything no longer OPEN says what settled it and when.
    /// <para>
    /// The date is not bookkeeping. The outbox lesson in this repository was that a closure recorded
    /// only in the phase that performed it becomes invisible to everyone who reads the finding
    /// later; a dated row is the annotation that survives.
    /// </para>
    /// </summary>
    [Fact]
    public void A_finding_that_is_no_longer_open_records_evidence_and_the_date_it_changed()
    {
        var closed = Ledger.Value.Where(finding => finding.State != "OPEN").ToList();

        closed.Should().NotBeEmpty("BSEC-001 was closed on 9 November 2026");

        closed.Where(finding => finding.Evidence.Length == 0)
            .Select(finding => $"{finding.Id} ({finding.State})")
            .Should().BeEmpty("a state change with no evidence is the prose closure XC-007 names");

        closed.Where(finding => !IsIsoDate(finding.Decided))
            .Select(finding => $"{finding.Id}={finding.Decided}")
            .Should().BeEmpty("Decided is the yyyy-MM-dd date this row last changed state");

        closed.Where(finding => finding.State == "ACCEPTED")
            .Where(finding => !finding.Evidence.StartsWith("decision:", StringComparison.Ordinal))
            .Select(finding => finding.Id)
            .Should().BeEmpty(
                "ACCEPTED means somebody with the authority to carry the risk decided to carry it, "
                + "so the evidence is the decision and its compensating control — see the index's "
                + "Wave 0 item 4, which asks exactly this of BSEC-007");

        closed.Where(finding => finding.State == "SUPERSEDED")
            .Where(finding => !finding.Evidence.StartsWith("superseded:", StringComparison.Ordinal))
            .Select(finding => finding.Id)
            .Should().BeEmpty(
                "re-reading a finding legitimately changes its classification — PSR-1 re-read six "
                + "debt items and reclassified four — but the reason has to be written down, or the "
                + "row is indistinguishable from one that was dropped");
    }

    /// <summary>
    /// A closed backend finding names a test, and the test exists.
    /// <para>
    /// This is XC-007 turned into a rule the build can apply. BSEC-001 set the standard: twenty-one
    /// tests for a mechanism that had none, after two comments had asserted the opposite of its
    /// behaviour for a phase. Resolved against the source tree rather than by reflection, because
    /// the evidence lives in sibling test projects this assembly deliberately does not reference.
    /// </para>
    /// <para>
    /// Where a finding genuinely cannot be closed by a test — rotating a credential, a recorded
    /// decision, a probe against a deployed host — the evidence uses the <c>probe:</c>,
    /// <c>decision:</c> or <c>superseded:</c> prefix instead and is checked for shape only. That is
    /// a narrower exemption than it looks: it still requires a date, and it still requires somebody
    /// to have written what was done.
    /// </para>
    /// </summary>
    [Fact]
    public void A_closed_backend_finding_names_a_test_that_exists()
    {
        var unresolved = new List<string>();

        foreach (var finding in Ledger.Value.Where(IsClosedByCode))
        {
            var references = finding.Evidence
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (finding.Repo == "backend" && !references.Exists(IsTestReference))
            {
                unresolved.Add(
                    $"{finding.Id}: {finding.State} with no test: reference ({finding.Evidence})");
                continue;
            }

            unresolved.AddRange(references
                .Where(IsTestReference)
                .Where(reference => !Resolves(reference))
                .Select(reference => $"{finding.Id}: {reference} does not exist under tests/"));

            unresolved.AddRange(references
                .Where(reference => !IsKnownEvidenceForm(reference))
                .Select(reference => $"{finding.Id}: {reference} is not a recognised evidence form"));
        }

        unresolved.Should().BeEmpty(
            "a finding closes on something that executes, or it does not close. Evidence is "
            + "semicolon-separated and each entry is test:Type, test:Type.Method, probe:what-was-run, "
            + "decision:what-was-decided, superseded:why, or frontend:where — and a backend finding "
            + "in FIXED or VERIFIED needs at least one test: that resolves");
    }

    // ─── Order ──────────────────────────────────────────────────────────────────────────────

    /// <summary>A dependency names a row in this ledger.</summary>
    [Fact]
    public void A_blocked_by_reference_names_a_row_in_this_ledger()
    {
        var ids = Ledger.Value.Select(finding => finding.Id).ToHashSet(StringComparer.Ordinal);

        Ledger.Value
            .SelectMany(finding => finding.BlockedBy.Select(blocker => (finding.Id, blocker)))
            .Where(pair => !ids.Contains(pair.blocker))
            .Select(pair => $"{pair.Id} is blocked by {pair.blocker}, which is not a finding")
            .Should().BeEmpty();

        Ledger.Value
            .Where(finding => finding.BlockedBy.Contains(finding.Id, StringComparer.Ordinal))
            .Select(finding => finding.Id)
            .Should().BeEmpty("a finding cannot block itself");
    }

    /// <summary>
    /// Nothing is VERIFIED ahead of what it depends on.
    /// <para>
    /// The index states the one ordering rule that matters most in its own words: "1 before 2 (a
    /// limiter with a forgeable key is not a limiter)".
    /// <para>
    /// Updated 10 Nov 2026, because this paragraph used to read "BSEC-002 is not started" and that
    /// is the sentence this rule exists to outlive. Rate limiting has since been extended across
    /// the whole route table and BSEC-002 is FIXED, so the rule now has something to hold: both
    /// findings are FIXED and neither is deployed, and the first attempt to call the coverage
    /// VERIFIED will fail here until the forgeable partition key it rests on is verified too.
    /// Fixing a limiter's reach proves nothing while its key can still be chosen by the caller.
    /// </para>
    /// </para>
    /// </summary>
    [Fact]
    public void A_verified_finding_is_not_blocked_by_one_that_is_unverified()
    {
        var byId = Ledger.Value.ToDictionary(finding => finding.Id, StringComparer.Ordinal);

        Ledger.Value
            .Where(finding => finding.State == "VERIFIED")
            .SelectMany(finding => finding.BlockedBy.Select(blocker => (finding.Id, blocker)))
            .Where(pair => byId.TryGetValue(pair.blocker, out var blocker)
                && blocker.State is not ("VERIFIED" or "ACCEPTED" or "SUPERSEDED"))
            .Select(pair => $"{pair.Id} is VERIFIED but {pair.blocker} is "
                + $"{byId[pair.blocker].State}")
            .Should().BeEmpty(
                "verifying a finding whose prerequisite is unverified proves less than it appears "
                + "to — the thing it was built on can still be false");
    }

    // ─── Reading the ledger ─────────────────────────────────────────────────────────────────

    private static bool IsClosedByCode(Finding finding) =>
        finding.State is "FIXED" or "VERIFIED";

    private static bool IsTestReference(string reference) =>
        reference.StartsWith("test:", StringComparison.Ordinal);

    private static bool IsKnownEvidenceForm(string reference) =>
        reference.StartsWith("test:", StringComparison.Ordinal)
        || reference.StartsWith("probe:", StringComparison.Ordinal)
        || reference.StartsWith("decision:", StringComparison.Ordinal)
        || reference.StartsWith("superseded:", StringComparison.Ordinal)
        || reference.StartsWith("frontend:", StringComparison.Ordinal);

    private static bool Resolves(string reference)
    {
        var target = reference["test:".Length..];
        var separator = target.LastIndexOf('.');

        if (separator < 0)
        {
            return DeclaredTests.Value.ContainsKey(target);
        }

        return DeclaredTests.Value.TryGetValue(target[..separator], out var methods)
            && methods.Contains(target[(separator + 1)..]);
    }

    private static bool IsIsoDate(string value) =>
        DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static List<Finding> LoadLedger()
    {
        var path = Path.Combine(SourceTree.Root.FullName, LedgerPath);

        File.Exists(path).Should().BeTrue(
            "{0} is the remediation ledger and is tracked precisely so that it is here", LedgerPath);

        var rows = ParseCsv(File.ReadAllText(path));

        rows.Should().HaveCountGreaterThan(1, "the ledger has a header and at least one finding");

        rows[0].Should().Equal(
            ["Id", "Severity", "Wave", "Blocker", "Repo", "Title",
                "State", "BlockedBy", "Evidence", "Decided"],
            "this suite reads the columns positionally");

        return rows.Skip(1).Select(Row).ToList();
    }

    private static Finding Row(string[] fields)
    {
        fields.Should().HaveCount(10, "row '{0}' does not have ten fields", string.Join('|', fields));

        return new Finding(
            Id: fields[0],
            Severity: fields[1],
            Wave: int.Parse(fields[2], CultureInfo.InvariantCulture),
            Blocker: fields[3].Length == 0
                ? null
                : int.Parse(fields[3], CultureInfo.InvariantCulture),
            Repo: fields[4],
            Title: fields[5],
            State: fields[6],
            BlockedBy: fields[7]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Evidence: fields[8],
            Decided: fields[9]);
    }

    /// <summary>
    /// Enough of RFC 4180 for this file: quoted fields, doubled quotes inside them, CRLF or LF.
    /// Hand-written rather than taken as a dependency, because the ledger is one file this suite
    /// owns and a parser is cheaper than a package the audit would then have to account for.
    /// </summary>
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];

            if (quoted)
            {
                if (character != '"')
                {
                    field.Append(character);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    rows.Add([.. fields]);
                    fields.Clear();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add([.. fields]);
        }

        return rows;
    }

    /// <summary>
    /// Every type declared under <c>tests/</c>, with its methods, parsed from source.
    /// <para>
    /// Local to this suite rather than added to <see cref="SourceTree"/>, which loads <c>src/</c>
    /// and is shared by a dozen rules that have no interest in test code.
    /// </para>
    /// </summary>
    private static Dictionary<string, HashSet<string>> LoadDeclaredTests()
    {
        var root = new DirectoryInfo(Path.Combine(SourceTree.Root.FullName, "tests"));
        var declared = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var file in root.EnumerateFiles("*.cs", SearchOption.AllDirectories))
        {
            if (file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file.FullName)).GetRoot();

            foreach (var type in syntax.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!declared.TryGetValue(type.Identifier.ValueText, out var methods))
                {
                    methods = new HashSet<string>(StringComparer.Ordinal);
                    declared[type.Identifier.ValueText] = methods;
                }

                methods.UnionWith(type.Members
                    .OfType<MethodDeclarationSyntax>()
                    .Select(method => method.Identifier.ValueText));
            }
        }

        return declared;
    }

    private sealed record Finding(
        string Id,
        string Severity,
        int Wave,
        int? Blocker,
        string Repo,
        string Title,
        string State,
        string[] BlockedBy,
        string Evidence,
        string Decided);
}
