using System.Linq;
using Microsoft.CodeAnalysis;

namespace Diaspora.Analyzers;

/// <summary>
/// The platform's own types, resolved once per compilation.
/// <para>
/// <b>By metadata name, because an analyzer cannot reference the platform.</b> This assembly
/// targets netstandard2.0 so the compiler can load it; the platform targets net10.0. Naming the
/// types as strings is the standard consequence, and it has one useful property — a project that
/// does not reference the Application layer resolves nothing here and the analyzer costs it
/// nothing.
/// </para>
/// <para>
/// Everything is nullable and every caller checks. A missing symbol means "this rule does not
/// apply to this project", never "assume the worst".
/// </para>
/// </summary>
internal sealed class PlatformSymbols
{
    private PlatformSymbols(Compilation compilation)
    {
        DomainEventHandlerBase = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.DomainEventHandler`1");
        IntegrationEventHandlerBase = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.IntegrationEventHandler`1");
        DomainEventNotification = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.DomainEventNotification`1");
        IntegrationEventNotification = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.IntegrationEventNotification`1");
        CommandHandler = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.ICommandHandler`1");
        CommandHandlerWithResponse = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.ICommandHandler`2");
        QueryHandler = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.IQueryHandler`2");
        TransactionalAttribute = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.TransactionalAttribute");
        SideEffectingAttribute = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.SideEffectingAttribute");

        Command = compilation.GetTypeByMetadataName("Diaspora.Application.Abstractions.ICommand");
        CommandWithResponse = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.ICommand`1");
        Query = compilation.GetTypeByMetadataName("Diaspora.Application.Abstractions.IQuery`1");

        RequirePermissionAttribute = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.RequirePermissionAttribute");
        RequirePolicyAttribute = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.RequirePolicyAttribute");
        RequireAuthenticationAttribute = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.RequireAuthenticationAttribute");
        AllowAnonymousAttribute = compilation.GetTypeByMetadataName(
            "Diaspora.Application.Abstractions.AllowAnonymousAttribute");

        NotificationHandler = compilation.GetTypeByMetadataName("MediatR.INotificationHandler`1");
        Mediator = compilation.GetTypeByMetadataName("MediatR.IMediator");
        Sender = compilation.GetTypeByMetadataName("MediatR.ISender");
        Publisher = compilation.GetTypeByMetadataName("MediatR.IPublisher");

        DomainEvent = compilation.GetTypeByMetadataName("Diaspora.SharedKernel.Primitives.IDomainEvent");
        IntegrationEvent = compilation.GetTypeByMetadataName("Diaspora.Contracts.Events.IIntegrationEvent");
        StronglyTypedId = compilation.GetTypeByMetadataName(
            "Diaspora.SharedKernel.Primitives.StronglyTypedId");

        IntegrationEventPublisher = compilation.GetTypeByMetadataName(
            "Diaspora.Platform.Messaging.IIntegrationEventPublisher");
        NotificationPublisher = compilation.GetTypeByMetadataName(
            "Diaspora.Platform.Services.Abstractions.INotificationPublisher");

        FeatureDefinition = compilation.GetTypeByMetadataName(
            "Diaspora.Contracts.Authorization.FeatureDefinition");
        PermissionDefinition = compilation.GetTypeByMetadataName(
            "Diaspora.Contracts.Authorization.PermissionDefinition");
        PolicyDefinition = compilation.GetTypeByMetadataName(
            "Diaspora.Contracts.Authorization.PolicyDefinition");

        UnitOfWork = compilation.GetTypeByMetadataName("Diaspora.Domain.Abstractions.IUnitOfWork");
    }

    public INamedTypeSymbol? DomainEventHandlerBase { get; }
    public INamedTypeSymbol? IntegrationEventHandlerBase { get; }
    public INamedTypeSymbol? DomainEventNotification { get; }
    public INamedTypeSymbol? IntegrationEventNotification { get; }
    public INamedTypeSymbol? CommandHandler { get; }
    public INamedTypeSymbol? CommandHandlerWithResponse { get; }
    public INamedTypeSymbol? QueryHandler { get; }
    public INamedTypeSymbol? TransactionalAttribute { get; }
    public INamedTypeSymbol? SideEffectingAttribute { get; }
    public INamedTypeSymbol? Command { get; }
    public INamedTypeSymbol? CommandWithResponse { get; }
    public INamedTypeSymbol? Query { get; }
    public INamedTypeSymbol? RequirePermissionAttribute { get; }
    public INamedTypeSymbol? RequirePolicyAttribute { get; }
    public INamedTypeSymbol? RequireAuthenticationAttribute { get; }
    public INamedTypeSymbol? AllowAnonymousAttribute { get; }
    public INamedTypeSymbol? NotificationHandler { get; }
    public INamedTypeSymbol? Mediator { get; }
    public INamedTypeSymbol? Sender { get; }
    public INamedTypeSymbol? Publisher { get; }
    public INamedTypeSymbol? DomainEvent { get; }
    public INamedTypeSymbol? IntegrationEvent { get; }
    public INamedTypeSymbol? StronglyTypedId { get; }
    public INamedTypeSymbol? IntegrationEventPublisher { get; }
    public INamedTypeSymbol? NotificationPublisher { get; }
    public INamedTypeSymbol? FeatureDefinition { get; }
    public INamedTypeSymbol? PermissionDefinition { get; }
    public INamedTypeSymbol? PolicyDefinition { get; }
    public INamedTypeSymbol? UnitOfWork { get; }

    public static PlatformSymbols For(Compilation compilation) => new(compilation);

    /// <summary>Whether <paramref name="type"/> or any base of it is <paramref name="definition"/>.</summary>
    public static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol? definition)
    {
        if (definition is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, definition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The type argument <paramref name="type"/> supplied to <paramref name="definition"/>, walking
    /// the base chain — so a consumer three abstractions deep still reports the event it handles.
    /// </summary>
    public static ITypeSymbol? BaseTypeArgument(ITypeSymbol? type, INamedTypeSymbol? definition)
    {
        if (definition is null)
        {
            return null;
        }

        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, definition) &&
                current.TypeArguments.Length == 1)
            {
                return current.TypeArguments[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Every closed instance of <paramref name="definition"/> in <paramref name="type"/>'s interface
    /// list, including the ones it inherits.
    /// </summary>
    public static INamedTypeSymbol[] ImplementedInstancesOf(
        ITypeSymbol type, INamedTypeSymbol? definition)
    {
        if (definition is null)
        {
            return [];
        }

        return type.AllInterfaces
            .Where(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, definition))
            .ToArray();
    }

    public static bool Implements(ITypeSymbol type, INamedTypeSymbol? definition) =>
        definition is not null &&
        type.AllInterfaces.Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, definition));

    public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attribute) =>
        attribute is not null &&
        symbol.GetAttributes().Any(data =>
            SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute));
}
