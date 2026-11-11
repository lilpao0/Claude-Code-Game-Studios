# Module Boundaries — Curio District backend

> **Governing ADRs**: ADR-0004 (modular monolith; Presentation → Domain ← Data),
> ADR-0008 (server order/stock/ledger — no client authority), doc 06 §3
> (logical module boundaries), doc 18 (backend module & integration SDD).
> **Traceable to**: `docs/architecture/control-manifest.md` (Server layer).
> **Checked by**: `backend/tests/unit/fnd/ModuleBoundaryTests.cs`
> (the automated edge/lint check — story 002 AC4). An edge is only "allowed" if
> it is in the allowed table **and** has a manifest citation below.

This is the single source of truth for which project may reference which, and
which module owns which database tables. The automated check fails the build
(if a forbidden edge appears) because it runs inside `dotnet test`, which CI
runs on every pipeline.

## 1. Project reference edges

The backend has four host/domain/data projects plus the Config leaf under `backend/`, and test projects.
The composition root is the API host (ADR-0004); the Worker is a separate host
that shares Domain. References point inward only.

| From \ To | Domain | Data | API | Worker | Config |
|-----------|:------:|:----:|:---:|:------:|:------:|
| `Domain` | — | ✗ | ✗ | ✗ | ✗ |
| `Data` | ✓ | — | ✗ | ✗ | ✓ |
| `API` | ✓ | ✓ | — | ✗ | ✓ |
| `Worker` | ✓ | ✗ | ✗ | — | ✗ |
| `Config` | ✗ | ✗ | ✗ | ✗ | — |
| `Tests` | ✓ | ✓ | ✓ | ✓ | ✓ |

### Allowed edges (each with its manifest citation)

- `Data → Domain` — the Data layer depends inward upon Domain (EF maps Domain
  entities to Postgres). Source: ADR-0004; manifest *Required: layered
  Presentation → Domain ← Data*.
- `API → Domain` — the API host binds use cases to endpoints. Source: ADR-0004.
- `API → Data` — the composition root registers the concrete Data
  implementations against the Domain ports they satisfy. Source: ADR-0004
  (*composition root at the API/host*); ADR-0008 (*server owns order/stock/
  ledger persistence*).
- `Worker → Domain` — the worker runs district-tick and offline-automation use
  cases. Source: ADR-0004; ADR-0018 (durable tick).
- `Tests → {Domain, Data, API, Worker}` — a test may reference any project it
  is verifying. Source: not a production edge; exempt from the lint.

- `Data → Config`, `API → Config` — story006 explicitly places versioned profiles in a configuration project reachable from Data/API, outside Domain. Source: ADR-0020 (versioned configured tunables); manifest Server/Backend Required versioned configuration. No host consumes a default profile implicitly.
- `Config` references only the BCL; it may not reference any other production module. Tests may reference Config to verify it.
- `Tools/ContentValidator` is a standalone BCL-only build tool with no production project references. Tests may reference it; hosts do not consume it. Source: story009 / ADR-0020 validation gate outside Unity. The automated edge check includes this leaf.

### Forbidden edges (each would be a build failure)

- `Domain→Data` — **forbidden.** Domain must not know how entities are
  persisted. Source: ADR-0004 (*Domain references nothing but the BCL*);
  manifest *Forbidden: no EF/Steam in Domain*.
- `Domain→API` — **forbidden.** Domain never references a host. Source: ADR-0004.
- `Domain→Worker` — **forbidden.** Domain never references a host. Source: ADR-0004.
- `Data→API` — **forbidden.** Data depends inward only on Domain, never
  outward to a host. Source: ADR-0004.
- `Data→Worker` — **forbidden.** Data depends inward only on Domain, never
  outward to a host. Source: ADR-0004.
- `API→Worker` — **forbidden.** API and Worker are sibling hosts; neither
  references the other. Source: ADR-0004 (two independent composition roots).
- `Worker→API` — **forbidden.** API and Worker are sibling hosts; neither
  references the other. Source: ADR-0004 (two independent composition roots).
- `Worker→Data` — **forbidden.** Worker talks to Domain through ports; Data
  implementations are composed into Worker's own host, not referenced as a
  project. Source: ADR-0004; ADR-0008.

The automated check reads every `ProjectReference` out of the source
`.csproj` files and fails on any edge not in the allowed table.

- `Domain→Config` — **forbidden.** Domain must not depend on the config format (story006, ADR-0004 BCL-only core).
- `Worker→Config` — **forbidden.** No new Worker edge is authorized by story006.
- `Config→Domain` — **forbidden.** Config is a BCL-only leaf.
- `Config→Data` — **forbidden.** Config is a BCL-only leaf.
- `Config→API` — **forbidden.** Config is a BCL-only leaf.
- `Config→Worker` — **forbidden.** Config is a BCL-only leaf; the automated edge check rejects all its project references.

## 2. Domain purity (no infrastructure)

Domain is the BCL-only core. The checker additionally scans
`backend/Domain/**.cs` for `using` directives that import infrastructure, and
for these package/project names in `CurioDistrict.Domain.csproj`:

- `Microsoft.EntityFrameworkCore` (or any `*.EntityFrameworkCore*`)
- `Microsoft.Extensions.*` (hosting / DI / logging — no infrastructure host)
- `Npgsql`, `Microsoft.Data.Sqlite`
- `Steamworks`, any `Steam*` SDK

A hit in either the `.csproj` reference list or a source `using` is a failure.
Source: ADR-0004 (*Domain references nothing but the BCL*); manifest
*Forbidden: no EF or Steam dependency in Domain*.

## 3. Table ownership

From doc 06 §3 "Authoritative ownership" (quoted in
`docs/architecture/architecture.md`). Each module owns the tables of its domain;
a write to a table happens **only** in that module's repository. No other
module writes a foreign module's tables — cross-module effects go through an
explicit use case and (eventually) transaction coordination, never a stray
`INSERT`/`UPDATE`/`DELETE` into another module's tables.

| Module | Owns (tables) | Repository (sanctioned write path) |
|--------|---------------|--------------------------------------|
| Identity | `players`, `shops`, `steam_accounts` | `Data/Repositories/Identity/` |
| PlayerStores | `shop_state`, `shop_layout_slots` | `Data/Repositories/PlayerStores/` |
| Catalog | `catalog_items`, `catalog_packs` | `Data/Repositories/Catalog/` |
| Inventory | `stock_movements`, `stock_locations` | `Data/Repositories/Inventory/` |
| Orders | `orders`, `order_events` | `Data/Repositories/Orders/` |
| Ledger | `ledger_entries`, `balance` | `Data/Repositories/Ledger/` |
| Collectibles | `collectibles`, `collectible_results` | `Data/Repositories/Collectibles/` |
| Market | `demand_pools`, `market_weights` | `Data/Repositories/Market/` |
| Customers | `npc_customers` | `Data/Repositories/Customers/` |
| Automation | `staff_rules` | `Data/Repositories/Automation/` |
| Layout | `grid_tiles` | `Data/Repositories/Layout/` |

**The sanctioned write path:** a `StockMovement` is written by the Inventory
repository, a `LedgerEntry` by the Ledger repository, an `Order` by the Orders
repository. A checkout is a cross-module use case (Orders + Inventory +
Ledger) — see ADR-0008 / ADR-0015 (order-fulfilment reservation) — and it
coordinates those writes through the use case, **not** by calling three
repositories' `Save` from a UI handler or by a module reaching into a foreign
table.

The automated check reads the table names that appear in
`backend/Data/Repositories/**.cs` and fails any that fall outside the owning
module's declared table set. Until the repositories are written (this story
leaves `Data/` as an empty shell with the folder split), this check passes
vacuously and is wired to fire the moment a repository lands — it is the epic
Definition-of-Done item "module-boundary check is an automated test".

## 4. How to run the check locally

The check is a test project, so it is the same command CI runs:

```bash
cd backend
dotnet test tests/unit/CurioDistrict.Tests.Unit.csproj --filter "FullyQualifiedName~ModuleBoundary"
```

A new forbidden edge, an infrastructure `using` in Domain, or an
out-of-module table write turns this red and names the offending edge.
