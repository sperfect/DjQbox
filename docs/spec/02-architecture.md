---
title: DJQBox — System Architecture
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — System Architecture

## Overview

DJQBox follows a service-oriented architecture where logical service boundaries are consistent across all deployment tiers. On a single Raspberry Pi, all services run as modules within one process, sharing an in-process event bus and an embedded database. On a distributed cloud deployment, each service is an independently deployed unit communicating over a network message broker. The architectural contracts — interfaces, events, and data shapes — are identical regardless of tier.

The guiding principle is that **deployment topology is a runtime concern, not an architectural one**. Core abstractions (Venue Player interface, Source Adapter interface, Event Bus contract) do not change when the deployment changes.

---

## Deployment Topology

```plantuml
@startuml DJQBox-Deployment-Topology
skinparam backgroundColor #FEFEFE
skinparam node {
  BackgroundColor #FFF7ED
  BorderColor #EA580C
}
skinparam component {
  BackgroundColor #EEF2FF
  BorderColor #4F46E5
}
skinparam database {
  BackgroundColor #F0FDF4
  BorderColor #16A34A
}
title DJQBox — Deployment Topology (All Tiers)

node "Tier 1 - Single Node (RPi)" as T1 {
  component "DJQBox Process [API + Queue + Profile + Auth + Sync]" as P1
  component "In-process Event Bus" as EB1
  database "SQLite" as DB1
  component "Venue Player Daemon (same host)" as VP1
  P1 <--> EB1
  P1 --> DB1
  VP1 <--> EB1 : local IPC
}

node "Tier 2 - Self-Hosted VPS" as T2 {
  component "API Gateway" as AG2
  component "Queue Service" as QS2
  component "Profile Service" as PS2
  component "Auth Service" as AS2
  component "Sync Service" as SS2
  component "Message Broker (lightweight)" as EB2
  database "Standalone DB" as DB2
  AG2 --> QS2
  AG2 --> PS2
  AG2 --> AS2
  AG2 --> SS2
  QS2 <--> EB2
  PS2 <--> EB2
  QS2 --> DB2
  PS2 --> DB2
  AS2 --> DB2
  SS2 --> DB2
}

node "Tier 3 - Distributed Cloud" as T3 {
  component "API Gateway (load balanced)" as AG3
  component "Queue Service (replicated)" as QS3
  component "Profile Service (replicated)" as PS3
  component "Auth Service (replicated)" as AS3
  component "Sync Service (replicated)" as SS3
  component "Managed Message Broker" as EB3
  database "Managed DB (replicated)" as DB3
  AG3 --> QS3
  AG3 --> PS3
  AG3 --> AS3
  AG3 --> SS3
  QS3 <--> EB3
  PS3 <--> EB3
  QS3 --> DB3
  PS3 --> DB3
  AS3 --> DB3
  SS3 --> DB3
}
@enduml
```

---

## Component Architecture

```plantuml
@startuml DJQBox-Component-Architecture
skinparam componentStyle rectangle
skinparam backgroundColor #FEFEFE
skinparam component {
  BackgroundColor #EEF2FF
  BorderColor #4F46E5
}
title DJQBox — Internal Component Architecture

package "API Gateway" {
  component "Request Router" as RR
  component "Auth Middleware" as AM
  component "WebSocket Manager" as WM
  RR --> AM
  RR --> WM
}

package "Auth Service" {
  component "Identity Provider Integration" as IDP
  component "Token Issuer" as TI
  component "Session Store" as SES
  IDP --> TI
  TI --> SES
}

package "Queue Service" {
  component "Queue Manager" as QM
  component "Recommendation Processor" as RP
  component "Veto Window Timer" as VWT
  component "Source Adapter Registry" as SAR
  QM --> RP
  RP --> VWT
  QM --> SAR
}

package "Profile Service" {
  component "Profile Manager" as PM
  component "Scorer" as SC
  component "Feedback Processor" as FP
  PM --> SC
  PM --> FP
}

package "Sync Service" {
  component "Delta Manager" as DM
  component "Epoch Tracker" as ET
  component "Conflict Resolver LWW/HLC" as CR
  DM --> ET
  DM --> CR
}

package "Event Bus" {
  component "Publisher" as PUB
  component "Subscriber Registry" as SUB
  PUB --> SUB
}

RR --> QM
RR --> PM
RR --> TI
RR --> DM
QM --> PUB : queue.updated / nowplaying.changed
RP --> PUB : recommendation.received / veto.cast
PM --> PUB : profile.updated
@enduml
```

---

## Data Flow — Key Scenarios

### Scenario 1: Guest Submits a Recommendation in DJ Mode

```plantuml
@startuml DJQBox-Flow-DJMode
skinparam backgroundColor #FEFEFE
title Guest Submits Recommendation — DJ Mode

actor "Guest (Alex)" as G
participant "API Gateway" as API
participant "Auth Service" as AS
participant "Queue Service" as QS
participant "Event Bus" as EB
actor "DJ/Admin (Carlos)" as DJ

G -> API : POST /venues/{id}/recommendations
API -> AS : validate guest token
AS --> API : token valid, scope: queue:recommend
API -> QS : createRecommendation(venueId, trackRef, guestId)
QS -> QS : validate venue is in DJ Mode
QS -> QS : score recommendation against Music Profile
QS --> API : 201 Created { recommendationId, status: pending }
API --> G : 201 Created
QS -> EB : publish recommendation.received
EB -> DJ : push recommendation.received (WebSocket)
DJ -> API : PATCH /recommendations/{id} { action: accept }
API -> QS : acceptRecommendation(recommendationId)
QS -> QS : append TrackRef to Queue as QueueItem
QS -> EB : publish queue.updated
EB -> G : push queue.updated (status: queued)
EB -> DJ : push queue.updated
@enduml
```

### Scenario 2: Autopilot Veto Window Expires — Track Promoted

```plantuml
@startuml DJQBox-Flow-Autopilot
skinparam backgroundColor #FEFEFE
title Autopilot: Veto Window Expires — Track Promoted

actor "Guest (Alex)" as G
participant "API Gateway" as API
participant "Queue Service" as QS
participant "Veto Window Timer" as VWT
participant "Event Bus" as EB
participant "Venue Player" as VP

G -> API : POST /venues/{id}/recommendations
API -> QS : createRecommendation(venueId, trackRef, guestId)
QS -> QS : validate venue is in Autopilot Mode
QS -> VWT : startTimer(recommendationId, vetoWindowSeconds)
QS --> API : 201 Created { status: pending, vetoDeadline: T+60s }
API --> G : 201 Created

note over VWT : 60 seconds pass\nno veto cast

VWT -> QS : timerExpired(recommendationId)
QS -> QS : promote recommendation, add to Queue
QS -> EB : publish queue.updated
QS -> EB : publish recommendation.promoted
EB -> G : push recommendation status: promoted
EB -> VP : push queue.updated
VP -> VP : load next QueueItem if idle
@enduml
```

---

## Service Descriptions

### API Gateway
**Responsibility:** Single entry point for all client traffic. Routes requests to internal services, enforces authentication via Auth Service middleware, manages WebSocket connections, and fans out push events from the Event Bus to connected clients.
**Inputs:** HTTP requests and WebSocket connections from Owner Dashboard, Guest UI, Venue Player clients.
**Outputs:** HTTP responses, WebSocket push events.
**Data owned:** None. Stateless.

### Queue Service
**Responsibility:** Manages the queue for each venue, processes recommendations, runs the Veto Window timer in Autopilot Mode, controls QueueItem lifecycle (pending to playing to played/skipped), and dispatches to Source Adapters to resolve stream URLs.
**Inputs:** Queue operation commands from API Gateway; timer callbacks from Veto Window Timer; playback state events from Venue Players.
**Outputs:** `queue.updated`, `nowplaying.changed`, `recommendation.received`, `recommendation.promoted`, `veto.cast` events on the Event Bus.
**Data owned:** Queue, QueueItem, Recommendation, Veto.

### Profile Service
**Responsibility:** Manages the Music Profile for each venue, scores incoming recommendations against the active profile, processes feedback events (play, skip, veto) to update profile weights.
**Inputs:** Profile read/write commands from API Gateway; playback feedback events from Queue Service.
**Outputs:** `profile.updated` events on the Event Bus; recommendation compatibility scores returned synchronously.
**Data owned:** MusicProfile, ProfileFeedbackEvent.

### Auth Service
**Responsibility:** Handles all identity and authorization. Issues and validates tokens for owners (OAuth/passkey), admins (elevated venue scope), and guests (QR-derived venue token). Manages session lifecycle and revocation.
**Inputs:** Auth commands from API Gateway (login, token validation, refresh, revocation).
**Outputs:** Signed tokens; session records.
**Data owned:** User, Session, VenueMembership.

### Sync Service
**Responsibility:** Manages delta sync between the local store and remote peers or cloud. Tracks sync epochs per peer, computes delta sets since a given epoch, applies incoming deltas with HLC-based conflict resolution, and triggers full sync when epoch gaps are detected.
**Inputs:** Sync request commands from API Gateway; local write events (to build delta log).
**Outputs:** Delta payloads; epoch acknowledgements.
**Data owned:** SyncEpoch, delta operation log.

### Event Bus
**Responsibility:** Decouples event producers from event consumers. On single-node deployments, this is an in-process pub/sub mechanism. On distributed deployments, this is a message broker.
**Inputs:** Published events from any service.
**Outputs:** Delivered events to all registered subscribers.
**Data owned:** In-flight events only; no persistent storage required.

---

## Edge Devices — Venue Player Integration

Venue Players (browser tab or daemon) integrate with the system as follows:

1. **Registration:** The player sends a registration request to `POST /api/v1/venues/{id}/players` at startup, declaring its type, capabilities, and version. The server responds with a `player_id` and a scoped player token.
2. **Command reception:** The player subscribes to the Event Bus (via WebSocket) for events scoped to its venue. When a `queue.updated` event is received and the player is idle, it fetches the next QueueItem and begins playback.
3. **Heartbeat:** The player sends a `player.heartbeat` WebSocket message every 15 seconds. If the server receives no heartbeat for 45 seconds (3x interval), it marks the player as offline and emits a `player.offline` event.
4. **State reporting:** The player emits `stateChanged` events when playback state changes (playing, paused, error). The server reflects this in the Now Playing state.

---

## Quality Attributes

### Offline Resilience
All queue operations, playback control, and recommendation processing function without network connectivity. Local state is the primary source of truth. Sync occurs opportunistically when connectivity is restored.

### Horizontal Scalability (Tier 3 only)
On distributed deployments, the Queue Service, Profile Service, and Auth Service are stateless (state in the database; coordination via Event Bus) and can be horizontally scaled. This attribute is not relevant or required for Tier 1 or Tier 2.

### Extensibility
New Source Adapters and Venue Player implementations can be added without modifying any core service. Both use a registration-at-startup pattern and a defined interface contract.

### Privacy by Default
No data leaves the deployment host unless the operator configures a remote sync target. Guest sessions are scoped to a single venue. No telemetry is collected.

### Single-Node Deployability
The entire system — all services, event bus, database, and a Venue Player daemon — runs on a Raspberry Pi 4 with 4GB RAM, consuming under 500MB RAM under normal load.

---

## Single-Node vs Distributed Operation

| Concern | Single Node (Tier 1) | Distributed (Tier 3) |
|---|---|---|
| Service boundaries | Module boundaries in one process | Network boundaries between processes |
| Event Bus | In-process function call / in-memory queue | Network message broker |
| Database | SQLite (embedded, single file) | Managed relational or document store |
| Auth token signing | Key stored in local file | Key stored in secrets manager |
| Venue Player | Daemon on same host, local IPC | Remote device via WebSocket |
| Sync | Not applicable (no remote) | Epoch-based delta sync to cloud or peer |
| Scaling | Vertical only | Horizontal (add service replicas) |
