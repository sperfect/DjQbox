---
title: DJQBox — POC Implementation Plan (.NET + Docker)
version: 1.0.0
date: 2025-07-01
status: Draft
stack: .NET 8 Minimal API · PostgreSQL · Docker Compose · Vanilla JS
---

# DJQBox — POC Implementation Plan

## Purpose

This document is the implementation guide for the DJQBox Proof of Concept. It is intended to be handed directly to a coding agent or developer. It references the canonical spec suite in `docs/spec/` for domain rules and data shapes, and adds concrete stack decisions, project structure, task breakdown, and acceptance criteria for local validation.

The POC proves the core loop: a guest joins a venue, submits a recommendation, an admin accepts or vetoes it, the queue updates, and a browser tab plays the track. Everything else is deferred to MVP.

---

## Stack Decisions

| Concern | Choice | Reason |
|---|---|---|
| Runtime | .NET 8 | LTS, minimal API support, good Docker story |
| API style | Minimal API (`app.MapGet`, `app.MapPost`) | Less ceremony, faster to scaffold for POC |
| ORM | Entity Framework Core 8 | Code-first migrations, strong Postgres support |
| Database | PostgreSQL 16 (Docker container) | Closest to Azure Database for PostgreSQL (MVP target) |
| Real-time | SignalR (WebSocket abstraction) | Built into .NET, minimal setup, matches WebSocket contract in spec |
| Auth | Simple JWT (HS256, static signing key) | No WebAuthn/OAuth for POC; real auth comes in MVP |
| Frontend | Single HTML file + vanilla JS (fetch + EventSource/WebSocket) | No build step, zero dependencies, immediately runnable |
| Containerisation | Docker Compose (API + Postgres) | Local dev and CI parity |
| Testing | xUnit + WebApplicationFactory (integration tests) | In-process test server, no mocking of HTTP layer |
| Logging | Serilog → stdout (structured JSON) | Docker-friendly, Azure-ready |

---

## POC Scope

### In Scope

- Single venue (seeded at startup, no venue CRUD needed)
- Queue: view items, add item directly (admin), reorder (admin)
- Recommendations: submit (guest), list pending (admin), accept / reject (DJ Mode), auto-promote on veto window expiry (Autopilot Mode)
- Now Playing: get current track, advance to next (player reports track ended)
- Operating mode: switch between DJ Mode and Autopilot Mode
- Veto window: configurable, timer runs server-side
- Auth: two static tokens seeded at startup — one admin token, one guest token (no login flow)
- WebSocket (SignalR hub): `queue.updated`, `nowplaying.changed`, `recommendation.received`
- Source: Local Files adapter — a seeded list of 10 tracks in the database (no file system scanning for POC)
- Venue Player: browser tab player in the dummy frontend (HTML Audio element)
- Dummy frontend: single `index.html` — Now Playing display, queue list, recommendation search + submit, admin accept/reject panel, mode toggle
- Docker Compose: `api` + `db` services
- Integration tests: one test per major flow

### Out of Scope for POC

- Multi-venue, venue CRUD
- Real auth (WebAuthn, OAuth, QR codes)
- Sync / offline / delta log
- Music Profile scoring and feedback loop
- Raspberry Pi daemon player
- Spotify / YouTube adapters
- Azure deployment (MVP)
- Observability beyond structured stdout logging

---

## Project Structure

```
/DJQbox
  /src
    /DJQbox.Api                   ← .NET 8 Minimal API project
      /Domain
        Venue.cs
        Queue.cs
        QueueItem.cs
        Track.cs
        Recommendation.cs
        Veto.cs
        User.cs
        Enums.cs                  ← VenueMode, RecommendationStatus, QueueItemStatus
      /Infrastructure
        /Persistence
          DjqboxDbContext.cs      ← EF Core DbContext
          Migrations/             ← EF migrations
        /Hubs
          VenueHub.cs             ← SignalR hub
        /Services
          QueueService.cs         ← Queue operations + veto timer
          RecommendationService.cs
          TokenService.cs         ← JWT issue/validate
      /Endpoints
        QueueEndpoints.cs
        RecommendationEndpoints.cs
        NowPlayingEndpoints.cs
        SourceEndpoints.cs
        AuthEndpoints.cs
        VenueEndpoints.cs
      /Contracts
        Requests/                 ← Request record types
        Responses/                ← Response record types
      Program.cs                  ← App bootstrap, DI, middleware, endpoint mapping
      appsettings.json
      appsettings.Development.json
      Dockerfile
    /DJQbox.Tests
      /Integration
        QueueTests.cs
        RecommendationTests.cs
        AutopilotTests.cs
        AuthTests.cs
      TestWebApplicationFactory.cs
  /frontend
    index.html                    ← Single-file dummy frontend
  docker-compose.yml
  docker-compose.override.yml     ← Dev overrides (volume mounts, env vars)
  .env.example
  README.md
```

---

## Data Seeding

On startup, the application seeds the database with:

- **1 Venue:** `{ id: "00000000-0000-0000-0000-000000000001", name: "The POC Venue", mode: "autopilot", veto_window_seconds: 30 }`
- **2 Users:**
  - Admin: `{ id: "00000000-0000-0000-0000-000000000010", role: "admin", display_name: "Admin" }`
  - Guest: `{ id: "00000000-0000-0000-0000-000000000020", role: "guest", display_name: "Guest" }`
- **10 Tracks** (local files stub — no actual audio files required for POC; use placeholder stream URLs):
  - Mix of mood tags (chill, energetic, focused) and genre tags (jazz, electronic, lo_fi)
  - BPM and energy values set for scoring tests
- **Static tokens** (configured in `appsettings.Development.json`):
  - `AdminToken`: a pre-signed JWT with admin scopes for the POC venue
  - `GuestToken`: a pre-signed JWT with guest scopes for the POC venue

---

## Task Breakdown

### Task 1 — Project Scaffold

**Goal:** Runnable .NET 8 Minimal API with Postgres, Docker Compose, EF Core connected, health endpoint responding.

**Steps:**
1. Create solution: `dotnet new sln -n DJQbox`
2. Create API project: `dotnet new webapi -n DJQbox.Api --use-minimal-apis`
3. Create test project: `dotnet new xunit -n DJQbox.Tests`
4. Add NuGet packages to `DJQbox.Api`:
   - `Microsoft.EntityFrameworkCore.Design`
   - `Npgsql.EntityFrameworkCore.PostgreSQL`
   - `Microsoft.AspNetCore.SignalR`
   - `Microsoft.AspNetCore.Authentication.JwtBearer`
   - `Serilog.AspNetCore`
   - `Serilog.Sinks.Console`
5. Add NuGet packages to `DJQbox.Tests`:
   - `Microsoft.AspNetCore.Mvc.Testing`
   - `Testcontainers.PostgreSql` (spins up real Postgres in tests)
6. Create `docker-compose.yml`:
   ```yaml
   services:
     db:
       image: postgres:16
       environment:
         POSTGRES_DB: djqbox
         POSTGRES_USER: djqbox
         POSTGRES_PASSWORD: djqbox
       ports:
         - "5432:5432"
       volumes:
         - pgdata:/var/lib/postgresql/data
     api:
       build: ./src/DJQbox.Api
       environment:
         ConnectionStrings__Default: "Host=db;Database=djqbox;Username=djqbox;Password=djqbox"
       ports:
         - "8080:8080"
       depends_on:
         - db
   volumes:
     pgdata:
   ```
7. Implement `GET /health` → `{ status: "ok", timestamp: "..." }`
8. Configure Serilog to write structured JSON to stdout.
9. Apply EF migrations on startup (`context.Database.MigrateAsync()`).

**Acceptance:** `docker compose up` → `GET http://localhost:8080/health` returns `200 { status: "ok" }`.

---

### Task 2 — Domain Model + EF Core

**Goal:** All POC entities defined as C# records/classes, mapped to Postgres via EF Core, initial migration applied.

**Entities to create** (align with `docs/spec/03-data-model.md`):

```csharp
// Venue.cs
public class Venue
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public VenueMode Mode { get; set; } = VenueMode.Autopilot;
    public int VetoWindowSeconds { get; set; } = 30;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Queue Queue { get; set; } = null!;
}

// Enums.cs
public enum VenueMode { Dj, Autopilot }
public enum QueueItemStatus { Pending, Playing, Played, Skipped }
public enum RecommendationStatus { Pending, Accepted, Rejected, Promoted, Expired }

// Track.cs — represents the Local Files stub tracks
public class Track
{
    public Guid Id { get; set; }
    public string SourceAdapter { get; set; } = "local_files";
    public string SourceId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public int DurationSeconds { get; set; }
    public int? Bpm { get; set; }
    public float? EnergyLevel { get; set; }
    public string[] MoodTags { get; set; } = [];
    public string[] GenreTags { get; set; } = [];
    public bool Explicit { get; set; }
    public string StreamUrl { get; set; } = "";   // placeholder URL for POC
}

// Queue + QueueItem, Recommendation, Veto, User — follow spec field definitions
```

**DbContext:** `DjqboxDbContext : DbContext` with DbSets for all entities. Configure Postgres-specific column types for `string[]` (use `text[]`).

**Seeder:** `DatabaseSeeder` class called from `Program.cs` after migration. Seeds the venue, users, and 10 tracks described in the Data Seeding section.

**Acceptance:** `dotnet ef migrations add InitialCreate` runs clean. `docker compose up` applies migrations and seed data. Inspect with `psql` or a DB tool to confirm tables and rows.

---

### Task 3 — Auth (Simplified JWT)

**Goal:** Token validation middleware. Two pre-signed tokens (admin, guest) work. All protected endpoints reject requests with no/invalid token.

**Implementation:**

- `TokenService`: generates and validates HS256 JWTs. Claims: `sub` (user_id), `role`, `venue_id`, `scopes[]`.
- `Program.cs`: configure `AddAuthentication().AddJwtBearer(...)` with the static signing key from `appsettings`.
- Pre-generate `AdminToken` and `GuestToken` in `appsettings.Development.json` at app startup if not present (or hardcode for POC).
- Helper extension: `HttpContext.GetUserId()`, `HttpContext.GetRole()`, `HttpContext.GetScopes()`.
- Endpoints declare required scopes via a simple policy check helper.

**appsettings.Development.json (example shape):**
```json
{
  "Jwt": {
    "SigningKey": "poc-signing-key-change-in-production",
    "AdminToken": "",
    "GuestToken": ""
  }
}
```

Tokens are printed to stdout at startup if generated fresh, so the developer can copy them into the frontend.

**Acceptance:** `GET /api/v1/venues/{id}/queue` with no token → `401`. With `AdminToken` → `200`. With `GuestToken` → `200` (guests can read queue). `POST /api/v1/venues/{id}/queue/items` with `GuestToken` → `403` (guests cannot add directly).

---

### Task 4 — Queue Endpoints

**Goal:** Full queue read/write API matching `docs/spec/04-api-contracts.md`.

**Endpoints to implement:**

```
GET  /api/v1/venues/{venueId}/queue
POST /api/v1/venues/{venueId}/queue/items          [admin]
DELETE /api/v1/venues/{venueId}/queue/items/{id}   [admin]
PATCH /api/v1/venues/{venueId}/queue/items/{id}/position  [admin]
GET  /api/v1/venues/{venueId}/now-playing
POST /api/v1/venues/{venueId}/now-playing/advance  [player — marks current as played, advances to next]
PATCH /api/v1/venues/{venueId}                     [admin — mode switch + veto window]
```

**QueueService responsibilities:**
- `GetQueue(venueId)` — return ordered QueueItems with Track data
- `AddItem(venueId, sourceAdapter, sourceId, addedBy)` — append to queue (position = max + 1.0)
- `RemoveItem(queueItemId)` — delete item
- `ReorderItem(queueItemId, newPosition)` — update position; trigger rebalance if needed
- `GetNowPlaying(venueId)` — return item with `status: Playing`
- `AdvanceQueue(venueId)` — set current Playing item to Played, set next Pending item to Playing; emit `nowplaying.changed` via SignalR
- After any queue mutation: emit `queue.updated` via SignalR hub

**Fractional position:** new items get `position = Math.Floor(maxPosition) + 1.0`. Rebalance when any two positions are within 0.001 of each other.

**Acceptance:**
- Seed queue has items in correct order.
- `POST /queue/items` adds item at end.
- `PATCH /position` reorders correctly.
- `POST /now-playing/advance` moves to next track and SignalR event fires.

---

### Task 5 — Recommendation Endpoints + Veto Timer

**Goal:** Full recommendation lifecycle matching `docs/spec/06-queue-spec.md` state machines for both DJ Mode and Autopilot Mode.

**Endpoints:**
```
POST  /api/v1/venues/{venueId}/recommendations         [guest, admin]
GET   /api/v1/venues/{venueId}/recommendations         [admin]
PATCH /api/v1/venues/{venueId}/recommendations/{id}    [admin — action: accept|reject|veto]
```

**RecommendationService responsibilities:**
- `Submit(venueId, trackRef, submittedBy)`:
  - Check for duplicate (same track already pending or in queue) → `409`
  - Create Recommendation with `status: Pending`
  - If venue mode is `Autopilot`: set `VetoDeadline = now + VetoWindowSeconds`, start veto timer
  - If venue mode is `Dj`: no timer; emit `recommendation.received` via SignalR to admins
- `Accept(recommendationId, adminId)` [DJ Mode only]:
  - Set status to `Accepted`
  - Call `QueueService.AddItem(...)` to promote to queue
  - Emit `queue.updated`
- `Reject(recommendationId, adminId)` [DJ Mode]:
  - Set status to `Rejected`
  - Notify submitter via SignalR
- `Veto(recommendationId, adminId, reason)` [Autopilot Mode]:
  - Set status to `Vetoed`
  - Cancel veto timer
  - Notify submitter
- **Veto timer (background):** Use `IHostedService` or `PeriodicTimer` to poll for expired recommendations every 5 seconds. For any recommendation where `VetoDeadline <= now` and `status == Pending`:
  - Promote to queue via `QueueService.AddItem(...)`
  - Set status to `Promoted`
  - Emit `queue.updated` + `recommendation.promoted` via SignalR

**Mode switch behavior** (from spec):
- DJ → Autopilot: all Pending recommendations get `VetoDeadline = now + VetoWindowSeconds`; timer kicks in
- Autopilot → DJ: all Pending recommendations get `VetoDeadline = null`; they move to DJ inbox

**Acceptance:**
- DJ Mode: submit recommendation → appears in admin list → accept → appears in queue
- DJ Mode: reject → status Rejected
- Autopilot Mode: submit recommendation → wait 30 seconds → auto-promoted to queue
- Autopilot Mode: submit → veto within 30 seconds → status Vetoed, not promoted
- Mode switch mid-flight: pending recs follow new mode rules

---

### Task 6 — SignalR Hub + Real-Time Events

**Goal:** WebSocket connection via SignalR. Frontend receives live updates without polling.

**Hub: `VenueHub`**

```csharp
public class VenueHub : Hub
{
    public async Task JoinVenue(string venueId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"venue:{venueId}");

    public async Task LeaveVenue(string venueId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"venue:{venueId}");
}
```

**Events emitted by services (via `IHubContext<VenueHub>`):**

| Event | Payload | Sent to |
|---|---|---|
| `queue.updated` | `{ venueId, changedItemIds[], timestamp }` | Group `venue:{venueId}` |
| `nowplaying.changed` | `{ venueId, queueItemId, track, timestamp }` | Group `venue:{venueId}` |
| `recommendation.received` | `{ venueId, recommendationId, track, submittedBy, vetoDeadline }` | Group `venue:{venueId}` — admins filter client-side for POC |
| `recommendation.promoted` | `{ venueId, recommendationId, timestamp }` | Group `venue:{venueId}` |
| `mode.changed` | `{ venueId, newMode, timestamp }` | Group `venue:{venueId}` |

**Hub endpoint:** mapped at `/hubs/venue` in `Program.cs`.

**Auth on hub:** Hub connection authenticated via JWT token passed as query parameter `?access_token=...` (standard SignalR pattern).

**Acceptance:** Open two browser tabs. Tab 1 (admin) connects to hub. Tab 2 (guest) submits a recommendation. Tab 1 receives `recommendation.received` event in real time without refresh.

---

### Task 7 — Source Endpoint (Local Files Stub)

**Goal:** Track search from the seeded local tracks. No real file system scanning for POC.

**Endpoints:**
```
GET /api/v1/sources
GET /api/v1/sources/local_files/search?q={query}
```

**Implementation:**
- `GET /sources` returns the list of registered adapters. For POC: one adapter, `local_files`, with full capabilities.
- `GET /sources/local_files/search?q=` performs a case-insensitive `LIKE` query against `Track.Title` and `Track.Artist` in Postgres.
- Returns up to 10 results as canonical Track response objects.

**Acceptance:** `GET /sources/local_files/search?q=jazz` returns tracks with "jazz" in title/artist or genre tags.

---

### Task 8 — Dummy Frontend (`frontend/index.html`)

**Goal:** A single HTML file that demonstrates the full POC flow. No build step. Open in a browser pointed at `http://localhost:8080`.

**Sections to implement:**

```
┌─────────────────────────────────────────────────────┐
│  DJQBox POC                         Mode: [Autopilot▼]  Veto: 30s  │
├────────────────────────┬────────────────────────────┤
│  NOW PLAYING           │  QUEUE                     │
│  ┌──────────────────┐  │  1. Track A — 3:45         │
│  │ [audio element]  │  │  2. Track B — 4:12         │
│  │ Title / Artist   │  │  [+ Add track (admin)]     │
│  └──────────────────┘  │                            │
├────────────────────────┴────────────────────────────┤
│  RECOMMENDATIONS                                    │
│  Search: [_______________] [Search]                 │
│  Results: Track X — Submit ▶                        │
│  Pending: Track X — [Accept] [Reject/Veto] ⏱ 28s   │
├─────────────────────────────────────────────────────┤
│  AUTH: Admin Token [________________] [Set]         │
│        Guest Token [________________] [Set]         │
└─────────────────────────────────────────────────────┘
```

**Implementation notes:**
- Tokens are stored in `localStorage`. The UI shows two token input boxes so the developer can paste the seeded tokens.
- All API calls use `fetch()` with `Authorization: Bearer <token>`.
- SignalR JS client (`@microsoft/signalr` from CDN) connects to `/hubs/venue` on page load.
- On `queue.updated` event: re-fetch and re-render the queue list.
- On `nowplaying.changed` event: update the Now Playing section and set `<audio>` element `src`.
- On `recommendation.received` event: append to the pending recommendations panel.
- The `<audio>` element has an `ended` event listener that calls `POST /api/v1/venues/{id}/now-playing/advance` to trigger the next track.
- Mode toggle calls `PATCH /api/v1/venues/{id}` with `{ mode: "dj" | "autopilot" }`.

**Acceptance:** Full flow works end-to-end in browser:
1. Paste admin token, guest token.
2. Guest searches for a track and submits recommendation.
3. In Autopilot Mode: recommendation auto-promotes after 30 seconds; queue updates; audio starts.
4. In DJ Mode: recommendation appears in pending panel; admin accepts; queue updates; audio starts.
5. Audio element plays (placeholder URL is fine — can be a public MP3 URL for testing).
6. When track ends, queue advances automatically.

---

### Task 9 — Integration Tests

**Goal:** Automated tests covering the key flows. Use `WebApplicationFactory` + `Testcontainers.PostgreSql` for a real Postgres instance per test run.

**Test cases to implement:**

```
QueueTests.cs
  - AddItemToQueue_AsAdmin_Returns201AndItemInQueue
  - AddItemToQueue_AsGuest_Returns403
  - ReorderItem_UpdatesPositionCorrectly
  - AdvanceQueue_MovesToNextPendingItem

RecommendationTests.cs
  - SubmitRecommendation_AsGuest_Returns201WithPendingStatus
  - SubmitDuplicateRecommendation_Returns409
  - AcceptRecommendation_DjMode_PromotesToQueue
  - RejectRecommendation_DjMode_SetsRejectedStatus
  - VetoRecommendation_AutopilotMode_SetsVetoed

AutopilotTests.cs
  - VetoWindowExpiry_PromotesRecommendationToQueue
    (set veto window to 2 seconds; submit; wait 3 seconds; verify status = Promoted)

AuthTests.cs
  - NoToken_Returns401
  - GuestToken_CannotAddQueueItem_Returns403
  - AdminToken_CanAddQueueItem_Returns201
```

**Acceptance:** `dotnet test` passes all tests. Tests run in CI (GitHub Actions or local `docker compose run`).

---

### Task 10 — Docker Polish + README

**Goal:** `docker compose up --build` from the repo root starts everything. README explains how to run, how to get tokens, and how to open the frontend.

**Dockerfile (`src/DJQbox.Api/Dockerfile`):**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish DJQbox.Api/DJQbox.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "DJQbox.Api.dll"]
```

**README.md sections:**
1. Prerequisites (Docker Desktop, .NET 8 SDK optional for local dev)
2. Quick start (`docker compose up --build`)
3. How to get the admin and guest tokens (check stdout on first run)
4. Open `frontend/index.html` in a browser (no server needed — uses `http://localhost:8080` API)
5. Running tests (`dotnet test` or `docker compose run tests`)
6. Key API endpoints (curl examples for the main flows)

---

## Error Handling Convention

All endpoints return errors in this shape (align with spec `04-api-contracts.md`):

```json
{
  "error": {
    "code": "DUPLICATE_RECOMMENDATION",
    "message": "This track is already pending or in the queue.",
    "detail": null
  }
}
```

Use a `Results.Problem` extension or a custom `ApiError` result type. Do not return raw exception messages in non-Development environments.

---

## Logging Convention

Use Serilog structured logging. Every request logs:
- `RequestId`, `Method`, `Path`, `StatusCode`, `ElapsedMs`

Key domain events log at `Information` level:
- `RecommendationSubmitted { VenueId, RecommendationId, TrackRef }`
- `RecommendationPromoted { RecommendationId, Reason: "accepted|autopilot" }`
- `QueueAdvanced { VenueId, PreviousItemId, NextItemId }`
- `ModeChanged { VenueId, OldMode, NewMode }`

No PII in logs (no user display names, no token values).

---

## Definition of Done (POC)

The POC is complete when:

- [ ] `docker compose up --build` starts cleanly with no manual steps.
- [ ] All 11 integration tests pass via `dotnet test`.
- [ ] The full guest→recommendation→queue→playback flow works in `index.html` in a browser.
- [ ] DJ Mode accept/reject flow works.
- [ ] Autopilot 30-second auto-promote works.
- [ ] Mode switch mid-flight works correctly.
- [ ] SignalR events update the frontend in real time.
- [ ] No hardcoded secrets in committed code (use `appsettings.Development.json` which is in `.gitignore`).
