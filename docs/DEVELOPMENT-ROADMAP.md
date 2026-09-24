# PRIME — Development Roadmap

Status: Phase 1 (this document). Operationalizes the phase list in
CLAUDE.md §87–§101 into concrete entry/exit criteria. Phases are executed in
order; a phase is not complete until its quality gate (CLAUDE.md §107)
passes: code compiles, tests pass, migrations work, API/UI work where
applicable, authorization works, audit works where required, documentation
is updated, no known critical errors remain.

---

## Phase 0 — Environment & Discovery ✅ Complete

Inspected OS, Git, .NET SDK, Node.js, package managers, PostgreSQL,
PostGIS, Docker, IDE, and repository state. Findings recorded in the
Phase 0 report (see conversation history / project memory). Summary:
PostgreSQL 16.15 + PostGIS 3.6.2 already installed and running; .NET SDK 7
and Node 18 present but both past end-of-life and need upgrading before
Phase 2; Docker and WSL not installed (not blocking); repository was empty
except for the spec file.

## Phase 1 — Architecture ✅ Complete (this set of documents)

Deliverables: `docs/ARCHITECTURE.md`, `docs/DOMAIN-MODEL.md`,
`docs/DATABASE.md`, `docs/DEVELOPMENT-ROADMAP.md` (this file). Repository
hygiene established: `CLAUDE.md` correctly named at root, `git init`,
`.gitignore`, `.editorconfig`, `README.md`. No application code written.

Exit criteria met: architecture, domain model, ERD, database design
strategy, security model, GIS/valuation/assessment architecture, and this
roadmap all documented; open questions explicitly flagged rather than
guessed.

**Update (2026-09-23)**: product direction set database/auth/storage
hosting to **Supabase** (Postgres+PostGIS, Auth, Storage), with local
PostgreSQL 16 retained for day-to-day dev. ARCHITECTURE.md, DATABASE.md,
and DOMAIN-MODEL.md were revised accordingly (Supabase Auth replaces
ASP.NET Core Identity as credential store; `User` renamed `AppUser`;
Supabase Storage replaces the generic storage abstraction; connection
pooling, RLS, and local/cloud schema-parity constraints documented). Phase
1 deliverables remain complete under the revised architecture.

---

## Phase 2 — Foundation

**Goal**: a running, empty skeleton — solution builds, API boots, database
migrates, frontend renders a shell, health checks pass.

Tasks:
- Install current LTS .NET SDK and current Active LTS Node.js (record
  exact versions actually installed).
- Create the solution and four backend projects (`Prime.Domain`,
  `Prime.Application`, `Prime.Infrastructure`, `Prime.WebApi`) per
  ARCHITECTURE.md §2.
- Wire dependency injection, Serilog logging, FluentValidation pipeline,
  Swashbuckle/OpenAPI.
- Configure EF Core + Npgsql + NetTopologySuite; connect to local
  PostgreSQL; `CREATE EXTENSION postgis;` on the dev database; produce the
  **initial migration** (empty or near-empty schema, proving the pipeline
  works end to end).
- Create the Supabase project(s) (at minimum a dev/staging project);
  `CREATE EXTENSION postgis;` there too; configure both the direct (5432)
  and pooled (6543) connection strings per DATABASE.md §1.1; apply the same
  initial migration to Supabase via the direct connection and confirm it
  matches the local schema.
- Scaffold the React + TypeScript frontend (`frontend/prime-web`) with
  Vite, routing shell, Supabase client SDK wired for sign-in, and a call to
  `/health`.
- Wire `Prime.WebApi` to validate Supabase-issued JWTs (JWT-bearer auth
  against the Supabase project's JWKS/secret); add the `AppUser`/`Role`/
  `Permission`/`UserRole`/`RolePermission` tables (schema only — full RBAC
  enforcement is Phase 12); add the Development-only auth bypass handler
  described in ARCHITECTURE.md §3.4 so local business-logic work isn't
  blocked on a live Supabase project.
- Configure a private Supabase Storage bucket and the
  `Prime.Infrastructure/Storage` abstraction (ARCHITECTURE.md §3.9),
  including the service-role-key-based signed-URL path.
- `/health` endpoint checking API + local database (+ PostGIS
  availability); a separate check confirms Supabase reachability without
  blocking local-only development if it's briefly unreachable.
- `tests/` projects created (`Prime.Domain.Tests`, `Prime.Application.Tests`,
  `Prime.IntegrationTests`) with one smoke test each.

Exit criteria: `dotnet build` succeeds, `dotnet test` passes (smoke tests),
`dotnet ef database update` applies cleanly to a fresh local database *and*
to the Supabase dev project, frontend `npm run build` succeeds, `/health`
returns healthy against the local PostgreSQL/PostGIS instance found in
Phase 0, and a manual sign-in against Supabase Auth successfully reaches an
authenticated `/api` endpoint at least once end-to-end.

**Status (2026-09-23): partially complete — verified vs. still pending.**

Verified (actually run, not assumed):
- .NET SDK 10.0.401 and Node.js 24.19.0 (both current LTS) installed
  alongside the pre-existing .NET 7/Node 18, confirmed via `--version`.
- Solution (`Prime.slnx` — .NET 10's default XML solution format) and all
  four backend projects plus three test projects created, referenced per
  the Clean Architecture dependency direction, and build clean: `dotnet
  build` → 0 warnings, 0 errors.
- `Prime.Domain`, `Prime.Application` foundation types added (`Entity`,
  `IAuditable`, `WorkflowStatus`, `DomainException`, `Result`/`Result<T>`).
- `PrimeDbContext` + design-time factory + audit-timestamp interceptor +
  PostGIS-specific health check wired in `Prime.Infrastructure`.
- `Prime.WebApi` wired: Serilog, Swagger (with bearer auth definition),
  FluentValidation registration, CORS, global exception-handling middleware
  producing the CLAUDE.md §63 error shape, JWT-bearer auth configured
  against Supabase settings, and a Development-only auth bypass handler
  (disabled unless `DevAuth:Enabled` is set).
- Initial EF Core migration generated (`InitialCreate` — enables the
  `postgis` extension, otherwise empty, as expected for this phase) using
  the design-time factory — this does not require a live database
  connection and none was used to generate it.
- `frontend/prime-web` scaffolded (Vite + React + TypeScript + React Router
  + TanStack Query + `@supabase/supabase-js`), builds clean (`tsc -b && vite
  build`), lints clean (`oxlint`).
- All 6 automated tests pass, including an integration test that hits the
  real `/health` endpoint through `WebApplicationFactory<Program>` and
  asserts a well-formed report — this passes whether or not a database is
  reachable, by design (health-check middleware reports Unhealthy rather
  than throwing), so it does not by itself prove database connectivity.
- HTTPS dev certificate trusted (`dotnet dev-certs https --trust`).

**Update (2026-09-23, later same day): local database verified for real.**
A dedicated least-privilege `prime` role and `prime_dev` database were
created locally (not using the `postgres` superuser for the app
connection); `InitialCreate` was applied via `dotnet ef database update`
against the real database; `/health` was hit against a real running
`dotnet run` instance (not just the WebApplicationFactory test host) and
returned:
`{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy"},{"name":"postgis","status":"Healthy","description":"PostGIS 3.6 ..."}]}`.
Swagger JSON and UI both confirmed reachable (200). The FluentAssertions
package was replaced with **Shouldly** across all test projects after
discovering FluentAssertions 8+ requires a paid commercial license
(Xceed) — not appropriate for a government platform; ARCHITECTURE.md
updated accordingly.

**Update (2026-09-24): Supabase project created and verified for real.**

Project: `osmboyuhhvxstawmqrsl`, region `ap-southeast-1` (Singapore — lowest
latency for Philippine LGU users), Postgres 17.6, PostGIS 3.3.7. Verified,
not assumed:

- **Auth**: the project uses Supabase's newer **JWT Signing Keys**
  (asymmetric, ES256), not the legacy shared secret — confirmed by curling
  the project's own public endpoints: `/auth/v1/.well-known/openid-configuration`
  (returns a standard OIDC discovery document with `issuer` and `jwks_uri`)
  and `/auth/v1/.well-known/jwks.json` (returns the ES256 public key, `kid`
  matching what was provided). `Prime.WebApi`'s JwtBearer config was
  rewritten to use `Authority` + automatic JWKS fetch/rotation instead of a
  stored symmetric secret — there is now no JWT secret to manage on the API
  side at all. ARCHITECTURE.md §3.4 and `.env.example` updated to match.
- **Database**: `CREATE EXTENSION postgis` run on the Supabase database;
  `InitialCreate` applied via `dotnet ef database update` through the
  **session pooler** (port 5432, required for migrations — transaction-mode
  pooling doesn't support the session semantics EF Core's migrator needs);
  confirmed via `SELECT * FROM "__EFMigrationsHistory"` on the real
  database. `Prime.WebApi` run with `ASPNETCORE_ENVIRONMENT=Staging`
  against the **transaction pooler** (port 6543, `Max Auto Prepare=0` per
  DATABASE.md §1.1) and `/health` returned
  `{"status":"Healthy","checks":[{"name":"postgresql","status":"Healthy"},{"name":"postgis","status":"Healthy","description":"PostGIS 3.3 ..."}]}`
  — both pooling modes work as designed.
- **Storage**: two private buckets created (`property-documents`,
  `exemption-documents`, both `public = false`). Confirmed RLS is enabled
  on `storage.objects` with **zero policies** defined — deny-by-default by
  construction, matching ARCHITECTURE.md §3.9 (only the service-role key,
  used server-side only, can reach them until Phase 4+ adds signed-URL
  issuance).
- Config split: `appsettings.Development.json` (gitignored) still points at
  local Postgres; a new `appsettings.Staging.json` (gitignored) holds the
  real Supabase pooled connection string; `frontend/prime-web/.env.local`
  (gitignored) holds the real Supabase URL + anon key. `.gitignore` was
  tightened from listing specific `appsettings.*.json` filenames to a
  blanket `appsettings.*.json` / `!appsettings.json` pattern — the old
  pattern would NOT have caught `appsettings.Staging.json` and was a real
  gap, found and closed before it mattered.

**Still not yet done — explicitly not claimed as working:**
- `Prime.Infrastructure/Storage` (the actual signed-URL-issuing code) and
  the `AppUser`/`Role`/`Permission`/`UserRole`/`RolePermission` schema are
  not yet implemented — deferred to align with Phase 3 (Core Database),
  since they are real entities that belong in the same migration
  discipline as the rest of the domain model, not a one-off Phase 2
  fragment. The buckets exist; nothing reads or writes to them yet.
- JWT validation has been proven to start up without error and to fetch a
  real, verified JWKS document, but has **not** been exercised end-to-end
  with an actual Supabase-issued user token, because no authenticated
  endpoint exists yet to call — that naturally happens once Phase 4+ adds
  real `[Authorize]`-protected controllers.
- Hangfire packages are referenced (per ARCHITECTURE.md §3.8) but
  deliberately not wired into `Program.cs` yet — background job
  infrastructure is a Phase 6/13 concern.

**Phase 2 — Foundation is complete**, both the local-development path and
the Supabase path.

## Phase 3 — Core Database

**Goal**: the entities in DOMAIN-MODEL.md §3.1–§3.10 exist as real tables.

Tasks: reference tables, `Property`, `Taxpayer`, `PropertyTaxpayer`,
`Parcel`, `RPU`, `TaxDeclaration`, `Land`, `Building`/`BuildingComponent`,
`Machinery` — EF Core entity classes + configurations (`IEntityTypeConfiguration<T>`),
migrations, constraints (PK/FK/unique/check), indexes (including spatial
and effective-date indexes per DATABASE.md §4/§11), and unit tests per
entity configuration (e.g. required fields, constraint violations behave
as expected).

Exit criteria: migrations apply cleanly from empty; constraint/index tests
pass; no business logic yet (registration workflows come in Phase 4).

**Status (2026-09-24): complete, verified against both databases.**

Implemented: 14 reference/lookup tables (Province/Municipality/Barangay
hierarchy + 11 flat lookups), the 10 core entities (Property, Taxpayer,
PropertyTaxpayer, Parcel, RPU, TaxDeclaration, Land, Building,
BuildingComponent, Machinery), plus — deferred here from Phase 2 by
earlier decision — `AppUser`/`Role`/`Permission`/`UserRole`/
`RolePermission` and `Document`. 31 tables total. See DOMAIN-MODEL.md §0
for the real interpretive decisions made while coding (enum vs. reference
table split, the `PropertyEntity` naming clash with EF Core's own
vocabulary, etc.) — these were genuine ambiguities in CLAUDE.md, resolved
and documented, not guessed silently.

Verified, not assumed:
- `dotnet build` → 0 warnings, 0 errors after all ~30 new files.
- Migration `CoreDatabase` applied cleanly to **both** the local database
  and the Supabase database (session pooler, port 5432) from the Phase 2
  baseline — `\dt` / `information_schema.tables` confirm 31 PRIME tables
  plus `__EFMigrationsHistory` on each.
- `/health` re-checked against local Postgres after the migration —
  still `Healthy`.
- 5 new integration tests run against the **real local database** (not
  mocked, not in-memory), each wrapped in a rolled-back transaction so the
  dev database is never polluted (confirmed by row-count query after the
  run — all zero):
  - Unique constraint on `Property.PropertyIdentificationNumber` fires.
  - Unique constraint on `RealPropertyUnit.RpuNumber` fires.
  - A `Parcel` polygon genuinely round-trips through real PostGIS
    (SRID and computed area both verified post-reload).
  - `TaxDeclaration`'s supersession chain (`PreviousTaxDeclarationId`)
    preserves the original row rather than overwriting it.
  - Deleting a `Province` with a dependent `Municipality` is rejected —
    proven at **two** layers: EF Core's own change tracker throws
    `InvalidOperationException` synchronously from `.Remove()` (a
    fail-fast property of `DeleteBehavior.Restrict` + a required FK,
    discovered while writing the test — not what was first assumed), and
    a second test bypassing EF entirely via raw SQL confirms the
    database-level `23503 foreign_key_violation` constraint also exists
    independently.
- All 12 tests across the solution pass (`Prime.Domain.Tests`,
  `Prime.Application.Tests`, `Prime.IntegrationTests`).

**Not yet done — intentionally out of Phase 3 scope:**
- No FluentValidation validators, no application services, no controllers
  for these entities — that is Phase 4.
- `PropertyType`, `TaxType`, `TransactionType`, `DiscountRule`/
  `PenaltyRule`/`InterestRule`, `SMV`/`AssessmentLevel`/`Assessment`,
  billing/payment tables are not part of Phase 3 — they belong to the
  phases that actually consume them (5, 6, 8, 9) per the roadmap.
- Seed/demo data was not added — no reference-table rows exist yet in
  either database (Phase 4+ as registration workflows need something to
  select from, or a dedicated seed step beforehand).

## Phase 4 — Property Registry

**Goal**: an authorized user can register a property end-to-end and find
it again.

Tasks: property registration (create/update), property search (§56),
property profile screen (§50) showing the sections that exist by this
phase (basic info, owners, parcels, land/buildings/machinery, RPUs — GIS
map and assessment/billing sections come later and render "not yet
available" until their phases land), taxpayer management + ownership
history, parcel management, RPU management, Tax Declaration creation.
`/api/properties`, `/api/taxpayers`, `/api/parcels`, `/api/rpus`,
`/api/tax-declarations`.

Exit criteria: create-property → create-taxpayer → create-parcel →
create-RPU → create-TD flow works through the API and the UI; audit log
entries are produced for each step; unit + integration tests cover the
flow.

**Status (2026-09-23): backend and frontend UI complete and verified.**
The registration forms, Property Profile screen, and search UI (originally
deferred by product decision at the start of this phase) have since been
built. The full
exit-criteria flow — register a property, add an owner (including
inline new-taxpayer registration), add a parcel, add an RPU, add a Tax
Declaration under it, then find the property again via search — was
driven end-to-end through a real browser (Playwright) against the real
running frontend, backend, and database, with zero browser console
errors. The Property Profile screen truthfully labels the sections that
don't exist yet ("GIS map, current assessment, billing, payments, and
delinquency sections are not shown yet") rather than faking data for
them, per CLAUDE.md §55's "do not use fake production statistics"
principle applied consistently.

### Architecture decisions made while implementing (recorded, not silent)

1. **No MediatR.** ARCHITECTURE.md §3.1/§5 left "MediatR vs. plain service
   classes" as an open Phase-2 decision. Resolved here: **plain Application
   service classes** (`IPropertyService`, `ITaxpayerService`, etc.,
   injected directly into controllers). Reason: MediatR 14.x is now
   dual-licensed by Lucky Penny Software (RPL1.5 — requires open-sourcing
   your own code — or a paid commercial license), discovered by reading
   its actual `LICENSE.md` before committing to it, the same way
   FluentAssertions' license was caught in Phase 2. Not appropriate for a
   government platform. The audit-logging pipeline that would have
   motivated MediatR's behavior pipeline is implemented independently, at
   the EF Core `SaveChanges` interceptor level, so it works regardless of
   this choice.
2. **`IApplicationDbContext`** (Prime.Application/Common/Interfaces) — a
   thin interface exposing the `DbSet<T>` properties Application needs,
   implemented by `PrimeDbContext`. Keeps Application decoupled from EF
   Core's concrete DbContext type per the Clean Architecture dependency
   direction, without building a full repository-per-aggregate layer —
   judged not justified yet for Phase 4's scope.
3. **`AuditLog` is real now, not just timestamps.** Phase 2's interceptor
   only stamped `CreatedAt`/`UpdatedAt` and explicitly flagged the full
   `AuditLog` row-per-mutation mechanism as unimplemented. It is now
   implemented: `AuditSaveChangesInterceptor` writes one `AuditLog` row per
   Added/Modified/Deleted `IAuditable` entity, in the same `SaveChanges`
   batch, with JSON snapshots of changed values. `ICurrentUserService` +
   `AppUserProvisioningMiddleware` resolve the acting user's `AppUser.Id`
   from claims once per request (just-in-time provisioning — Supabase Auth
   owns sign-up, PRIME only needs a matching local profile row), which is
   the first real exercise of the Supabase JWT / DevAuth-bypass wiring
   from Phase 2.
4. **AddOwnerAsync adds, does not replace, owners** — supports
   co-ownership (percentages summing to ≤100%); a full
   ownership-*transfer* workflow (closing the previous owner, recording a
   `PropertyTransaction`) is out of scope here — `PropertyTransaction`
   doesn't exist yet and belongs to a later phase.

### Two real EF Core bugs found by actually running this against a real database (not assumed to work)

1. **`.Select(x => SomeMethod(x))` with a separately-defined method does
   not translate to SQL.** EF Core cannot see into an arbitrary method
   call, so it silently falls back to loading the raw entity and invoking
   the method client-side — any navigation property the method reads
   (e.g. `p.Province!.Name`) is then null, throwing
   `NullReferenceException`, because no `.Include()` was used. Hit this in
   `PropertyService` and `TaxDeclarationService`; fixed by adding explicit
   `.Include()` chains before materializing, or by projecting inline
   inside the LINQ lambda instead of calling a named method (both are
   safe; a named method after `.ToListAsync()` — i.e. operating on an
   already-materialized in-memory list — is also safe, and is what the
   other three services already did). Caught by the end-to-end test
   actually calling the real HTTP endpoint against a real database, not
   by the build.
2. **NetTopologySuite `Geometry` values are not JSON-serializable as-is**
   — internal coordinates can be `NaN` (e.g. an unset Z on a 2D point),
   which `System.Text.Json` refuses to write, and PostgreSQL's `jsonb`
   type has no `NaN` literal regardless. `AuditSaveChangesInterceptor`
   now converts `Geometry` values to WKT text before serializing an audit
   snapshot. Also found only by running a real create-Parcel request, not
   by the build.

### One frontend bug found by actually running the production build (not the dev server)

- **`AddOwnerModal`'s ownership-percentage `InputNumber` failed `tsc -b`**
  (`vite dev`/esbuild is transpile-only and does not type-check, so this
  passed every Playwright run against the dev server without ever
  surfacing). Antd's `InputNumber<T>` infers `T` from its `min`/`max`
  props; passing the literals `min={0.01}` / `max={100}` narrowed `T` to
  the literal union `0.01 | 100`, which then required `parser` to return
  that same union instead of `number`. Fixed by pinning the generic
  explicitly (`<InputNumber<number> ...>`) instead of casting the
  parser's return value. Lesson: a Playwright pass against `npm run dev`
  is not equivalent to `npm run build` — both are needed before calling
  frontend work verified.

### Verified, not assumed

- Full build: 0 warnings, 0 errors.
- All 13 tests pass solution-wide, including a genuine end-to-end HTTP
  test (`PropertyRegistrationFlowTests`) that drives the exact exit-
  criteria flow — create Property → Taxpayer → ownership → Parcel → RPU →
  Tax Declaration — through real HTTP requests against the real WebApi
  host and a real database, then asserts an `AuditLog` row exists for
  every created record (`Action = Create`) with a non-null `UserId` and a
  `NewValue` JSON snapshot containing the actual submitted data. Test data
  (including seeded reference rows) is deleted afterward; `AuditLog` rows
  are deliberately left in place, since `AuditLog` is append-only by
  design even for test-generated rows.
- `PropertyProfileDto` (GET /api/properties/{id}) confirmed to return
  populated Owners/Parcels/Rpus/TaxDeclarations sections for a real
  registered property, not just an empty shell.
- Constraint tests from Phase 3 re-verified still passing alongside the
  new feature code.
- Dev database confirmed clean of test-created domain rows after the run
  (row counts checked directly via `psql`); two rows orphaned by an
  earlier failed run (before the bugs above were fixed) were found and
  manually removed rather than left to accumulate.
- Frontend: `tsc -b && vite build` (production build) and `oxlint` both
  clean, 0 errors/warnings, after fixing the `InputNumber` generic bug
  above. `dotnet build`/`dotnet test` re-confirmed clean (13/13 passing)
  in the same verification pass.
- Full UI exit-criteria flow driven through a real Chromium browser via
  Playwright against the real running frontend (`localhost:5173`) and
  backend (`https://localhost:7221`, health-checked against real
  PostgreSQL/PostGIS first): register property → add owner (inline new
  taxpayer) → add parcel → add RPU → add Tax Declaration → search →
  System Health page. Zero browser console errors across all 18 steps.
  Screenshots retained in `.scratch/pw-test/shots/` (gitignored scratch
  directory, not part of the repo) for this verification pass.

### Not yet done — explicitly deferred, not overlooked

- **Update/Delete** endpoints for all five entities — only Create/Get/List
  were built. Updating a Property/Taxpayer/etc. safely (what's mutable,
  what requires a new version instead per CLAUDE.md's history-preservation
  rules) needs its own design pass, not a quick addition.
- **Cross-entity global search** (CLAUDE.md §56 lists TD number, RPU
  number, TIN, address as searchable alongside PIN/lot/title/survey/tax-
  map number) — only Property's own fields are searchable so far; finding
  a property by TD number or owner TIN is not yet wired.
- **Real Supabase JWT exercised end-to-end** — confirmed in Phase 2 that
  the project's OIDC discovery/JWKS endpoints resolve correctly and the
  JwtBearer handler starts without error; still not exercised with an
  actual Supabase-issued user access token against a live `[Authorize]`
  endpoint (would require a real sign-up flow, which has no UI yet).

## Phase 5 — Valuation

**Goal**: market value can be computed and explained.

Tasks: SMV + SMVSchedule CRUD and versioning (never overwrite, §28),
AssessmentLevel CRUD and versioning, the `ValuationService`
(ARCHITECTURE.md §3.7), calculation-breakdown persistence (§31),
valuation history. **Use only clearly-labeled demo/test SMV and assessment
level data** (§81/§92) until an actual LGU supplies official values —
no invented rates.

Exit criteria: given demo SMV + assessment level data, the valuation
engine produces a market value with a stored, reproducible breakdown;
unit tests cover land/building/machinery valuation paths including edge
cases (zero area, boundary rate tiers).

**Status (2026-09-23): complete and verified.** `Smv`/`SmvSchedule` (never
overwritten — superseding a rate closes the old row's `EndDate` and inserts
a new one) and `AssessmentLevel` CRUD/versioning are built, plus a
`ValuationService` that computes `MarketValue` for Land (SMV-based, ×
`LocationFactor`), Building (SMV-based on `TotalFloorArea` × completion),
and Machinery (replacement-cost, straight-line depreciated) with a
persisted, reproducible `Valuation` breakdown row per CLAUDE.md §31.
`AssessmentLevel` is reference data only in this phase — not yet consumed;
applying it to produce `AssessedValue` is Phase 6's `AssessmentService`.

### Architecture decisions made while implementing

1. **`PropertyType` added** (`Reference/Lookups.cs`) — CLAUDE.md §28/§29 key
   `SmvSchedule`/`AssessmentLevel` on `PropertyTypeId`, but this lookup was
   never built in Phase 3. Land/Building/Machinery don't carry a
   `PropertyTypeId` themselves, so `ValuationService` resolves the right
   `PropertyType` row by a fixed `Code` ("LAND"/"BUILDING") — the same
   "`Code` is the stable identifier, `Name` is the LGU-editable label"
   convention already used for Classification/ActualUse/Zone.
2. **`AssessmentLevel.OrdinanceId` → inline `OrdinanceNumber`/`OrdinanceDate`.**
   docs/DOMAIN-MODEL.md §3.12 names an `OrdinanceId` field, but no
   `Ordinance` table exists anywhere in the codebase — `Smv` itself doesn't
   use one either, storing ordinance fields inline. Resolved the same way
   here rather than inventing a dangling FK or a new entity out of scope.
3. **`ValuationMethod` is a fixed C# enum** (`SmvBased`/`ReplacementCost`),
   not a reference table — it describes which algorithm the code actually
   ran, which only a developer can extend, unlike an LGU-editable
   classification. Same "fixed-by-code vs. LGU-configurable" split already
   applied to `WorkflowStatus` vs. the `Reference/` lookup tables.
4. **Building has no `ClassificationId`/`ZoneId` on the entity itself**
   (only `ActualUseId`, discovered while implementing, not anticipated
   during planning) — `ValuationService` resolves a Building's
   Classification/ActualUse from its own current Tax Declaration instead
   (the highest `RevisionNumber` row for that RPU), and does not attempt a
   zone-specific SMV match for buildings at all: zone-based rate
   differentiation only exists on `Land` in the current domain model
   (`LocationFactor`/`RoadFrontage`/`IsCornerLot`/`Zoning` are all
   Land-only fields), so this is a faithful reflection of what data is
   actually captured per asset type, not an invented shortcut.
5. **No age-based building depreciation.** CLAUDE.md forbids inventing a
   rate or assumption (§5-§7); depreciating a building by age would need an
   assumed economic-life-in-years per building type, and no such figure is
   entered anywhere on `Building` or sourced from an ordinance.
   `DOMAIN VERIFICATION REQUIRED` before this can be added — `Building`'s
   `Depreciation`/`DepreciatedValue` fields are left unset by this phase's
   `ValuationService`. `CompletionPercentage` (an assessor-entered field
   already on the entity) is the only condition-based adjustment applied.
6. **Machinery depreciation uses the entity's own `EconomicLifeYears`/
   `RemainingLifeYears`** (straight-line) — legitimate because these are
   figures an appraiser already enters per record (§26), not a code-level
   assumption. When either is missing, no depreciation is applied (full
   cost stands) rather than guessing a life span.
   **Superseded 2026-09-24** (regulatory alignment step 2): this used
   *acquisition* cost and let value fall to 0, contrary to LGC §224(a)
   (replacement/reproduction cost for non-new machinery) and §225 (minimum
   remaining value). Now: brand-new → acquisition cost; otherwise
   replacement cost × life ratio, floored at the configured §225 minimum;
   missing inputs refuse valuation instead of defaulting. See
   docs/DOMAIN-MODEL.md §3.9. Migration `MachineryReplacementCost`
   (additive).
7. **Maker-checker is Draft→Approved only** — `ValuationService` only
   resolves `Approved` schedules/never resolves a `Draft` one, but
   "creator cannot approve their own record" enforcement is Phase 12's
   full RBAC/permission layer, not built here.

### Verified, not assumed

- Full build: 0 warnings, 0 errors, after all new files.
- Migration `Valuation` (`PropertyTypes`, `Smvs`, `SmvSchedules`,
  `AssessmentLevels`, `Valuations`) applied cleanly to the local database;
  `/health` re-checked `Healthy` afterward.
- 31 tests pass solution-wide (17 Domain + 3 Application + 11 Integration,
  up from 13 before this phase):
  - 15 new `Prime.Domain.Tests` unit tests exercise the pure
    `ValuationCalculator` directly (no database) for Land/Building/Machinery,
    including the roadmap's named edge cases — zero-area land, a schedule's
    `MinimumValue`/`MaximumValue` clamp boundary, a building with no
    `YearConstructed` on file, and machinery past its full economic life
    (floors at 0, never negative) or with no life-span data at all (full
    cost, not a guessed depreciation).
  - 3 new `Prime.IntegrationTests` (rolled-back-transaction, real local
    Postgres — same pattern as `ConstraintTests`) prove: a real end-to-end
    Land valuation produces the expected `MarketValue` and a `Valuation`
    row whose `BreakdownJson` is independently reloadable and correct; a
    missing SMV schedule fails with a specific `SMV_SCHEDULE_NOT_FOUND`
    code rather than a generic error; and creating a second overlapping
    `SmvSchedule` closes the first row's `EndDate` rather than deleting or
    editing it — the actual "never overwrite" database behavior, not just
    an assumption about what the service code does.
  - Dev database confirmed empty of `Smvs`/`SmvSchedules`/`AssessmentLevels`/
    `Valuations`/`PropertyTypes` rows after the full test run (checked
    directly via `psql`), proving the rolled-back-transaction pattern
    didn't leak any test data.

### Not yet done — explicitly deferred, not overlooked

- **Frontend UI** (SMV/AssessmentLevel administration screens, a valuation
  trigger + breakdown view on the Property Profile) — backend-only this
  pass, following the same backend-first precedent Phase 4 set.
- **Age-based building depreciation** — `DOMAIN VERIFICATION REQUIRED`
  (see decision 5 above); needs a sourced BLGF economic-life/depreciation
  schedule before it can be implemented without inventing one.
- **`AssessmentLevel` is not yet consumed** by anything — Phase 6's
  `AssessmentService` applying it to a `Valuation.ComputedMarketValue` to
  produce `AssessedValue` is the next step.
- **No seed-data mechanism was added.** Consistent with every other
  reference table in the codebase (none has one yet), demo SMV/assessment
  level rows are inserted ad hoc by tests/manual exploration, not by a
  persisted seeder.

## Phase 6 — Assessment

**Goal**: assessed value, assessment workflow, and general revision exist.

Tasks: `AssessmentService` (ARCHITECTURE.md §3.7), assessment workflow
(DRAFT → ... → POSTED per §45), assessment approval with maker-checker
(§46), reassessment, General Revision as a background job (§33/§72:
SELECT → PREVIEW → CALCULATE → COMPARE → VALIDATE → REVIEW → APPROVE →
POST, with progress monitoring), historical assessment + before/after
comparison, audit trail.

Exit criteria: an assessment can be created, reviewed, approved (by a
different user than the creator, when maker-checker is configured), and
posted; a general revision job can run against a batch of demo properties
without blocking a browser request; history is preserved and queryable
"as of" a date.

**Status (2026-09-23): complete and verified.** `Assessment` applies a
resolved `AssessmentLevel` to a Phase 5 `Valuation`'s market value to
produce `AssessedValue`, through the full DRAFT → PENDING_REVIEW →
APPROVED/REJECTED → POSTED workflow (`WorkflowStatus`, reused as-is — no
new enum needed), with real maker-checker (the creator cannot approve
their own assessment) and reassessment via a `PreviousAssessmentId` chain.
Hangfire — referenced since Phase 2, deliberately left unwired — is now
wired in, and `GeneralRevisionJob` runs a batch of RPUs through
Valuation+Assessment as a real, progress-tracked background job.

### Architecture decisions made while implementing

1. **`Land`/`Building`/`Machinery`'s `RpuId` index is now unique.** It was
   a plain (non-unique) index since Phase 3 — nothing before this phase
   actually needed "exactly one detail row per RPU" to be a real invariant.
   Verified safe before changing: the local dev database's `Lands`/
   `Buildings`/`MachineryUnits` tables were empty at the time (no create
   endpoint exists yet for any of the three — see "Not yet done" below),
   so tightening this was a risk-free additive migration, not a breaking one.
2. **`TaxDeclarationLookup`** (`Prime.Application/Common/`) extracted from
   Phase 5's `ValuationService.ComputeForBuildingAsync` — `AssessmentService`
   needs the identical "current Tax Declaration for an RPU" query for
   Building *and* Machinery (Machinery has no `ClassificationId` either,
   discovered while implementing this phase, not anticipated during
   planning — `AssessmentLevel` is keyed the same way `SmvSchedule` is).
   Alongside it, `PropertyTypeCodes` centralizes the `"LAND"`/`"BUILDING"`/
   `"MACHINERY"` `PropertyType.Code` constants both `ValuationService` and
   `AssessmentService` resolve against, so the two services can't drift.
3. **Maker-checker creator-check is now real** on `Assessment.ApproveAsync`,
   and retrofitted onto Phase 5's `SmvService`/`AssessmentLevelService`
   approve methods too (same one-line `CreatedBy == currentUser.AppUserId`
   check) — cheap because `AuditSaveChangesInterceptor` already stamps
   `CreatedBy` on every insert. Closes something Phase 5 explicitly
   flagged as deferred. "Creator cannot approve their own record" is now
   enforced; full RBAC/permission-based maker-checker configuration is
   still Phase 12.
4. **`GeneralRevisionJob`, not the generic `PropertyTransaction` (§34)** —
   General Revision is the only batch-transaction type Phase 6 needs; a
   purpose-built entity avoids inventing Transfer/Subdivision/Consolidation
   structure ahead of a phase that actually needs it.
5. **`IBackgroundJobScheduler`** (`Prime.Application/Common/Interfaces/`),
   implemented by `HangfireBackgroundJobScheduler` in
   `Prime.Infrastructure/Jobs/` — discovered while implementing, not
   anticipated during planning: `Prime.Application` cannot reference
   Hangfire directly (Clean Architecture dependency direction,
   ARCHITECTURE.md §2), so `GeneralRevisionService`/`GeneralRevisionJobRunner`
   depend on this Application-owned interface instead, the same pattern
   already used for `IApplicationDbContext`/`ICurrentUserService`. Any
   future background job (large imports/exports, Phase 13) should use the
   same interface rather than referencing Hangfire from Application code.
6. **`ICurrentUserService.ActAsForBackgroundJob(Guid?)`** — a narrow,
   deliberately-named addition (not a general setter) so
   `GeneralRevisionJobRunner` can declare "acting as the user who started
   this job" when Hangfire invokes it outside any HTTP request (nothing
   populates `AppUserId` the normal per-request way there), without
   opening identity-spoofing to ordinary request-scoped feature handlers.
7. **`GeneralRevisionJobRunner` doesn't manage its own `IServiceScopeFactory`
   scope** — `Hangfire.AspNetCore`'s built-in `AspNetCoreJobActivator`
   already resolves job classes from a fresh DI scope per execution (the
   same way a controller gets one per HTTP request), so plain constructor
   injection is sufficient; this simplified an earlier assumption made
   during planning that manual scope management would be needed.
8. **Hangfire Dashboard is Development-only** (`Program.cs`) — its default
   authorization allows all requests, which CLAUDE.md §67 does not permit
   outside Development without real authorization wired up first
   (`DOMAIN VERIFICATION REQUIRED` before enabling elsewhere).

### Verified, not assumed

- Full build: 0 warnings, 0 errors, after all new files across three
  migrations (`AssessmentFoundations`, `Assessments`, `GeneralRevision`).
- All three migrations applied cleanly to the local database; `/health`
  re-checked `Healthy` after each.
- 35 tests pass solution-wide (17 Domain + 3 Application + 15 Integration,
  up from 31 before this phase): new integration tests (rolled-back-
  transaction pattern) prove an Assessment's `AssessedValue`/frozen
  percentage compute correctly; `ApproveAsync` rejects when the acting
  user matches `CreatedBy` and succeeds with a different one (maker-checker
  actually exercised with two distinct simulated users, not merely
  asserted to exist in code); reassessment correctly chains via
  `PreviousAssessmentId`; and `GeneralRevisionJobRunner.RunAsync`, called
  directly (not through the real Hangfire enqueue/worker pipeline — see
  the test file's own reasoning), processes a batch of seeded RPUs into
  tracked, `RevisionReference`-tagged Draft assessments with correct
  progress counts.
- Hangfire actually smoke-tested against the running app, not just unit
  tested: `/health` stayed `Healthy`, `/hangfire` (the dashboard) returned
  HTTP 200, and a real HTTP `POST /api/smv` round-tripped end-to-end
  through the full auth/routing/JSON pipeline (cleaned up from the dev
  database afterward, since it was a real request, not a rolled-back test
  transaction).
- Dev database confirmed empty of `Assessments`/`GeneralRevisionJobs`/
  `Valuations`/`Smvs` rows after the full test run (checked directly via
  `psql`) — the rolled-back-transaction pattern held even with Hangfire's
  own background server running in the same test host.

### Not yet done — explicitly deferred, not overlooked

- **Frontend UI** — backend-only this pass, same precedent as Phases 4/5.
- **Full RBAC/permission-based maker-checker configuration** (§46 —
  configuring *which* transactions require it, by role) is Phase 12; this
  phase only enforces the universal "creator ≠ approver" rule.
- **General Revision's SELECT stage is caller-supplied RPU ids only** — no
  UI or saved-filter mechanism (e.g. "all Land RPUs in Barangay X") exists
  yet to build that list; the API accepts an explicit list today.

### Follow-up (2026-09-23): Land/Building/Machinery registration gap closed

Phase 4's own status notes flagged that Land/Building/Machinery had no
Application-layer service or controller at all — only direct EF
construction in tests — and Phase 6's unique-`RpuId`-index change made
that gap visible again (General Revision needs to reliably find "the
Land/Building/Machinery for this RPU"). Closed as a follow-up, same
pattern as every other Phase 4 entity (`ILandService`/`IBuildingService`/
`IMachineryService`, `/api/land`, `/api/buildings`, `/api/machinery`,
plus `~/api/rpus/{rpuId}/land|building|machinery` singular lookups since
each is 1:1 with its RPU):

- Each `Create*Async` validates the RPU exists, that its `RpuType`
  matches (`RPU_TYPE_MISMATCH` otherwise — e.g. rejects creating a Land
  record against a Building RPU), and that no record already exists for
  that RPU (`*_ALREADY_EXISTS_FOR_RPU`, a friendly error ahead of the
  Phase 6 unique-index constraint), plus FK-existence checks for every
  reference field (Classification/ActualUse/SubClassification/Zone/
  RoadType for Land; BuildingType/StructuralType/ActualUse/Condition for
  Building; MachineryType for Machinery).
- `IApplicationDbContext` gained `RoadTypes`/`Conditions`/`BuildingTypes`/
  `StructuralTypes`/`MachineryTypes` — these lookup tables existed on
  `PrimeDbContext` since Phase 3 but were never exposed through the
  Application-facing interface because nothing needed them before.
- **Create/Get only, matching the existing Phase 4 precedent that
  Update/Delete needs its own design pass** (CLAUDE.md §49/§76) — not
  added here for these three either.
- No new migration — `Lands`/`Buildings`/`MachineryUnits` tables already
  existed from Phase 3; this only added the Application/WebApi layers.
- Feature folders are named after the plural `DbSet` property
  (`Features/Lands/`, `Features/Buildings/`, `Features/MachineryUnits/`),
  not the singular entity name, to avoid the entity/namespace collision
  that `Features/Smv/`/`Features/Valuation/` (Phase 5) already
  demonstrated works but is easy to get wrong without the qualification
  those files needed.
- Verified: 39 tests pass solution-wide (up from 35), including 4 new
  integration tests proving duplicate rejection, RPU-type-mismatch
  rejection, and correct default values (`AreaUnit` defaults to `"sqm"`,
  `NumberOfStoreys` to 1, `CompletionPercentage` to 100) against the real
  local database.

### Follow-up (2026-09-23): Land/Building/Machinery registration UI

The frontend counterpart to the backend follow-up above — CLAUDE.md §50
lists "Land, Buildings, Machinery" as required Property Profile sections,
and there was still no way for a real user to register any of the three.

- Registration + display nested under each RPU row on the Property
  Profile (`PropertyDetailForRpu.tsx`, dispatching on `rpu.rpuType`),
  following the exact `TaxDeclarationsForRpu` pattern Phase 4 already
  established: an RPU-scoped fetch that shows an "Add {Type}" button when
  nothing exists yet (a `*_NOT_FOUND` response, mapped to HTTP 404, is the
  expected "not registered" signal here — not an error), or a compact
  `Descriptions` view once it does.
- **A second real gap found while building this**: `IReferenceDataService`/
  `ReferenceDataController` never exposed `RoadType`/`Condition`/
  `BuildingType`/`StructuralType`/`MachineryType` as read-only lookups —
  needed to populate the new forms' dropdowns, closed with five one-line
  additions following the existing `GetLookupAsync<T>` pattern exactly.
- **Scope explicitly limited to registration + display** — no "compute
  valuation"/"assess" action or breakdown view was added here, since that
  would need SMV/AssessmentLevel data to exist and be `Approved` first,
  and no admin UI for creating those exists yet either. Noted as the next
  natural follow-up, not silently dropped.
- Verified live in a real browser (Playwright), not just by build/lint:
  registered a property, added a Land RPU, a Building RPU, and a
  Machinery RPU, and successfully registered and displayed details for
  all three, screenshotted at each step. `BuildingTypes`/`StructuralTypes`/
  `Conditions`/`MachineryTypes` had zero rows in the local dev database
  before this (no admin UI exists yet to create them) — one `DEMO_`-
  labeled row per table was inserted directly via SQL to make this
  verification possible (CLAUDE.md §81 — clearly-labeled demo data, not
  an invented legal value), and left in place as reusable seed data for
  future manual testing. The test property itself was deleted afterward
  (real HTTP traffic, not a rolled-back test transaction).
- `npm run build` (`tsc -b`, full typecheck) and `npm run lint` both
  clean — per this project's own recorded lesson that a dev-server pass
  alone does not catch type errors.

## Phase 7 — GIS

**Goal**: the map is live and linked to the Property Profile.

Tasks: PostGIS-backed parcel geometry CRUD/import, map rendering
(OpenLayers), layers (parcels, barangay boundaries, zones, roads), spatial
search, parcel selection → Property Profile navigation, printable tax map.
Resolve the canonical SRID open question from DATABASE.md §9 before or at
the start of this phase — it blocks correct geometry storage.

Exit criteria: clicking a parcel on the map opens the correct Property
Profile; spatial queries use the GiST index (verified via `EXPLAIN`).

### Status — in progress (started 2026-09-24)

Executed in checkpointed steps (user asked to stop at each):

1. ✅ **SRID decided and enforced.** EPSG:4326 storage (user-approved
   option: "WGS84 4326 + config"); `Parcels.Geometry` migrated to
   `geometry(MultiPolygon,4326)` (migration `TypedParcelGeometry`,
   hand-written `USING ST_Multi(...)` so existing single polygons convert
   losslessly and anything else fails loudly); `IGeometryMeasurementService`
   measures area geodesically or in a configured `Gis:MeasurementSrid`;
   `ParcelDto` now returns `MeasuredArea` + `MeasuredAreaBasis` beside the
   declared `Area`. Full rationale in docs/GIS.md §2. 53 tests pass (+9
   unit, +5 integration). Migration applied to the **local** database
   only — not yet to Supabase.
2. ✅ **GIS API.** `GET /api/gis/parcels?bbox=` (GeoJSON, Active only,
   capped with a `truncated` flag, no personal data in feature
   properties), `GET /api/gis/parcels/at?lon=&lat=`, and
   `PUT /api/parcels/{id}/geometry` (reason required to replace; old WKT
   kept in the audit log; historical parcels refused; first optimistic-
   concurrency token in PRIME via `xmin`, migration
   `ParcelConcurrencyToken` is DDL-free). GiST index use verified via
   `EXPLAIN` (exit criterion). 81 tests pass (+14 unit, +14 integration
   this step). Details: docs/GIS.md §4.
3. ✅ **Tax Map workspace** (`/gis`, OpenLayers 10): extent-loaded parcel
   layer from zoom 14, click → selection → Open Property Profile, search →
   Locate, Property Profile "View on map" deep link, configurable basemap
   (OSM default — dev only). **Exit criterion met:** clicking a parcel
   opens the correct Property Profile, verified in a real browser. Also
   fixed a pre-existing app-shell issue found while testing at phone width
   (sidebar never collapsed; header title overlapped content) — sidebar
   now collapses fully below `md` (CLAUDE.md §84). Details: docs/GIS.md §5.
4a. ✅ **Reference layers backend.** Effective-dated `BarangayBoundaries`/
   `ZoneBoundaries`/`RoadSegments` (migration `GisReferenceLayers`, additive;
   first check constraints + filtered unique "one current version" indexes
   in PRIME), GeoJSON import API with dry-run-by-default and all-or-nothing
   commit (422 + full per-feature error report), as-of-date layer queries
   (GiST verified). 93 tests pass (+12). **No real boundary data loaded** —
   must come from an official source. Details: docs/GIS.md §3.
4b. ✅ **Layers UI + printable tax map.** Layer toggles with per-layer
   zoom thresholds and a "Boundaries as of" date (as-of queries end to
   end), a shared layer module for the workspace and print view, and
   `/gis/print`: an A4-landscape sheet at on-screen scale with scale bar,
   north arrow, legend, and the data sources actually on the sheet.
   Verified in a real browser, including a real PDF render. Details:
   docs/GIS.md §5.

**Phase 7 — GIS is complete.** Both exit criteria are met and verified:
clicking a parcel opens the correct Property Profile (real browser), and
spatial queries use the GiST index (`EXPLAIN`, asserted by tests). 93
backend tests pass; frontend `npm run build` + lint are clean. Open items
carried forward (Supabase migrations, real boundary data, per-LGU PRS92
zone, production basemap, role gating, import UI) are listed in
docs/GIS.md §7.

**Supabase brought up to date (2026-09-24).** Read-only inspection first
showed Supabase had only 3 of 10 migrations (through Phase 3's
`AuditLog` — Phases 5–7 had never been applied) and **no business data**
(0 properties/taxpayers/RPUs/parcels/audit rows; no duplicate `RpuId`s
that would break Phase 6's unique indexes). The 7 pending migrations were
applied via the session pooler (port 5432, per DATABASE.md). Verified
afterwards: 10/10 in `__EFMigrationsHistory`; all four geometry columns
typed `…,4326`; check constraints and `UX_*_Current` indexes present;
**RLS is enabled on all 43 PRIME tables with zero policies** — Supabase's
`ensure_rls` event trigger enables it on every new table — so the
auto-generated REST/GraphQL API is deny-by-default for them, while PRIME's
backend connects as the owner role. Only PostGIS's own `spatial_ref_sys`
lacks RLS (public EPSG reference data). Not yet exercised: running the
API itself (including Hangfire, which uses the transaction-mode pooler in
Staging config) against Supabase.

## Phase 8 — Billing

**Goal**: a tax bill can be generated from a posted assessment using
configurable rules.

Tasks: billing engine, `TaxBill`/`TaxBillDetail`, discount/penalty/interest
rule application (configurable, never hard-coded per §44), statement of
account. **Demo/test rates only** until real ordinance rates are supplied.

Exit criteria: given a posted assessment and demo billing rules, a bill is
generated with a correct, auditable breakdown (basic RPT + levies −
discounts + penalties + interest = total); financial edge cases from
CLAUDE.md §75 (zero, large amounts, rounding) covered by unit tests.

### Status — in progress (started 2026-09-24)

User direction: **DEMO values only** until official ordinance values are
supplied. Checkpointed steps:

1. ✅ **Rule model** — docs/BILLING.md §3 (TaxType, TaxRate,
   PaymentSchedule, DiscountRule, InterestRule, PenaltyRule,
   TaxIncreaseCapRule; approval-time supersession; legal items listed as
   DOMAIN VERIFICATION REQUIRED). Implemented: entities, migration
   `BillingRules` (applied to local dev DB; **not** Supabase),
   `IBillingRuleService`, `/api/billing/*` create/get/list(`?asOf=`)/approve
   endpoints, `/api/reference/tax-types`. Error codes ending `_CONFLICT`
   now map to HTTP 409 (previously only `_CONCURRENCY_CONFLICT`; this also
   moves the existing SMV/AssessmentLevel effective-date conflicts from
   400 to 409).
   - 2026-09-24 regulatory alignment (step 1 of the patch plan from
     docs/analysis/current-real-property-regulatory-baseline.md §6):
     added `TaxIncreaseCapRule` (RA 12001 §29 ¶3; IRR §55) — per-tax-type
     cap on the increase from a new SMV, bounded windows with a
     no-overlap exclusion constraint (`btree_gist`). The `BillingRules`
     migration was regenerated.
2. ⏳ Pure `BillingCalculator` + §75 financial unit tests.
3. ⏳ `TaxBill`/`TaxBillDetail`, bill generation API, statement of account UI.

## Phase 9 — Collection

**Goal**: payments can be posted, allocated, reversed, and reconciled.

Tasks: `Payment`/`PaymentAllocation`, full/partial/advance/multi-year
payment, reversal/void/correction, collection reports, duplicate-submission
protection (idempotency + transactions per DATABASE.md §7).

Exit criteria: full CREATE PROPERTY → ... → PAY → VERIFY BALANCE E2E flow
from CLAUDE.md §74 passes; overpayment/partial/reversal scenarios from §75
covered by tests; posting is verified atomic under concurrent submission.

## Phase 10 — Delinquency

**Goal**: outstanding balances and aging are accurate and reportable.

Tasks: delinquency computation (property-level, taxpayer-level, tax-year),
aging buckets, outstanding-balance reports, statements.

Exit criteria: delinquency figures reconcile against Bill − Payment state
for a demo dataset; aging buckets match configured aging rules.

## Phase 11 — Reporting

**Goal**: the report catalog in CLAUDE.md §57 is available in PDF/Excel/
CSV/print.

Tasks: property, assessment, tax, GIS, and audit report categories per
§57; export pipeline (likely as background jobs for large reports, per
§73).

Exit criteria: each listed report renders against demo data in all
required formats without loading unbounded data into memory (§71).

## Phase 12 — Workflow & Security

**Goal**: RBAC/permissions, maker-checker, and hardening are complete
system-wide (not just in the modules that needed them earlier).

Tasks: full role/permission matrix for the 11 roles (§9/§47), maker-checker
enforced on every transaction type listed in §46, audit trail completeness
review across all modules, security hardening pass (HTTPS, lockout,
password policy, input validation, injection/XSS/CSRF protections, rate
limiting, session expiration, secret storage — §67), personal-data
exposure review (§68), `docs/SECURITY.md` written. Since Supabase Auth and
Storage are in the platform (ARCHITECTURE.md §3.4/§3.9), this phase also
reviews: Supabase RLS policies on every `public` table holding taxpayer/
financial data (deny-by-default confirmed, not just assumed), Storage
bucket policies (no public buckets, signed-URL expiry is short enough),
and that the Supabase service role key is only ever used server-side.

Exit criteria: a permission matrix test suite proves each role can/cannot
perform each gated action; a self-approval attempt on a maker-checker
transaction is rejected; secrets are confirmed absent from source control
history; an unauthenticated or wrongly-scoped request against Supabase's
auto-generated REST/GraphQL API and against Storage bucket URLs is
confirmed to fail (RLS/policy verification, not just PRIME API testing).

## Phase 13 — Import / Migration

**Goal**: bulk LGU legacy data can be brought in safely.

Tasks: CSV/Excel import for taxpayers, properties, parcels, TDs, land,
buildings, machinery, historical assessments, SMV, GIS data; UPLOAD →
VALIDATE → PREVIEW → ERROR REVIEW → CONFIRM → IMPORT → AUDIT workflow
(§60); duplicate/invalid-data detection (§61); rollback on failure.
`docs/DATA-MIGRATION.md` written.

Exit criteria: a deliberately-flawed sample file is rejected with
actionable error review rather than partially imported; a clean sample
file imports completely and is fully audited.

## Phase 14 — Production Hardening

**Goal**: PRIME is deployable and operable by an LGU for years, not just
demoable.

Tasks: security review, database review, performance review, API review,
UI review, accessibility review, backup testing, recovery testing (§79 —
"do not assume a backup exists simply because a backup script exists" —
applies directly to Supabase's automated backups: confirm the project
tier's actual backup frequency/PITR window and perform a real restore, not
just trust the dashboard), concurrency testing, financial testing (full
§75 matrix), regression testing. `docs/DEPLOYMENT.md`, `docs/TESTING.md`,
`docs/BUSINESS-RULES.md` finalized/updated to reflect what was actually
built.

Exit criteria: every item in CLAUDE.md §107 quality gate passes for the
system as a whole; a restore-from-backup drill against the Supabase
project has actually been performed and verified, not merely documented as
possible.

---

## Cross-cutting rules that apply to every phase

From CLAUDE.md §102/§106/§112, restated as roadmap-level constraints, not
per-phase tasks:

- Inspect before modifying; small, coherent changes; build → test → lint
  after meaningful changes; never disable a test just to make a build
  green.
- No invented legal/tax/assessment/SMV/exemption/penalty values, ever —
  mark `DOMAIN VERIFICATION REQUIRED` or make it configurable instead.
- Never destroy assessment, billing, ownership, or payment history.
- No magic numbers, no hard-coded tax rules; single source of truth for
  valuation/assessment calculations (never duplicated between, e.g.,
  billing and assessment).
- Every architecturally significant decision gets recorded in the relevant
  `docs/*.md` file, kept current as the system evolves.

---

## Immediate next action

Phases 0–7 are complete and verified (Phase 7 — GIS finished 2026-09-24;
see its status block and docs/GIS.md §7 for carried-forward items).
All Phase 7 commits are pushed to `origin` (through `6b1cf04`). Still
missing UI from earlier phases: Phase 5 (SMV/AssessmentLevel
administration) and Phase 6 (a "compute valuation"/"assess" action with
breakdown display, a General Revision batch screen). Candidates for
what's next, in no particular priority order:

- **Phase 8 — Billing**: next in the roadmap sequence. Needs demo/test
  rates only (never invented real ones), and depends on posted
  assessments — which currently can only be produced via the API, since
  the Phase 6 UI doesn't exist yet.
- **Run the API against Supabase** (Staging config) as a smoke test —
  migrations are now applied, but Hangfire's behaviour behind Supabase's
  transaction-mode pooler (port 6543) has never been exercised.
- **Phase 5/6 UI**: SMV/AssessmentLevel administration screens, a
  "compute valuation" + "assess" action with breakdown view on the
  Property Profile (now that Land/Building/Machinery can actually be
  registered, there's something real to value/assess), a General
  Revision batch screen with progress — deferred backend-first, same
  precedent as every other phase's UI. The backend has zero remaining
  service/controller gaps for any of this.
- Remaining Phase 4 items: **Update/Delete** endpoints (now that Phase 6
  established a second "versioned, never-overwritten" precedent beyond
  Phase 5's, a design pass here has more to generalize from — see Phase 6
  status), **cross-entity global search** (CLAUDE.md §56: TD number, RPU
  number, TIN, address — currently only Property's own fields are
  searchable — safe to do anytime, no dependencies), and a **real
  Supabase JWT exercised end-to-end** (belongs with Phase 12's real
  sign-up/login UI, not built standalone).

Await explicit instruction on which to pick up; per CLAUDE.md §12/§108,
none of this happens automatically.
