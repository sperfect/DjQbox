# ADR-010: Azure MVP Topology

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

The DJQBox POC runs locally via Docker Compose. The MVP must run on Azure. An Azure topology must be chosen that:

1. Maps cleanly to the logical architecture defined in `docs/spec/02-architecture.md`.
2. Provides a managed, low-ops hosting environment suitable for a small team.
3. Keeps costs low for a first production deployment (no over-provisioning).
4. Preserves the deployment-spectrum principle (ADR-008): the same application code runs locally and on Azure.
5. Does not introduce Azure-specific dependencies into the application core (no Azure SDK calls in domain logic).

---

## Decision

Use **Azure Container Apps** as the hosting platform for the MVP, with the following supporting services:

| Logical Component | Azure Service |
|---|---|
| API Gateway + all services (single process) | Azure Container Apps (single Container App, managed ingress with TLS) |
| PostgreSQL database | Azure Database for PostgreSQL Flexible Server |
| Internal event bus (backend) | Azure Service Bus (Standard tier, topics + subscriptions) |
| Client push (browser + daemon players) | Azure Web PubSub (Free tier, MQTT 3.1.1/5.0) |
| Container image registry | Azure Container Registry |
| Secrets management | Azure Key Vault + Managed Identity |
| Structured logging + metrics | Azure Monitor + Log Analytics + Application Insights |
| Frontend hosting | Azure Static Web Apps (free tier) |
| CI/CD | GitHub Actions → Azure Container Registry → Container Apps |

**Rationale for Azure Container Apps over alternatives:**
- Serverless container runtime: scales to zero when idle (cost-efficient for MVP).
- No Kubernetes cluster management required.
- Native integration with Azure Container Registry, Key Vault, and Managed Identity.
- Built-in HTTPS ingress and TLS termination.
- Revision-based deployments allow rollback without infrastructure changes.

**Architecture remains Tier 2** for the MVP (single Container App = single logical process; not yet microservices). The transition to Tier 3 (independent microservices, replicated services) is a future step beyond MVP.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Azure App Service | Mature; easy deployment; built-in .NET support | Less container-native; scaling is per-plan not per-container; more expensive for always-on | Container Apps is the modern container-first choice |
| Azure Kubernetes Service (AKS) | Full orchestration; maximum flexibility | Overkill for MVP; requires cluster management; significantly higher ops burden | Too heavy for MVP scope |
| Azure Functions (serverless) | Scale to zero; pay-per-execution | Poor fit for long-running WebSocket connections (SignalR); stateful queue timer doesn't map well to function model | SignalR and background timers incompatible with Functions model |
| Azure Spring Apps / other PaaS | Native platform integrations | Language/framework lock-in; not applicable to .NET | Not applicable |
| Self-hosted VM (Azure VM) | Full control; cheapest per-compute | High ops burden; no managed TLS, scaling, or health probes | Too much ops for MVP |

---

## Consequences

**Positive:**
- Azure Container Apps handles TLS, scaling, and health probes with minimal configuration.
- Managed Identity eliminates credential management in application code (no Azure SDK calls in domain logic; secrets injected as environment variables).
- Azure Service Bus and Azure Web PubSub are configuration swaps — the application code does not change between POC (in-process) and MVP (managed services). See ADR-011.
- Container Apps revision model enables blue-green deployments and rollback.
- Application Insights provides distributed tracing, request logging, and custom event tracking aligned with the observability spec.
- Cost is predictable and low for MVP traffic (see cost estimate in `docs/impl/MVP-azure-plan.md`).

**Negative / Trade-offs:**
- Azure Container Apps cold-start latency (scale to zero) is 2–5 seconds on first request. Set minimum replicas to 1 for production to eliminate this.
- Azure Web PubSub free tier supports approximately 20 units / 400 concurrent MQTT connections. Upgrade to Standard if concurrent connections exceed this.
- Azure Database for PostgreSQL Flexible Server is the primary cost driver (~$30–40/month on Burstable tier).
- The MVP remains a single Container App (Tier 2 equivalent). Splitting into microservices (Tier 3) requires additional Container Apps — deferred to post-MVP.
- Azure-specific services (Service Bus, Web PubSub, Key Vault, Container Registry) are introduced at the hosting layer, not the application layer. The MQTT protocol used by Web PubSub is an open standard; clients are not Azure-specific.

---

## Related ADRs

- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — Azure MVP is Tier 2 in the deployment spectrum
- [ADR-009: .NET POC Stack](ADR-009-dotnet-poc-stack.md) — the application stack being deployed
- [ADR-011: Messaging Architecture](ADR-011-messaging-architecture.md) — Service Bus (internal) + Web PubSub MQTT (client push)
