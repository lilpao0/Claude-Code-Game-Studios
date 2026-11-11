# ADR-0008: Server-authoritative Order + Stock Reservation + Ledger

## Status

`Accepted`

> **MIGRATED from .docx — provenance note**: converted 2026-11-01 from
> `docs/Curio District — Project Documentation/04 — Architecture Decision Register`
> (ADR-008). The register carries a **one-line decision** for ADR-001..013;
> the full Context/Decision/Consequences body lives only in the .docx.

## Date

12/10/2026 (approval record; register v1.0)

## Decision (verbatim, one line)

> **Server-authoritative Order + Stock Reservation + Ledger** — order
> handling, stock reservation, and ledger posting are all server-authoritative.

## Notes

- Shared consequence (doc 04): an order must complete at most once, including
  on request retry and worker replay — idempotent outcomes (see ADR-015,
  ADR-021).
- The ledger is the audit source; a balance alone is never sufficient
  provenance (doc 04 "Architecture consequences" block).
- See doc 09 (Customer, Orders, Inventory & Ledger detailed design) and doc 14
  (checkout sequences).

## GDD Requirements Addressed

> Foundational — transaction authority decision. Trace to doc 04 register +
> doc 09 + doc 14 + doc 26 SRS. TR-ID mapping pending
> `/architecture-review` (Step 3a).

---
*Register-source line for ADR-008; full body NOT in the register — see `.docx`.*
