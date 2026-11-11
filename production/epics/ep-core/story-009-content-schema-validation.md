# Story 009: Add content schema validation build script

> **Epic**: EP-CORE — Foundation & Repo Bootstrap
> **Status**: In Progress
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-11-04
> **Tracker ticket**: FND-010 (stub: `production/epics/Foundation/story-fnd-010.md`)

## Context

**GDD**: `design/gdd/systems-index.md` (row #30 Config & Seed, #12 Collectibles; doc 24 §1 Phase 0; doc 19 content schema, doc 20 config/seed)
**Requirement**: `TR-fnd-009`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0020: Configuration, Benchmark and Release Gates
**ADR Decision Summary**: All tunable values are versioned and configured, then validated; no invented fixed number is approved — and the validation is a gate, not a convention.
**ADR Version**: unversioned

**Engine**: Unity 6.3 LTS (C# / URP) + ASP.NET Core / PostgreSQL backend | **Risk**: HIGH
**Engine Notes**: No engine API is touched — this is a build script over content files, runnable outside the editor so CI can run it without a Unity install. The pinned Unity 6.3 LTS editor is not installed on this machine; irrelevant here.

**Control Manifest Rules (Server / Backend layer)**:
- Required: content values are **versioned and configured**, and validated by a gate — source: ADR-0020
- Forbidden: **never hardcode a loot probability in client or server code** — the pool is data — source: ADR-0005, ADR-0003
- Guardrail: the server RNG draws from a **versioned published pool** — a pool that fails validation is a pool the server may not publish (source: ADR-0003)

---

## Acceptance Criteria

*From doc 19 and doc 20:*

- [ ] AC1: All four loot pools total exactly 10,000 basis points
- [ ] AC2: Every reference resolves — a loot entry's SKU, and any content item referenced by another item, exists in the content set
- [ ] AC3: No missing and no duplicate IDs across the content set
- [ ] AC4: The build script fails, listing every violation found (not just the first), and runs in CI on content changes

---

## Implementation Notes

*Derived from ADR-0020 and doc 19:*

- The validator is a standalone script/tool runnable from the CLI (the CI job in story 005 runs it on content changes — it must not require a Unity install).
- "All four loot pools" — the count is fixed at four by doc 19 and by the FR-COL-001/FR-COL-006 pack structure (the card pack uses a separate pool). The four pools are named in doc 19; the script reads all pools it finds and fails if the count is not four, so a missing pool file is caught rather than silently reducing the set.
- Basis-point totals are per pool, and each pool's weights must be non-negative as well as summing to 10,000 — a pool summing to 10,000 with a negative entry is a violation.
- Reference resolution covers at least: a loot entry's SKU id, and item→item references (e.g. a recipe's output). Each unresolved reference is reported with the referring id and the missing id.
- Duplicate ids are reported per content type with the id and the two files that declare it.
- The script lists **every** violation in one run, grouped by violation kind — a fail-fast validator that stops at the first error is a failure of AC4.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006 (FND-006): the DEV_SEED profile loader and its own validation rules (this script validates the content set, not a seed profile)
- Story 005 (FND-005): the CI workflow that runs this script on content changes

---

## Files

- `backend/Tools/ContentValidator/` (or `tools/validate-content/`)
- `backend/tests/unit/content/content_schema_test.cs`
- `[test root]` `backend/tests/unit/content/content_schema_test.cs`

---

## QA Test Cases

*Written by qa-lead at story creation (QL-STORY-READY GAPS refinements applied — AC1/AC2/AC3/AC4 split to make each observable). Logic → **BLOCKING**: an automated unit test is required, in `backend/tests/unit/content/`.*

- **AC1**: All four loot pools total exactly 10,000 basis points
  - Given: the content set with four valid loot-pool files, each totaling 10,000
  - When: the validator runs
  - Then: it exits zero and reports four pools
  - Edge cases: a pool totaling 9,999 → violation reported naming the pool id and its total; a pool totaling 10,001 → same; a pool containing a negative weight but totaling 10,000 → still a violation (the sum check alone must not pass it); a fifth pool file present → violation (the pool count is fixed at four)
- **AC2**: Every reference resolves
  - Given: the content set with one loot entry referencing a SKU id that does not exist
  - When: the validator runs
  - Then: it exits non-zero and the output names the referring entry id and the unresolved SKU id
  - Edge cases: an item→item reference to a missing id is reported the same way; a resolved reference produces no finding; the validator must report an unresolved reference in a loot pool and in an item definition independently
- **AC3**: No missing and no duplicate IDs
  - Given: the content set with one id declared in two files
  - When: the validator runs
  - Then: it exits non-zero and reports the duplicated id together with both declaring file paths
  - Edge cases: the same id appearing in two different content types is a duplicate (the id namespace is global); an id declared exactly once produces no finding
- **AC4**: Build script fails listing every violation, and runs in CI on content changes
  - Given: the validator and a content set seeded with three distinct violations (one pool total, one unresolved reference, one duplicate id)
  - When: the validator runs
  - Then: it exits non-zero and the output contains all three violations in one run, not just the first
  - And: the CI workflow (story 005) runs the validator on content changes, and a content-changing PR with a violation turns the CI job red
  - Edge cases: a validator that stops at the first violation is a failure; a validator that exits zero when violations exist is a failure

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `backend/tests/unit/content/content_schema_test.cs` — must exist and pass (`/story-done` checks that it EXISTS; pass/fail is established by `/gate-check` and `/smoke-check`, both later)
- Status: [x] Local unit/process evidence created and passing; actual remote CI portion of AC4 remains pending.

Local verification and independent LP/language/QL reviews: `production/qa/story009-local-verification-2026-11-11.md`. Story remains In Progress until actual content-changing PR CI red/green evidence exists; local passes do not close AC4.

---

## Dependencies

- Depends on: `production/epics/ep-core/story-006-dev-seed-validator.md`
- Unlocks: None directly (story 005 runs it in CI)
