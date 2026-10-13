---
title: DJQBox — Queue Mechanics Specification
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — Queue Mechanics Specification

## Overview

The Queue is the central operating object of a venue. It is an ordered list of tracks scheduled for playback. Every venue has exactly one active queue. The queue operates in one of two modes: **DJ Mode** or **Autopilot Mode**. Mode is a configurable property of the venue and can be switched by any admin at any time.

---

## DJ Mode

In DJ Mode, the DJ/Admin is the gatekeeper of the queue. All recommendations require DJ approval before any track enters the queue.

### DJ Mode — Recommendation State Machine

```plantuml
@startuml Queue-DJMode-StateMachine
skinparam backgroundColor #FEFEFE
title Recommendation Lifecycle — DJ Mode

[*] --> submitted : Guest submits recommendation

state submitted {
  : Track ref captured\nProfile score computed\nNotification sent to DJ
}

submitted --> pending : System creates recommendation

state pending {
  : Awaiting DJ action\nNo time limit
}

pending --> accepted : DJ accepts
pending --> rejected : DJ rejects

state accepted {
  : QueueItem created\nAdded to end of queue\nGuest notified
}

state rejected {
  : Removed from inbox\nGuest notified
}

accepted --> [*]
rejected --> [*]
@enduml
```

| From | To | Trigger | Actor |
|---|---|---|---|
| `submitted` | `pending` | Recommendation created | System |
| `pending` | `accepted` | PATCH /recommendations/{id} action: accept | Admin/DJ |
| `pending` | `rejected` | PATCH /recommendations/{id} action: reject | Admin/DJ |

**Rules:**
- Only the Admin/DJ role can accept or reject in DJ Mode.
- If multiple admins are present, any admin may act (first-action wins).
- Pending recommendations have no automatic expiry in DJ Mode.
- A rejected recommendation cannot be re-submitted by the same guest within a configurable cooldown (default: 1 session).

---

## Autopilot Mode

In Autopilot Mode, recommendations self-govern. A submitted recommendation starts a countdown. If no admin casts a veto before the deadline, the recommendation is automatically promoted to the queue.

### Autopilot Mode — Recommendation State Machine

```plantuml
@startuml Queue-AutopilotMode-StateMachine
skinparam backgroundColor #FEFEFE
title Recommendation Lifecycle — Autopilot Mode

[*] --> submitted : Guest submits recommendation

state submitted {
  : Track ref captured\nProfile score computed\nVeto window timer started
}

submitted --> pending : Timer running

state pending {
  : Countdown active\nAdmins may veto\nveto_deadline = now + veto_window_seconds
}

pending --> promoted : Veto window expires\nwith no veto cast
pending --> vetoed : Admin casts veto\nbefore deadline

state promoted {
  : QueueItem created\nAdded to queue\nGuest notified
}

state vetoed {
  : Recommendation blocked\nGuest notified
}

promoted --> [*]
vetoed --> [*]
@enduml
```

| From | To | Trigger | Actor |
|---|---|---|---|
| `submitted` | `pending` | Recommendation created; veto timer started | System |
| `pending` | `promoted` | Veto window expires without a veto | System (timer) |
| `pending` | `vetoed` | Admin casts veto before deadline | Admin |

**Rules:**
- Any admin at the venue may cast a veto.
- A veto is irrevocable. Once vetoed, the recommendation cannot be re-promoted.
- The veto window duration is configurable per venue (10-3600 seconds, default 60).

---

## QueueItem Lifecycle

```plantuml
@startuml Queue-QueueItem-StateMachine
skinparam backgroundColor #FEFEFE
title QueueItem Lifecycle

[*] --> pending : Item added to queue

state pending {
  : Awaiting playback\nHas a position in queue
}

pending --> playing : Venue Player begins playback

state playing {
  : Audio playing\nNow Playing = this item
}

playing --> played : Track completes (Venue Player emits trackEnded)
playing --> skipped : Admin triggers skip

state played {
  : Completed normally\nFeedback event: complete
}

state skipped {
  : Skipped by admin\nFeedback event: skip
}

played --> [*]
skipped --> [*]
@enduml
```

---

## Veto Window

| Property | Value |
|---|---|
| Default | 60 seconds |
| Minimum | 10 seconds |
| Maximum | 3600 seconds |
| Granularity | 1 second |
| Timer start | When Recommendation transitions to `pending` |
| Timer authority | Queue Service; persistent (survives service restart) |

**Timer persistence:** The `veto_deadline` timestamp is stored on the Recommendation record. On service restart, the Queue Service scans all pending recommendations and restores timers for those whose `veto_deadline` is in the future.

**Changing the veto window:** Changes apply to new recommendations only. In-flight pending recommendations retain their original `veto_deadline`.

---

## Mode Switch Behavior

Mode can be switched at any time by any admin. All connected clients receive a `mode.changed` WebSocket event within 2 seconds.

### DJ Mode to Autopilot Mode

| Pending recommendation state | Action |
|---|---|
| Recommendations in DJ inbox (status: `pending`) | Each one starts a new veto timer. `veto_deadline` = now + veto_window_seconds. |
| No pending recommendations | No action. New recommendations follow Autopilot rules. |

### Autopilot Mode to DJ Mode

| Pending recommendation state | Action |
|---|---|
| Pending with active veto timers | Veto timers are paused. The `veto_deadline` is preserved but countdown stops. Recommendations move into DJ inbox. |
| No pending recommendations | No action. New recommendations follow DJ Mode rules. |

**Timer pause semantics:** When Autopilot switches to DJ Mode, remaining time on each recommendation veto window is preserved. If mode switches back to Autopilot, the timer resumes from its remaining duration.

---

## Queue Ordering

- Queue items have a `position` field using fractional indices (e.g. 1.0, 2.0, 3.0).
- New items appended receive `position = max(existing_positions) + 1.0`.
- Inserting between positions `p1` and `p2` yields `position = (p1 + p2) / 2`.
- If fractional precision degrades (positions within 0.001 of each other), a rebalancing operation renumbers all items with integer spacing. This is an admin-transparent background operation.
- The Venue Player always plays the item with the lowest `position` value among items with `status: pending`.

---

## Multi-Admin Quorum

| Mode | Quorum for Veto/Rejection |
|---|---|
| Autopilot | Any single admin may veto. No quorum required. First veto wins. |
| DJ Mode | The designated DJ makes all decisions. Other admins may not accept/reject. |

**Designating a DJ:** In DJ Mode, any admin has DJ rights by default. If the venue owner configures a specific DJ user, only that user actions are processed.

---

## Edge Cases

### Empty Queue
When the queue is empty and the Venue Player reports `trackEnded` or `idle`:
- The Venue Player transitions to `idle` state.
- A `nowplaying.changed` event is emitted with `queue_item_id: null`.
- No automatic action is taken. The next accepted/promoted recommendation will create a QueueItem and trigger playback.

### Duplicate Recommendations
A guest may not submit a recommendation for a track that is currently pending, playing, or already in a pending recommendation state. Attempting to do so returns `409 DUPLICATE_RECOMMENDATION`. The check is per-venue, not per-guest.

### Track Ends During Mode Switch
If a `trackEnded` event arrives while a mode switch is in progress, the Queue Service processes the `trackEnded` first, then applies the mode switch. The mode switch is idempotent.

### Recommendation Submitted During Mode Switch
Recommendations submitted while a mode switch is processing are held in `submitted` state and assigned to the new mode rules once the switch completes (within 500ms).
