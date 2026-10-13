# DJQBox — Agent Knowledge Base

This file is the entry point for any agent (or human) starting work on DJQBox. Read it first. It captures the project context, design decisions, working conventions, and pointers to every document produced so far.

---

## What DJQBox Is

DJQBox is a **cloud-enabled, local-first web application** for collaborative music control across physical venues (café, restaurant, workspace, home). Key properties:

- Runs on a spectrum from a single **Raspberry Pi with no internet** (Tier 1) to a **distributed cloud deployment** (Tier 3). Both are first-class targets.
- Abstracts over multiple audio sources (Spotify, YouTube, local files) behind a **Source Adapter interface**.
- Has two queue operating modes: **DJ Mode** (admin controls what plays; guests recommend) and **Autopilot Mode** (recommendations auto-promote after a configurable veto window).
- Learns the venue's sonic taste over time via a **Music Profile** feedback loop.
- Designed for a future upgrade to **CRDTs** without data migration (see ADR-007).

---

## Repository Layout

```
/docs
  /spec          ← Platform-agnostic domain specifications (source of truth)
  /adr           ← Architecture Decision Records
  /impl          ← Implementation guides (stack-specific, not source of truth)
/src             ← Application source code (created during implementation)
/frontend        ← Single-file dummy frontend (created during POC)
/infra           ← Infrastructure-as-code (created during MVP)
AGENTS.md        ← This file
README.md        ← Project README
```

---

## Document Map

### Spec Suite (`docs/spec/`)

| File | Purpose |
|---|---|
| `00-overview.md` | Glossary, design principles, deployment spectrum, component map |
| `01-prd.md` | Personas, 12 user stories, acceptance criteria, success metrics |
| `02-architecture.md` | Component architecture, data flows, service descriptions, quality attributes |
| `03-data-model.md` | 12 canonical entities, ER diagram, field tables, CRDT annotations |
| `04-api-contracts.md` | 25 REST endpoints, 6 WebSocket channels, sequence diagrams |
| `05-auth-spec.md` | Owner / admin / guest auth flows, token spec, offline caching, revocation |
| `06-queue-spec.md` | DJ Mode and Autopilot state machines, veto window, mode-switch behavior |
| `07-music-profile-spec.md` | Tag taxonomy, scoring algorithm, feedback loop, ML interface |
| `08-venue-player-spec.md` | Abstract player interface, browser tab and daemon reference implementations |
| `09-sync-spec.md` | Offline behavior, delta sync protocol, conflict resolution, CRDT upgrade path |
| `10-source-adapter-spec.md` | Adapter interface, Spotify/YouTube/LocalFiles sketches, conformance checklist |
| `11-observability-spec.md` | Logs, metrics, traces, health endpoints across all deployment tiers |

### ADRs (`docs/adr/`)

| File | Decision |
|---|---|
| `ADR-001` | Epoch-based delta sync with LWW/HLC |
| `ADR-002` | Fractional index positions for CRDT-compatible queue ordering |
| `ADR-003` | Abstract Venue Player interface over browser, daemon, and future players |
| `ADR-004` | Source Adapter anti-corruption layer pattern |
| `ADR-005` | Three-tier auth: owner OAuth/passkey, admin elevation, guest QR token |
| `ADR-006` | Tag-weight Music Profile with pluggable ML interface |
| `ADR-007` | Data model and operation log designed for future CRDT upgrade |
| `ADR-008` | Cloud-enabled not cloud-required; RPi to distributed cloud spectrum |
| `ADR-009` | .NET 8 Minimal API + PostgreSQL for first implementation |
| `ADR-010` | Azure Container Apps topology for MVP |

### Implementation Guides (`docs/impl/`)

| File | Purpose |
|---|---|
| `POC-implementation-plan.md` | 10-task .NET 8 + Docker Compose build guide with acceptance criteria |
| `MVP-azure-plan.md` | Azure promotion path: Container Apps, PostgreSQL Flexible Server, SignalR Service |

### Agent Guides (this directory)

| File | Purpose |
|---|---|
| `AGENTS.md` | This file. Project context and pointers for all agents. |
| `AGENTS-spec.md` | Instructions for agents working on spec documents. |
| `AGENTS-impl.md` | Instructions for agents writing implementation code. |

---

## Key Design Decisions (know before you touch anything)

1. **Spec is the source of truth.** Implementation code must conform to the spec. If there is a conflict, update the spec first (with rationale), then update the code.

2. **Cloud-enabled, not cloud-required.** Never introduce a hard dependency on a cloud service in the application core. Azure-specific services (Key Vault, SignalR Service) are injected at the hosting layer via configuration, not baked into domain logic.

3. **Venues are isolated.** No cross-venue data access is possible. Every query, token, and operation is scoped to a single venue. Enforce this at the service and database layer.

4. **Fractional position for queue ordering.** `QueueItem.position` is a float. New items get `floor(max) + 1.0`. Inserts between items use the midpoint. Rebalance when any two positions are within `0.001`. See ADR-002.

5. **CRDT-ready data model.** Every mutation writes a field-level operation log entry alongside the state update. Do not skip this — it is the foundation of the CRDT upgrade path. See ADR-007 and `09-sync-spec.md`.

6. **No PII in logs.** User display names, email addresses, and token values must never appear in structured logs. Use IDs only.

7. **All diagrams in PlantUML.** When adding diagrams to spec documents, use PlantUML syntax in fenced code blocks.

8. **Spec documents are platform-agnostic.** Do not add framework names, NuGet packages, or cloud service names to the `docs/spec/` documents. Those belong in `docs/impl/` or `docs/adr/`.

---

## Parallel Work Tracks

Two tracks run in parallel and do not block each other:

**Track 1 — Spec evolution**
Add new spec documents and ADRs. Current backlog:
- `12-error-handling-spec.md` — standard error codes, retry policy, circuit breaker
- `13-venue-player-browser-spec.md` — detailed browser tab player spec
- `ADR-011` — error handling strategy
- Review and refine existing specs as implementation reveals gaps

See `AGENTS-spec.md` for full instructions.

**Track 2 — POC implementation (.NET)**
Build the POC against `docs/impl/POC-implementation-plan.md`. Ten tasks, each with acceptance criteria. Uses TDD.

See `AGENTS-impl.md` for full instructions.

---

## Glossary Quick Reference

| Term | Definition |
|---|---|
| Autopilot Mode | Recommendations auto-promote after veto window expires without a veto |
| DJ Mode | Admin reviews and accepts/rejects all recommendations manually |
| Deployment Host | The machine(s) running DJQBox services (RPi, VPS, or cloud cluster) |
| Event Bus | Internal pub/sub; in-process on Tier 1, message broker on Tier 3 |
| Music Profile | Per-venue weighted tag vector encoding the venue's sonic identity |
| Now Playing | The QueueItem currently playing at a venue |
| Queue | Ordered list of QueueItems for a venue; one per venue |
| QueueItem | A single track entry in a Queue with fractional position |
| Recommendation | A guest/admin suggestion to add a track to the queue |
| Source Adapter | Anti-corruption layer translating a music source to canonical Track model |
| Sync Epoch | Monotonically increasing logical counter for delta sync bookkeeping |
| TrackRef | `(source_adapter, source_id)` — the minimal stable identifier for a track |
| Venue | Isolated namespace (café, room, etc.) with its own queue and music profile |
| Venue Player | Audio output component implementing the player interface |
| Veto | Admin action blocking a pending Autopilot recommendation |
| Veto Window | Configurable countdown after recommendation submission; default 60 seconds |
