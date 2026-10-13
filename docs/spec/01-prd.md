---
title: DJQBox — Product Requirements Document
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — Product Requirements Document

## Purpose and Scope

This document defines the product requirements for DJQBox: a cloud-enabled, local-first web application for collaborative music control across physical venues. It covers goals, non-goals, user personas, user stories with acceptance criteria, success metrics, and constraints. It is the primary reference for product and engineering decisions.

---

## Goals

1. Allow a venue owner to manage music playback across multiple venues from a single dashboard.
2. Give guests a frictionless way to participate in what plays at a venue — no account required.
3. Support two explicit queue operating modes: DJ Mode (admin control) and Autopilot Mode (recommendation-driven).
4. Abstract over multiple music sources so that any source can be swapped or added without changing the core experience.
5. Operate fully offline on a single local device; optionally sync state to the cloud.
6. Run on minimal hardware (e.g. Raspberry Pi) with no internet dependency as a fully supported deployment scenario.

---

## Non-Goals

The following are explicitly out of scope for DJQBox:

- **Live audio mixing** — DJQBox controls what plays and when; it does not mix audio streams in real time.
- **Social networking** — DJQBox is not a social platform. There are no follower graphs, public profiles, or social feeds.
- **Paid subscription management** — DJQBox does not manage billing, subscriptions, or payment for music services. Source adapter authentication is out-of-band.
- **Hardware manufacturing specs** — DJQBox specifies a software interface for Venue Players; it does not specify audio hardware design.
- **Content licensing** — DJQBox does not manage music rights, licensing, or royalty payments.
- **Multi-venue simultaneous broadcast** — Queues are per-venue and independent; synchronized cross-venue playback is not a goal.
- **Voice control** — No voice interface is specified.

---

## User Personas

### Persona 1: Maria — The Owner
Maria runs two businesses: a café and a co-working space. She wants consistent, venue-appropriate music without babysitting a playlist every day. She sets up music profiles for each venue (jazz and chill for the café, focused and lo-fi for the workspace), grants her staff DJ access, and checks in on what is playing from her phone when she is away. She cares about reliability, simplicity, and not giving up control.

**Technical comfort:** Medium. Comfortable with web apps; not a developer.
**Primary device:** Mobile browser, occasionally desktop.
**Key needs:** Multi-venue dashboard, music profile configuration, remote visibility, guest access management.

---

### Persona 2: Carlos — The DJ / Admin
Carlos is a barista at Maria's café. He has been given DJ access for the morning shift. He reviews guest recommendations and decides what actually plays. On quiet mornings he switches the venue to Autopilot and lets the profile steer things. He skips tracks he does not like and occasionally reorders the queue.

**Technical comfort:** Medium-high. Comfortable with apps; not a developer.
**Primary device:** Tablet or laptop behind the counter.
**Key needs:** Real-time recommendation inbox, accept/reject controls, mode switching, queue reordering.

---

### Persona 3: Alex — The Guest
Alex is a regular at the café. They scan the QR code on the table, see what is playing, and queue up a favourite track. They do not want to create an account. They may submit a couple of recommendations and check if they made it into the queue.

**Technical comfort:** Low-medium. Comfortable with mobile apps.
**Primary device:** Mobile browser.
**Key needs:** Frictionless join, now-playing view, recommendation submission, recommendation status feedback.

---

## User Stories and Acceptance Criteria

### Owner Stories

#### US-01: Create a Venue
**As** Maria (Owner),
**I want** to create a new venue with a name and description,
**so that** I can manage music for a specific physical location.

**Acceptance Criteria:**
- [ ] Owner can create a venue by providing a name (required) and optional description.
- [ ] A new venue is created with mode defaulting to Autopilot, veto window defaulting to 60 seconds.
- [ ] The new venue appears in the owner dashboard immediately.
- [ ] Venue creation fails with a clear error if name is blank or exceeds 100 characters.

---

#### US-02: Configure a Music Profile
**As** Maria (Owner),
**I want** to set mood tags, genre tags, BPM range, and energy level for a venue,
**so that** Autopilot Mode steers music recommendations toward the right vibe.

**Acceptance Criteria:**
- [ ] Owner can open a venue music profile editor.
- [ ] Owner can select one or more mood tags from the defined taxonomy.
- [ ] Owner can select one or more genre tags from the defined taxonomy.
- [ ] Owner can set a BPM range (min/max integers 0-250).
- [ ] Owner can set an energy range (min/max floats 0.0-1.0).
- [ ] Owner can toggle explicit content allowed/blocked.
- [ ] Profile updates are saved and take effect immediately for new recommendation scoring.
- [ ] Profile state is persisted locally and syncs to cloud if configured.

---

#### US-03: View All Venues Dashboard
**As** Maria (Owner),
**I want** to see all my venues and their current status on a single screen,
**so that** I can monitor what is happening without switching between venues.

**Acceptance Criteria:**
- [ ] Dashboard lists all venues owned by the user.
- [ ] Each venue shows: name, current mode (DJ/Autopilot), now-playing track title and artist, number of pending recommendations, and player online/offline status.
- [ ] Dashboard updates in real time via WebSocket; no manual refresh required.
- [ ] Works on mobile and desktop screen sizes.

---

#### US-04: Revoke Guest Access
**As** Maria (Owner),
**I want** to revoke guest access to a venue,
**so that** I can close public access at end of service or in response to abuse.

**Acceptance Criteria:**
- [ ] Owner can disable the active QR code / token for a venue.
- [ ] Existing guest sessions are invalidated within 60 seconds of revocation (next heartbeat check).
- [ ] A new QR code / token can be generated to re-enable access.
- [ ] Revocation requires connectivity; an offline revocation attempt shows a clear offline warning.

---

### DJ / Admin Stories

#### US-05: View Pending Recommendations
**As** Carlos (DJ/Admin),
**I want** to see a live list of pending recommendations from guests,
**so that** I can decide what to let into the queue.

**Acceptance Criteria:**
- [ ] In DJ Mode, a recommendation inbox is visible showing all pending recommendations.
- [ ] Each recommendation shows: track title, artist, source, submitter display name (or "Anonymous"), time submitted.
- [ ] New recommendations appear in real time without a page refresh.
- [ ] In Autopilot Mode, pending recommendations show with remaining veto window time.

---

#### US-06: Accept or Reject a Recommendation (DJ Mode)
**As** Carlos (DJ/Admin),
**I want** to accept or reject a pending recommendation,
**so that** I control exactly what enters the queue.

**Acceptance Criteria:**
- [ ] DJ can accept a recommendation; it is added to the end of the queue immediately.
- [ ] DJ can reject a recommendation; it is removed from the inbox and the submitter receives a status update.
- [ ] Accepting or rejecting is a one-tap / one-click action.
- [ ] A rejected recommendation cannot be re-submitted within the same session by the same guest (configurable cooldown).
- [ ] The action is reflected in real time across all connected clients.

---

#### US-07: Switch Operating Mode
**As** Carlos (DJ/Admin),
**I want** to switch the venue between DJ Mode and Autopilot Mode,
**so that** I can step back from manual curation when it suits me.

**Acceptance Criteria:**
- [ ] A mode toggle is available to any admin at the venue.
- [ ] Switching from DJ Mode to Autopilot Mode starts the veto window timer for all currently pending recommendations.
- [ ] Switching from Autopilot Mode to DJ Mode pauses all veto timers; pending recommendations move to the DJ inbox.
- [ ] The mode change is reflected on all connected clients within 2 seconds.
- [ ] The owner dashboard reflects the current mode in real time.

---

#### US-08: Configure Veto Window
**As** Carlos (DJ/Admin),
**I want** to set the veto window duration for the venue,
**so that** I can control how quickly recommendations auto-promote.

**Acceptance Criteria:**
- [ ] Admin can set veto window duration in seconds (minimum 10s, maximum 3600s).
- [ ] Default value is 60 seconds.
- [ ] Changes apply to new recommendations only; in-flight veto windows are not retroactively changed.
- [ ] Setting is persisted and survives a page reload or player restart.

---

### Guest Stories

#### US-09: Join a Venue via QR Code
**As** Alex (Guest),
**I want** to scan a QR code at the venue and join without creating an account,
**so that** I can participate immediately without friction.

**Acceptance Criteria:**
- [ ] Scanning the QR code opens a mobile browser to the venue guest page.
- [ ] No account creation or login is required.
- [ ] The guest is issued a scoped session token valid for the venue.
- [ ] If the QR token is invalid or expired, the guest sees a clear "Access unavailable" message.
- [ ] The flow completes in under 5 seconds on a normal mobile connection.

---

#### US-10: View Now Playing
**As** Alex (Guest),
**I want** to see what is currently playing at the venue,
**so that** I know what music is on and can decide whether to submit a recommendation.

**Acceptance Criteria:**
- [ ] Now-playing shows: track title, artist, source name, album art if available, and elapsed/total duration.
- [ ] Updates in real time when the track changes; no manual refresh required.
- [ ] If nothing is playing, shows a "Nothing playing" state gracefully.
- [ ] Works on a mobile browser with no app install.

---

#### US-11: Submit a Recommendation
**As** Alex (Guest),
**I want** to search for a track and submit it as a recommendation,
**so that** my music taste can influence what plays at the venue.

**Acceptance Criteria:**
- [ ] Guest can search for tracks by title or artist across available sources.
- [ ] Search results show: title, artist, duration, source.
- [ ] Guest can submit one track as a recommendation with a single tap.
- [ ] Submitted recommendation shows a status indicator (pending, accepted, playing, rejected).
- [ ] Guest receives a real-time update when their recommendation status changes.
- [ ] Guest cannot submit the same track twice while it is pending or already in the queue.

---

#### US-12: Autopilot Auto-Promotion
**As** Alex (Guest),
**I want** my recommendation to automatically enter the queue if no admin objects,
**so that** my suggestion gets a fair chance without requiring DJ approval.

**Acceptance Criteria:**
- [ ] In Autopilot Mode, a submitted recommendation shows a countdown timer reflecting the veto window.
- [ ] When the timer reaches zero without a veto, the recommendation is promoted to the queue and status changes to "Queued".
- [ ] If a veto is cast before the timer expires, status changes to "Vetoed" with a clear message.
- [ ] The promotion is reflected in real time on all connected clients.

---

## Success Criteria

| Metric | Target |
|---|---|
| Guest join flow completion time | < 5 seconds on a normal mobile connection |
| Recommendation submission to queue promotion | Real-time, < 2 seconds latency |
| Mode switch propagation to all clients | < 2 seconds |
| Offline operation | All queue and playback operations function with no internet connectivity |
| Single-node deployment | Full system runs on a Raspberry Pi 4 with 500MB RAM or less |
| Source adapter extensibility | A new source adapter can be added without modifying core services |
| Venue Player extensibility | A new player type can be added without modifying core services |

---

## Constraints

### Technical Constraints
- The system must run on a single Raspberry Pi 4 (4GB RAM) as a fully supported deployment.
- All client interfaces must be web-based (no native app install required for guests).
- The data model must support a future CRDT upgrade without a destructive migration.
- Source adapters must be isolated from the core; failures in one adapter must not affect others.

### Operational Constraints
- Venue access must be revocable by the owner at any time.
- Guest tokens must not grant access to any data outside the scoped venue.
- No telemetry or usage data may leave the deployment host without explicit operator consent.

### Design Constraints
- All specifications are platform and tool agnostic; no specific programming language, framework, or cloud vendor is required.
- Diagrams use PlantUML syntax.
