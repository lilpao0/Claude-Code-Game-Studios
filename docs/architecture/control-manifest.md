# Control Manifest (approved)

> **Engine**: Unity 6.3 LTS (client, C# / URP) + ASP.NET Core / PostgreSQL (server, per ADR-0003 Notes)
> **Last Updated**: 2026-11-04
> **Manifest Version**: 2026-11-04 (revision 4)
> **ADRs Covered**: ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009, ADR-0010, ADR-0011, ADR-0012, ADR-0013, ADR-0014, ADR-0015, ADR-0016, ADR-0017, ADR-0018, ADR-0019, ADR-0020, ADR-0021
> **ADRs Left Out**: ADR-0022 (Proposed — pixel-art / 2.5D art direction; not Approved, so it contributes no rule)
> **Status**: APPROVED — TD-MANIFEST gate verdict **APPROVE**, 2026-11-04.
> Revision 3 was CONCERNS (2 blocking, 1 non-blocking); revision 4 applied the
> fixes — the server-authority family was relocated out of the client-scoped
> Core layer, the physics citations were corrected to the APIs the cited file
> actually contains, and the FR-COL-007 no-infinite-arbitrage clause was added.
> The gate confirmed all five validation criteria PASS with no blocking items.
> Two non-blocking additions were applied at approval time (ADR-0006's
> one-account-one-shop rule as a Server-layer pattern; the UI Toolkit package-id
> citation). Stories embedding a manifest version must use **2026-11-04**; any
> other version string is stale.

This manifest is a programmer's quick reference taken from the Accepted ADRs, the
technical preferences and the engine reference docs. For the reason behind a rule,
read its ADR. Every rule carries its source; no rule is invented.

**Layer map for this project.** The four client layers below (Foundation, Core,
Feature, Presentation) describe the **Unity client**. The **Server / Backend
Layer** section describes the separate **ASP.NET Core** backend (ADR-0003 Notes:
server is ASP.NET Core / PostgreSQL; client is Unity 6.3 LTS, C#). A rule is
placed under the layer of the system it governs; rules that constrain a *runtime
boundary* between the two are marked with a scope note.

---

## Foundation Layer Rules

*Applies to (Unity client): scene management, asset loading, engine start-up*
*No Accepted ADR mandates an event bus or a client save format — persistence is
server-side (ADR-0018 tick persistence, ADR-0021 snapshot), so no rule is
stated here for either.*

### Required Patterns
- Use the **Input System package** (`InputAction`, `Keyboard.current`, `Mouse.current`) instead of the legacy `Input` class — source: `docs/engine-reference/unity/current-best-practices.md`
- Load assets through **Addressables** (`Addressables.LoadAssetAsync` / `InstantiateAsync`), releasing handles on destroy — source: `docs/engine-reference/unity/current-best-practices.md`
- Load scenes with **`SceneManager.LoadScene`** — source: `docs/engine-reference/unity/deprecated-apis.md`

### Forbidden Approaches
- **Never use the legacy `Input` class** (`Input.GetKey`, `GetKeyDown`, `GetMouseButton`, `GetAxis`, `mousePosition`) — deprecated in favour of the Input System — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never call `Resources.Load()` or load assets synchronously** — deprecated in favour of Addressables — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never call `Application.LoadLevel()`** — deprecated in favour of `SceneManager` — source: `docs/engine-reference/unity/deprecated-apis.md`

## Core Layer Rules

*Applies to (Unity client): core gameplay loop, main player systems, physics, collision*
*This layer holds **client-side mechanics** rules only. Every rule that decides
economic truth lives in Server / Backend Layer Rules; the rules that bind the
client to the server sit in Global Rules → Cross-Cutting Constraints, because
they bind both sides.*

### Required Patterns
- Use the **non-allocating raycast variant** for the physics API in use — source: `docs/engine-reference/unity/deprecated-apis.md` § Physics (the 3D form `Physics.RaycastNonAlloc()`); the 2D analogue `Physics2D.RaycastNonAlloc()` is the same rule applied to the 2D API, and is **not** listed in the cited file
- Apply forces with **`AddForce()`** rather than writing velocity directly — source: `docs/engine-reference/unity/deprecated-apis.md` § Physics (the 3D form `Rigidbody.AddForce()`); the 2D analogue `Rigidbody2D.AddForce()` is the same rule applied to the 2D API, and is **not** listed in the cited file
- **Which physics API applies is not yet fixed.** `project.yaml` records `physics: "PhysX"` (the 3D physics backend); the GDD describes a 2.5D sprite presentation and states that no free rigid-body physics is required at every step; the camera model (top-down vs isometric-lite) is an unapproved ADR-0022 choice (PA-01). Until the camera model is approved, treat both API families as in scope and pick per subsystem — **do not** assert 2D primacy, and do not treat the 3D-first wording of the cited source as a project decision.

### Forbidden Approaches
- **Never use the allocating `*RaycastAll()`** — `Physics.RaycastAll()` is the cited 3D form; `Physics2D.RaycastAll()` is the same rule applied to the 2D API — use the `NonAlloc` variant — source: `docs/engine-reference/unity/deprecated-apis.md` § Physics
- **Never write velocity directly** — `Rigidbody.velocity` (3D) is the cited form; `Rigidbody2D.velocity` (2D) is the same rule applied to the 2D API — apply forces with `AddForce` — source: `docs/engine-reference/unity/deprecated-apis.md` § Physics

### Client/Server Boundary
- The client **renders** server state and never decides it — all server-authority rules (economy, identity, order flow, staff, offline report, ledger) are in **Server / Backend Layer Rules**, where their ADRs are cited. The rules that bind the client to the server are in Global Rules → Cross-Cutting Constraints. No rule in this layer decides economic truth.

## Feature Layer Rules

*Applies to: secondary mechanics, AI systems, secondary features*
*These are the game-feature rules. Their enforcement is server-side — see
Server / Backend Layer Rules for the runtime rules that implement them.*

### Required Patterns
- Give a District **shared AI demand**, and let the game **auto-partition** it — source: ADR-0007
- Keep the District **demand budget finite** — source: ADR-0007, ADR-0018 (maps OD-05, EC-01/03/05/06)
- Persist **tick allocations and the random seed under a unique tick ID/version**; make jobs **idempotent**; let **only completed sales post wallet revenue** — source: ADR-0018 (maps OD-05, EC-01/03/05/06)

### Forbidden Approaches
- **Never allow free transfer between districts** — out of scope — source: ADR-0007

*No ADR-sourced performance guardrails exist for this layer — no Accepted ADR
carries a Performance Implications section. The budgets live in Global Rules →
Performance Budgets.*

## Presentation Layer Rules

*Applies to: rendering, audio, UI, VFX, shaders, animations*

### Required Patterns
- Render with **URP (Universal Render Pipeline)** — source: `engine.rendering` in project.yaml; `docs/engine-reference/unity/current-best-practices.md`
- Build runtime UI with **UI Toolkit** (`UIDocument`, UXML + USS) — source: `docs/engine-reference/unity/current-best-practices.md`
- Write custom URP passes with the **RenderGraph API** (`RecordRenderGraph`) — source: `docs/engine-reference/unity/current-best-practices.md`
- Animate through the **Animator Controller / `Animator.Play()`** state machine — source: `docs/engine-reference/unity/deprecated-apis.md`
- Render repeated objects with **GPU instancing** (`Graphics.RenderMeshInstanced`) — *scope note: the cited API is a 3D mesh API; 2D sprite batching is not covered by this source* — source: `docs/engine-reference/unity/current-best-practices.md`
- Label UI text with **TextMeshPro or a UI Toolkit `Label`**, not the legacy `Text` component — source: `docs/engine-reference/unity/deprecated-apis.md`

### Forbidden Approaches
- **Never use UGUI `Canvas` for new runtime UI** — UGUI still works, but UI Toolkit is the production-ready path for new work — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never use the legacy `Text` component** — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never use the UGUI `Image` component** — use a UI Toolkit `VisualElement` with a background — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never use the Legacy Particle System** — use the Visual Effect Graph — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never use `OnPreRender()` / `OnPostRender()` / `Camera.SetReplacementShader()`** — not supported in SRP — source: `docs/engine-reference/unity/deprecated-apis.md`
- **Never use `CommandBuffer.DrawMesh()`** — use the RenderGraph API — source: `docs/engine-reference/unity/deprecated-apis.md`

## Server / Backend Layer Rules

*Applies to (ASP.NET Core backend, per ADR-0003 Notes and ADR-0004): backend modules, domain services, data access, API host*
*This layer holds the server-authority family: every rule that decides economic
truth lives here, not in the client layers.*

### Required Patterns
- Build the backend as a **modular monolith**, layered **Presentation → Domain ← Data** — source: ADR-0004
- Keep **Use Cases in `Domain/UseCases`, separated from Entities** — no entity behaviour in entities — source: ADR-0004
- Place the **composition root at the API/host** — source: ADR-0004
- Implement the economy **server-authoritative and tick-driven** on the backend; model District demand per-District rather than as live server-side NPC agents — source: ADR-0003
- Treat the **server snapshot as the source for reconnect**; treat **event/outbox notifications as advisory** — source: ADR-0021 (maps OD-03/04/05/08)
- Guard retries with an **idempotent request hash plus domain-transaction source keys** — source: ADR-0021 (maps OD-03/04/05/08)
- Keep every tunable (timeouts, market weights, prices, tick duration, District cap, rare probabilities, economy balance, API p95, RPO/RTO, load targets) **versioned and configured**, then validated by benchmarks and playtests — source: ADR-0020 (maps OD-10, EC-03/05/06)
- Make **order handling, stock reservation and ledger posting server-authoritative** — source: ADR-0008
- Model the order flow as: market-generated `PurchaseIntent` → accepted `Order` → **stock reservation at validated order acceptance / product pick** → **snapshotted server price** → a **single `CompleteOrder` use case** → **atomic inventory + ledger + receipt** → **immutable idempotent outcome** — source: ADR-0015 (maps OD-01/02/03, EC-04)
- Use **conditional stock reservation** with an explicit **`on_hand` / `reserved` invariant** — source: ADR-0016 (maps OD-04/09)
- Use a **single order-level claim with a lease/fencing token** — source: ADR-0016 (maps OD-04/09)
- Represent **carried stock as in-transit store inventory with a recovery lease** — source: ADR-0016 (maps OD-04/09)
- Model inventory as **SKU + stackable + unique item + storage location** (stackable sealed SKUs vs unique opened collectibles with per-instance IDs) — source: ADR-0009
- Use an **integer Shop Coin** and a **balanced, auditable ledger including system counterpart accounts** — source: ADR-0019 (maps OD-07, EC-02/03/04/05/07/08)
- Treat **NPC suppliers and staff costs as sinks** — source: ADR-0019 (maps OD-07, EC-02/03/04/05/07/08)
- Keep **pricing and segmented-demand effects bounded and versioned**; resale of a unique collectible to an NPC (**if supported**) must obey a **real, bounded buyer budget** — source: ADR-0019 (maps OD-07, EC-02/03/04/05/07/08)
- Map **one Steam account to one primary shop** in the MVP; keep the internal `PlayerId` distinct from `SteamID` — source: ADR-0006
- Validate the Steam authentication ticket **server-side** — source: ADR-0006
- Have staff **respect roles, available inventory and budget**, and pause safely when exhausted — source: ADR-0010, ADR-0017 (maps OD-06/08, EC-02/08)
- On reconnect, **reload the server snapshot and reconcile pending actions** — source: ADR-0017 (maps OD-06/08, EC-02/08)
- Derive the offline report from **ledger/orders** — source: ADR-0017 (maps OD-06/08, EC-02/08)

### Forbidden Approaches
- **Never let the client decide balances, items, pack outcomes or transactions** — the server is the sole source of truth (see Cross-Cutting Constraints) — source: ADR-0013, ADR-0008
- **Never run individual NPCs as live server agents** — demand is modelled per-District, not as thousands of live shopper entities — source: ADR-0003
- **Never allocate the District demand budget twice for the same tick** — tick allocations and the random seed are persisted under a unique tick ID/version, so a replay must not duplicate budget — source: ADR-0018 (maps OD-05, EC-01/03/05/06)
- **Never hardcode a fixed numeric constant for a tunable** — no invented fixed number is approved — source: ADR-0020 (maps OD-10, EC-03/05/06)
- **Never treat Market Tick interval, District cap, premium cosmetics or rarity rates as approved architectural constants** — they are config/balance — source: ADR-0020 (maps OD-10, EC-03/05/06)
- **Never let worker or order recovery double-post** — source: ADR-0021 (maps OD-03/04/05/08)
- **Never let the offline report mint income** — it is derived from ledger/orders — source: ADR-0017 (maps OD-06/08, EC-02/08)
- **Never satisfy the same order from both a manual action and a staff action** — the single order-level claim prevents double-fulfilment — source: ADR-0016 (maps OD-04/09)

### Invariants & Guardrails
*Correctness invariants, not performance budgets. All performance budgets live in
Global Rules → Performance Budgets.*
- **Order fulfilment**: an order completes **at most once**, even under request retry and worker replay — source: ADR-0015 (maps OD-01/02/03, EC-04); see also ADR-0021
- **Ledger**: the ledger is the audit source; **a balance alone is never sufficient provenance** — source: ADR-0008
- **No infinite arbitrage**: NPC resale, when enabled, must obey both a bounded buyer budget **and** an absence of unbounded price cycling between buy and sell routes — source: ADR-0019 (bounded, versioned pricing / demand effects), SRS FR-COL-007

## Global Rules (All Layers)

### Naming Conventions
| Element | Convention | Example | Source |
|---------|-----------|---------|--------|
| Classes | PascalCase | `PlayerController` | `naming.classes` in project.yaml |
| Variables | **CONFLICT — UNRESOLVED, confirm before relying on it.** `project.yaml` says `camelCase` for variables; `technical-preferences.md` says public fields/properties are PascalCase and private fields `_camelCase`. These disagree on public fields. Both sources are recorded; neither is silently preferred. Locals and private fields are not in conflict: camelCase locals, `_camelCase` private fields. **Resolution note:** `.claude/docs/config-resolution.md` puts `project.yaml` at step 2 and `technical-preferences.md` at the legacy fallback, so config resolution selects `camelCase` — the conflict is recorded here, but the resolution chain already answers it. | `moveSpeed` (local), `_moveSpeed` (private), `MoveSpeed` (public — *per technical-preferences.md only; project.yaml would say `moveSpeed`*) | `naming.variables` in project.yaml (camelCase); `.claude/docs/technical-preferences.md` (public PascalCase, private `_camelCase`) |
| Signals/Events | PascalCase `On<Event>` | `OnStockChanged` | `naming.signals` in project.yaml |
| Files | PascalCase | `PlayerController.cs` | `naming.files` in project.yaml |
| Constants | **UNVERIFIED — no convention is confirmed.** `project.yaml` records the value `SCREAMING_SNAKE` (flagged UNVERIFIED in the file itself: "confirm before relying on it"). `technical-preferences.md` allows PascalCase *or* `UPPER_SNAKE_CASE`. The value `SCREAMING_SNAKE` denotes the `UPPER_SNAKE_CASE` option, so the two sources do not conflict — the only open question is whether the convention is confirmed at all. | `MAX_STOCK` | `naming.constants` in project.yaml (flagged UNVERIFIED in the file itself); `.claude/docs/technical-preferences.md` |

### Performance Budgets
| Target | Value | Source |
|--------|-------|--------|
| Framerate | [TO BE CONFIGURED — doc 22 QA-007 benchmarks; revisit via /settings] | `performance.target_framerate` absent from project.yaml; `.claude/docs/technical-preferences.md` |
| Frame budget | [TO BE CONFIGURED] | `performance.frame_budget_ms` absent from project.yaml; `.claude/docs/technical-preferences.md` |
| Draw calls | [TO BE CONFIGURED] | `performance.draw_call_limit` absent from project.yaml; `.claude/docs/technical-preferences.md` |
| Memory ceiling | [TO BE CONFIGURED] | `performance.memory_ceiling_mb` absent from project.yaml; `.claude/docs/technical-preferences.md` |
| API p95 | [TO BE CONFIGURED] | ADR-0020 (maps OD-10, EC-03/05/06) — versioned config validated by benchmarks; `performance.*` absent from project.yaml |
| RPO (recovery point objective) | [TO BE CONFIGURED] | ADR-0020 (maps OD-10, EC-03/05/06) — versioned config validated by benchmarks; `performance.*` absent from project.yaml |
| RTO (recovery time objective) | [TO BE CONFIGURED] | ADR-0020 (maps OD-10, EC-03/05/06) — versioned config validated by benchmarks; `performance.*` absent from project.yaml |
| Load target | [TO BE CONFIGURED] | ADR-0020 (maps OD-10, EC-03/05/06) — versioned config validated by benchmarks; `performance.*` absent from project.yaml |

No numeric budget is stated here: ADR-0020 approves no invented fixed number, and every budget is versioned config validated by benchmarks and playtests.

### Approved Libraries / Addons
These are the third-party dependencies the Required Patterns above depend on:
- **Input System** (`com.unity.inputsystem`) — input handling — source: `docs/engine-reference/unity/deprecated-apis.md` (package id); `docs/engine-reference/unity/current-best-practices.md` (pattern)
- **Addressables** (`com.unity.addressables`) — asset loading — source: `docs/engine-reference/unity/plugins/addressables.md` (package id); `docs/engine-reference/unity/current-best-practices.md` (pattern)
- **UI Toolkit** (`com.unity.ui`) — runtime UI — source: `docs/engine-reference/unity/PLUGINS.md` § UI Toolkit (package id); `docs/engine-reference/unity/current-best-practices.md` (pattern)
- **TextMeshPro** — text rendering (UI Toolkit `Label` alternative) — source: `docs/engine-reference/unity/deprecated-apis.md`
- **URP** (`com.unity.render-pipelines.universal`) — rendering pipeline — source: `engine.rendering` in project.yaml; `docs/engine-reference/unity/modules/rendering.md` (package id); `docs/engine-reference/unity/current-best-practices.md` (pattern)

*Maintenance item (synchronized 2026-11-04, re-synced at revision 4, confirmed at approval 2026-11-04):* `.claude/docs/technical-preferences.md` § "Allowed Libraries / Addons" previously read "[None configured yet]" and now lists the five packages above; its "Forbidden Patterns" and "Architecture Decisions Log" sections — which also read "[None configured yet]" / "[No ADRs yet]" — have been populated from this manifest at the same time. At revision 4 the mirrored physics row was corrected to the same wording as this manifest's Core layer (the cited 3D form, plus the 2D analogue labelled as such). The TD-MANIFEST gate verified the two lists match item-for-item (13 forbidden rows each, 5 allowed libraries each) and the mirrored header records "Mirrored as of manifest revision 4". This manifest is the authority for all three lists; `technical-preferences.md` is the human-readable legacy fallback and is not auto-migrated, so the two must be kept in sync by hand. The mirror carries the **forbidden** rows only — the positive Required Patterns (e.g. `SceneManager.LoadScene`, releasing Addressables handles on destroy) live here and are not duplicated there by design.

### Forbidden APIs (Unity 6.3 LTS)
Scoped to what this project builds: a 2.5D Unity 6.3 LTS client on URP with UI Toolkit, the Input System and Addressables, plus a separate ASP.NET Core backend. Full table: `docs/engine-reference/unity/deprecated-apis.md`.
- `Input.*` (GetKey / GetKeyDown / GetMouseButton / GetAxis / mousePosition) — legacy Input
- `Canvas` (UGUI) for new runtime UI
- `Text` component
- `Image` component (UGUI)
- `Resources.Load()` / synchronous asset loading
- `Application.LoadLevel()`
- `Physics.RaycastAll()` (cited form) / `Physics2D.RaycastAll()` (2D analogue) — allocating
- `Rigidbody.velocity` (cited form) / `Rigidbody2D.velocity` (2D analogue) — direct write
- Legacy Animation component / `Animation.Play()`
- Legacy Particle System
- `OnPreRender()` / `OnPostRender()` / `Camera.SetReplacementShader()`
- `CommandBuffer.DrawMesh()`
- `WWW` class
- Source: `docs/engine-reference/unity/deprecated-apis.md`

**Deliberately excluded** (the project does not use them): the DOTS/Entities rows (`ComponentSystem`, `JobComponentSystem`, `GameObjectEntity`, `EntityManager.CreateEntity()` old signature, `ComponentDataFromEntity<T>`), the Netcode for GameObjects row, and the WebGL rows.

### Not Approved / Draft
Per ADR-0021's Status rule (doc 04), the following **remain draft or validation candidates** and must not be treated as fixed by this manifest: route names, DDL detail, numeric tuning, and the exact retry-timeout implementation — source: ADR-0021 (maps OD-03/04/05/08)

From ADR-0022 (**Proposed**, so it contributes no rule above), these technical choices are **explicitly not approved** and must not be treated as settled: camera model (PA-01, top-down vs isometric-lite), grid scheme and scale (PA-02, 32x32 tile trial vs 64x64 cells), and the rendering / import / PPU choices (PA-03 to PA-07 — consistent WORLD PPU, Point filter, integer scaling, sorting). Stories referencing ADR-0022 stay Blocked until it is Accepted — source: ADR-0022 (via `docs/architecture/adr-requirement-map.md` §3)

### Cross-Cutting Constraints
*These bind both the client and the server, so they are stated here rather than
in either client layer or the server layer.*
- The server is the **sole source of truth** for balances, items, pack outcomes and transactions — source: ADR-0013, ADR-0008
- **Never generate goods or money on the client** — the server owns the truth — source: ADR-0010
- **All player-value exchange routes through NPC suppliers / market demand** — source: ADR-0013
- **No direct money or item trading between players** in the MVP — source: ADR-0013
- **No premium token and no in-game purchases** in the MVP — source: ADR-0012
- **No real-time co-op** in the MVP — source: ADR-0002
- **Ship on Steam first**; the economy requires a connected, server-backed backend — source: ADR-0001
- Target **Windows PC on Steam** as the first platform — source: ADR-0011, ADR-0014 (M2 = Steam Windows buy-to-play)
- **Non-Windows platforms are deferred scope** — do not target them in the MVP; bringing them back requires a new ADR — source: ADR-0011, ADR-0014
- **Prototype one complete shop first, then a small District** — M0 technical spike → M1 one-shop vertical slice → M2 Steam MVP — source: ADR-0005, ADR-0014
- The **local prototype is not a commercial offline mode** — it is a test bed — source: ADR-0014
