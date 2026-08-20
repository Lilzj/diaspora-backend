using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Devices;
using FluentValidation;

namespace Diaspora.Application.Identity.Commands.Devices.RegisterDevice;

public sealed class RegisterDeviceCommandValidator : IdentityValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator(ILocalizedMessageProvider messages) : base(messages)
    {
        RuleFor(command => command.DeviceIdentifier)
            .NotEmpty().WithMessage(Message(IdentityMessageKeys.DeviceIdentifierRequired))
            .MaximumLength(UserDevice.MaxIdentifierLength)
            .WithMessage(Message(IdentityMessageKeys.DeviceIdentifierLength));

        RuleFor(command => command.Platform)
            .Must(IdentityValueParser.IsKnownPlatform)
            .WithMessage(Message(IdentityMessageKeys.PlatformUnknown));

        RuleFor(command => command.PushToken)
            .MaximumLength(UserDevice.MaxPushTokenLength)
            .WithMessage(Message(IdentityMessageKeys.PushTokenLength))
            .When(command => !string.IsNullOrWhiteSpace(command.PushToken));
    }
}
