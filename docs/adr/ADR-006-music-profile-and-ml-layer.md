# ADR-006: Music Profile and ML Layer

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox needs to steer Autopilot Mode using a "music profile" — a representation of what kind of music fits a venue at a given time. The profile must:

1. Be useful from day one, even before any play history exists (cold-start problem).
2. Improve over time as play, skip, and veto data accumulates.
3. Not require a cloud ML service to function (must work on a Raspberry Pi with no internet).
4. Be upgradeable to a more sophisticated model in the future without breaking the API.

The profile is also used to score incoming recommendations so that the DJ (in DJ Mode) has signal about how well a recommendation fits the venue's current vibe.

---

## Decision

Use a **tag-weight vector Music Profile** with a **rule-based scorer** and a **pluggable scoring interface** for future ML upgrade.

**Profile structure:** Weighted mood tags, weighted genre tags, BPM range, energy range, explicit flag. Weights initialized to even distribution; updated by a feedback loop.

**Feedback loop:** Play events apply small positive increments to matching tags; skip and veto events apply negative increments. A daily decay function prevents overfitting to recent history. BPM and energy ranges nudge toward completed tracks and away from skipped/vetoed tracks.

**Scoring interface:**
```
interface ScoringEngine:
  score(track, profile) -> float
  processFeedback(event, profile) -> MusicProfile
  getVersion() -> string
```

The default implementation is rule-based. A future model-based implementation implements the same interface and can be swapped in without changing any caller.

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Hardcode genre-based rules only | Very simple | Does not learn; no BPM/energy dimension; rigid and brittle | Too rigid; does not improve over time |
| Require a cloud ML API from day one | More sophisticated recommendations immediately | Requires internet; adds cost and third-party dependency; violates local-first | Violates local-first; adds dependency |
| Collaborative filtering across venues | High-quality recommendations based on cross-venue data | Requires cross-venue data sharing; privacy concern; cold-start requires many venues; not viable for Tier 1 | Privacy concern; not feasible for isolated deployments |
| Pure audio fingerprinting (no tags) | Source-agnostic; doesn't rely on metadata quality | Requires audio analysis at ingest time; computationally expensive on RPi; no metadata available until played | Too compute-heavy for Tier 1 |

---

## Consequences

**Positive:**
- Works offline and on minimal hardware (rule-based scorer has no external dependencies).
- No ML infrastructure required for v1.
- Profile improves over time through the feedback loop.
- Model-based implementation can be swapped in by implementing the interface; no API change.
- Cold-start problem is handled by the even-distribution initialization; owner can set weights explicitly from day one.

**Negative / Trade-offs:**
- Rule-based scoring is a simplification; it cannot capture nuanced cross-tag relationships (e.g. "energetic jazz" vs "energetic electronic" may have different venue fit).
- Feedback loop delta magnitudes are hardcoded defaults; may need tuning per venue type.
- Tag quality depends on Source Adapter metadata; poor metadata degrades scoring accuracy.
- Even-distribution cold start may not match the owner's actual taste; the profile editor exists to address this.

---

## Related ADRs

- [ADR-001: Local-First Sync Strategy](ADR-001-local-first-sync-strategy.md) — ProfileFeedbackEvents generated offline are synced on reconnect
- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — rule-based scorer must run on Tier 1 (RPi) within the 500MB RAM budget
