# DJQBox — Spec Agent Instructions

Read `AGENTS.md` first. This file contains specific instructions for agents working on the DJQBox specification suite.

---

## Your Role

You are a **Principal Architect** contributing to the DJQBox spec suite. Your job is to write, extend, and refine platform-agnostic specification documents. You do not write implementation code. You do not make tool, framework, or vendor decisions (those go in `docs/adr/` or `docs/impl/`).

---

## Spec Conventions

### Document Header
Every spec document starts with a YAML front matter block:
```markdown
---
title: DJQBox — [Document Title]
version: 1.0.0
date: YYYY-MM-DD
status: Draft | Review | Accepted
---
```

Increment the minor version (`1.1.0`) for additions. Increment the major version (`2.0.0`) for breaking changes to interfaces or data shapes.

### Language and Style
- Write in precise technical English. No marketing language.
- Active voice in decisions: "The system must..." not "It is recommended that..."
- Define every term on first use. If a term is in the glossary (`00-overview.md`), use it consistently.
- Use tables for structured comparisons. Use numbered lists for sequences. Use bullet points for unordered sets.
- Pseudocode is preferred over actual code for algorithm descriptions (the spec is language-agnostic).

### Diagrams
- All diagrams use **PlantUML syntax** in fenced code blocks: ` ```plantuml `.
- Every diagram has a `title` directive.
- Use `skinparam backgroundColor #FEFEFE` as the default background.
- State machine diagrams: use `@startuml` / `@enduml` with `state` blocks.
- Sequence diagrams: use `actor`, `participant`, `->`, `-->`.
- ER diagrams: use `entity` blocks with `--` separating key fields from non-key fields.
- Component diagrams: use `node`, `component`, `database` with `skinparam componentStyle rectangle`.

### Platform Agnosticism
- Never mention a specific programming language, framework, cloud provider, or vendor in `docs/spec/` documents.
- Acceptable: "a relational database", "a message broker", "an HTTP endpoint", "a WebSocket connection".
- Not acceptable in spec: "PostgreSQL", "Azure Service Bus", ".NET", "React", "Docker".
- When a concrete example helps clarity, prefix it with "e.g." and keep it brief.
- Implementation-specific details belong in `docs/impl/` or `docs/adr/`.

---

## Adding a New Spec Document

### Step 1 — Check what exists
Read `docs/spec/00-overview.md` (Document Index section) and the existing spec files to understand what is already defined. Do not duplicate content; cross-reference instead.

### Step 2 — Identify the gap
Clearly state what problem or domain this spec addresses that is not covered elsewhere. If an existing spec needs extending rather than a new document, extend it and update the version number.

### Step 3 — Write the document
Follow this structure for new spec documents:

```markdown
# DJQBox — [Title]

## Overview
One paragraph: what this spec covers, why it exists, what problem it solves.

## [Domain Section 1]
...

## [Domain Section 2]
...

## Conformance Checklist (if applicable)
Numbered list of MUST requirements for implementers.

## Related Specifications
Links to other spec documents this one depends on or extends.
```

### Step 4 — Update the Document Index
Add a row to the Document Index table in `docs/spec/00-overview.md`.

### Step 5 — Update AGENTS.md
Add the new document to the Document Map table in `AGENTS.md`.

---

## Adding a New ADR

ADRs live in `docs/adr/`. They are numbered sequentially (`ADR-011`, `ADR-012`, ...).

### ADR Template
```markdown
# ADR-NNN: [Short Noun-Phrase Title]

- **Date:** YYYY-MM-DD
- **Status:** Proposed | Accepted | Deprecated | Superseded by ADR-NNN
- **Deciders:** Architecture Team

---

## Context

What situation or problem drove this decision? Include relevant constraints,
requirements, and forces at play. Keep descriptive and factual.

---

## Decision

State the decision in active voice:
"We will [do X] because [brief reason]."

---

## Alternatives Considered

| Option | Pros | Cons | Why Rejected |
|---|---|---|---|
| Option A | ... | ... | ... |

---

## Consequences

**Positive:**
- ...

**Negative / Trade-offs:**
- ...

---

## Related ADRs

- [ADR-NNN: Title](ADR-NNN-title.md) — relationship
```

### ADR Rules
- ADRs are **immutable once Accepted**. To change a decision, write a new ADR that supersedes the old one, then update the old ADR's `Status` to `Superseded by ADR-NNN`.
- The `Decision` section must use active voice: "We will..." not "It was decided..."
- The `Alternatives Considered` section is required. A decision without documented alternatives provides no value.
- After writing a new ADR, update `docs/adr/README.md` to add it to the index.

---

## Spec Backlog

These are the next spec documents to write, in priority order:

### Priority 1 — Needed before MVP

#### `12-error-handling-spec.md`
Define the canonical error handling contract across all services:
- Standard error response schema (already sketched in `04-api-contracts.md` — expand it)
- Full list of error codes and their HTTP status mappings
- Retry policy for client-side retries (which errors are retryable, backoff strategy)
- Circuit breaker behavior for Source Adapter failures
- Error propagation rules (which internal errors are exposed vs. opaque to clients)
- Logging requirements for each error class

#### `ADR-011-error-handling-strategy.md`
Why the error code / schema approach was chosen over alternatives (HTTP status only, gRPC status codes, Problem Details RFC 7807, etc.).

---

### Priority 2 — Needed before full multi-venue MVP

#### `13-venue-management-spec.md`
Detailed spec for venue CRUD and lifecycle:
- Venue creation flow (owner perspective)
- Venue archival vs. deletion
- Venue settings: all configurable properties and their defaults
- Multi-venue dashboard data contract (what the owner sees per venue)
- Venue access token management (generate, rotate, revoke QR tokens)

---

### Priority 3 — Needed before Tier 3 / cloud scale

#### `14-rate-limiting-spec.md`
- Rate limit policies per role (guest: 10 recommendations/hour, admin: unlimited)
- Rate limit response format (`429 Too Many Requests`, `Retry-After` header)
- Per-venue vs. per-user rate limiting
- Burst allowances

#### `15-multi-tenancy-spec.md`
- How the data model enforces venue isolation at the database level
- Row-level security patterns (platform-agnostic)
- Cross-tenant data leakage prevention
- Owner account spanning multiple venues: data access boundaries

---

### Ongoing — Spec Refinement

As the POC implementation proceeds, gaps and ambiguities in the existing specs will surface. When they do:
1. Open the relevant spec document.
2. Add or clarify the section.
3. Increment the minor version in the front matter.
4. Note the change in a `## Changelog` section at the bottom of the document.

---

## Changelog Section Format

When updating an existing spec, append a changelog entry at the bottom:

```markdown
---

## Changelog

### v1.1.0 — 2025-07-15
- Added `ProfileSnapshot` entity to `07-music-profile-spec.md` (missing from v1.0.0)
- Clarified veto window timer persistence behavior in `06-queue-spec.md`

### v1.0.0 — 2025-07-01
- Initial version
```

---

## Cross-Reference Rules

- When referencing another spec document, use a relative markdown link: `[Queue Spec](06-queue-spec.md)`.
- When referencing an ADR, use: `(see [ADR-001](../adr/ADR-001-local-first-sync-strategy.md))`.
- When referencing a field or entity, use backtick formatting: `` `QueueItem.position` ``, `` `Recommendation.status` ``.
- When referencing an API endpoint, use: `` `POST /api/v1/venues/{id}/recommendations` ``.

---

## Quality Checklist

Before considering a spec document complete, verify:

- [ ] Every term used is defined in the Glossary (`00-overview.md`) or defined on first use in this document.
- [ ] All diagrams are valid PlantUML syntax with a `title` directive.
- [ ] No framework, language, or vendor names appear in the document body (only in "e.g." examples if necessary).
- [ ] All interface operations have: parameter types, return type, and error conditions documented.
- [ ] All state machines have: every state named, every transition labeled with trigger and actor.
- [ ] Conformance checklists use numbered, testable requirements ("MUST", "MUST NOT").
- [ ] The Document Index in `00-overview.md` has been updated.
- [ ] `AGENTS.md` Document Map has been updated.
