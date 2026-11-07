namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Table and schema names for the identity module.
/// <para>
/// Identity tables live in their own schema so the modular monolith's boundary is visible
/// in the database as well as in the code — and so extracting the module later is a schema
/// move rather than an archaeology exercise.
/// </para>
/// </summary>
public static class IdentitySchema
{
    public const string Name = "identity";

    public const string Users = "users";
    public const string PasswordHistory = "user_password_history";
    public const string Sessions = "user_sessions";
    public const string RefreshTokens = "refresh_tokens";
    public const string Devices = "user_devices";
    public const string OtpChallenges = "otp_challenges";
    public const string SecurityEvents = "security_events";
    public const string AccountActionProposals = "account_action_proposals";
}
