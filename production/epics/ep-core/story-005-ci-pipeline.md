# Story 005: Add CI pipeline for C# tests and Unity checks

> **Epic**: EP-CORE — Foundation & Repo Bootstrap
> **Status**: In Progress
> **Layer**: Foundation
> **Type**: Config/Data
> **Estimate**: M
> **Manifest Version**: 2026-11-04
> **Tracker ticket**: FND-005 (stub: `production/epics/Foundation/story-fnd-005.md`)

## Context

**GDD**: `design/gdd/systems-index.md` (row #30 Config & Seed; doc 24 §1 Phase 0, §5 engineering priority #1 "repository skeleton + agents rules/CI")
**Requirement**: `TR-fnd-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-0020: Configuration, Benchmark and Release Gates
**ADR Decision Summary**: Versioned and configured values, validated by benchmarks and playtests; no invented fixed number is approved; gates are explicit — a release is never deployed implicitly.
**ADR Version**: unversioned

**Engine**: Unity 6.3 LTS (C# / URP) + ASP.NET Core / PostgreSQL backend | **Risk**: HIGH
**Engine Notes**: The Unity compilation job cannot run yet: ADR-0022 (pixel-art art direction) is `Proposed`, so story 010 (FND-003) is Blocked, and no Unity 6.3 LTS editor is installed on this machine (only 6000.4.0f1, the 6.4 line — `docs/engine-reference/unity/VERSION.md` records this as MISMATCH). `project.yaml` `commands.build`/`commands.test`/`commands.smoke` still carry the literal placeholder `"<Unity editor>"` and must be filled before any Unity-side check can run.

**Control Manifest Rules (Foundation layer)**:
- Required: (no engine rule applies to CI configuration itself)
- Forbidden: no implicit release deploy — the pipeline must not publish or deploy on a pull request
- Guardrail: (none performance-related; load targets belong to Global Rules and are validated later, per ADR-0020)

---

## Acceptance Criteria

*From doc 15, doc 23 and ADR-0020:*

- [ ] AC1: The PR workflow runs the backend build, the C# unit tests and the content validator, and posts the results as a PR check
- [ ] AC2: The workflow contains a Unity compilation job that invokes the Unity compilation smoke command (`project.yaml` `commands.smoke`), **disabled with a stated reason** while FND-003 is blocked, and **enabled automatically** once FND-003 closes
- [ ] AC3: No implicit release deploy — the workflow never publishes or deploys; release is a separate, explicitly triggered path

---

## Implementation Notes

*Derived from ADR-0020 and doc 15/23:*

- One workflow file (`.github/workflows/pr.yml`) triggered on `pull_request`. It must not be restricted to the default branch only.
- Jobs: `build` (dotnet build + unit tests) and `content` (the validator from story 009). The validator step must not be `continue-on-error` — it has to fail the job.
- The Unity job is present but skipped, with the skip reason naming ADR-0022 (Proposed) — this is what makes the deferral observable rather than a silent omission. Remove the skip as part of closing story 010.
- The traceability guardrail from story 003 is a step in this workflow.
- The CI status must be reported as a PR check (a status check on the head commit), so a failed run blocks merge.
- `project.yaml` `commands.smoke` needs its real Unity invocation before the job can be enabled.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010 (FND-003): the Unity project and its build — this story's Unity job stays skipped until it lands
- Story 009 (FND-010): the content validator itself (this story only runs it)
- Story 002 (FND-002): the module-boundary check (this story only runs it)

---

## Files

- `.github/workflows/pr.yml`
- `.github/scripts/unity-ci.py`, `check-pr-event.py`, `requirements-ci.txt`, `README-ci.md`
- `backend/tests/integration/ci/test_pr_checks.py` (configuration/process controls)
- `project.yaml` Unity placeholders remain owned by blocked010; closing it without configured editor/smoke settings fails preflight.

---

## QA Test Cases

*Written by qa-lead at story creation (QL-STORY-READY GAPS refinement applied — AC2 restated as an observable skip-with-reason). Config/Data → advisory smoke-check form.*

- **AC1**: PR workflow validates build and test results
  - Given: the pull-request workflow definition
  - When: the workflow is inspected for its trigger and steps
  - Then: the workflow triggers on pull requests and defines at least three steps — the backend build, the C# unit test run and the content validator — and reports a status check on the head commit
  - Edge cases: a workflow triggered only on the default branch, with no pull-request trigger, is a failure; a test step marked `continue-on-error` is a failure; the pipeline must be observably red when a deliberately failing test is added
- **AC2**: Unity compilation job exists, disabled with a stated reason
  - Given: the pull-request workflow definition and story 010's status
  - When: the workflow's Unity job is inspected
  - Then: a job invoking the Unity compilation smoke command (`project.yaml` `commands.smoke`) exists, is disabled, and its skip reason names ADR-0022 (Proposed); the skip is removed automatically once story 010 closes
  - Edge cases: a workflow with no Unity job at all is a failure — the criterion requires the job to exist and be skipped; a job that runs unconditionally against a non-existent editor is a failure; a skip with no stated reason is a failure
- **AC3**: No implicit release deploy
  - Given: the pull-request workflow definition
  - When: every step and job is inspected for publish, deploy, push-to-registry or release actions
  - Then: none is present, and any deployment lives in a separate workflow triggered explicitly
  - Edge cases: a deploy step guarded only by a branch condition inside the PR workflow is a failure; a release action in a reusable workflow invoked by the PR workflow is a failure

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- Config/Data: smoke check pass (`production/qa/smoke-*.md`)
- Status: [x] Local config smoke and independent code/QA review passed; actual PR CI evidence remains pending.

See `production/qa/story005-local-verification-2026-11-11.md`. Local negative build/unit/content controls and corrected passes do not substitute for actual remote PR checks; this story remains In Progress.

---

## Dependencies

- Depends on: `production/epics/ep-core/story-002-backend-solution.md`
- Depends on: `production/epics/ep-core/story-003-traceability-templates-guardrails.md` (existing traceability guard)
- Integration dependency: `production/epics/ep-core/story-009-content-schema-validation.md` (reviewed/passing local CLI;005 and009 share pending actual remote CI closure evidence, so009 is not marked Complete to start local workflow preparation)
- Unlocks: None directly (story 010 closes the Unity job's skip)
