---
title: DJQBox — Venue Player Interface Specification
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — Venue Player Interface Specification

## Overview

The Venue Player is the audio output component of a venue. It is responsible for receiving playback commands from the Queue Service and controlling the physical or virtual audio output at the venue. DJQBox defines an abstract Venue Player interface — any conforming implementation is a valid player.

Two reference implementations are defined: a Browser Tab Player (for web-based deployments) and a Daemon Player (for headless or embedded deployments such as Raspberry Pi). Third-party implementations (Sonos, Chromecast, AirPlay, etc.) are possible by implementing the same interface.

---

## Interface Definition

### Commands (Host → Player)

Commands are sent to the player via the Event Bus or WebSocket for remote players.

| Command | Parameters | Description |
|---|---|---|
| `play` | `trackRef: TrackRef`, `streamUrl: string` | Begin playback of the specified track. The `streamUrl` is resolved by the Queue Service from the Source Adapter before sending. |
| `pause` | — | Pause playback at the current position. |
| `resume` | — | Resume playback from the paused position. |
| `skip` | — | Stop current track and advance to next item in queue. |
| `setVolume` | `level: float [0.0–1.0]` | Set playback volume. 0.0 = muted, 1.0 = maximum. |
| `getState` | — | Request current player state. Player responds with a `stateChanged` event. |
| `preload` | `trackRef: TrackRef`, `streamUrl: string` | Optional. Pre-fetch the stream for the next queue item to reduce gap between tracks. |

### Events (Player → Host)

Events are emitted by the player to the Event Bus or WebSocket.

| Event | Payload | When Emitted |
|---|---|---|
| `stateChanged` | `PlayerState` | Whenever player state changes, or in response to `getState`. |
| `trackEnded` | `{ trackRef, reason: "complete" or "error" }` | When the current track finishes naturally or errors. |
| `positionUpdated` | `{ trackRef, positionSeconds: float }` | Every 5 seconds during active playback, and on seek. |
| `error` | `{ code: ErrorCode, detail: string }` | When an unrecoverable error occurs. |

### PlayerState Type

```
PlayerState {
  status:          "idle" | "loading" | "playing" | "paused" | "error"
  trackRef:        TrackRef | null          // null when idle
  positionSeconds: float                    // 0.0 when idle or loading
  durationSeconds: float | null             // null if unknown
  volume:          float                    // current volume level 0.0–1.0
  errorDetail:     string | null            // populated when status = "error"
}
```

---

## Registration Protocol

Before a Venue Player can receive commands, it must register with the system.

**Request:**
```
POST /api/v1/venues/{venueId}/players
Authorization: Bearer <admin or player token>

{
  "player_type": "browser_tab | daemon | custom",
  "display_name": "Main Speaker (Raspberry Pi)",
  "version": "1.0.0",
  "capabilities": {
    "streaming": true,
    "local_files": true,
    "volume_control": true,
    "position_seeking": false,
    "offline_playback": true
  }
}
```

**Response 201:**
```json
{
  "player_id": "uuid",
  "player_token": "scoped-token-for-player-commands",
  "heartbeat_interval_seconds": 15
}
```

The `player_token` is a scoped token with `player:control` scope for the specific venue. It is used to authenticate the player's WebSocket connection.

---

## Heartbeat Protocol

An active Venue Player must send a heartbeat message every `heartbeat_interval_seconds` (15 by default).

**Heartbeat message (Client → Server, WebSocket):**
```json
{
  "event": "player.heartbeat",
  "player_id": "uuid",
  "venue_id": "uuid",
  "state": "playing | paused | idle | error",
  "timestamp": "2025-07-01T09:10:15Z"
}
```

**Server behavior:**
- If no heartbeat is received within `3 × heartbeat_interval_seconds` (45 seconds default), the server marks the player as `offline`.
- A `player.offline` event is published on the Event Bus and delivered to the owner's dashboard.
- When the player reconnects and sends a heartbeat, it transitions back to `online` and re-registers if necessary.
- Playback commands are not sent to an offline player. The Queue Service holds the `playing` state until the player reconnects.

---

## Reference Implementation A: Browser Tab Player

The Browser Tab Player runs in a web browser context. It is suitable for venues where a computer or tablet is left open with the DJQBox player page active.

**Implementation Notes:**

- **Audio playback:** Uses the `HTMLMediaElement` API (an `<audio>` element) for stream playback. The `streamUrl` is set as the `src` attribute.
- **Media controls:** Implements the Web MediaSession API to surface now-playing metadata and playback controls in the OS-level media UI (lock screen, notification bar).
- **Position reporting:** Listens to the `timeupdate` event on the media element to emit `positionUpdated` at 5-second intervals.
- **Track end detection:** Listens to the `ended` event on the media element to emit `trackEnded`.
- **Volume control:** Uses `HTMLMediaElement.volume` property. Capability flag `volume_control: true`.
- **Offline resilience:** The player page should be served by a service worker so it loads even when offline. Stream URLs from the Local Files adapter can be served from cache.
- **Tab visibility:** Uses the Page Visibility API to detect when the tab is hidden and maintain playback state. A background audio context keeps audio playing when the tab is not focused.
- **Reconnection:** If the WebSocket connection drops, the player attempts reconnection with exponential backoff (1s, 2s, 4s, max 30s). Playback continues locally during reconnection.

---

## Reference Implementation B: Daemon Player

The Daemon Player is a lightweight background process running on a host machine, typically a Raspberry Pi or similar single-board computer. It controls system audio output directly.

**Implementation Notes:**

- **Process type:** A long-running background process. On Linux: managed by `systemd`. On macOS: managed by `launchd`. The process starts automatically at boot.
- **Audio output:** Uses the host operating system's audio API to play streams. On Linux: ALSA or PulseAudio. On macOS: CoreAudio. The specific API is abstracted behind the player interface.
- **Stream fetching:** Downloads the stream URL incrementally and pipes it to the audio output. Handles HTTP range requests for seeking if `position_seeking` is declared as a capability.
- **Volume control:** Calls the OS volume API. Capability flag `volume_control: true` if the OS API is available.
- **Local files:** If the Source Adapter is Local Files, the daemon reads file paths directly from the local filesystem. Capability flags `local_files: true`, `offline_playback: true`.
- **WebSocket connection:** Connects to the Deployment Host's WebSocket endpoint using the `player_token` from registration. If the Deployment Host is on the same device (Tier 1), uses `localhost`.
- **Reconnection:** Same exponential backoff policy as the Browser Tab Player. Playback continues locally during reconnection.
- **Logging:** Writes structured logs to the system journal (systemd) or a log file. Log level is configurable.
- **Hardware requirements:** Minimum: 256MB RAM, audio output hardware (3.5mm jack or USB DAC), network connectivity (wired or wireless).

---

## Capability Flags

Each Venue Player declares its capabilities at registration. The host queries capabilities before dispatching commands.

| Flag | Type | Description |
|---|---|---|
| `streaming` | boolean | Can play HTTP/HTTPS stream URLs |
| `local_files` | boolean | Can play local file paths |
| `volume_control` | boolean | Supports `setVolume` command |
| `position_seeking` | boolean | Supports seeking to a specific position within a track |
| `offline_playback` | boolean | Can play cached content without network connectivity |
| `preloading` | boolean | Supports the `preload` command for gapless playback |

---

## Conformance Checklist

A Venue Player implementation is conformant if it satisfies all of the following:

1. Implements all six mandatory commands: `play`, `pause`, `resume`, `skip`, `setVolume`, `getState`.
2. Emits all four mandatory events: `stateChanged`, `trackEnded`, `positionUpdated`, `error`.
3. Registers with the system via `POST /api/v1/venues/{id}/players` before receiving commands.
4. Sends a heartbeat every `heartbeat_interval_seconds` via WebSocket.
5. Declares accurate capability flags at registration (does not claim capabilities it cannot fulfill).
6. Handles `play` when already playing by stopping the current track and starting the new one.
7. Emits `trackEnded` with `reason: "error"` if a stream fails to load after 3 retries.
8. Handles WebSocket disconnection gracefully (continues local playback; reconnects with backoff).
9. Does not expose raw stream URLs to end users (URLs are internal to the player).
10. Transitions to `idle` state after `trackEnded` if no next command is received within 5 seconds.

---

## Error Contract

| Code | Description | Recovery |
|---|---|---|
| `STREAM_UNAVAILABLE` | The stream URL returned a non-2xx response or timed out | Emit `trackEnded(reason: error)`; system advances to next queue item |
| `UNSUPPORTED_FORMAT` | The stream format is not supported by this player | Emit `error`; system logs adapter issue |
| `VOLUME_NOT_SUPPORTED` | `setVolume` called but `volume_control: false` | Return error response; do not crash |
| `HARDWARE_ERROR` | Audio hardware failure (device not found, permission denied) | Emit `error`; player transitions to `error` state |
| `AUTH_EXPIRED` | Player token has expired | Player re-registers; brief playback interruption acceptable |
| `POSITION_SEEK_NOT_SUPPORTED` | Seek requested but `position_seeking: false` | Return error response; current playback continues |
