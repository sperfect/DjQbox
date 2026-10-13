# ADR-001: Local-First Sync Strategy

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox must operate across a spectrum from a fully isolated Raspberry Pi to a multi-region cloud deployment. Users must be able to control music playback, manage queues, and submit recommendations even when internet connectivity is unavailable. A sync strategy must be chosen that:

1. Works offline by default, not as a special mode.
2. Scales from single-node to distributed deployment without architectural change.
3. Is implementable without heavy external dependencies.
4. Provides a credible upgrade path to stronger consistency as usage grows.

The system involves multiple concurrent writers (admins, guests, automated timers) operating on shared state (queue order, recommendation status, profile weights). Conflicts are possible but infrequent in the expected deployment scenarios.

---

## Decision

Adopt **epoch-based delta sync with last-write-wins (LWW) conflict resolution using Hybrid Logical Clocks (HLC)**.

- Local state is the primary source of truth. Sync to cloud or peer devices is additive and opportunistic.
- Every local write produces a timestamped delta log entry alongside the resulting state snapshot.
- On reconnection, a sync handshake exchanges deltas since the last known shared epoch.
- Conflicts are resolved by HLC timestamp: the delta with the higher HLC wins, applied field-by-field.
- When a peer's epoch predates the server's compaction horizon, a full-state sync is triggered and the epoch resets.
- The operation log structure (field-level ops, HLC timestamps) is designed from day one to support a future CRDT upgrade without data migration.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Full CRDT from day one | No data loss on concurrent edits; mathematically sound | Significant library complexity; steep learning curve; overkill for single-venue v1 | Too complex for v1; upgrade path preferred |
| Operational transforms (OT) | Designed for real-time collaboration | Designed for ordered text editing; poor fit for queue/profile data; complex to implement correctly | Wrong problem shape |
| Event sourcing with no local state | Strong audit trail; easy replay | Requires connectivity for every operation; violates local-first requirement | Violates core design principle |
| Simple last-write-wins on wall clock | Trivially simple | Wall clock skew causes incorrect conflict resolution; not monotonic | Clock skew makes this unreliable |

---

## Consequences

**Positive:**
- Works fully offline; no connectivity required for any queue or playback operation.
- Simple to implement and reason about; LWW with HLC is well-understood.
- Delta log enables future upgrade to CRDT without migration.
- Scales from SQLite on RPi to distributed database on cloud with the same protocol.

**Negative / Trade-offs:**
- LWW can lose concurrent writes in rare multi-admin scenarios (two admins reorder the same item simultaneously).
- Does not guarantee causal consistency across devices; a guest on a slow connection may see stale state briefly.
- Full-sync fallback (epoch gap) can be expensive for large data sets on slow connections.

---

## Related ADRs

- [ADR-007: CRDT Readiness](ADR-007-crdt-readiness.md) — documents the upgrade path from LWW to CRDT
- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — describes the deployment spectrum this strategy must cover
