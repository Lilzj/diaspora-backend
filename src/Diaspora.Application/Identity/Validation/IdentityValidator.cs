using Diaspora.Application.Identity.Abstractions;
using FluentValidation;

namespace Diaspora.Application.Identity.Validation;

/// <summary>
/// Base class for identity validators. Supplies the localisation helpers so every
/// validator states its rules in message keys rather than English literals.
/// </summary>
public abstract class IdentityValidator<T> : AbstractValidator<T>
{
    private readonly ILocalizedMessageProvider _messages;

    protected IdentityValidator(ILocalizedMessageProvider messages)
    {
        _messages = messages;
    }

    /// <summary>Resolves a message key in the caller's language.</summary>
    protected string Message(string key) => _messages.GetMessage(key);

    /// <summary>Resolves a parameterised message key in the caller's language.</summary>
    protected string Message(string key, params object[] arguments) => _messages.GetMessage(key, arguments);
}
