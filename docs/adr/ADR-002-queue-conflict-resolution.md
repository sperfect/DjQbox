# ADR-002: Queue Conflict Resolution

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

Multiple admins at a venue may simultaneously modify the queue: reorder items, remove items, or promote recommendations. The sync strategy (ADR-001, LWW/HLC) handles state sync at the entity level, but the queue's ordered nature means concurrent position updates can produce inconsistent orderings.

Specific problems with naive integer positions:
- If two admins both insert an item "at position 2", both must renumber all subsequent items. In a distributed system, this causes conflicts on every other item in the queue.
- LWW on integer positions destroys one admin's change silently.

A position model is needed that:
1. Allows insert without renumbering other items.
2. Works with the LWW strategy already chosen.
3. Is compatible with a future upgrade to a sequence CRDT (LSEQ or RGA).

---

## Decision

Use **fractional index positions** for QueueItem ordering.

- Queue positions are stored as floating-point numbers (e.g. 1.0, 1.5, 2.0, 2.5).
- New items appended to the end receive `position = floor(max_position) + 1.0`.
- Inserting between two items at positions `p1` and `p2` yields `position = (p1 + p2) / 2`.
- Concurrent position conflicts are resolved by LWW on the `position` field using HLC timestamp.
- When fractional precision degrades (two positions within `0.001` of each other), a background rebalancing operation renumbers all items with integer spacing. This is transparent to users.

The fractional index design is structurally compatible with LSEQ/RGA sequence CRDTs: the position field can be replaced by a CRDT identifier in a future upgrade without changing the entity shape.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Integer positions with renumber on every change | Simple to reason about | Every insert causes conflicts on all subsequent items; incompatible with distributed writes | Conflict explosion |
| Lock-based queue operations | Guarantees no conflicts | Requires connectivity; violates offline operation requirement | Violates local-first |
| Full RGA sequence CRDT from v1 | True conflict-free ordering | Complex library dependency; significant implementation effort; overkill for v1 single-venue | Too complex for v1 |
| Linked list (prev/next pointers) | No renumbering on insert | Concurrent inserts at the same position still conflict; harder to query by order | Same conflict problem in a different form |

---

## Consequences

**Positive:**
- Insert at any position without touching other items; LWW conflicts are local to the inserted item only.
- Structurally CRDT-upgrade-compatible; migration to LSEQ is a merge-function swap.
- Simple to implement; floating-point arithmetic is universally available.
- Offline-compatible; position assignment requires no coordination.

**Negative / Trade-offs:**
- Floating-point positions degrade over time with many inserts at the same position; requires periodic rebalancing.
- Two truly concurrent inserts at the same midpoint still cause LWW loss of one insert. Rare but possible in multi-admin venues.
- Rebalancing is a write operation on all queue items; must be throttled on large queues.

---

## Related ADRs

- [ADR-001: Local-First Sync Strategy](ADR-001-local-first-sync-strategy.md) — LWW/HLC is the conflict resolution strategy applied to positions
- [ADR-007: CRDT Readiness](ADR-007-crdt-readiness.md) — LSEQ/RGA upgrade path for queue ordering
