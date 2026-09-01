using Diaspora.Contracts.Identity.V1.Requests;
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
public abstract class IdentityTestBase<TFactory> : IClassFixture<TFactory>, IDisposable
    where TFactory : CustomWebApplicationFactory, new()
{
    private static int _phoneCounter;

    protected TFactory Factory { get; }

    protected HttpClient HttpClient { get; }

    protected IdentityApiClient Api { get; }

    protected IdentityTestBase(TFactory factory)
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
    protected async Task<string> RegisterAsync(string? phoneNumber = null, string password = ValidPassword)
    {
        var phone = phoneNumber ?? NextPhoneNumber();

        var response = await Api.RegisterAsync(RegistrationFor(phone, password));
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
        string? deviceName = null)
    {
        var phone = await RegisterAsync(phoneNumber, password);
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
        string? phoneNumber = null)
    {
        var (phone, auth) = await RegisterAndVerifyAsync(phoneNumber);

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
