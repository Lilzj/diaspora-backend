namespace Diaspora.Application.Exceptions;

public sealed class BusinessRuleViolationException : Exception
{
    public string RuleCode { get; }

    public BusinessRuleViolationException(string message, string ruleCode = "BusinessRule.Violation")
        : base(message)
    {
        RuleCode = ruleCode;
    }
}
