# ADR-009: .NET POC Stack Selection

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

The DJQBox spec suite is platform and tool agnostic. The first concrete implementation is a Proof of Concept (POC) that validates the core queue and recommendation loop end-to-end. A technology stack must be chosen for this first implementation pass.

Requirements for the POC stack:
1. Runs in Docker Compose locally with no cloud dependencies.
2. Has a clear promotion path to Azure for the MVP.
3. Supports Minimal API style to reduce ceremony and accelerate POC delivery.
4. Has strong support for PostgreSQL (the chosen POC database and Azure MVP target).
5. Supports WebSockets / real-time events with minimal setup (SignalR or equivalent).
6. Has a well-established testing model for integration tests against a real database.

---

## Decision

Use **.NET 8 Minimal API** for the POC and MVP implementation.

**Specific choices:**

| Concern | Choice |
|---|---|
| Runtime | .NET 8 (LTS) |
| API style | Minimal API (`app.MapGet`, `app.MapPost`) |
| ORM | Entity Framework Core 8 with Npgsql provider |
| Database | PostgreSQL 16 (Docker for POC; Azure Database for PostgreSQL Flexible Server for MVP) |
| Real-time | ASP.NET Core SignalR (in-process for POC; Azure SignalR Service backplane for MVP) |
| Auth | JWT Bearer (HS256; static key in POC; Azure Key Vault in MVP) |
| Logging | Serilog (stdout JSON for POC; Application Insights sink for MVP) |
| Testing | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers.PostgreSql` |
| Containerisation | Docker (single-stage Dockerfile; Docker Compose for local) |
| Frontend | Single HTML file + vanilla JS + SignalR JS client from CDN |

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Node.js + Express/Fastify | Fast to scaffold; large ecosystem; JS everywhere (frontend + backend) | Weaker type safety; EF Core equivalent (Prisma/TypeORM) less mature for complex queries; no built-in SignalR equivalent | Type safety and EF Core integration preferred |
| Python + FastAPI | Very fast to prototype; excellent async support | Less native Azure integration; no built-in SignalR; ORM ecosystem less integrated than EF Core | Azure promotion path less smooth |
| Go | Excellent performance; small binary; good Docker story | More boilerplate for ORM and WebSockets; smaller team familiarity assumed | Velocity tradeoff for POC not worth it |
| Java + Spring Boot | Enterprise-grade; excellent Azure support | Higher ceremony; heavier container image; slower POC iteration | Too heavy for POC pace |
| .NET 8 Controller-based MVC | Familiar to most .NET teams; full scaffolding support | More ceremony than Minimal API for a POC; not needed for the endpoint count | Minimal API is the right fit for POC scale |

---

## Consequences

**Positive:**
- Minimal API reduces boilerplate; POC endpoints map directly to spec contracts with minimal scaffolding.
- EF Core + Npgsql is a mature, well-documented combination for PostgreSQL.
- SignalR provides the WebSocket abstraction from the spec with in-process backplane for POC and managed Azure SignalR Service for MVP — the same code, different configuration.
- Azure Container Apps, Azure Database for PostgreSQL, and Azure Key Vault are all first-class .NET deployment targets.
- `Testcontainers.PostgreSql` allows integration tests against a real Postgres instance with zero manual setup.
- .NET 8 Docker images are small (`mcr.microsoft.com/dotnet/aspnet:8.0` ~200MB) and Azure-optimised.

**Negative / Trade-offs:**
- .NET is not the natural fit for a Tier 1 Raspberry Pi daemon player (heavier runtime than a Go or Rust binary). The Venue Player daemon implementation on RPi may use a different stack; the interface contract remains the same.
- Minimal API lacks some MVC conventions (e.g. automatic model validation filters); validation must be implemented explicitly.
- The POC stack choice does not bind future implementations — the spec remains agnostic and other stacks will produce their own ADR-009 equivalents.

---

## Related ADRs

- [ADR-010: Azure MVP Topology](ADR-010-azure-mvp-topology.md) — describes the Azure services this stack deploys to
- [ADR-003: Venue Player Abstraction](ADR-003-venue-player-abstraction.md) — player interface is stack-agnostic; daemon implementation may use a different stack
