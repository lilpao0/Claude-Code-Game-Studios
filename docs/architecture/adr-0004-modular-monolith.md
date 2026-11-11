# ADR-0004: Modular Monolith; Presentation–Domain–Data; Use Cases trong Domain tách Entities

## Status

`Accepted`

> **MIGRATED from .docx — provenance note**: converted 2026-11-01 from
> `docs/Curio District — Project Documentation/04 — Architecture Decision Register`
> (ADR-004). The register carries a **one-line decision** for ADR-001..013;
> the full Context/Decision/Consequences body lives only in the .docx.

## Date

12/10/2026 (approval record; register v1.0)

## Decision (verbatim, one line)

> **Modular Monolith; Presentation–Domain–Data; Use Cases trong Domain tách
> Entities** — the backend is a modular monolith; layered
> Presentation → Domain ← Data; domain Use Cases are separated from Entities
> (no behaviour mixed into entities).

## Notes

- Registers the "Architecture consequences" shared block from doc 04:
  Clean Architecture 3-layer, composition root at the API/host, Use Cases in
  `Domain/UseCases` without entity behaviour.
- See doc 06 HLD (System Architecture & Boundaries) and doc 18 (backend
  module & integration SDD) for the full boundary detail.

## GDD Requirements Addressed

> Foundational — backend structure decision. Trace to doc 04 register + doc
> 06 HLD + doc 18. TR-ID mapping pending `/architecture-review` (Step 3a).

---
*Register-source line for ADR-004; full body NOT in the register — see `.docx`.*
