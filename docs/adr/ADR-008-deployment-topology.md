# ADR-008: Deployment Topology

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox was initially conceived as a cloud-first application. During architecture review, a core requirement emerged: the system must run in complete isolation on a single Raspberry Pi — no internet, no cloud dependency, no external services — as a fully supported and equal deployment target.

This requirement creates tension with cloud-native design assumptions:
- Cloud-native architectures assume managed services (object storage, message brokers, managed databases, secrets managers) that are not available in an air-gapped RPi deployment.
- Auto-scaling, multi-region replication, and managed identity services are cloud-only concepts.
- A cloud-native design that assumed these services would require a separate "offline mode" codebase or heavy adaptation for embedded deployment.

The project name originally used "cloud-native." This ADR documents the decision to change the governing deployment principle.

---

## Decision

Adopt **"cloud-enabled, not cloud-required"** as the governing deployment principle.

The logical architecture is identical across all deployment tiers:
- The same services (API Gateway, Queue Service, Profile Service, Auth Service, Sync Service, Event Bus) exist at every tier.
- The same interfaces (Venue Player, Source Adapter), events, and data shapes are used at every tier.
- The same spec documents describe the behavior at every tier.

Only the **runtime hosting** changes between tiers:

| Concern | Tier 1 (RPi) | Tier 2 (VPS) | Tier 3 (Cloud) |
|---|---|---|---|
| Services | Single process, module boundaries | Separate processes | Independent microservices |
| Event Bus | In-process pub/sub | Lightweight message broker | Managed message broker |
| Database | SQLite (embedded) | Standalone relational DB | Managed replicated DB |
| Secrets | Local key file | Environment variables | Secrets manager |
| Scaling | Vertical only | Vertical | Horizontal |
| Sync | Not applicable (no remote) | Optional cloud sync | Full multi-device sync |

The three tiers form a **spectrum**, not discrete product variants. An operator moves from Tier 1 to Tier 2 by splitting the process into services; from Tier 2 to Tier 3 by replacing the broker and database with managed equivalents. The application code does not change.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Cloud-native only | Modern; best tooling; auto-scaling; managed everything | Excludes RPi and air-gapped deployments entirely; forces internet dependency; contradicts owner data sovereignty goals | Violates core requirement |
| Two separate codebases (local and cloud) | Each codebase optimized for its target | Double maintenance burden; feature divergence over time; impossible to test consistently | Maintenance nightmare |
| Containerize everything, require container runtime on RPi | One deployment artifact | Container runtime overhead is significant on RPi Zero/3; adds operational complexity for non-technical owners | Overly heavy for minimal hardware |
| Electron/desktop app for local | No server setup needed | Platform-specific; not web-based; guest UX requires a running server anyway; no path to cloud | Not web-based; dead end for cloud growth |

---

## Consequences

**Positive:**
- Total isolation deployment on a Raspberry Pi is a first-class supported scenario.
- No vendor lock-in; the same system runs on any infrastructure.
- Same spec documents are authoritative for all tiers; no tier-specific documentation.
- Operator can start at Tier 1 and grow to Tier 3 without rewriting the application.
- Data sovereignty: on Tier 1, all data stays on the owner's hardware.

**Negative / Trade-offs:**
- Implementation must avoid hard dependencies on cloud-specific services (no AWS S3, no GCP Pub/Sub in core code).
- Service boundary design must be careful to avoid tight coupling that makes Tier 2/3 splitting difficult.
- The Event Bus abstraction must be genuinely swappable (in-process vs network broker) — a leaky abstraction here would break Tier 1.
- Testing requires coverage across multiple deployment configurations.

---

## Related ADRs

- [ADR-001: Local-First Sync Strategy](ADR-001-local-first-sync-strategy.md) — sync works without cloud; no sync is required for Tier 1
- [ADR-003: Venue Player Abstraction](ADR-003-venue-player-abstraction.md) — daemon player is the Tier 1 reference implementation
- [ADR-005: Auth Model](ADR-005-auth-model.md) — deployment host signing key replaces cloud key management on Tier 1
