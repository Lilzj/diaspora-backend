using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// BBL-004. Who may read a receipt.
/// <para>
/// <b>Before this, any member holding <c>GivingRead</c> could read any receipt.</b> The reference
/// was thirty bits, so finding a given day's receipts cost about a million requests and harvesting
/// the day's population was an afternoon. What that yields is not personal data — the response
/// carries no donor and no message — but the platform's funding intelligence: which causes attract
/// money, in what amounts, from where.
/// </para>
/// <para>
/// <b>The route stays unscoped for the one case that needs it.</b> An anonymous gift stores no
/// giver, completes asynchronously after its payment, appears in no history and generates no
/// notification, so this route is the only way its giver can ever learn the money arrived. Those
/// receipts are reached by reference alone and their references are minted at 130 bits.
/// </para>
/// </summary>
public sealed class ReceiptAccessTests : CampaignTestBase<CustomWebApplicationFactory>
{
    public ReceiptAccessTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    /// <summary>
    /// The finding's first verification criterion. Two real members, two real donations through
    /// <c>POST /financial/donations</c>, and the receipt each one may read.
    /// </summary>
    [Fact]
    public async Task A_member_reads_their_own_receipt_and_nobody_elses()
    {
        var (_, firstToken) = await SignedInMemberAsync();
        var mine = await DonateForReceiptAsync();

        ActAs(firstToken);

        (await GetReceiptAsync(mine)).StatusCode.Should().Be(
            HttpStatusCode.OK, "the donor reads their own receipt");

        var (_, secondToken) = await SignedInMemberAsync();

        ActAs(secondToken);

        (await GetReceiptAsync(mine)).StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a second verified member holds GivingRead and is still not this receipt's owner");
    }

    /// <summary>
    /// <b>The refusal cannot be told apart from a reference that was never issued.</b> A caller who
    /// can distinguish "not yours" from "no such thing" has an existence oracle, and enumeration
    /// survives a fix that was supposed to end it — slower, because it yields references rather than
    /// receipts, but it still maps the population.
    /// <para>
    /// <c>instance</c>, <c>correlationId</c>, <c>traceId</c> and <c>timestamp</c> are excluded: the
    /// first is the path the caller asked for and the other three differ on every request, so none
    /// of them can carry the answer.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_refusal_is_indistinguishable_from_a_reference_that_was_never_issued()
    {
        await SignedInMemberAsync();
        var somebodyElses = await DonateForReceiptAsync();

        var (_, token) = await SignedInMemberAsync();

        ActAs(token);

        var refused = await GetReceiptAsync(somebodyElses);
        var absent = await GetReceiptAsync("DON-20261117-ZZZZZZ");

        refused.StatusCode.Should().Be(HttpStatusCode.NotFound);
        absent.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var refusedBody = await DistinguishingFieldsAsync(refused);
        var absentBody = await DistinguishingFieldsAsync(absent);

        refusedBody.Should().NotBeEmpty("the comparison below proves nothing over an empty document");
        refusedBody.Should().BeEquivalentTo(
            absentBody,
            "every field a caller can read must say the same thing, or the difference is the oracle");
    }

    /// <summary>
    /// The second half of the design: an anonymous gift's receipt is reachable by its reference by
    /// anybody who holds it, because nothing else will ever reach it — and that reference is
    /// therefore minted as a credential rather than as a handle.
    /// </summary>
    [Fact]
    public async Task An_anonymous_receipt_is_reached_by_a_reference_long_enough_to_be_a_credential()
    {
        await SignedInMemberAsync();

        var anonymous = await DonateForReceiptAsync(isAnonymous: true);

        var suffix = anonymous.Split('-')[2];

        // Five bits a character: the alphabet is 32 symbols drawn uniformly. Spelled out here
        // rather than read from the generator's own constant, so that this test measures the
        // string that came back over HTTP instead of agreeing with the code that made it.
        (suffix.Length * 5).Should().BeGreaterThanOrEqualTo(
            128,
            "no owner is stored, so this string is the whole of the access decision. Reference "
            + "was: {0} characters",
            suffix.Length);

        var (_, otherToken) = await SignedInMemberAsync();

        ActAs(otherToken);

        (await GetReceiptAsync(anonymous)).StatusCode.Should().Be(
            HttpStatusCode.OK,
            "deliberate: a gift with no giver cannot be scoped to one, and its giver's client is "
            + "the only holder of this reference");
    }

    /// <summary>
    /// And the legibility requirement BBL-004 was right to protect survives for the receipts that
    /// actually need it — the ones somebody can ring up about, which are the ones support can see.
    /// </summary>
    [Fact]
    public async Task An_owned_receipt_keeps_a_reference_a_person_can_read_aloud()
    {
        await SignedInMemberAsync();

        var owned = await DonateForReceiptAsync();

        var parts = owned.Split('-');

        parts.Should().HaveCount(3);
        parts[2].Should().HaveLength(
            6, "ownership decides access here, so the reference guards nothing and stays short");

        // The suffix alone. The prefix has an O in it and the date is full of noughts and ones —
        // neither is read back to the platform, so neither has to survive being misheard.
        parts[2].Should().NotContainAny("I", "O", "0", "1");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A donation to the platform, pledged. No capture: the reference is issued at the pledge, and
    /// what is under test is who may read it rather than whether the money moved.
    /// </summary>
    private async Task<string> DonateForReceiptAsync(bool isAnonymous = false)
    {
        var response = await HttpClient.PostAsJsonAsync(
            new Uri($"{Financial}/donations", UriKind.Relative),
            new
            {
                amount = 10m,
                currency = "GBP",
                isAnonymous,
                methodKind = "Card",
                idempotencyKey = Guid.NewGuid().ToString("N")
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the donation itself must succeed");

        var receipt = await ReadDataAsync<ReceiptShape>(response);

        receipt.ReceiptReference.Should().NotBeNullOrWhiteSpace();

        return receipt.ReceiptReference;
    }

    private Task<HttpResponseMessage> GetReceiptAsync(string reference) =>
        HttpClient.GetAsync(new Uri($"{Financial}/receipts/{reference}", UriKind.Relative));

    /// <summary>
    /// Everything in a problem document that a caller could read an answer out of. The four
    /// excluded fields are named in the test that uses this, with why.
    /// </summary>
    private static async Task<Dictionary<string, string>> DistinguishingFieldsAsync(
        HttpResponseMessage response)
    {
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        var ignored = new[] { "instance", "correlationId", "traceId", "timestamp" };

        return document
            .EnumerateObject()
            .Where(property => !ignored.Contains(property.Name, StringComparer.Ordinal))
            .ToDictionary(
                property => property.Name,
                property => property.Value.ToString(),
                StringComparer.Ordinal);
    }

    private sealed record ReceiptShape(string ReceiptReference);
}
