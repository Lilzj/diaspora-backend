# ADR 001: Modular Monolith Architecture

## Status
Accepted

## Context
The Diaspora platform requires a robust backend architecture that can scale rapidly while remaining simple to develop and deploy during initial phases. Microservices introduce operational overhead, whereas a traditional monolith risks becoming a spaghetti codebase.

## Decision
We adopt a **Modular Monolith** architecture:
- Bounded contexts exist within a single solution as distinct modules.
- Cross-module communication occurs via `Contracts` and platform abstractions.
- Dynamic module registration is managed via `IPlatformModule`.

## Consequences
- **Positive**: Low operational complexity, easy local development, unified deployment, clear boundaries for future microservice extraction.
- **Negative**: Requires strict discipline to prevent invalid cross-module coupling (enforced via architecture tests).
