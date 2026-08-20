namespace Diaspora.AnalyzerTests;

/// <summary>
/// The scaffolding every probe shares.
/// <para>
/// Kept in one place because a probe is only interesting for the two or three lines that break a
/// rule, and repeating twenty lines of usings and a repository interface around each of them would
/// bury the thing under test. Each helper below composes a compilable file from a fragment.
/// </para>
/// </summary>
internal static class Probes
{
    private const string Usings =
        """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using Diaspora.Application.Abstractions;
        using Diaspora.Contracts.Authorization;
        using Diaspora.Contracts.Events;
        using Diaspora.Domain.Abstractions;
        using Diaspora.Platform.Messaging;
        using Diaspora.Platform.Modules.Contributors;
        using Diaspora.SharedKernel.Primitives;
        using Diaspora.SharedKernel.Results;
        using MediatR;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Metadata.Builders;
        """;

    /// <summary>
    /// <b>The namespace is under <c>Diaspora.</c> because two rules only look at seams declared
    /// there.</b> A probe living in <c>Probe</c> would satisfy the contributor-shape test by not
    /// being seen at all, which is the worst way for a test to pass.
    /// </summary>
    private const string Namespace = "namespace Diaspora.Modules.Probe;";

    /// <summary>
    /// A module's own furniture: an event or two, an aggregate, a repository. Every probe gets it
    /// so that "the handler saves" and "the handler stages" have something real to do it to.
    /// </summary>
    private const string Fixtures =
        """
        public sealed record ThingHappened : DomainEvent;
        public sealed record OtherThingHappened : DomainEvent;

        public sealed record ThingPublished : IntegrationEvent
        {
            public string Name { get; init; } = string.Empty;
        }

        public sealed record OtherThingPublished : IntegrationEvent;

        public sealed record ThingId(Guid Value) : StronglyTypedId(Value);

        public sealed class Thing
        {
            public ThingId Id { get; set; } = new(Guid.Empty);
            public int Revision { get; set; }
            public string Slug { get; set; } = string.Empty;
            public List<Tag> Tags { get; } = [];
        }

        public sealed class Tag
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public interface IThingRepository
        {
            IQueryable<Thing> Query { get; }
            void Add(Thing thing);
            Task<Thing?> GetAsync(ThingId id, CancellationToken cancellationToken);
        }
        """;

    /// <summary>
    /// One compilable file: the usings, one namespace declaration, the shared fixtures, then the
    /// fragment. A fragment declares no namespace of its own — a file may hold exactly one
    /// file-scoped declaration, and a second one is a compile error rather than a violation.
    /// </summary>
    public static string File(string fragment) =>
        Usings + Environment.NewLine + Environment.NewLine +
        Namespace + Environment.NewLine + Environment.NewLine +
        Fixtures + Environment.NewLine + Environment.NewLine +
        fragment;
}
