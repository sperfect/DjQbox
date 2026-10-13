# ADR-007: CRDT Readiness

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

The chosen sync strategy (ADR-001) uses last-write-wins with Hybrid Logical Clocks for simplicity and offline compatibility. However, as DJQBox scales to multi-admin venues or multi-device owners, LWW will cause data loss in concurrent edit scenarios:

- Two admins reorder the queue simultaneously → one reorder is silently lost.
- An admin and the feedback loop update profile weights simultaneously → one update is lost.
- Two admins veto different recommendations at the same moment → potential race condition.

A migration path to CRDTs is needed that does not require a full data model rewrite or a breaking migration when the time comes.

---

## Decision

Design the data model and operation log with CRDT upgrade in mind from day one, without adopting CRDTs in v1.

Four structural decisions enable the upgrade:

1. **Operation log alongside state:** Every mutation is recorded as a timestamped, field-level operation log entry alongside the resulting state snapshot. This log is the raw material for CRDT operation-log-sync.

2. **Fractional index positions (ADR-002):** Queue positions use floating-point fractional indices that are structurally compatible with LSEQ/RGA sequence CRDTs.

3. **Set-oriented veto model:** Vetoes are stored as an OR-Set-compatible append-only set per recommendation. No in-place updates.

4. **Map-oriented profile weights:** Profile weights are stored as a `map(tag -> float)` with per-key HLC timestamps, compatible with LWW-Map and upgradeable to PN-Counter per key.

When the upgrade is triggered:
- The sync protocol switches from state-delta exchange to operation-log-sync.
- The merge function for each entity type is replaced with the appropriate CRDT merge.
- No data migration is required; the operation log was maintained from the start.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Ignore CRDT until needed | Simplest v1 | Requires painful schema migration and data transformation when needed; impossible to do zero-downtime | Migration cost is too high if deferred |
| Adopt full CRDT library from v1 | No migration ever needed | Significant library complexity (Automerge, Yjs, etc.); steep learning curve; bundle size impact on browser client; overkill for single-venue v1 | Premature optimization |
| Operational transforms | Proven in collaborative editors | Designed for ordered text editing; extremely difficult to implement correctly for arbitrary data types; poor fit for queue/profile data shapes | Wrong problem shape |
| Custom CRDT per entity | Maximum control | Requires deep CRDT expertise per entity; high implementation risk; equivalent to adopting a library | High risk without library support |

---

## Consequences

**Positive:**
- Smooth upgrade path from LWW to CRDT; no architectural debt incurred.
- Operation log doubles as an audit trail for all mutations.
- Data model designed once; upgrade is a merge-function swap.
- Each entity type can be independently upgraded (e.g. queue before profile weights).
- CRDT candidates are documented and annotated in the data model spec.

**Negative / Trade-offs:**
- Operation log requires additional storage and background compaction.
- Slightly more complex write path (write state + write log entry atomically).
- Developers must understand the field-level operation log format when implementing new entity mutations.
- The CRDT upgrade itself still requires careful implementation and testing of each CRDT merge function.

---

## Migration Timeline

| Phase | Strategy | Trigger |
|---|---|---|
| v1 | LWW/HLC on state deltas (current) | Now |
| v2 | Field-level LWW-Map on MusicProfile weights | When multi-admin concurrent profile updates cause visible data loss |
| v3 | LSEQ on queue ordering; OR-Set on veto sets | When multi-admin concurrent queue editing becomes a product requirement |

---

## Related ADRs

- [ADR-001: Local-First Sync Strategy](ADR-001-local-first-sync-strategy.md) — LWW/HLC is the current strategy this ADR upgrades
- [ADR-002: Queue Conflict Resolution](ADR-002-queue-conflict-resolution.md) — fractional index positions are the queue-specific CRDT preparation
