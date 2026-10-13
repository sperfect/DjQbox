---
title: DJQBox — Observability Specification
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — Observability Specification

## Overview

Observability in DJQBox covers three signals: **logs**, **metrics**, and **traces**. The specification is platform-agnostic — it defines what must be observable and what each signal should contain, not which vendor or tool collects it. Concrete implementations (stdout + Azure Monitor, OpenTelemetry + Grafana, etc.) are implementation choices.

The system must be observable at every deployment tier: a single Raspberry Pi with stdout logs is a valid observability implementation for Tier 1. A full APM stack is the target for Tier 3.

---

## Principles

1. **Structured over unstructured.** All logs are structured (JSON or key-value). Free-text log lines are not acceptable in production.
2. **Correlation everywhere.** Every log line, metric data point, and trace span carries a `request_id` and `venue_id` where applicable. This allows cross-signal correlation.
3. **No PII in telemetry.** User display names, email addresses, and token values must never appear in logs, metrics, or traces.
4. **Cardinality control.** Metric labels use low-cardinality values (venue mode, operation type, status code) — never entity IDs as label values.
5. **Health over noise.** Health endpoints are the primary signal for infrastructure probes. Logs and traces are for human investigation.

---

## Health Endpoints

All deployments must expose two health endpoints.

### GET /health
**Purpose:** Liveness probe. Answers: "is the process alive?"
**Auth:** None required.
**Response 200:**
```json
{ "status": "ok", "timestamp": "2025-07-01T09:00:00Z" }
```
This endpoint must always return `200` as long as the process is running, even if dependencies are degraded.

---

### GET /health/ready
**Purpose:** Readiness probe. Answers: "can this instance serve traffic?"
**Auth:** None required.
**Response 200 (all checks pass):**
```json
{
  "status": "ok",
  "checks": {
    "database": "ok",
    "event_bus": "ok"
  },
  "timestamp": "2025-07-01T09:00:00Z"
}
```
**Response 503 (any check fails):**
```json
{
  "status": "degraded",
  "checks": {
    "database": "error",
    "event_bus": "ok"
  },
  "timestamp": "2025-07-01T09:00:00Z"
}
```

**Checks to implement:**
- `database`: execute a lightweight query (e.g. `SELECT 1`) against the primary store. Fail if it takes more than 2 seconds.
- `event_bus`: check that the in-process event bus or external broker connection is alive. For in-process, this is always `ok`.

Infrastructure orchestrators (Docker health check, Kubernetes readiness probe, Azure Container Apps readiness probe) must be configured to use `/health/ready`.

---

## Structured Logging

### Log Format

Every log line must be a single JSON object. Minimum fields:

| Field | Type | Description |
|---|---|---|
| `timestamp` | ISO 8601 UTC | When the event occurred |
| `level` | string | `debug`, `info`, `warn`, `error` |
| `message` | string | Human-readable summary |
| `request_id` | UUID | Correlation ID for the HTTP request (if in request context) |
| `venue_id` | UUID | Venue being operated on (if applicable) |
| `component` | string | Which service/module emitted the log (e.g. `queue_service`, `auth_service`) |

### Log Levels

| Level | When to use |
|---|---|
| `debug` | Detailed internal state useful for development. Disabled in production by default. |
| `info` | Normal operational events (recommendation submitted, queue advanced, mode changed). |
| `warn` | Unexpected but recoverable conditions (token near expiry, adapter temporarily unavailable). |
| `error` | Failures requiring operator attention (DB connection lost, veto timer crashed, auth service error). |

### Required Domain Events (log at `info`)

Every implementation must log the following events at `info` level with the specified fields:

| Event | Fields |
|---|---|
| `recommendation.submitted` | `venue_id`, `recommendation_id`, `source_adapter`, `source_id` |
| `recommendation.promoted` | `venue_id`, `recommendation_id`, `reason: "accepted" or "autopilot"` |
| `recommendation.rejected` | `venue_id`, `recommendation_id`, `reason: "dj_reject" or "veto"` |
| `queue.item_added` | `venue_id`, `queue_item_id`, `source_adapter`, `source_id`, `added_by_role` |
| `queue.advanced` | `venue_id`, `previous_item_id`, `next_item_id` |
| `queue.empty` | `venue_id` — emitted when queue empties after `trackEnded` |
| `mode.changed` | `venue_id`, `old_mode`, `new_mode`, `changed_by_role` |
| `player.offline` | `venue_id`, `player_id`, `last_heartbeat_at` |
| `player.reconnected` | `venue_id`, `player_id` |
| `auth.token_issued` | `role`, `venue_id` (no user_id in logs) |
| `auth.token_rejected` | `reason: "invalid" or "expired" or "revoked"` |
| `adapter.unavailable` | `adapter_id`, `error_code` |
| `sync.epoch_advanced` | `peer_id`, `old_epoch`, `new_epoch` |
| `sync.full_sync_triggered` | `peer_id`, `reason: "epoch_gap" or "first_sync"` |

### Request Logging

Every HTTP request must produce a request log at `info` level:

```json
{
  "timestamp": "2025-07-01T09:00:00Z",
  "level": "info",
  "message": "HTTP request completed",
  "request_id": "uuid",
  "method": "POST",
  "path": "/api/v1/venues/{id}/recommendations",
  "status_code": 201,
  "elapsed_ms": 42,
  "venue_id": "uuid",
  "component": "api_gateway"
}
```

Path parameters must be templated (not substituted) in the log path to prevent high cardinality.

---

## Metrics

Metrics are numerical measurements emitted on a schedule or per event. The following metrics must be exposed by every production deployment.

### Metric Naming Convention

`djqbox_<component>_<noun>_<unit>`

Examples: `djqbox_queue_items_total`, `djqbox_recommendations_pending_count`

### Required Metrics

| Metric | Type | Labels | Description |
|---|---|---|---|
| `djqbox_recommendations_submitted_total` | Counter | `venue_mode`, `source_adapter` | Total recommendations submitted |
| `djqbox_recommendations_promoted_total` | Counter | `venue_mode`, `reason` | Total recommendations promoted to queue |
| `djqbox_recommendations_rejected_total` | Counter | `venue_mode`, `reason` | Total recommendations rejected or vetoed |
| `djqbox_queue_items_total` | Counter | `status` | Queue items created by status |
| `djqbox_queue_advances_total` | Counter | — | Total queue advance operations |
| `djqbox_recommendations_pending_count` | Gauge | `venue_mode` | Current count of pending recommendations |
| `djqbox_veto_window_remaining_seconds` | Histogram | — | Remaining seconds when a veto was cast (0 = last-second veto) |
| `djqbox_player_heartbeat_latency_seconds` | Histogram | `player_type` | Time between expected and received heartbeat |
| `djqbox_adapter_request_duration_seconds` | Histogram | `adapter_id`, `operation` | Source adapter operation latency |
| `djqbox_adapter_errors_total` | Counter | `adapter_id`, `error_code` | Source adapter errors by type |
| `djqbox_http_requests_total` | Counter | `method`, `path_template`, `status_code` | HTTP request count |
| `djqbox_http_request_duration_seconds` | Histogram | `method`, `path_template` | HTTP request latency |
| `djqbox_db_query_duration_seconds` | Histogram | `operation` | Database query latency |

### Metrics Exposition

Metrics must be exposed in a format readable by standard monitoring tools. The preferred format is Prometheus text format at `GET /metrics`. This endpoint:
- Requires no auth on Tier 1/2 (scrape from localhost only).
- Should be protected or excluded from public ingress on Tier 3.

---

## Distributed Tracing

Tracing applies to Tier 2 and Tier 3 deployments. On Tier 1 (single process), structured request logs provide sufficient correlation.

### Trace Context Propagation

All inbound HTTP requests must read the `traceparent` header (W3C Trace Context standard). All outbound calls (to source adapters, to the database, to the event bus on Tier 3) must propagate the trace context.

### Required Spans

| Span | Operation | Attributes |
|---|---|---|
| `http.request` | Every inbound HTTP request | `http.method`, `http.route`, `http.status_code` |
| `db.query` | Every database query | `db.operation`, `db.table` |
| `recommendation.process` | Full recommendation lifecycle | `venue_id`, `recommendation_id`, `venue_mode` |
| `queue.advance` | Queue advance operation | `venue_id`, `previous_item_id` |
| `adapter.search` | Source adapter search call | `adapter_id`, `query_length` |
| `adapter.resolve` | Source adapter resolve call | `adapter_id`, `source_id` |
| `signalr.publish` | Event published to hub | `event_name`, `venue_id` |

### Trace Sampling

- Development/Tier 1: 100% sampling.
- Tier 3 production: 10% head-based sampling by default; 100% for error spans.

---

## Alerting (Tier 3 Reference)

The following alert conditions are recommended for Tier 3 deployments. The alerting mechanism (PagerDuty, Azure Monitor Alerts, etc.) is implementation-specific.

| Alert | Condition | Severity |
|---|---|---|
| API error rate high | `http_requests_total{status_code=~"5.."}` > 5% of total for 5 minutes | High |
| Database unavailable | `/health/ready` check `database` = `error` for 2 consecutive checks | Critical |
| Player offline | `player.offline` event not followed by `player.reconnected` within 5 minutes | Medium |
| Veto timer stalled | No `recommendation.promoted` events for a venue in Autopilot Mode for 10 minutes while recommendations are pending | Medium |
| Adapter unavailable | `adapter.unavailable` events for the same adapter_id > 3 times in 5 minutes | Medium |
| Auth rejection spike | `auth.token_rejected` events > 50 in 1 minute | High |

---

## Deployment-Tier Mapping

| Signal | Tier 1 (RPi) | Tier 2 (VPS) | Tier 3 (Cloud) |
|---|---|---|---|
| Logs | Stdout (JSON) | Stdout → file or syslog | Stdout → managed log service (e.g. Azure Monitor) |
| Metrics | Prometheus `/metrics` endpoint | Prometheus scrape + local Grafana | Managed metrics (e.g. Azure Monitor, Datadog) |
| Traces | Not required | OpenTelemetry → Jaeger (local) | OpenTelemetry → managed APM |
| Health probes | Docker health check | Process supervisor health check | Container orchestrator readiness/liveness probes |
| Alerting | Not required | Email on error log | Managed alerting service |
