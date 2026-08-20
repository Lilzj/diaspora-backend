namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>The outcome of one call to a gateway. Several per intent is normal.</summary>
public enum PaymentAttemptStatus
{
    Initiated = 1,
    Succeeded = 2,
    Failed = 3,

    /// <summary>The issuer wants the payer to do something — 3-D Secure, an OTP, a redirect.</summary>
    RequiresAction = 4,

    Abandoned = 5
}
