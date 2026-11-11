# QA Plan: Sprint003 — Content schema validator

**Date:** 2026-11-11 Asia/Saigon. **State:** preimplementation, no execution/signoff claimed. **Reviewer/drafter:** `/root/review006_qa`, qa-lead, existing sprint-plan Phase5. Full QA/workflow/review; studio; autonomous. Scope existing009 Logic and005 Config/Data, estimateM each; dependency006Complete/integrated; no002carryover; capacity/end unknown.

| Story | Type | Automated required | Manual required |
|---|---|---|---|
|009 Content schema validation build script|Logic|Unit tests, actual separate CLI, relevant full backend regressions and actual remoteCI forAC4|Independent code/evidence review; no gameplay/UI playtest|
|005 PR pipeline|Config/Data|Workflow/preflight fixture checks and actual PR checks; use existing traceability guard|Independent workflow/evidence review; no Unity execution claimed while010 is blocked|

## 11 November preimplementation addition — existing005

Independent producer/readiness `/root/readiness005`: local preparation READY after documenting002/003 and009 CLI dependencies, QL-STORY-READY ADEQUATE; PR-SPRINT CONCERNS and schedule NOT ASSESSED. Independent QA `/root/review006_qa`: Phase5 addition ADEQUATE. This section is recorded before005 implementation;009 local code is verified but its remote AC4 remains open.

- Inspect `pull_request` on all target branches, no content/path filter suppressing required checks, backend build/unit/content/traceability commands, read-only permissions and no hidden release/deploy path. Follow all local/reusable workflow or action calls when checking AC3.
- Exercise nonzero build/test/content/traceability results; no `continue-on-error`, pipeline or wrapper may hide failure. Reuse the existing guard and test complete body, missing ticket/requirement, placeholders and TC-only input.
- Unity preflight must read010's authoritative header status. Current Blocked plus placeholder emits disabled eligibility and a visible ADR0022 Proposed reason. Complete plus valid configured smoke enables automatically; Complete plus blank/placeholder fails. Missing/malformed/unrecognized status fails; body text saying Complete must not override a Blocked header.
- Use a harmless fixture command to observe exact smoke invocation and nonzero propagation. A machine-local editor path does not establish runner availability; actual enabled Unity compilation requires a suitably provisioned verified runner. Do not claim that run from fixture tests.
- Actual authorized PR evidence must show a deliberate failing unit test and invalid content red, corrected head green, with URLs, event and revisions. Check repository rules before claiming failed statuses prevent merges. Local controls do not replace these required external observations.

**Required test:** `backend/tests/unit/content/content_schema_test.cs`, blocking. Sources: freshTR-fnd-009,009AC/QA, systems-index rows30/12, AcceptedADR0020, controlmanifest. Migrated index has no separate detailedAC/EdgeCases; concrete cases derive from story/source. Use actual production seed as validbaseline; preserve all values/version/provenance/DEV_ONLY. Synthetic changes are test input, not balance approval.

## Automated requirements

- AC1:4validpools/count4/total10000 pass. Reject9999/10001, negativeweight despite compensated10000, missing/fifthpool; zero accepted with validtotal. Identify pool/entry/value.
- AC2:explicit documentedSKU and item-to-item reference format; resolved/missingSKU/missingitem/simultaneousfailures/crossfile cases. Report referringID,field/file,target. Repeated references are not declarations.
- AC3:reject absent/empty/whitespace IDs; duplicate withinfile/acrossfiles/acrosstypes; global namespace per explicitQA. Report duplicatedID and both declarationpaths; uniqueIDs pass.
- AC4local:pooltotal+missingreference+duplicate in sameinput all reported, groupedkind/deterministicorder/repeatableoutput; invalid neverexit0.
- Input:malformed/unreadable controlleddiagnostic; recognizedfields documented; unknownmember test so reference-bearingfields do not disappear in permissive deserialization. Reject unsupportedstructure or explicitly retain/validate it.
- Actualprocess:invoke builtCLI valid/invalidsets, assert exits/outputidentifiers; in-process assertions alone insufficient.
- Regression/provenance:complete relevantunit/integration/moduleboundary suite, currentfixture matches acceptedhash; nativeexits/skips. Meaningfulnegativecontrol with finalsource restored.

## Smoke and externalCI

ReverifySDK, ReleasebuildCLI/solution, execute baseline/missingfifthpool/crossfilerefduplicate/multidefectCLI; retain commands/output/nativeexits/sourcehashes. Independent LP/language/QL review and existing smoke/teamQA.

AC4 requires actual005workflow/PR evidence on contentchanges:retain remoteURLs,revision,trigger,command/result with violatingred/correctedgreen.005 is now allocated by the reviewed update; commit/push/PR is a human boundary once concrete reviewedresultready. Localtests do notreplace remoteevidence:009 remains open while pending.

## Entry/exit

Implementation entry:this plan, readinessADEQUATE,006/sourceavailable. QAexecution:stablebuild, assessedsmoke, eligible sprintstorystatus. Done:everyACpassingexecutedevidence includingrealCI, LP/QL/storydone, teamQAapproval,noS1/S2. Report localdelivery separately ifclosurepending. No Unity/gameplay/productionbalance/newstories/stageadvance/merge/deploy claim.
