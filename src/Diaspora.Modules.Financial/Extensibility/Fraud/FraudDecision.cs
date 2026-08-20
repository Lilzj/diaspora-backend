namespace Diaspora.Modules.Financial.Extensibility.Fraud;

/// <summary>What to do about a payment a fraud assessor has looked at.</summary>
public enum FraudDecision
{
    /// <summary>Proceed.</summary>
    Allow = 1,

    /// <summary>Proceed, and mark it for a human to look at afterwards.</summary>
    Review = 2,

    /// <summary>Refuse. The payment is not attempted.</summary>
    Refuse = 3
}
