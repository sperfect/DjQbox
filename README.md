# DJQBox — Collaborative Music Control

DJQBox is a cloud-enabled, local-first web application for collaborative music control across physical venues (cafés, workspaces, restaurants, homes).

---

## Prerequisites

- **Docker & Docker Compose** (Docker Desktop on Windows/macOS or Docker Engine on Linux)
- *(Optional for local dev)*: **.NET 8 SDK** (pinned via `global.json` to 8.0.x)

---

## Quick Start

### 1. Start the stack

Run from the repository root:

```bash
docker compose up --build
```

This starts:
- **db**: PostgreSQL 16 on port `5432` with an initialized `djqbox` database and volume persistence.
- **api**: ASP.NET Core 8 Web API on port `8080`, which automatically executes database migrations and populates seed data (default venue, users, and 10 local sample tracks).

### 2. Retrieve Authentication Tokens

On API startup, pre-generated JWT tokens for the default venue are logged to stdout in Serilog JSON format:

- `POC Startup Admin Token: <token>` (scopes: `queue:read`, `queue:write`, `queue:recommend`, `venue:admin`, `profile:read`, `profile:write`)
- `POC Startup Guest Token: <token>` (scopes: `queue:read`, `queue:recommend`)

Alternatively, fetch the seed tokens at any time via:

```bash
curl http://localhost:8080/api/v1/auth/tokens
```

Response:
```json
{
  "venue_id": "00000000-0000-0000-0000-000000000001",
  "admin_token": "<jwt-token>",
  "guest_token": "<jwt-token>"
}
```

### 3. Open the Frontend

No build step or node runtime is required. Simply open `frontend/index.html` directly in your browser:

- On Windows: Double-click `frontend/index.html` or open in Chrome/Edge/Firefox.
- Pointed at `http://localhost:8080` by default.
- Click **⚡ Fetch Seed Tokens** (or paste your tokens) to authenticate and connect to the real-time SignalR hub.

---

## Running Integration Tests

To run the full suite of integration tests locally:

```bash
dotnet test
```

All 14 integration tests run against either Testcontainers PostgreSQL (if Docker is running) or an in-memory test database fallback, covering:
- Health check verification
- JWT token authentication and scope authorization
- Queue management, fractional index reordering, and track playback advancement
- Guest recommendation submission, duplicate rejection, DJ Mode review (accept/reject), and Autopilot vetoing
- Autopilot background veto timer expiration and auto-promotion

---

## Key API Endpoints & Usage

Default Venue ID: `00000000-0000-0000-0000-000000000001`

### Health Check
```bash
curl http://localhost:8080/health
```

### Get Current Queue
```bash
curl -H "Authorization: Bearer <ADMIN_OR_GUEST_TOKEN>" \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001/queue
```

### Add Track Directly to Queue (Admin)
```bash
curl -X POST \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"sourceAdapter": "local_files", "sourceId": "track-003"}' \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001/queue/items
```

### Search Seeded Tracks
```bash
curl -H "Authorization: Bearer <GUEST_TOKEN>" \
  "http://localhost:8080/api/v1/sources/local_files/search?q=chill"
```

### Submit Recommendation (Guest)
```bash
curl -X POST \
  -H "Authorization: Bearer <GUEST_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"sourceAdapter": "local_files", "sourceId": "track-005"}' \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001/recommendations
```

### Review Recommendation (Admin)
Accept recommendation in DJ Mode:
```bash
curl -X PATCH \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"action": "accept"}' \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001/recommendations/<REC_ID>
```

Veto recommendation in Autopilot Mode:
```bash
curl -X PATCH \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"action": "veto", "reason": "Not fitting venue vibe"}' \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001/recommendations/<REC_ID>
```

### Advance Playback
```bash
curl -X POST \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001/now-playing/advance
```

### Toggle Venue Mode
```bash
curl -X PATCH \
  -H "Authorization: Bearer <ADMIN_TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{"mode": "dj"}' \
  http://localhost:8080/api/v1/venues/00000000-0000-0000-0000-000000000001
```

---

## Real-Time Events (SignalR)

The API exposes a SignalR hub at `/hubs/venue`. Clients authenticate with `?access_token=<JWT>` and call `JoinVenue(venueId)`. Events emitted:

- `queue.updated`: Broadcast when tracks are added, removed, or reordered.
- `nowplaying.changed`: Broadcast when playback advances or initial track starts.
- `recommendation.received`: Broadcast when a guest submits a track recommendation.
- `recommendation.promoted`: Broadcast when a recommendation is promoted into the queue (by admin accept or Autopilot veto deadline expiry).
- `mode.changed`: Broadcast when the venue switches between DJ Mode and Autopilot Mode.
