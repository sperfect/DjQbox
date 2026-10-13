# ADR-004: Source Adapter Pattern

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox must queue and play tracks from multiple music sources: Spotify, YouTube, local files, and any future sources the operator or community adds. Each source has:

- A different API (REST, SDK, file system scan)
- A different authentication model (OAuth, API key, none)
- A different data format (different field names, metadata availability, ID schemes)
- Different availability constraints (Spotify requires internet; local files work offline)

The core system must not be coupled to any specific source. Adding a new source should not require modifying the Queue Service, Profile Service, or any other core component.

---

## Decision

Define a **Source Adapter interface** with standardized operations and a capability flag system. Adapters translate source-specific data into the canonical Track model (anti-corruption layer pattern).

**Interface operations:** `initialize`, `isAvailable`, `search`, `resolve`, `getMetadata`, `getCapabilities`, `teardown`

**Capability flags:** `search`, `streaming`, `metadata`, `offline_cache`, `recommendations`

Adapters register at system startup via a manifest file. The system queries `getCapabilities()` before dispatching operations. The system ships three reference adapters: Spotify, YouTube, and Local Files.

New adapters can be added by: (1) implementing the interface, (2) shipping an adapter manifest, (3) placing the adapter in the configured adapters directory. No core service modification is required.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Hardcode Spotify and YouTube only | Simpler initial implementation | Kills extensibility; local files (offline) not supported; adding SoundCloud etc. requires core changes | Non-extensible; violates local-first |
| Use a third-party music metadata aggregator | Unified API; less adapter work | Third-party dependency; potential cost; data ownership concern; offline operation impossible | Violates local-first; data sovereignty concern |
| Dynamic plugin loading at runtime | Maximum flexibility | Security risk (arbitrary code execution); complex sandboxing; over-engineered for v1 | Over-engineered; security risk |
| GraphQL federation per source | Modern API composition | Adds GraphQL dependency; not meaningfully better than the interface pattern for this problem | Adds complexity without benefit |

---

## Consequences

**Positive:**
- Any music source can be added without touching core services.
- Local Files adapter provides full offline capability.
- Clean anti-corruption layer: core system is insulated from source API changes.
- Capability flags allow graceful degradation (e.g. skip BPM scoring if adapter doesn't provide it).
- Adapters fail in isolation; one adapter crashing does not affect others.

**Negative / Trade-offs:**
- Adapter authors must implement the full interface; there is no partial-implementation shortcut.
- `resolve()` returns short-lived stream URLs that may require per-user auth tokens (e.g. Spotify); the adapter must manage token refresh.
- Metadata quality varies by source; the Profile Scorer must handle null fields gracefully.

---

## Related ADRs

- [ADR-005: Auth Model](ADR-005-auth-model.md) — user tokens may need to be delegated to adapters (e.g. Spotify OAuth per user)
- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — Local Files adapter is the primary offline-capable source on Tier 1
