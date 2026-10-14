# ADR-011: Messaging Architecture — Service Bus (Internal) + Web PubSub MQTT (Client Push)

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox requires two distinct messaging layers:

1. **Internal event bus** — backend services communicating with each other. Queue Service notifies Profile Service of playback events. Veto timer triggers queue promotion. These are server-to-server messages that must be reliable, ordered, and decoupled.

2. **Client push** — real-time events delivered to browser tab players, Raspberry Pi daemon players, and the owner dashboard. These require low latency, pub/sub group semantics (one venue = one group), and must work for both browser (WebSocket) and daemon (native TCP) clients.

The initial MVP plan conflated these two concerns by using Azure SignalR Service for both. This is architecturally incorrect: SignalR Service is a client notification layer, not a backend message broker.

Additionally, the Venue Player spec (`docs/spec/08-venue-player-spec.md`) defines the Raspberry Pi daemon as a first-class deployment target. Using a proprietary protocol (SignalR) for client push would require a SignalR .NET client on the RPi, creating a vendor dependency on a protocol not suited to constrained devices.

MQTT is the natural protocol for both browser (via WebSocket) and embedded/IoT devices (via native TCP). Azure Web PubSub supports MQTT 3.1.1 and 5.0 alongside its native WebSocket protocol — a single managed service that handles all client types.

---

## Decision

Use **two dedicated messaging services**, each handling a distinct concern:

### Internal Event Bus: Azure Service Bus

- **Service:** Azure Service Bus (Standard tier for MVP; Premium for production scale)
- **Model:** Topics and subscriptions
- **Usage:** Backend service-to-service messaging only. Never exposed to clients.
- **Key topics:**
  - `queue-events` — queue mutations, QueueItem status changes
  - `recommendation-events` — recommendation lifecycle (submitted, promoted, vetoed)
  - `playback-events` — player state changes (trackEnded, stateChanged)
  - `profile-events` — feedback loop events (play, skip, veto, complete)
- **Why Service Bus over alternatives:** Guaranteed delivery, dead-letter queues, message sessions for ordered processing, built-in retry/backoff, native .NET SDK, direct Azure integration.

### Client Push: Azure Web PubSub with MQTT

- **Service:** Azure Web PubSub (Free tier for MVP; Standard for scale)
- **Protocol:** MQTT 3.1.1 / 5.0 (browser clients via MQTT over WebSocket; RPi daemon via native MQTT TCP)
- **Topic structure:**
  ```
  venues/{venueId}/queue.updated
  venues/{venueId}/nowplaying.changed
  venues/{venueId}/recommendation.received
  venues/{venueId}/recommendation.promoted
  venues/{venueId}/mode.changed
  venues/{venueId}/player/{playerId}/heartbeat
  venues/{venueId}/player/{playerId}/state
  ```
- **Authentication:** SAS token or Managed Identity for server-side publish; client tokens issued by the API at connection time.
- **QoS:** QoS 1 (at-least-once) for `queue.updated` and `nowplaying.changed`; QoS 0 (at-most-once) for `player.heartbeat`.
- **Why Web PubSub + MQTT over SignalR Service:** Open standard protocol (no vendor SDK required on client), supports RPi native MQTT TCP alongside browser MQTT-over-WSS from one service, QoS levels align with the spec's offline-resilience requirements, MQTT topic hierarchy maps directly to the spec's event channel contract.
- **Why Web PubSub over Event Grid MQTT Broker:** Web PubSub is a more mature service with better .NET SDK support, cleaner REST publish API from the backend, and a simpler pricing model for the MVP traffic volume.

### Flow

```
Queue Service
  → publish to Service Bus topic "queue-events"
    → API Service Bus consumer receives message
      → API publishes to Web PubSub:
          topic: venues/{venueId}/queue.updated
            → Browser tab clients (MQTT.js over WSS)
            → RPi daemon players (native MQTT TCP)
            → Owner dashboard (MQTT.js over WSS)
```

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Azure SignalR Service (original plan) | Mature; easy .NET integration; good browser support | Proprietary SignalR protocol; RPi requires SignalR .NET client; no QoS; no offline buffering | Vendor lock-in; poor RPi fit |
| Azure SignalR Service + separate MQTT broker for RPi | Each client gets best protocol | Two separate services; operational overhead; topic sync between services | Two services for one concern |
| Azure Event Grid MQTT Broker | Native MQTT; same protocol for all clients | Newer service; less mature .NET SDK; more complex auth model (X.509 primary); less straightforward publish from API | Web PubSub is more mature; better SDK |
| Server-Sent Events (SSE) — no Azure service | Zero cost; no SDK; browser-native `EventSource` | One-directional only; RPi daemon still needs separate connection; no QoS | Doesn't serve RPi natively; no QoS |
| WebSocket (custom, no managed service) | Full control; no dependency | Requires managing connection state across Container App replicas; operational burden | Operational complexity at scale |
| Azure Service Bus for both internal and client push | One service | Service Bus is not designed for browser WebSocket connections; no browser SDK | Wrong abstraction for client push |

---

## Consequences

**Positive:**
- One open-standard protocol (MQTT) serves all client types: browsers, RPi daemons, future mobile apps, future third-party players.
- QoS 1 delivery for critical events (`queue.updated`, `nowplaying.changed`) aligns with local-first resilience requirements.
- MQTT persistent sessions allow temporarily offline players to receive buffered messages on reconnect.
- Service Bus provides dead-letter queues, retry policies, and message ordering for backend reliability.
- The spec's event channel contract maps directly to MQTT topics — no translation layer needed.
- Protocol portability: MQTT clients work with any MQTT broker; swapping Web PubSub for a self-hosted broker (e.g. Mosquitto on Tier 1/2) requires only a connection string change.
- Web PubSub free tier handles the MVP traffic volume at zero cost.

**Negative / Trade-offs:**
- MQTT.js adds ~40KB to the browser bundle (CDN delivery mitigates this).
- MQTT client authentication requires issuing connection tokens from the API — slightly more setup than bearer token on WebSocket.
- Two managed Azure services instead of one (but each is correctly scoped to its concern).
- The .NET Service Bus SDK and Web PubSub SDK add dependency surface area.
- The API process bridges the two systems (Service Bus consumer → Web PubSub publisher); this bridge must be reliable and monitored.

---

## MQTT Topic Access Control

Client tokens issued by the API must restrict MQTT topic access:

| Client type | Subscribe permission | Publish permission |
|---|---|---|
| Browser (guest) | `venues/{venueId}/#` | `venues/{venueId}/player/+/heartbeat` only |
| Browser (admin) | `venues/{venueId}/#` | `venues/{venueId}/player/+/heartbeat` |
| RPi daemon player | `venues/{venueId}/#` | `venues/{venueId}/player/{playerId}/#` |
| API (backend) | None | `venues/#` (all topics) |

---

## Related ADRs

- [ADR-003: Venue Player Abstraction](ADR-003-venue-player-abstraction.md) — RPi daemon is a first-class player; MQTT TCP is its natural protocol
- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — Tier 1 (RPi) can use Mosquitto as a local MQTT broker; same client code
- [ADR-010: Azure MVP Topology](ADR-010-azure-mvp-topology.md) — updated to reflect this architecture
