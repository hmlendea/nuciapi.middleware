# NuciAPI.Middleware Documentation

## Repository Summary

NuciAPI.Middleware is an ASP.NET Core middleware base utilities library targeting .NET 10.0. It provides an abstract base class (`NuciApiMiddleware`) and header name constants (`NuciApiHeaderNames`) for building custom middleware in NuciAPI-based services. The library does not operate as a service and contains no ready-to-register middleware components.

## Root Document Map

- [ARCHITECTURE.md](../ARCHITECTURE.md) — System architecture, component boundaries, runtime flows, design constraints
- [SECURITY.md](../SECURITY.md) — Vulnerability reporting, supported versions, disclosure policy
- [PRIVACY.md](../PRIVACY.md) — Data handling and privacy statement
- [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md) — Community conduct standards
- [CONTRIBUTING.md](../CONTRIBUTING.md) — Contribution guidelines, code style, testing
- [LICENSE](../LICENSE) — GNU GPL v3.0 or later terms
- [README.md](../README.md) — Installation, usage example, development commands

## Documentation Catalogue

### Architecture & Design
- [architecture.md](./architecture.md) — High-level architecture complementing root ARCHITECTURE.md
- [design-decisions.md](./design-decisions.md) — Key architectural choices and rationale
- [invariants.md](./invariants.md) — System-wide invariants and contracts
- [dependencies.md](./dependencies.md) — External and internal dependencies

### Components
- [components/presentation.md](./components/presentation.md) — Public API surface: NuciApiMiddleware, NuciApiHeaderNames
- [components/host-and-composition.md](./components/host-and-composition.md) — ASP.NET Core integration, middleware registration
- [components/application-services.md](./components/application-services.md) — Base class helpers: header access, IP extraction, URL decoding, hostname resolution

### Flows
- [flows/middleware-invocation.md](./flows/middleware-invocation.md) — Request pipeline execution flow
- [flows/client-ip-extraction.md](./flows/client-ip-extraction.md) — Forwarding header parsing and IP normalization
- [flows/header-access.md](./flows/header-access.md) — Required/optional header reading and decoding

### API Reference
- [api-reference/INDEX.md](./api-reference/INDEX.md) — Public API summary table
- [api-reference/nuci-api-middleware.md](./api-reference/nuci-api-middleware.md) — NuciApiMiddleware base class documentation
- [api-reference/nuci-api-header-names.md](./api-reference/nuci-api-header-names.md) — NuciApiHeaderNames constants documentation

### Cross-Cutting Concerns
- [configuration.md](./configuration.md) — Hardcoded forwarding headers, no configuration model
- [error-handling.md](./error-handling.md) — BadHttpRequestException, URL decode fallback, IP parsing failures
- [logging.md](./logging.md) — No built-in logging; derived middleware may add observability
- [security.md](./security.md) — Trust boundaries, forwarding header risks, no authentication/authorization
- [concurrency-and-scheduling.md](./concurrency-and-scheduling.md) — Stateless per-request, no shared state or locks
- [state-and-persistence.md](./state-and-persistence.md) — No persistent state; in-flight transformations only

### Operations
- [build-and-deployment.md](./build-and-deployment.md) — Build, test, pack, release workflows
- [testing.md](./testing.md) — NUnit test strategy, coverage, gaps
- [troubleshooting.md](./troubleshooting.md) — Common issues and solutions
- [change-guide.md](./change-guide.md) — Safe modification patterns

### Meta
- [documentation-maintenance.md](./documentation-maintenance.md) — How to keep docs current
- [ambiguities-and-open-questions.md](./ambiguities-and-open-questions.md) — Unresolved items, known gaps
- [api-usage-examples.md](./api-usage-examples.md) — Example middleware implementations
- [repository-overview.md](./repository-overview.md) — Repository purpose, scope, entry points
- [repository-structure.md](./repository-structure.md) — Source tree layout, module organisation
- [data-model.md](./data-model.md) — Header values, client IP, hostnames as in-flight data
- [integrations.md](./integrations.md) — NuciAPI, NuciWeb.HTTP, ASP.NET Core integrations
- [faq.md](./faq.md) — Frequently asked questions
- [quick-start.md](./quick-start.md) — 5-minute getting started guide
- [invariants.md](./invariants.md) — System-wide invariants and contracts
- [design-decisions.md](./design-decisions.md) — Key architectural choices and rationale
- [dependencies.md](./dependencies.md) — External and internal dependencies
- [components/browser-state-and-localisation.md](./components/browser-state-and-localisation.md) — Browser state and localisation handling
- [components/integration-models.md](./components/integration-models.md) — External system integration contracts
- [behaviour/browse-and-search.md](./behaviour/browse-and-search.md) — Discovering library capabilities
- [behaviour/inspect-edit-delete.md](./behaviour/inspect-edit-delete.md) — Inspecting and customising runtime behavior

## Navigation Aids

- **Start here** (newcomers): [quick-start.md](./quick-start.md) → [repository-overview.md](./repository-overview.md) → [api-usage-examples.md](./api-usage-examples.md)
- **Deep dive** (component owners): [components/](./components/) → [architecture.md](./architecture.md) → [design-decisions.md](./design-decisions.md)
- **Flows** (debuggers): [flows/](./flows/) → [error-handling.md](./error-handling.md) → [testing.md](./testing.md)

## Maintenance Metadata

- Last reviewed: 2026-11-03
- Owner: Repository maintainers
- Coverage status: Initial generation from repository evidence