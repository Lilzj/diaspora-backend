using Diaspora.Contracts.Identity.V1.Requests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Verification;
using Xunit;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// Shared setup for identity integration tests: a client per test, and helpers that walk a
/// member through registration and verification so each test starts from the state it
/// actually cares about.
/// </summary>
public abstract class IdentityTestBase : IdentityTestBase<CustomWebApplicationFactory>
{
    protected IdentityTestBase(CustomWebApplicationFactory factory) : base(factory) { }
}

/// <summary>
/// The same setup over a chosen host, with a host of its own.
/// <para>
/// <c>IClassFixture</c> lives here rather than on <see cref="IdentityTestCore{TFactory}"/>, which
/// is the whole difference between this and <see cref="PooledIdentityTestBase"/>: xUnit builds one
/// fixture per test class that asks for a class fixture, and building this application costs
/// <b>32 seconds</b> — measured, against 4 seconds for the schema and 7 for the reference data.
/// A class that does not need a host to itself should not pay that.
/// </para>
/// </summary>
public abstract class IdentityTestBase<TFactory> : IdentityTestCore<TFactory>, IClassFixture<TFactory>
    where TFactory : CustomWebApplicationFactory, new()
{
    protected IdentityTestBase(TFactory factory) : base(factory) { }
}

/// <summary>
/// The same setup over a host shared with the rest of its collection.
/// <para>
/// <b>123 test classes were building 123 applications.</b> At 32 seconds each that is roughly
/// 3,900 processor-seconds of construction before a single test body runs, and with eight of them
/// building at once on an eight-processor machine it is also where the suite's timeouts came from:
/// fifteen to twenty-four failures a run, every one a <c>TaskCanceledException</c>, never an
/// assertion, a different set each time.
/// </para>
/// <para>
/// A class using this base carries <c>[Collection(...)]</c> and gets the collection's one
/// application. <b>It also gets the collection's one database</b>, which is the thing to think
/// about before joining one: a test that assumes an empty table will find its neighbours' rows.
/// The suite is largely written for this already — <see cref="NextPhoneNumber"/> is a static
/// counter whose own note says the database is shared, and tests tag their data with a run
/// identifier — but "largely" is not "entirely", and each collection is proved by running it.
/// </para>
/// </summary>
public abstract class PooledIdentityTestBase : IdentityTestCore<CustomWebApplicationFactory>
{
    protected PooledIdentityTestBase(CustomWebApplicationFactory factory) : base(factory) { }
}

/// <summary>
/// The same setup over a chosen host.
/// <para>
/// <b>Generic only because one suite needs a different database arrangement, not a different
/// host.</b> The real-time tests hold an HTTP request open, which the single shared SQLite
/// connection cannot serve alongside a second request — see
/// <see cref="RealtimeWebApplicationFactory"/>. Everything else about the host, the registration
/// flow and the trust decisions is identical, and duplicating this class to change one line of
/// database setup is how two copies of a registration helper start to drift.
/// </para>
/// </summary>
public abstract class IdentityTestCore<TFactory> : IDisposable
    where TFactory : CustomWebApplicationFactory, new()
{
    private static int _phoneCounter;

    protected TFactory Factory { get; }

    protected HttpClient HttpClient { get; }

    protected IdentityApiClient Api { get; }

    protected IdentityTestCore(TFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        Factory = factory;

        // Starts the server; the schema can only be created once its provider exists.
        HttpClient = factory.CreateClient();
        factory.EnsureDatabaseCreated();

        Api = new IdentityApiClient(HttpClient);
    }

    protected const string ValidPassword = "Correct-Horse-Battery-7";

    /// <summary>
    /// A phone number unique to this call. The database is shared across a fixture's
    /// tests, and a duplicate number would collide on the unique index.
    /// </summary>
    protected static string NextPhoneNumber() =>
        $"+23480{Interlocked.Increment(ref _phoneCounter):D8}";

    /// <summary>
    /// The address that belongs to a test phone number.
    /// </summary>
    /// <remarks>
    /// Sign-in moved from a phone one-time code to an email address and a password, so
    /// every test that authenticates now needs an address as well as a number. Deriving
    /// one from the other keeps the existing tests intact: a test that only tracks the
    /// phone it registered can still compute the credential it has to sign in with, and
    /// the address inherits the number's per-call uniqueness.
    /// </remarks>
    protected static string EmailFor(string phoneNumber) =>
        $"member{phoneNumber.TrimStart('+')}@diaspora.test";

    protected static RegisterRequest RegistrationFor(string phoneNumber, string password = ValidPassword) =>
        new()
        {
            FullName = "Amara Okafor",
            PhoneNumber = phoneNumber,
            Email = EmailFor(phoneNumber),
            Password = password,
            Nationality = "NG",
            CountryOfResidence = "CA",
            PreferredLanguage = "en",
            PreferredTimeZone = "UTC"
        };

    /// <summary>Registers an account and leaves it awaiting phone verification.</summary>
    /// <remarks>
    /// <paramref name="countryOfResidence"/> overrides the suite's default (CA) for a test about
    /// what residence decides — a member's currency, for one.
    /// </remarks>
    protected async Task<string> RegisterAsync(
        string? phoneNumber = null,
        string password = ValidPassword,
        string? countryOfResidence = null,
        string? referralCode = null)
    {
        var phone = phoneNumber ?? NextPhoneNumber();

        var registration = RegistrationFor(phone, password) with { ReferralCode = referralCode };

        if (countryOfResidence is not null)
        {
            registration = registration with { CountryOfResidence = countryOfResidence };
        }

        var response = await Api.RegisterAsync(registration);
        await IdentityApiClient.ReadDataAsync<RegistrationResponse>(response);

        return phone;
    }

    /// <summary>
    /// Registers, verifies the phone, and returns the resulting authentication —
    /// a member holding limited access.
    /// </summary>
    protected async Task<(string PhoneNumber, AuthenticationResponse Auth)> RegisterAndVerifyAsync(
        string? phoneNumber = null,
        string password = ValidPassword,
        string? deviceIdentifier = null,
        string? deviceName = null,
        string? countryOfResidence = null,
        string? referralCode = null)
    {
        var phone = await RegisterAsync(phoneNumber, password, countryOfResidence, referralCode);
        var code = Factory.OtpCodes.LatestFor(phone, OtpPurpose.PhoneVerification);

        var response = await Api.VerifyPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = phone,
            Code = code,
            DeviceIdentifier = deviceIdentifier,
            DeviceName = deviceName,
            Platform = "Android",
            AppVersion = "1.0.0"
        });

        var auth = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(response);
        return (phone, auth);
    }

    /// <summary>
    /// Registers, verifies, requests verification and applies a granted trust decision —
    /// the only legitimate route to a verified member.
    /// </summary>
    protected async Task<(string PhoneNumber, AuthenticationResponse Auth)> RegisterVerifiedMemberAsync(
        string? phoneNumber = null, string? countryOfResidence = null, string? referralCode = null)
    {
        var (phone, auth) = await RegisterAndVerifyAsync(
            phoneNumber, countryOfResidence: countryOfResidence, referralCode: referralCode);

        Api.Authenticate(auth.Tokens.AccessToken);
        await Api.SubmitVerificationAsync();
        Api.ClearAuthentication();

        await ApplyTrustDecisionAsync(auth.User.Id, "UnderReview");
        await ApplyTrustDecisionAsync(auth.User.Id, "Granted");

        // Re-authenticate so the returned tokens carry the new access state.
        var loginResponse = await Api.LoginAsync(new LoginRequest
        {
            Email = EmailFor(phone),
            Password = ValidPassword
        });

        var refreshed = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(loginResponse);
        return (phone, refreshed);
    }

    /// <summary>
    /// A real member, given a platform role in the test database, signed in again.
    /// <para>
    /// <b>The way to test a staff path over HTTP.</b> A token assembled in the test process carries
    /// a session identifier that belongs to no session, and every authenticated request is checked
    /// against live session state — so it is refused at the door, before any gate it was minted to
    /// exercise. This produces a caller the platform recognises: a real account, a real session, and
    /// the <c>rol</c> and <c>perm</c> claims its role actually earns.
    /// </para>
    /// <para>
    /// <b>The role is written straight onto the account (since 7 Nov 2026).</b> It used to go through
    /// <c>AssignRoleCommand</c>, with a second member holding a borrowed permission as the appointer.
    /// The platform's route now reads the appointer's rank from their account and refuses anybody not
    /// above the member, and granting Admin needs a second member of staff to confirm it — two staff
    /// accounts the suite does not have before this makes its first. The route itself, rank, proposal
    /// and confirmation, is proved in <c>AccountAdministrationTests</c>; this only seats a caller.
    /// The write raises no event, so the seated member is not sent a role notice either.
    /// </para>
    /// </summary>
    protected async Task<(Guid UserId, AuthenticationResponse Auth)> SignInAsRoleAsync(string role)
    {
        var (phone, member) = await RegisterVerifiedMemberAsync();

        await SeatRoleAsync(member.User.Id, role);

        // The token from registration carries the role the member had then; a fresh sign-in is the
        // only way to hold the new claims.
        var signedIn = await Api.LoginAsync(new LoginRequest
        {
            Email = EmailFor(phone),
            Password = ValidPassword
        });

        var refreshed = await IdentityApiClient.ReadDataAsync<AuthenticationResponse>(signedIn);

        Api.Authenticate(refreshed.Tokens.AccessToken);
        HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", refreshed.Tokens.AccessToken);

        return (member.User.Id, refreshed);
    }

    /// <summary>
    /// Writes a platform role onto an account in the test database, with no event and no notice.
    /// The caller signs the member in again to hold the role's claims.
    /// </summary>
    protected async Task SeatRoleAsync(Guid userId, string role)
    {
        var userRole = Diaspora.Domain.Identity.Users.UserRole.All.Single(candidate => candidate.Name == role);
        var id = Diaspora.SharedKernel.Identity.UserId.From(userId);

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Diaspora.Persistence.DiasporaDbContext>();

        var updated = await context.Set<Diaspora.Domain.Identity.Users.User>()
            .Where(user => user.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.Role, userRole));

        updated.Should().Be(1, $"the suite cannot test a {role} path without one");
    }

    /// <summary>
    /// Calls the Trust Engine endpoint using a token minted with the trust permission —
    /// the credential B3 would hold. No human role can obtain it.
    /// </summary>
    protected async Task<HttpResponseMessage> ApplyTrustDecisionAsync(
        Guid userId,
        string outcome,
        string? reason = null)
    {
        var trustToken = TrustEngineTokenFactory.Create(userId);

        using var client = Factory.CreateClient();
        var trustApi = new IdentityApiClient(client);
        trustApi.Authenticate(trustToken);

        return await trustApi.ApplyTrustDecisionAsync(new ApplyTrustDecisionRequest
        {
            UserId = userId,
            Outcome = outcome,
            Reason = reason,
            DecidedBy = "integration-test"
        });
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            HttpClient.Dispose();
        }
    }
}
