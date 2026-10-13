---
title: DJQBox — Source Adapter Interface Specification
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — Source Adapter Interface Specification

## Overview

The Source Adapter is the boundary between DJQBox and any external music source. It implements the anti-corruption layer pattern: the adapter translates source-specific data models, APIs, and error conditions into the DJQBox canonical Track model. The core system never knows about Spotify track IDs, YouTube video IDs, or local file paths — only canonical `TrackRef` values.

Any conforming Source Adapter implementation is a valid music source for DJQBox. Three reference adapters are described: Spotify, YouTube, and Local Files.

---

## Interface Definition

Every Source Adapter must implement the following operations.

### Operations

| Operation | Signature | Description |
|---|---|---|
| `initialize` | `(config: AdapterConfig) -> void` | Called once at system startup. Load credentials, establish connection, warm caches. |
| `isAvailable` | `() -> bool` | Returns true if the adapter can currently reach its source. Called before dispatching search or resolve. |
| `search` | `(query: string, options: SearchOptions) -> Track[]` | Search for tracks matching the query. Returns canonical Track objects. |
| `resolve` | `(trackRef: TrackRef) -> StreamURL` | Return a playable stream URL for the given TrackRef. URL may be short-lived; callers should not cache it. |
| `getMetadata` | `(trackRef: TrackRef) -> TrackMetadata` | Fetch or return cached full metadata for a track. |
| `getCapabilities` | `() -> CapabilitySet` | Declare which operations this adapter supports. Called at registration. |
| `teardown` | `() -> void` | Called on graceful shutdown. Release connections, flush caches. |

### SearchOptions Type

```
SearchOptions {
  limit:           integer       // Max results to return (default 10, max 50)
  offset:          integer       // Pagination offset (default 0)
  mood_hints:      string[]      // Optional mood tags to bias results
  genre_hints:     string[]      // Optional genre tags to bias results
  bpm_range:       { min: int, max: int } | null
  energy_range:    { min: float, max: float } | null
  explicit_allowed: boolean      // Whether to include explicit tracks
}
```

### CapabilitySet Type

```
CapabilitySet {
  search:          boolean  // Supports search() operation
  streaming:       boolean  // Supports resolve() for HTTP stream URLs
  metadata:        boolean  // Supports getMetadata() with full metadata
  offline_cache:   boolean  // Can serve cached results when source is unreachable
  recommendations: boolean  // Source can provide autonomous recommendations (future)
}
```

---

## Error Contract

All operations must return typed errors rather than unhandled exceptions. The following error codes are defined:

| Code | Description | Recovery |
|---|---|---|
| `NOT_AVAILABLE` | `isAvailable()` returned false; source is unreachable | Caller skips this adapter; tries another |
| `NOT_FOUND` | The requested `trackRef` does not exist at the source | Return empty result; log incident |
| `UNAUTHORIZED` | The adapter's credentials have expired or been revoked | Adapter emits `auth_required` event; operator must re-authenticate |
| `RATE_LIMITED` | The source API has rate-limited this adapter | Adapter backs off; retry after `retry_after` seconds |
| `UNSUPPORTED_OPERATION` | The operation is not in the adapter's `CapabilitySet` | Caller checks capabilities before calling |
| `SOURCE_ERROR` | An unclassified error from the source API | Include source detail in error payload; log for operator |

---

## Registration Protocol

Source Adapters register with the system at startup by providing a manifest.

**Adapter Manifest Format:**
```json
{
  "id":           "spotify",
  "name":         "Spotify",
  "version":      "1.0.0",
  "capabilities": {
    "search":          true,
    "streaming":       true,
    "metadata":        true,
    "offline_cache":   false,
    "recommendations": false
  },
  "config_schema": {
    "client_id":     { "type": "string", "required": true },
    "client_secret": { "type": "string", "required": true, "secret": true }
  }
}
```

The system reads all adapter manifests at startup from a configured adapters directory. For each manifest, the system calls `initialize(config)` passing the operator-provided configuration values. If initialization fails, the adapter is marked `unavailable` but does not prevent other adapters from loading.

**Health check:** The system calls `isAvailable()` on each adapter periodically (default every 30 seconds) and reflects availability in the `GET /api/v1/sources` endpoint.

---

## Normalization Contract

The adapter is responsible for translating source-specific fields into the canonical `Track` entity. The table below shows the mapping pattern:

| Canonical Field | Spotify Example | YouTube Example | Local Files Example |
|---|---|---|---|
| `source_adapter` | `"spotify"` | `"youtube"` | `"local_files"` |
| `source_id` | Spotify track ID | YouTube video ID | Relative file path |
| `title` | `track.name` | `snippet.title` | ID3 tag `TIT2` |
| `artist` | `track.artists[0].name` | `snippet.channelTitle` | ID3 tag `TPE1` |
| `duration_seconds` | `track.duration_ms / 1000` | `contentDetails.duration` (ISO 8601) | File duration from audio metadata |
| `bpm` | Not available natively; use audio analysis API | Not available | ID3 tag `TBPM` |
| `energy_level` | Spotify Audio Features `energy` (0.0–1.0) | Not available | Not available |
| `mood_tags` | Derived from Spotify Audio Features (valence, danceability) | Not available | Not available |
| `genre_tags` | `track.artists[0].genres` (via artist lookup) | Not available | ID3 tag `TCON` |
| `explicit` | `track.explicit` | `contentDetails.contentRating` | ID3 tag or filename flag |

Fields that cannot be populated from the source are left `null`. The Profile Scorer handles null fields gracefully (see `07-music-profile-spec.md`).

---

## Reference Adapter Sketches

### Spotify Adapter

**What it wraps:** Spotify Web API (REST, OAuth 2.0).

**Search:** Maps `search(query, options)` to `GET /search?type=track`. Applies `explicit_allowed` as `include_external=audio` and market filtering. Returns up to `options.limit` canonical Track objects.

**Resolve:** Maps `resolve(trackRef)` to a Spotify playback URL or streaming preview URL. Full stream requires Spotify Premium OAuth token per user; the adapter manages token delegation.

**Metadata:** `getMetadata(trackRef)` calls `GET /tracks/{id}` plus `GET /audio-features/{id}` to populate BPM, energy, and valence. Results are cached locally with a 24-hour TTL.

**Capabilities:** `{ search: true, streaming: true, metadata: true, offline_cache: false, recommendations: false }`

**Known limitations:**
- Streaming requires a valid Spotify Premium token; the adapter prompts for re-authentication when tokens expire.
- Audio features API may be rate-limited on free tier; BPM/energy metadata may be delayed.
- Track availability varies by market; the adapter filters by the deployment host's configured market.

---

### YouTube Adapter

**What it wraps:** YouTube Data API v3 (REST, API key or OAuth).

**Search:** Maps `search(query, options)` to `GET /search?part=snippet&type=video`. Filters by `videoEmbeddable=true` and `safeSearch` based on `explicit_allowed`.

**Resolve:** `resolve(trackRef)` returns the resolved stream URL for the YouTube video. The adapter uses a stream URL resolution mechanism appropriate to the deployment context.

**Metadata:** `getMetadata(trackRef)` calls `GET /videos?part=snippet,contentDetails` to populate title, channel name (as artist), and duration. BPM and energy are not available from YouTube; fields are left null.

**Capabilities:** `{ search: true, streaming: true, metadata: true, offline_cache: false, recommendations: false }`

**Known limitations:**
- Music metadata is lower fidelity than dedicated music APIs (no BPM, energy, mood tags without external enrichment).
- Content licensing for stream resolution varies by region; the adapter must handle `403` responses from stream resolution gracefully.
- Rate limits apply to the YouTube Data API; the adapter implements exponential backoff.

---

### Local Files Adapter

**What it wraps:** A local directory of audio files on the Deployment Host.

**Search:** Scans the configured media directory and indexes files by ID3/metadata tags. `search(query, options)` performs an in-memory or lightweight full-text search over the index. Re-indexes on file system change events.

**Resolve:** `resolve(trackRef)` returns a `file://` path or a locally-served HTTP URL pointing to the audio file on disk. This is always available without network connectivity.

**Metadata:** `getMetadata(trackRef)` reads ID3 tags directly from the audio file. Supports MP3 (ID3v2), FLAC (Vorbis Comments), AAC (iTunes tags), and OGG.

**Capabilities:** `{ search: true, streaming: true, metadata: true, offline_cache: true, recommendations: false }`

**Known limitations:**
- BPM and energy level require audio analysis (not standard ID3 tags); these fields are populated only if an analysis tool is configured.
- Mood and genre tags depend on ID3 tag quality; many local files have incomplete or absent tags.
- The index must be rebuilt after large file system changes; full reindex time depends on library size.

---

## Conformance Checklist

A Source Adapter implementation is conformant if it satisfies all of the following:

1. Implements all seven interface operations: `initialize`, `isAvailable`, `search`, `resolve`, `getMetadata`, `getCapabilities`, `teardown`.
2. Ships a valid adapter manifest (`adapter.json`) declaring its `id`, `name`, `version`, `capabilities`, and `config_schema`.
3. Returns only canonical `Track` objects from `search()` — no source-specific fields exposed.
4. Returns only the error codes defined in the Error Contract — no raw source API errors propagate to callers.
5. Declares only capabilities it can fulfill. If `search: true` is declared, `search()` must return results.
6. Handles `isAvailable() -> false` gracefully: all operations return `NOT_AVAILABLE` without throwing.
7. Does not store user credentials in plaintext. Uses the host secret management facility (environment variable, secret file, or secret store).
8. Implements backoff on `RATE_LIMITED` responses — does not retry immediately.
9. Respects `SearchOptions.explicit_allowed: false` by excluding explicit content from results.
10. Populates the `metadata_fetched_at` field on Track objects so the system knows when to refresh stale metadata.
