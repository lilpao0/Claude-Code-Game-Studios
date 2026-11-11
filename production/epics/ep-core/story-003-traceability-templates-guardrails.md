# Story 003: Add traceability ticket/PR template and agent guardrails

> **Epic**: EP-CORE — Foundation & Repo Bootstrap
> **Status**: Done
> **Layer**: Foundation
> **Type**: Config/Data
> **Estimate**: S
> **Manifest Version**: 2026-11-04
> **Tracker ticket**: FND-007 (stub: `production/epics/Foundation/story-fnd-007.md`)

## Context

**GDD**: `design/gdd/systems-index.md` (row #30 Config & Seed; doc 24 §1 Phase 0, §5 engineering priority #1 "repository skeleton + agents rules/CI")
**Requirement**: `TR-fnd-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0020: Configuration, Benchmark and Release Gates
**ADR Decision Summary**: Versioned and configured values, validated by benchmarks and playtests; no invented fixed number is approved; release gates are explicit rather than implicit.
**ADR Version**: 12/10/2026

**Engine**: Unity 6.3 LTS (C# / URP) + ASP.NET Core / PostgreSQL backend | **Risk**: HIGH
**Engine Notes**: No engine API is touched by this story. The Unity 6.3 LTS pin is rated HIGH in `docs/engine-reference/unity/VERSION.md`; no Unity 6.3 editor is installed on this machine, which does not affect this story.

**Control Manifest Rules (Foundation layer)**:
- Required: (no engine rule applies — this story is repository governance, not Unity code)
- Forbidden: (none at this layer for repository files)
- Guardrail: (none performance-related)

Doc 24 §6 sets merge discipline — independent review, no hidden scope — and doc 15 defines the ticket/PR convention. This story encodes both as files a guardrail can enforce, so the discipline stops being a convention.

---

## Acceptance Criteria

*From doc 24 §6 and doc 15:*

- [x] AC1: PR template requires a ticket ID, FR/TC refs, a change list, an evidence link and a named reviewer — as five separately labelled fields
- [x] AC2: An agent guardrail (documented script or hook) rejects PRs missing ticket or FR/TC refs, exiting non-zero and naming what is missing
- [x] AC3: Doc 24 §6 merge discipline (independent review, no hidden scope) is encoded as a checklist with one item per rule

---

## Implementation Notes

*Derived from ADR-0020 and doc 24 §6:*

- PR template path: `.github/pull_request_template.md`. Each of the five fields is a labelled placeholder (`Ticket:`, `FR/TC:`, `Changes:`, `Evidence:`, `Reviewer:`) — free prose mentioning the words is not acceptable.
- The guardrail is an executable check (script or hook) whose path is recorded in the story file, wired into CI by story 005. It must reject a PR body with an empty ticket ID, a body citing only a TC id with no FR id, and a body saying "ticket" with no id. Placeholder values ("TBD", "n/a", "-") count as missing.
- The merge-discipline checklist has one checkbox line per §6 rule, at minimum "independent review" and "no hidden scope" as separate items. Doc 24 is a `.docx` and is not machine-readable in-repo, so the checklist's authoritative content is the two named rules plus any further §6 items the implementer can read from the `.docx`; this story's AC asserts the two named rules.
- TR-ID convention: PRs cite TR-IDs from `docs/architecture/tr-registry.yaml` (and FR/TC ids from the SRS) — this is what makes the traceability loop mechanical.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005 (FND-005): the CI pipeline this guardrail is wired into
- Story 011 (DOC-002): the API/SQL coverage matrix that makes FR/TC refs citable
- Story 012 (DOC-003): the M1/M2 ticket split that this template will reference

---

## Files

- `.github/pull_request_template.md`
- `.github/scripts/check-traceability.sh` (guardrail; path recorded here)
- `.github/merge-discipline-checklist.md`
- `[test root]` — none (Config/Data; guardrail is self-testing)

---

## QA Test Cases

*Written by qa-lead at story creation. Config/Data → advisory smoke-check form; the guardrail is exercised directly as an executable check.*

- **AC1**: PR template requires five labelled fields
  - Given: a fresh clone at the commit carrying this story
  - When: `.github/pull_request_template.md` is read
  - Then: it contains five distinct labelled fields — a ticket ID field, an FR/TC reference field, a change-list field, an evidence-link field and a named-reviewer field
  - Edge cases: a template that mentions the words without a labelled input (e.g. "Ticket: <id>") is a failure; each field must be a labelled placeholder, not a bare sentence
- **AC2**: Guardrail rejects PRs missing ticket or FR/TC refs
  - Given: the guardrail script or hook is present in the repo (path recorded in this story file)
  - When: it is invoked with a PR body containing a ticket ID but no FR/TC reference
  - Then: it exits with a non-zero status and its output names both the missing ticket ref and the missing FR/TC ref
  - And: when it is invoked with a PR body containing both a ticket ID and at least one FR- or TC-reference, it exits with a zero status
  - Edge cases: a PR body with an empty ticket ID, a body citing only a TC id with no FR id, and a body with the word "ticket" but no ID — each must be rejected (non-zero exit). Whitespace-only or placeholder values ("TBD", "n/a", "-") count as missing
- **AC3**: Merge discipline encoded as a checklist
  - Given: a fresh clone at the commit carrying this story
  - When: the merge-discipline checklist file (path recorded in this story file) is read
  - Then: it contains a checklist item for independent review and a checklist item for no hidden scope, each as a separate checkbox line
  - Edge cases: the two items must not be collapsed into one line, and the checklist must not be satisfied by a link to doc 24 with no itemised text. Note: doc 24's full §6 item list is not machine-readable in-repo (.docx); this AC asserts the two named rules, and additional §6 items are out of scope for this criterion

### Evidence

**AC1** — `.github/pull_request_template.md`. Five distinct labelled
placeholders, each on its own line: `**Ticket:**`, `**FR/TC:**`,
`**Changes:**`, `**Evidence:**`, `**Reviewer:**` (lines 24–32). Verified by
`grep -cE "^\*\*(Ticket|FR/TC|Changes|Evidence|Reviewer):\*\*"` → 1 each, and
the five lines are distinct.

**AC2** — `.github/scripts/check-traceability.sh` (POSIX sh + `grep -E` only;
no jq, no python, per the repo's hook rules). Exit 0 on acceptance, exit 1 on
rejection, and the output **names every missing field**. The script was
exercised against nine bodies; every rejection was observed, not assumed:

| PR body | Exit | What the output named |
|---|---|---|
| ticket + `FR-COL-007, TC-COL-001` + changes + evidence + reviewer | 0 | — (PASS) |
| ticket + `TR-fnd-003` + changes + evidence + reviewer | 0 | — (PASS) |
| ticket present, `FR/TC:` empty | 1 | FR/TC |
| `Ticket:` empty | 1 | Ticket |
| `Ticket: ticket` (the word, no id) | 1 | Ticket |
| `FR/TC: TC-COL-001` (a TC id only) | 1 | FR/TC |
| all fields placeholder (`TBD`, `n/a`, `-`) | 1 | Ticket, FR/TC, Changes, Evidence, Reviewer |
| framework-only opt-out on ticket + FR/TC | 1 | Evidence (opt-out covers ticket and FR/TC only) |
| `Evidence: n/a` with a valid ticket | 1 | Evidence |

The last two rows are deliberate: the opt-out is scoped to the fields it is
written into, and a placeholder is still a missing value everywhere else.

**Spec conflict resolved.** The story's happy-path clause says "ticket ID and
at least one FR- **or** TC-reference → exit 0", while its own edge case says "a
body citing only a TC id with no FR id → must be rejected". These contradict.
The stricter reading was chosen (the TC-only case is rejected), because a TC id
names a test case, not a requirement — a traceability link has to reach the
requirement being verified, otherwise the loop it closes is not closed. The
template states this rule so a PR author is not surprised by it. This is a
deviation from the story text and is flagged here for review.

**AC3** — `.github/merge-discipline-checklist.md`, 10 checkbox lines. "Independent
review" and "No hidden scope" are each their own line (verified: the two
`grep -n` line numbers differ, i.e. not collapsed), and neither is satisfied
by a bare link to doc 24.

**CI wiring:** the guardrail is a standalone script; story 005's pipeline
invokes it. Until that pipeline lands, it runs locally as
`bash .github/scripts/check-traceability.sh <body-file>`.

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- Config/Data: smoke check pass (`production/qa/smoke-*.md`)
- Status: [x] Created

---

## Dependencies

- Depends on: `production/epics/ep-core/story-001-monorepo-folder-architecture.md`
- Unlocks: None directly; the guardrail is wired into CI by story 005
