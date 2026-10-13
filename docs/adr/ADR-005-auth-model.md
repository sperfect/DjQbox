# ADR-005: Auth Model

- **Date:** 2025-07-01
- **Status:** Accepted
- **Deciders:** Architecture Team

---

## Context

DJQBox has three user types with very different authentication requirements:

- **Owners** manage the system across potentially multiple venues and devices. They need strong identity, persistent sessions, and cross-device access.
- **Admins / DJs** manage a specific venue. They are trusted by the owner and need elevated but venue-scoped access.
- **Guests** join a venue casually, often via a physical QR code. They expect zero friction — no account, no form, no app install.

Additional constraints:
- The system must work offline for cached sessions (tokens cannot be validated against the server every request when offline).
- Venues must be private by default; no data is accessible without authorization.
- The system must run without any dependency on a third-party identity service in isolated deployments (Tier 1 RPi).
- Guest tokens must not grant any cross-venue access.

---

## Decision

Adopt a **three-tier auth model** with tokens signed by the deployment host's own key.

**Tier 1 — Owner:** Authenticates via WebAuthn passkey (device-local, no password) or OAuth (delegated to a third-party provider). Receives a long-lived access token (1 hour) plus a refresh token (30 days). Token spans all owned venues.

**Tier 2 — Admin / DJ:** An owner grants admin role at a specific venue by creating a VenueMembership record. On the admin's next login, the Auth Service detects VenueMembership and includes admin scopes for that venue in the issued token. No separate login flow required.

**Tier 3 — Guest:** An admin generates a QR code containing a URL with a signed venue token. The guest scans the QR code; the browser opens the venue's guest page; the guest submits the token to `POST /venues/{id}/join`; the Auth Service issues a scoped guest session token (24 hours). No account required.

All tokens are signed by the deployment host's signing key. Tokens from one host are not valid at another. Tokens are cached on the client for offline use with defined grace periods (owner: 7 days, admin: 24 hours, guest: 4 hours).

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Single auth system for all roles | Simpler implementation | Forces guests to create accounts; unacceptable friction for casual venue participation | Violates guest UX requirement |
| Anonymous guests with no token | Maximum friction reduction | No security boundary; any person with the URL can access any venue data | Unacceptable security posture |
| Third-party auth for guests (e.g. Google Sign-In) | No token management code required | Requires internet; adds latency; privacy concern; guests may not have accounts | Violates offline requirement; privacy concern |
| Per-request server token validation | Strongest revocation guarantee | Requires connectivity for every authenticated request; breaks offline operation | Violates local-first requirement |
| Shared secret per venue | Simple | Single compromised secret exposes all sessions; no per-user revocation | Weak security model |

---

## Consequences

**Positive:**
- Guests need no account; the QR join flow completes in under 5 seconds.
- Offline sessions work within defined grace periods.
- Venues are private by default; no cross-venue token leakage.
- No dependency on a third-party identity service in isolated (Tier 1) deployments.
- Passkey auth is phishing-resistant and requires no password storage.

**Negative / Trade-offs:**
- QR token revocation is not instant: existing guest sessions remain valid until TTL or explicit purge.
- During offline operation, the token blacklist cannot be checked; revoked tokens may remain valid for the grace period.
- Passkey requires device support (WebAuthn); older devices may not support it. OAuth is the fallback.
- Signing key management on a Raspberry Pi (Tier 1) requires the operator to protect a local key file.

---

## Related ADRs

- [ADR-001: Local-First Sync Strategy](ADR-001-local-first-sync-strategy.md) — offline token caching grace periods align with sync reconnect windows
- [ADR-008: Deployment Topology](ADR-008-deployment-topology.md) — signing key is a local file on Tier 1, a secrets manager on Tier 3
