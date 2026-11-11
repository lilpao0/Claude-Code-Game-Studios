# Sprint003 — Content-set validation and PR checks

**Start:** 2026-11-11 Asia/Saigon. **End/capacity/effort days/buffer duration/normalized velocity:** unknown. **Modes:** full workflow/review/QA, autonomous, studio. **PR-SPRINT:** CONCERNS; schedule feasibility NOT ASSESSED (missing capacity/duration/velocity). Independent producer `/root/readiness005` reviewed before allocation. Existing authorization covers local implementation; external closure dependency retained, no scope/quality waiver.

## Goal

Deliver a locally runnable content-set CLI that reports all pool/ID/reference violations with deterministic grouped diagnostics, backed by actual unit/process red-green evidence, plus existing005 PR workflow. Keep005,009 and this sprint open until the required actual PR/CI evidence exists. This is approved EP-CORE groundwork for later milestone work, not M0 or a playable M1 claim.

## Prerequisites and scope

| Needed | State |
|---|---|
| Existing009 Ready, QL-STORY-READY ADEQUATE | Independent11Nov readiness confirmed |
|006 versioned seed loader and actual source profile | Complete, QA APPROVED WITH CONDITIONS,40delta integrated through guarded002receipt |
| SDK/runtime | SDK10.0.403; Docker29.8.2 restored after user crash; Unity not needed |
| Preimplementation QA plan | `production/qa/qa-plan-sprint-003-2026-11-11.md`, independently drafted/reviewed by qa-lead |
| Final AC4 CI | Pending005workflow and reviewed commit/push/PR authorization; no actual remote run claimed |

No carryover from002 (one of one complete). No risk-register directory; risks assessed from actual sprint contents. No story invented to pad the Ready backlog.

## Allocation

| Priority | Story | Estimate | Owner | Dependency |
|---|---|---|---|---|
|Must Have|EP-CORE009 / FND-010 Content schema validation build script|M; no conversion to invented days|Codex scoped recovery after Claude tool failure|006Complete;005workflow+actualremoteCI required for finalAC4|
|Must Have|EP-CORE005 / FND-005 PR build/test/content/traceability checks|M; no conversion to invented days|Unassigned; after preimplementation QA addition|002/003Complete;009reviewed localCLI;actualPR requires humanGitpermission|

**11Nov update:** independent fresh story-readiness/QL assessment and producer `/root/readiness005` support allocating existing005 now that the concrete009 CLI is reviewed and locally passing. PR-SPRINT remains CONCERNS; schedule feasibility NOT ASSESSED. QA `/root/review006_qa` approved the preimplementation005 plan addition before any005 code. Its dependency documentation now names003 and the009 integration interface.009 final remote AC is not waived or marked Complete;005/009 close only after shared actual CI evidence. Story010/FND003 Unity remains blocked on Proposed ADR0022, with its skip visible and automatic eligibility required when it closes. No Unity command placeholder or Proposed ADR decision changes are authorized by this update.

## Producer concerns resolved as implementation constraints

- Global ID namespace follows the explicit009AC3 QA criterion, including cross-type/cross-file duplicates; “per content type” in ancillary notes does not narrow that criterion. Record both declaration paths. Repeated reference IDs are not duplicate declarations.
- Document the tool content format and item-to-item references explicitly. The006serializer ignores unknown fields, so blindly loading through it must not silently lose references. Source DEV_SEED data remains unchanged; synthetic/reference-extension fixtures enable validation without inventing recipe gameplay.
- Keep one writer. Claude tool-health was verified with a real native Read in latest8ad; stop and hand off exact blockers if literal-toolcall output repeats. No guard/permission bypass.
- Local implementation may proceed while remote closure is pending. No Done or sprint completion from local evidence alone; no promised completion date. Routine work remains authorized; ask only when a concrete reviewed Git/PR action is ready.

## Definition of Done

- [ ] Exact4pools, totals10000/nonnegativeweights; reference resolution and global nonempty/uniqueIDs across complete content set.
- [ ] Every violation grouped/reported deterministically; validCLIexit0, invalidnonzero; parser/input boundary explicit.
- [ ] Actual unit/process red-green, full relevant backend regressions, source provenance retained; native exits/skips disclosed.
- [ ]005workflow executes validator on actual content-changing PR; violating run red and corrected run green with links/revision evidence.
- [ ] Independent LP/language/QL gates, assessed smoke and full team-QA; no open S1/S2.
- [ ] Story-done closure, retrospective/Vietnamese sprint report, reviewed guarded integration; preserve unrelated source/staging.
- [ ] Commit/merge/push/deploy performed only with separate applicable authorization; none inferred here.
