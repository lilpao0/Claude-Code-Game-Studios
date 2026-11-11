# ADR-0018: DURABLE TICK & SHARED DEMAND

## Status

`Accepted` — approved 12/10/2026 as an **architectural principle**
(see approval record below).

> **MIGRATED from .docx — provenance note**: converted 2026-11-01 from
> `docs/Curio District — Project Documentation/04 — Architecture Decision Register`
> (ADR-018). ADR-015..021 group the OD-01..10 / EC-01..08 principles to
> avoid one ADR per tunable parameter.

## Date

12/10/2026

## Decision

The District demand budget is finite; tick allocations and the random seed
are persisted under a unique tick ID/version; jobs are idempotent; only
completed sales post wallet revenue; no duplicate budget from replay.

**Maps: OD-05, EC-01/03/05/06.**

## GDD Requirements Addressed

> Durable-tick & shared-demand principle. Trace to doc 10 (Economy Worker)
> + doc 11 (Economy Design) + doc 26 SRS. TR-ID mapping pending
> `/architecture-review` (Step 3a).

---
*Register ADR-018; decision + traceability migrated, full Context/alternatives
not in the register.*
