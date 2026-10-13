# ADR-003: Venue Player Abstraction

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox must control audio playback at venues. The output hardware and environment varies widely across the deployment spectrum:

- A browser tab on a laptop or tablet (Tier 1 home, Tier 2 café)
- A Raspberry Pi with a USB DAC or 3.5mm audio output (Tier 1 embedded)
- A Sonos speaker system (possible future integration)
- A Chromecast Audio or AirPlay receiver (possible future integration)

Hardcoding any single playback mechanism would prevent deployment flexibility and lock the system to a specific technology stack. The system must also handle players going offline (network loss, browser tab closed, daemon crash) without losing queue state.

---

## Decision

Define an **abstract Venue Player interface** with standardized commands and events. Any conforming implementation is a valid player.

**Commands (Host → Player):** `play`, `pause`, `resume`, `skip`, `setVolume`, `getState`, `preload`

**Events (Player → Host):** `stateChanged`, `trackEnded`, `positionUpdated`, `error`

Players register with the system at startup, declare capability flags, and maintain a heartbeat connection. The system ships two reference implementations:

1. **Browser Tab Player** — uses Web Audio / HTMLMediaElement API; runs in a web browser context.
2. **Daemon Player** — lightweight background process; controls system audio hardware directly; suitable for Raspberry Pi.

New player types (Sonos, Chromecast, AirPlay) can be added by implementing the same interface and manifest without modifying any core service.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Browser-only playback | Simple; no daemon required | Cannot run headless; requires a screen and browser at every venue | Excludes Raspberry Pi and embedded deployments |
| Direct Sonos/Chromecast integration in core | Native support for popular hardware | Vendor lock-in; kills Raspberry Pi use case; requires updates when vendor APIs change | Vendor lock-in unacceptable |
| DLNA/UPnP media server protocol | Widely supported by audio hardware | Heavy specification; poor web integration; complex to implement; not aligned with web-first architecture | Over-engineered |
| OS audio API directly in the main process | No separate player process | Ties the main process to audio hardware; prevents cloud deployment; not viable on Tier 3 | Architecture violation |

---

## Consequences

**Positive:**
- Any device that implements the interface is a valid player; the system is hardware-agnostic.
- New player types added without modifying core services; clean separation of concerns.
- Offline resilience: players continue playing locally during connectivity loss; reconnect with backoff.
- Players self-describe their capabilities; the system adapts commands accordingly.

**Negative / Trade-offs:**
- Additional protocol complexity: registration, heartbeat, and event routing must be implemented correctly.
- Developers implementing new player types must understand the full interface contract.
- A player going offline mid-track causes a gap; the system must handle `player.offline` gracefully (hold queue state, resume on reconnect).

---

## Related ADRs

- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — deployment tier determines which player implementations are typical
