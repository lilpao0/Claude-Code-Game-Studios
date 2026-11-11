# ADR-0020: CONFIGURATION, BENCHMARK & RELEASE GATES

## Status

`Accepted` — approved 12/10/2026 as an **architectural principle**
(see approval record below).

> **MIGRATED from .docx — provenance note**: converted 2026-11-01 from
> `docs/Curio District — Project Documentation/04 — Architecture Decision Register`
> (ADR-020). ADR-015..021 group the OD-01..10 / EC-01..08 principles to
> avoid one ADR per tunable parameter.

## Date

12/10/2026

## Decision

Timeouts, market weights, prices, tick duration, District cap, rare
probabilities, economy balance, API p95, RPO/RTO and load targets are all
**versioned and configured**, then validated by benchmarks and playtests.
**No invented fixed number is approved here.**

**Maps: OD-10, EC-03/05/06.**

> Related note (doc 04 "Quy trình thay đổi"): proposals such as Market Tick
> interval, District cap, premium cosmetics and rarity rates are **NOT
> tagged Approved** — they are config/balance, not architectural constants.

## GDD Requirements Addressed

> Configuration/benchmark/release-gate principle. Trace to doc 20 (balance
> sim) + doc 22 (QA Master Plan) + doc 23 (release) + doc 26 SRS. TR-ID
> mapping pending `/architecture-review` (Step 3a).

---
*Register ADR-020; decision + traceability migrated, full Context/alternatives
not in the register.*
