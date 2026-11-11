# Story005 local verification — 11 November 2026

**Local configuration QA: PASS; remote acceptance evidence pending.** Story005,
Story009 and Sprint003 remain open. No Git commit, push, PR, merge or deployment
has been performed. This is backend/workflow verification, not a game or Unity
compilation signoff.

## Delivered behavior

The PR workflow runs backend build/unit/integration checks, the selected content
validator, the existing003 traceability guard and Unity eligibility controls.
It has no branch/path restriction, uses PR head SHA on hosted runners, grants
read-only contents permission and disables persisted checkout credentials.
PR-body text enters the guard as stdin data. Required failures propagate.
No local/reusable release workflow, publish step or deployment is invoked.

The current010 Blocked header leaves Unity skipped with the visible
ADR-0022 Proposed reason. Closing010 automatically makes its job eligible only
with explicit configured editor/smoke settings; malformed status, placeholder
configuration or absent runner editor fails. The wrapper invokes the exact
configured command with a native timeout and Bash failure/pipe propagation.
No real Unity editor was run or provisioned, and `project.yaml` Unity placeholders
remain for010 to resolve within its approved scope.

## Executed evidence

| Check | Result | Retained log |
|---|---|---|
| Initial config/preflight/event controls |12passed, native0 | `story005-initial-config-smoke-2026-11-11.log` |
| Blank-status regression before repair | Expected failure1, observed erroneous enable0; regression suite native1 | `story005-status-regression-red-2026-11-11.log` |
| Repaired controls |13passed, native0; blank header cannot consume Complete from the next line; injection marker remains absent | `story005-status-regression-green-2026-11-11.log` |
| Actionlint1.7.12 | Native0; official release archive verified against published checksum | `story005-actionlint-2026-11-11.log` |
| Actual current Unity preflight | Disabled with FND-003/ADR-0022 reason, native0 | `story005-current-unity-preflight-2026-11-11.log` |
| Real build negative control | Temporary compiler error rejected, native1 | `story005-native-build-test-controls-2026-11-11.log` |
| Real unit negative control | Compiling deliberate failing unit test rejected, native1 | Same native controls log |
| Restored final build/test | Release0warnings/errors;133unit and32integration passed, one standalone live-DB skip; native0 | Same native controls log |
| Publication package without build caches | Release build,133unit,32integration plus one live-DB skip,13config controls and blocked Unity preflight passed/native0 | `story005-pr-package-verification-2026-11-11.log` |
| Actual content failure/correction | Invalid synthetic content native1, same path corrected native0 | `story009-cli-smoke-2026-11-11.log` |

The temporary build/unit controls were removed in `finally`; their marker source
is absent from the final tree. Source content remained unchanged. These local
negative controls establish command behavior and do not claim remote CI.

## Independent review and acceptance gap

`/root/review006_qa` final local config QA: **PASS** after verifying the observed
red/green repair and marker nonexecution assertion. Config/Data formal QL coverage
is not applicable; the explicit remote acceptance evidence is still required.

`/root/review006_language` final code/DevOps review: **APPROVED** for the repaired
local implementation. Its first review missed the blank-status defect; the final
review supersedes it and checks the QA discovery and repaired source/evidence.
Formal `/root/review006_lp` local **LP-CODE-REVIEW: APPROVE** after reading the
workflow, helpers, source requirements and actual logs. Claude8ad also completed a
bounded no-tool design review, then was stopped; its unverified suggestions were
checked against actual source rather than promoted to verified findings.

| Criterion | Current assessment |
|---|---|
| AC1 build/test/content PR checks | Local definition/commands verified; actual PR statuses and red/green revisions pending |
| AC2 Unity job with visible deferral and automatic eligibility | Local definition, current preflight and fixture transitions verified; no real Unity compilation claimed |
| AC3 no implicit release deployment | Local workflow and referenced actions inspected; no deploy/release path |

Closing005/009 needs an authorized real PR showing failing unit and content
revisions red, followed by corrected green checks, with event, head revisions
and run/check URLs. Repository required-check settings remain unverified.
Prepare and review the concrete publication set before requesting Git/PR
authorization. Existing Sprint001/002 integration receipts remain unchanged;
Sprint003 is not integrated into the original checkout.
