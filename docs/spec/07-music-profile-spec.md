---
title: DJQBox — Music Profile Specification
version: 1.0.0
date: 2025-07-01
status: Draft
---

# DJQBox — Music Profile Specification

## Overview

The Music Profile is the sonic identity of a venue. It is a per-venue data structure that encodes what kind of music belongs in a space, expressed as weighted tags, numeric ranges, and a content flag. The Profile has two roles:

1. **Scoring:** When a Recommendation arrives, the Profile scores it for compatibility. This score informs the DJ (in DJ Mode) and can filter in Autopilot Mode.
2. **Feedback loop:** Every play, skip, veto, and track completion updates the Profile weights, so the Profile learns the venue's taste over time.

The Profile is designed as a rule-based system that can be upgraded to a model-based ML implementation without changing the interface.

---

## Tag Taxonomy

### Mood Tags

| Tag | Description |
|---|---|
| `energetic` | High tempo, driving energy, danceable |
| `chill` | Relaxed, low-tempo, background-friendly |
| `melancholic` | Reflective, minor keys, emotional |
| `upbeat` | Positive, feel-good, lively |
| `dark` | Heavy, moody, intense |
| `romantic` | Soft, intimate, warm |
| `focused` | Minimal distraction, steady rhythm, productivity-oriented |
| `festive` | Celebratory, seasonal, party-appropriate |

### Genre Tags

| Tag | Examples |
|---|---|
| `jazz` | Classic jazz, bebop, smooth jazz |
| `classical` | Orchestral, chamber, contemporary classical |
| `electronic` | Ambient electronic, techno, house, IDM |
| `hip_hop` | Rap, trap, lo-fi hip-hop |
| `pop` | Contemporary pop, synth-pop, indie pop |
| `rock` | Classic rock, indie rock, alternative |
| `folk` | Acoustic folk, singer-songwriter, Americana |
| `r_and_b` | Soul, funk, contemporary R&B |
| `latin` | Salsa, bossa nova, reggaeton |
| `world` | Afrobeat, flamenco, Bollywood, Celtic |
| `blues` | Delta blues, Chicago blues, electric blues |
| `country` | Classic country, alt-country, bluegrass |
| `metal` | Heavy metal, post-metal, doom |
| `punk` | Punk rock, hardcore, post-punk |
| `reggae` | Roots reggae, dancehall, dub |
| `soul` | Motown, neo-soul, gospel |
| `soundtrack` | Film scores, game soundtracks, cinematic |
| `ambient` | Drone, new age, sound design |
| `experimental` | Avant-garde, noise, glitch |
| `lo_fi` | Lo-fi hip-hop, bedroom pop, cassette aesthetics |

### Numeric Ranges

| Attribute | Type | Range | Description |
|---|---|---|---|
| `bpm_min` | integer | 0–250 | Minimum acceptable BPM |
| `bpm_max` | integer | 0–250 | Maximum acceptable BPM |
| `energy_min` | float | 0.0–1.0 | Minimum energy level (0 = near-silent, 1 = peak intensity) |
| `energy_max` | float | 0.0–1.0 | Maximum energy level |

### Content Flag

| Attribute | Type | Default | Description |
|---|---|---|---|
| `explicit_allowed` | boolean | `false` | Whether tracks with explicit content are eligible |

---

## Scoring Algorithm

The scoring algorithm computes a compatibility score between a candidate Track and the active MusicProfile. The result is a float in `[0.0, 1.0]`, where 1.0 is a perfect match and 0.0 is incompatible.

### Pseudocode

```
function scoreTrack(track, profile):

  // Hard reject: explicit content
  if track.explicit == true AND profile.explicit_allowed == false:
    return 0.0

  // Hard reject: BPM out of range (skip check if BPM metadata is absent)
  if track.bpm is not null:
    if track.bpm < profile.bpm_min OR track.bpm > profile.bpm_max:
      return 0.0

  // Hard reject: energy out of range (skip check if energy metadata is absent)
  if track.energy_level is not null:
    if track.energy_level < profile.energy_min OR track.energy_level > profile.energy_max:
      return 0.0

  // Mood score: weighted dot product
  mood_score = 0.0
  mood_weight_sum = sum(profile.mood_weights.values)
  if mood_weight_sum > 0:
    for tag in track.mood_tags:
      if tag in profile.mood_weights:
        mood_score += profile.mood_weights[tag]
    mood_score = mood_score / mood_weight_sum
  else:
    mood_score = 1.0  // No mood preference — any mood acceptable

  // Genre score: weighted dot product
  genre_score = 0.0
  genre_weight_sum = sum(profile.genre_weights.values)
  if genre_weight_sum > 0:
    for tag in track.genre_tags:
      if tag in profile.genre_weights:
        genre_score += profile.genre_weights[tag]
    genre_score = genre_score / genre_weight_sum
  else:
    genre_score = 1.0  // No genre preference — any genre acceptable

  // Combined score (equal weighting of mood and genre by default)
  final_score = (mood_score * 0.5) + (genre_score * 0.5)

  return clamp(final_score, 0.0, 1.0)
```

**Notes:**
- Tracks missing BPM or energy metadata are not hard-rejected for those attributes.
- Tracks with no mood or genre tags score the average of the profile's current weight distribution.
- The 0.5/0.5 split between mood and genre is the default weighting; configurable per venue.

---

## Profile Weights

### Initialization
When a venue is first created, the Music Profile is initialized with:
- Mood weights: equal distribution across all tags (`1.0 / num_tags` per tag)
- Genre weights: equal distribution across all tags
- BPM range: 0–250 (no restriction)
- Energy range: 0.0–1.0 (no restriction)
- Explicit: false

The owner may set custom weights via the profile editor. The feedback loop adjusts weights incrementally from whatever baseline the owner sets.

### Weight Constraints
- All weights are floats in `[0.0, 1.0]`.
- Weights are normalized after each update: the sum of all mood weights always equals 1.0; same for genre weights.
- A weight may not go below `0.01` through feedback alone (floor prevents complete exclusion). The owner may manually set a weight to `0.0` to explicitly exclude a tag.

---

## Feedback Loop

The feedback loop updates profile weights in response to playback events.

### Event Deltas

| Event | Mood Tag Delta | Genre Tag Delta | Notes |
|---|---|---|---|
| `play` | +0.02 per matching tag | +0.02 per matching tag | Small positive: track started |
| `complete` | +0.05 per matching tag | +0.05 per matching tag | Stronger positive: track played to end |
| `skip` | -0.03 per matching tag | -0.03 per matching tag | Negative: track not desired |
| `veto` | -0.08 per matching tag | -0.08 per matching tag | Strong negative: actively rejected |

All deltas are applied to matching tags of the track being played/skipped/vetoed, then weights are renormalized.

### Decay
To prevent the profile from overfitting to recent history, weights decay toward the uniform baseline on a daily schedule:

```
decay_rate = 0.01  // configurable per venue
baseline_weight = 1.0 / num_tags

for each tag in mood_weights:
  mood_weights[tag] = mood_weights[tag] * (1 - decay_rate) + baseline_weight * decay_rate
```

Over time, a profile that stops receiving feedback gradually returns to even distribution rather than remaining locked to a past state.

### BPM and Energy Adjustment
When a track is completed (positive signal), the BPM and energy ranges nudge toward the track's values:

```
bpm_min = bpm_min * 0.98 + track.bpm * 0.02
bpm_max = bpm_max * 0.98 + track.bpm * 0.02
```

When a track is skipped or vetoed, the range nudges away from the track's BPM/energy values.

---

## ML Interface

The scoring and feedback functions are defined as a pluggable interface. This allows the rule-based implementation to be replaced by a trained model without changing any calling code.

### Interface Definition

```
interface ScoringEngine:
  score(track: Track, profile: MusicProfile) -> float
  processFeedback(event: ProfileFeedbackEvent, profile: MusicProfile) -> MusicProfile
  getVersion() -> string
```

### Implementations

**Rule-based (default):** Implements the algorithm described above. No external dependencies. Works fully offline. Ships with every DJQBox deployment.

**Model-based (future):** Implements the same interface backed by a trained recommendation model (e.g. collaborative filtering, audio embedding similarity). The model is loaded from a local file and scores tracks against the profile using embedding distance rather than tag overlap. The feedback function updates a fine-tuning dataset rather than directly modifying weights. This is a future enhancement; out of scope for v1.

---

## Profile Inheritance

A venue may optionally declare a `parent_profile_id` pointing to a base profile defined by the owner. When set:
- The venue's profile inherits all weights and ranges from the parent.
- The venue may override any individual field.
- Feedback loop events update the venue's local weights, not the parent.
- The parent profile can be updated by the owner to cascade changes to all inheriting venues.

This allows the owner to define a "house style" and apply it across multiple venues with venue-specific overrides.

---

## Profile History

Every update to a MusicProfile creates a snapshot entry:

```
ProfileSnapshot {
  id:            UUID
  profile_id:    UUID
  snapshot_data: MusicProfile (full copy)
  reason:        "owner_edit | feedback_loop | decay | inheritance_update"
  created_at:    timestamp
}
```

Snapshots are retained for 90 days (configurable). An owner may roll back to any snapshot via the dashboard. Rollback creates a new profile edit event — it does not destructively overwrite the current state.
