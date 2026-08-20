using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Devices;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Devices.UpdatePushToken;

public sealed class UpdatePushTokenCommandValidator : IdentityValidator<UpdatePushTokenCommand>
{
    public UpdatePushTokenCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.DeviceId)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.DeviceIdentifierRequired));

        RuleFor(command => command.PushToken)
            .MaximumLength(UserDevice.MaxPushTokenLength)
            .WithMessage(Message(IdentityMessageKeys.PushTokenLength))
            .When(command => !string.IsNullOrWhiteSpace(command.PushToken));
    }
}
