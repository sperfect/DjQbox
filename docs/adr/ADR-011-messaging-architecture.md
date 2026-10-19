# ADR-011: Messaging Architecture — Service Bus (Internal) + Web PubSub MQTT 5 (Client Push)

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

MQTT is the natural protocol for both browser (via WebSocket) and embedded/IoT devices (via native TCP). Azure Web PubSub supports MQTT 3.1.1 and MQTT 5.0 alongside its native WebSocket protocol — a single managed service that handles all client types.

MQTT 5 is chosen over MQTT 3.1.1 because several of its new features map directly to DJQBox requirements: message expiry for heartbeats, user properties for correlation, will delay for graceful offline detection, session expiry for scoped guest sessions, shared subscriptions for multi-replica API consumers, and request/response for player command acknowledgement. See the MQTT 5 Feature Usage section below.

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

### Client Push: Azure Web PubSub with MQTT 5

- **Service:** Azure Web PubSub (Free tier for MVP; Standard for scale)
- **Protocol:** MQTT 5.0 (browser clients via MQTT over WebSocket; RPi daemon via native MQTT TCP)
- **Topic structure:**
  ```
  venues/{venueId}/queue.updated
  venues/{venueId}/nowplaying.changed
  venues/{venueId}/recommendation.received
  venues/{venueId}/recommendation.promoted
  venues/{venueId}/mode.changed
  venues/{venueId}/player/{playerId}/heartbeat
  venues/{venueId}/player/{playerId}/state
  venues/{venueId}/player/{playerId}/cmd
  venues/{venueId}/player/{playerId}/cmd/ack
  ```
- **Authentication:** SAS token or Managed Identity for server-side publish; client tokens issued by the API at connection time.
- **QoS:** QoS 1 (at-least-once) for `queue.updated`, `nowplaying.changed`, `cmd`; QoS 0 (at-most-once) for `player.heartbeat` and `positionUpdated`.

### Flow

```
Queue Service
  → publish to Service Bus topic "queue-events"
    → API Service Bus consumer receives message
      → API publishes to Web PubSub MQTT 5:
          topic: venues/{venueId}/queue.updated
          user properties: { venue_id, event_type, correlation_id }
          message expiry: 30s
            → Browser tab clients (MQTT.js v5 over WSS)
            → RPi daemon players (native MQTT 5 TCP)
            → Owner dashboard (MQTT.js v5 over WSS)
```

---

## MQTT 5 Feature Usage

The following MQTT 5 features are used by DJQBox and why:

### Message Expiry Interval
Set on every published message. Prevents stale events from being delivered to clients that reconnect after a delay.

| Topic | Expiry | Rationale |
|---|---|---|
| `player.heartbeat` | 15 seconds | A heartbeat older than one interval is meaningless; never deliver stale heartbeats |
| `positionUpdated` | 10 seconds | Position data is immediately stale; discard if not delivered promptly |
| `queue.updated` | 60 seconds | Queue changes are significant; allow some buffering for brief disconnects |
| `nowplaying.changed` | 60 seconds | Same as queue.updated |
| `cmd` | 30 seconds | Player commands older than 30 seconds should not be executed |

### User Properties
Every message published by the API carries standard user properties in the MQTT 5 envelope. Clients can route and filter messages without deserialising the JSON payload.

| Property key | Value | Present on |
|---|---|---|
| `venue_id` | UUID | All topics |
| `event_type` | e.g. `queue.updated` | All topics |
| `correlation_id` | UUID | `cmd` and `cmd/ack` |
| `api_version` | e.g. `1` | All topics |
| `timestamp` | Unix milliseconds | All topics |

### Will Delay Interval
Players set a Last Will and Testament (LWT) message on their MQTT connection with a will delay interval. This prevents false "player offline" alerts during brief network blips.

| Client type | Will delay | LWT topic | LWT payload |
|---|---|---|---|
| RPi daemon | 30 seconds | `venues/{venueId}/player/{playerId}/state` | `{ "status": "offline" }` |
| Browser tab | 10 seconds | `venues/{venueId}/player/{playerId}/state` | `{ "status": "offline" }` |

If the player reconnects and sends a PINGREQ within the will delay window, the LWT is cancelled and no "offline" event is published.

### Session Expiry Interval
Replaces the binary MQTT 3.1.1 clean/persistent session flag with a configurable TTL in seconds.

| Client type | Session expiry | Rationale |
|---|---|---|
| Guest browser | 86400 (24 hours) | Matches guest token TTL; buffered messages available for the session lifetime |
| Admin browser | 28800 (8 hours) | Matches admin token TTL |
| RPi daemon | 0 (persistent) | Daemon maintains session indefinitely; receives all buffered QoS 1 messages on reconnect |
| API publisher | 0 (clean) | API does not subscribe; no session state needed |

### Shared Subscriptions
When the API scales to multiple Container App replicas, each replica must not process the same heartbeat or state message multiple times. Shared subscriptions ensure each message is delivered to exactly one replica.

API replicas subscribe using the shared subscription syntax:
```
$share/api-consumers/venues/+/player/+/heartbeat
$share/api-consumers/venues/+/player/+/state
```

### Request / Response Pattern
MQTT 5 `response_topic` and `correlation_data` properties enable structured command acknowledgement between the API and players.

**Player command flow:**
```
API publishes to:
  topic:           venues/{venueId}/player/{playerId}/cmd
  payload:         { "command": "play", "trackRef": {...}, "streamUrl": "..." }
  response_topic:  venues/{venueId}/player/{playerId}/cmd/ack
  correlation_data: <uuid>
  qos:             1
  message_expiry:  30s

Player receives command, executes it, then publishes to:
  topic:           venues/{venueId}/player/{playerId}/cmd/ack
  payload:         { "status": "ok" | "error", "error_code": "..." }
  correlation_data: <same uuid>  ← matches original command
  qos:             0
```

This closes the command loop and removes the need for a separate REST polling mechanism to confirm player commands.

### Reason Codes
MQTT 5 reason codes on CONNACK, PUBACK, SUBACK, and DISCONNECT replace the binary success/failure of MQTT 3.1.1. The API uses reason codes to give clients actionable feedback:

| Reason code | Meaning in DJQBox |
|---|---|
| `0x87` Not Authorized | Client token is invalid or expired |
| `0x99` Quota Exceeded | Client is publishing heartbeats too frequently |
| `0x9C` Use Another Server | Web PubSub is redirecting to a different endpoint |
| `0x8D` Keep Alive Timeout | Server did not receive PINGREQ within the keep-alive window |

### Flow Control (Receive Maximum)
Clients set `receiveMaximum` on CONNECT to prevent the broker from overwhelming a slow browser or RPi with inflight messages.

| Client type | Receive maximum | Rationale |
|---|---|---|
| Browser tab | 10 | Browser event loop; avoid flooding slow connections |
| RPi daemon | 20 | Daemon has more headroom but is still a constrained device |
| API publisher | 100 | API process is well-resourced |

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| MQTT 3.1.1 (original) | Simpler; wider legacy client support | No message expiry, user properties, will delay, shared subscriptions, or request/response | Misses key features needed for DJQBox reliability and scalability |
| Azure SignalR Service | Mature; easy .NET integration | Proprietary protocol; RPi requires SignalR .NET client; no QoS; no message expiry | Vendor lock-in; poor RPi fit |
| Azure Event Grid MQTT Broker | Native MQTT 5 | Newer service; less mature .NET SDK; more complex auth model | Web PubSub is more mature; better SDK |
| Server-Sent Events (SSE) | Zero cost; browser-native | One-directional; no QoS; no RPi native support | Doesn't serve RPi; no QoS |
| Custom WebSocket server | Full control | Requires managing connection state across replicas; no MQTT features | Operational complexity; reinvents MQTT |

---

## Consequences

**Positive:**
- MQTT 5 message expiry prevents stale heartbeats and position updates reaching reconnecting clients.
- User properties allow client-side routing without payload deserialisation.
- Will delay interval eliminates false "player offline" alerts on brief network blips.
- Session expiry replaces binary clean/persistent session with a configurable TTL per client type.
- Shared subscriptions enable correct multi-replica API consumption of player events.
- Request/response pattern closes the player command loop without a separate polling mechanism.
- Reason codes give clients actionable connection failure information.
- One open-standard protocol (MQTT 5) serves all client types uniformly.
- Protocol portability: MQTT 5 clients work with any MQTT 5 broker; Mosquitto 2.x supports MQTT 5 for Tier 1/2 deployments.

**Negative / Trade-offs:**
- MQTT.js must be version 5.x (supports MQTT 5); older MQTT.js 4.x is MQTT 3.1.1 only.
- MQTTnet on .NET must be version 4.x for full MQTT 5 support.
- Mosquitto requires version 2.0+ for MQTT 5 on Tier 1/2.
- MQTT 5 connection options (session expiry, receive maximum, will delay) add connection setup complexity compared to MQTT 3.1.1.
- Shared subscription syntax (`$share/...`) is MQTT 5 / broker-specific; verify Azure Web PubSub supports it.

---

## MQTT 5 Topic Access Control

Client tokens issued by the API must restrict MQTT topic access:

| Client type | Subscribe permission | Publish permission |
|---|---|---|
| Browser (guest) | `venues/{venueId}/#` | `venues/{venueId}/player/+/heartbeat` only |
| Browser (admin) | `venues/{venueId}/#` | `venues/{venueId}/player/+/heartbeat` |
| RPi daemon player | `venues/{venueId}/#` | `venues/{venueId}/player/{playerId}/#` |
| API (backend) | `$share/api-consumers/venues/#` | `venues/#` |

---

## Client Library Versions

| Client type | Library | Minimum version | MQTT 5 support |
|---|---|---|---|
| Browser | MQTT.js | 5.0.0 | Full MQTT 5 |
| .NET daemon (RPi) | MQTTnet | 4.3.0 | Full MQTT 5 |
| .NET API (publisher) | MQTTnet | 4.3.0 | Full MQTT 5 |
| Local broker (Tier 1/2) | Mosquitto | 2.0.0 | Full MQTT 5 |

---

## Related ADRs

- [ADR-003: Venue Player Abstraction](ADR-003-venue-player-abstraction.md) — RPi daemon is a first-class player; MQTT 5 TCP is its natural protocol
- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — Tier 1 (RPi) uses Mosquitto 2.x as local MQTT 5 broker; same client code
- [ADR-010: Azure MVP Topology](ADR-010-azure-mvp-topology.md) — Azure Web PubSub hosts the MQTT 5 broker for Tier 3

---

## Changelog

### v1.1.0 — 2025-07-01
- Upgraded MQTT protocol from 3.1.1 to MQTT 5.
- Added MQTT 5 Feature Usage section: message expiry, user properties, will delay interval, session expiry interval, shared subscriptions, request/response pattern, reason codes, flow control.
- Added cmd/ack topic to topic structure.
- Added client library versions table.
- Updated consequences to reflect MQTT 5 trade-offs.

### v1.0.0 — 2025-07-01
- Initial version: Service Bus (internal) + Web PubSub MQTT 3.1.1/5.0 (client push).
