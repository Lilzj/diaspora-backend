using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// A thin typed client over the identity endpoints.
/// <para>
/// Tests exercise the API the way a real client does — over HTTP, through the whole
/// middleware pipeline, authentication included. Reaching into MediatR directly would skip
/// exactly the layers most likely to be wrong.
/// </para>
/// </summary>
public sealed class IdentityApiClient
{
    private const string BaseUrl = "/api/v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public IdentityApiClient(HttpClient client)
    {
        _client = client;
    }

    public void Authenticate(string accessToken) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public void ClearAuthentication() => _client.DefaultRequestHeaders.Authorization = null;

    public Task<HttpResponseMessage> RegisterAsync(RegisterRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/register", request, JsonOptions);

    public Task<HttpResponseMessage> VerifyPhoneAsync(VerifyPhoneRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/verify-phone", request, JsonOptions);

    public Task<HttpResponseMessage> ResendOtpAsync(ResendOtpRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/resend-otp", request, JsonOptions);

    public Task<HttpResponseMessage> LoginAsync(LoginRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/login", request, JsonOptions);

    public Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _client.PostAsJsonAsync(
            $"{BaseUrl}/auth/refresh", new RefreshTokenRequest { RefreshToken = refreshToken }, JsonOptions);

    public Task<HttpResponseMessage> LogoutAsync(bool revokeAllSessions = false) =>
        _client.PostAsJsonAsync(
            $"{BaseUrl}/auth/logout", new LogoutRequest { RevokeAllSessions = revokeAllSessions }, JsonOptions);

    /// <summary>Recovery identifies the member by email; the code is delivered by SMS.</summary>
    public Task<HttpResponseMessage> ForgotPasswordAsync(string email) =>
        _client.PostAsJsonAsync(
            $"{BaseUrl}/auth/forgot-password", new ForgotPasswordRequest { Email = email }, JsonOptions);

    public Task<HttpResponseMessage> ResetPasswordAsync(ResetPasswordRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/reset-password", request, JsonOptions);

    public Task<HttpResponseMessage> ChangePasswordAsync(ChangePasswordRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/change-password", request, JsonOptions);

    public Task<HttpResponseMessage> GetCurrentUserAsync() =>
        _client.GetAsync($"{BaseUrl}/auth/me");

    public Task<HttpResponseMessage> GetSessionsAsync(bool includeRevoked = false) =>
        _client.GetAsync($"{BaseUrl}/auth/sessions?includeRevoked={includeRevoked}");

    public Task<HttpResponseMessage> RevokeSessionAsync(Guid sessionId) =>
        _client.DeleteAsync($"{BaseUrl}/auth/sessions/{sessionId}");

    public Task<HttpResponseMessage> RevokeAllOtherSessionsAsync() =>
        _client.DeleteAsync($"{BaseUrl}/auth/sessions");

    public Task<HttpResponseMessage> GetSecurityEventsAsync() =>
        _client.GetAsync($"{BaseUrl}/auth/security-events");

    public Task<HttpResponseMessage> GetAccessStateAsync() =>
        _client.GetAsync($"{BaseUrl}/access/state");

    public Task<HttpResponseMessage> GetFeaturesAsync() =>
        _client.GetAsync($"{BaseUrl}/access/features");

    public Task<HttpResponseMessage> SubmitVerificationAsync(string? notes = null) =>
        _client.PostAsJsonAsync(
            $"{BaseUrl}/access/verification-requests", new SubmitVerificationRequest { Notes = notes }, JsonOptions);

    public Task<HttpResponseMessage> ApplyTrustDecisionAsync(ApplyTrustDecisionRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/trust/decisions", request, JsonOptions);

    public Task<HttpResponseMessage> UpdateProfileAsync(UpdateProfileRequest request) =>
        _client.PutAsJsonAsync($"{BaseUrl}/auth/profile", request, JsonOptions);

    public Task<HttpResponseMessage> RegisterDeviceAsync(RegisterDeviceRequest request) =>
        _client.PostAsJsonAsync($"{BaseUrl}/auth/devices", request, JsonOptions);

    public Task<HttpResponseMessage> GetDevicesAsync() =>
        _client.GetAsync($"{BaseUrl}/auth/devices");

    /// <summary>
    /// Unwraps the platform's <c>ApiResponse&lt;T&gt;</c> envelope, failing loudly with the
    /// raw body when the response is not what the test expected.
    /// </summary>
    public static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Expected a success response but got {(int)response.StatusCode}. Body: {body}");
        }

        var envelope = JsonSerializer.Deserialize<ApiResponseEnvelope<T>>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize the response body: {body}");

        return envelope.Data;
    }

    public static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return await response.Content.ReadAsStringAsync();
    }

    private sealed record ApiResponseEnvelope<T>(T Data);
}
