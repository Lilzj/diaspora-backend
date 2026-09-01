; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------------------|----------|--------------------------------------------------------
DSP1001 | Diaspora.DomainEvents | Warning | A handler must not depend on another handler
DSP1002 | Diaspora.DomainEvents | Warning | One domain event is settled by more than one handler in this module
DSP1003 | Diaspora.DomainEvents | Warning | One integration event is settled by more than one consumer in this module
DSP1004 | Diaspora.DomainEvents | Warning | A handler must not serve both the domain and the integration pipeline
DSP2001 | Diaspora.Messaging | Error | A type must not be both a domain event and an integration event
DSP2002 | Diaspora.Messaging | Warning | An integration event must be published through IIntegrationEventPublisher
DSP2003 | Diaspora.Messaging | Warning | Only the Communication Platform creates notifications
DSP3001 | Diaspora.Persistence | Warning | A Revision property must be configured as a concurrency token
DSP3002 | Diaspora.Persistence | Warning | An owned collection must declare its key
DSP3003 | Diaspora.Persistence | Warning | A strongly typed identifier must not be stringified or ordered inside a query
DSP4001 | Diaspora.Cqrs | Warning | A query handler must not write
DSP4002 | Diaspora.Cqrs | Warning | A handler that saves twice in a row must declare [Transactional]
DSP4003 | Diaspora.Cqrs | Warning | A command handler that stages a write must save it
DSP4004 | Diaspora.Cqrs | Warning | [Transactional] belongs on the command, not on the handler
DSP5001 | Diaspora.Contributors | Warning | A feature gate name must be module.capability
DSP5002 | Diaspora.Contributors | Warning | A permission key must be module:resource:action
DSP5003 | Diaspora.Contributors | Warning | A policy name must be dotted and namespaced to its module
DSP5004 | Diaspora.Contributors | Info | Prefer the platform's permission vocabulary
DSP5005 | Diaspora.Contributors | Warning | A contributor must be sealed and constructible without the container
DSP5006 | Diaspora.Contributors | Info | Catalogue names are lower case
DSP6001 | Diaspora.Readiness | Warning | A business module must not name an AI vendor
