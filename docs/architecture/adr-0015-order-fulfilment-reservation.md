# ADR-0015: ORDER FULFILMENT & RESERVATION

## Status

`Accepted` — approved 12/10/2026 as an **architectural principle**
(see approval record below).

> **MIGRATED from .docx — provenance note**: converted 2026-11-01 from
> `docs/Curio District — Project Documentation/04 — Architecture Decision Register`
> (ADR-015). ADR-015..021 group the OD-01..10 / EC-01..08 principles to
> avoid one ADR per tunable parameter.

## Date

12/10/2026

## Decision

Market-generated `PurchaseIntent`, accepted `Order`, stock reservation at
validated order acceptance / product pick, snapshotted server price, one
`CompleteOrder` use case, atomic inventory+ledger+receipt, immutable
idempotent outcome.

**Maps: OD-01/02/03, EC-04.**

## Consequences / Invariants

- An order completes **at most once**, even under request retry and
  worker replay (shared consequence, doc 04).
- Outcome is immutable and idempotent — see ADR-021 (server authoritative
  recovery) for the retry/recovery guard.

## GDD Requirements Addressed

> Order-fulfilment principle. Trace to doc 09 (Customer, Orders,
> Inventory & Ledger detailed design) + doc 14 (checkout sequences) + doc
> 26 SRS. TR-ID mapping pending `/architecture-review` (Step 3a).

---
*Register ADR-015; decision + traceability migrated, full Context/alternatives
not in the register.*
