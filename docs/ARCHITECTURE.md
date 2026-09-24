# PRIME — Architecture

Status: Phase 1 (proposed, not yet implemented). Supersedes nothing; first
architectural record for the project.

---

## 1. Purpose

This document defines the technical architecture for PRIME: the layering,
technology stack, cross-cutting concerns, and the major subsystem designs
(security, GIS, valuation/assessment, background processing, API). It is the
reference for Phase 2 (Foundation) onward.

---

## 2. Architectural style

Backend: **Clean Architecture**, four projects, strict dependency direction.

```text
Prime.WebApi
      │
      ▼
Prime.Application
      │
      ▼
Prime.Domain

Prime.Infrastructure ──▶ Prime.Application / Prime.Domain
```

Rules:

- `Prime.Domain` has no dependency on any other Prime project or on ASP.NET
  Core, EF Core, or any infrastructure concern. It contains entities, value
  objects, enums, domain services, domain events, and repository/interface
  contracts only.
- `Prime.Application` depends only on `Prime.Domain`. It contains use cases
  (features), DTOs, validators, and interfaces that `Infrastructure`
  implements. No EF Core types leak into this layer's public surface.
- `Prime.Infrastructure` implements the interfaces defined in `Domain` and
  `Application` (persistence, identity, GIS, documents, storage, external
  services). It depends inward only.
- `Prime.WebApi` is the composition root: DI wiring, controllers, middleware,
  auth, OpenAPI. It depends on `Application` and `Infrastructure` for wiring,
  but controllers depend only on `Application` (never on `Infrastructure`
  types directly).
- Business rules (valuation, assessment, billing, depreciation) live in
  `Domain`/`Application`, never in controllers.

Rationale: this directly implements CLAUDE.md §16–§17 and keeps valuation and
assessment logic — the parts of the system that must be legally defensible
and auditable for years — decoupled from web/framework concerns and testable
in isolation.

---

## 3. Technology stack

### 3.1 Backend

| Concern | Choice | Notes |
|---|---|---|
| Language / runtime | C# / **.NET (current LTS)** | Phase 0 found only .NET 7 SDK installed, which is out of support. Install the current LTS SDK at the start of Phase 2. **Verify the exact current LTS version number at install time** — do not assume a specific patch version from this document. |
| Web framework | ASP.NET Core Web API | REST, controller-based (minimal APIs may be used for simple, low-ceremony endpoints such as `/health`) |
| ORM | Entity Framework Core | Code-First, migration-driven |
| Spatial | `Npgsql.EntityFrameworkCore.PostgreSQL` + `NetTopologySuite` | Maps PostGIS `geometry` columns to `NetTopologySuite` types |
| Validation | FluentValidation | One validator per command/query, run via a pipeline behavior |
| Mapping | Mapster (or AutoMapper) | Prefer explicit mapping for financial/assessment DTOs — do not auto-map calculated monetary fields |
| Background jobs | Hangfire (PostgreSQL storage) | See §7 |
| Logging | Serilog (structured, sinks: console + file, later: a log aggregator) | See §8 |
| API documentation | Swashbuckle (OpenAPI/Swagger) | |
| Testing | xUnit, Shouldly, Testcontainers (PostgreSQL+PostGIS) for integration tests | Shouldly, not FluentAssertions — FluentAssertions 8+ requires a paid commercial license (Xceed), which doesn't fit a government platform; Shouldly is MIT-licensed |

**Feature folders, no MediatR** (decided in Phase 4): each
`Application/Features/<Area>` folder contains its own request/response
DTOs, a FluentValidation validator, and a plain service class
(`IPropertyService`/`PropertyService`, etc.) injected directly into the
matching controller — not a MediatR command/query pipeline. MediatR was
evaluated and rejected: version 14.x is dual-licensed by Lucky Penny
Software (RPL1.5, which requires open-sourcing code built against it, or a
paid commercial license) — found by reading its `LICENSE.md` before
depending on it, not assumed from "current stable." The uniform
validation/audit/authorization behavior a MediatR pipeline would have
provided is instead achieved independently: validation runs explicitly at
the top of each service method via an injected `IValidator<T>`, and audit
logging happens automatically at the EF Core `SaveChanges` interceptor
level (`AuditSaveChangesInterceptor`, Phase 4) regardless of which
Application-layer pattern calls it.

### 3.2 Frontend

| Concern | Choice | Notes |
|---|---|---|
| Framework | React + TypeScript | Per CLAUDE.md §11 |
| Build tool | Vite | Fast dev server, standard for new React+TS projects |
| Routing | React Router | |
| Server state | TanStack Query | Caching/pagination-friendly for large data tables |
| Client/UI state | Zustand (or Redux Toolkit if the team prefers a single-store convention) | |
| Component library | Enterprise-grade component kit (e.g. Ant Design or MUI) | Needed for data tables, forms, filters, approval panels per CLAUDE.md §82 |
| GIS | OpenLayers | Per CLAUDE.md §11/§38 |
| Forms/validation | React Hook Form + a schema validator (e.g. Zod) mirroring backend FluentValidation rules | |
| Testing | Vitest/Jest + React Testing Library; Playwright for E2E | |

Node.js: Phase 0 found v18.14.0, which is past LTS end-of-life. Install the
current **Active LTS** Node version at the start of Phase 2; verify the exact
version at install time rather than assuming one here.

### 3.3 Database & hosting — Supabase

**Decision (2026-09-23, product owner direction): PRIME uses Supabase as the
managed platform for Postgres/PostGIS, Auth, and Storage** — not just as a
bare Postgres host. This is a scope expansion beyond CLAUDE.md §11's literal
"ASP.NET Core Identity or equivalent" (Supabase Auth is the "equivalent"
chosen) and §59's "secure file/object storage" (Supabase Storage is that
object storage).

- **PostgreSQL**: hosted by Supabase for shared/staging/production. Supabase
  provisions its own major version per project (verify the exact version at
  project-creation time — it will not necessarily match the locally
  installed 16.15 found in Phase 0; PostGIS is a supported Supabase
  extension and is enabled the same way: `CREATE EXTENSION postgis;`).
- **PostGIS**: enabled via Supabase's extension mechanism (dashboard or
  `CREATE EXTENSION postgis;` through the SQL editor/migration). Functionally
  equivalent to local PostGIS for PRIME's purposes (geometry columns, GiST
  indexes, spatial queries) — no architecture change from DATABASE.md's
  spatial strategy.
- **Local development database**: the local PostgreSQL 16 + PostGIS 3.6.2
  found in Phase 0 remains the day-to-day dev database for schema and
  business-logic work (see §3.4 for why this creates a real seam with Auth,
  and how it's handled).
- **Connection pooling (important, Npgsql-specific)**: Supabase fronts
  Postgres with PgBouncer.
  - Use the **direct connection** (port 5432, session mode) for EF Core
    **migrations** (`dotnet ef database update`) — DDL and advisory locks
    need a real session.
  - Use the **transaction-mode pooled connection** (port 6543) for normal
    **application runtime** queries.
  - Npgsql must be configured for PgBouncer transaction-mode compatibility:
    disable server-side prepared statement caching
    (`Max Auto Prepare=0` in the connection string, or equivalent
    `NpgsqlDataSourceBuilder` configuration) — transaction-mode pooling
    reassigns the underlying server connection between transactions, so
    cached prepared statements/session state cannot be relied on.
  - Both connection strings are secrets (§67/§104): environment variables /
    secret manager only, never committed.
- **Backups/recovery**: Supabase provides automated backups (frequency and
  point-in-time-recovery depend on the project tier). CLAUDE.md §79 still
  applies in full — a backup existing is not the same as a verified restore;
  Phase 14 must actually perform a restore drill against Supabase's backups,
  not assume they work.

### 3.4 Authentication & authorization — Supabase Auth + PRIME RBAC

- **Supabase Auth (GoTrue)** is the credential store and token issuer:
  sign-up, sign-in, password hashing, password policy, account lockout,
  and MFA are Supabase Auth features, not custom ASP.NET Core Identity code.
  The frontend uses the Supabase client SDK for the sign-in/sign-up/
  MFA/password-reset flows and obtains a Supabase-issued JWT.
- **`Prime.WebApi` validates Supabase-issued JWTs** as bearer tokens rather
  than issuing its own tokens. This replaces "ASP.NET Core Identity issues
  the JWT" with "Supabase issues the JWT, PRIME validates it." Confirmed
  against the actual project (2026-09-24): it uses Supabase's newer **JWT
  Signing Keys** (asymmetric, ES256) rather than the legacy shared secret.
  Supabase Auth publishes standard OIDC discovery at
  `{Supabase:Url}/auth/v1/.well-known/openid-configuration`, so
  `Prime.WebApi` is configured with `JwtBearerOptions.Authority` pointing
  there — ASP.NET Core's JwtBearer handler fetches and caches the JWKS
  automatically, including key rotation, with **no secret to store or
  manage** on the API side at all. (If a future environment's Supabase
  project instead uses the legacy shared JWT secret, this would need a
  different — simpler but secret-bearing — configuration; verify under
  Settings > API > JWT Settings rather than assuming.)
- **PRIME still owns role/permission authorization**, per CLAUDE.md §9/§47 —
  Supabase Auth answers "who is this user," not "what can they do in PRIME."
  A `public.AppUser` table (see DOMAIN-MODEL.md §3.22) holds PRIME-specific
  profile/authorization data, referenced by the Supabase Auth user's UUID
  **as a plain value column, not a hard database foreign key to
  `auth.users`**. Reason: `auth.users` only exists inside a Supabase
  project's own database — a hard FK would break schema parity with the
  local Postgres dev database, which has no `auth` schema. `Role`,
  `Permission`, `UserRole`, `RolePermission` are ordinary PRIME tables in
  `public`, unaffected by which auth provider is in front of them.
- **Local dev / Auth seam (the real constraint this creates)**: Supabase
  Auth is a hosted service bound to one project's database — it cannot
  authenticate against the local PostgreSQL instance found in Phase 0.
  Resolution:
  - For **Development** environment only, `Prime.WebApi` registers a
    dev-only authentication handler that simulates an authenticated
    Supabase identity (configurable fake user id + roles) so business-logic
    and UI development is not blocked by needing a live Supabase project on
    every machine.
  - **Staging and Production always validate real Supabase-issued JWTs** —
    the dev handler is compiled out / disabled outside `Development`.
  - Real end-to-end Auth integration is exercised against an actual
    Supabase **dev/staging project** (cloud), and/or the **Supabase CLI**
    local stack (`supabase start`, which runs Postgres+GoTrue+Storage
    locally via Docker) for anyone who wants a fully offline Auth/Storage
    loop. Docker was found **not installed** in Phase 0; it is only
    required if the team wants that fully-local Supabase stack — it is not
    required to do schema/business-logic development against the local
    native Postgres install. This is a re-scoping of Phase 0's "Docker
    optional" note: still optional for core dev, but now clearly relevant
    (not just a deployment nicety) if full local Auth/Storage parity is
    wanted.
- **Role-based** authorization (the 11 roles in CLAUDE.md §9/§47) layered
  with **permission-based** authorization (fine-grained claims, e.g.
  `assessment:approve`, `payment:reverse`) so a role's exact capabilities are
  configurable without redeploying code — implemented as PRIME's own
  policy-based authorization in `Prime.Application`/`Prime.WebApi`, driven
  by the `Role`/`Permission` tables, independent of Supabase.
- **MFA**: provided by Supabase Auth directly (TOTP/other factors per
  Supabase's supported methods) rather than a custom PRIME implementation.
- **Maker-checker**: sensitive transactions (SMV approval, assessment
  approval, general revision approval, exemption approval, TD cancellation,
  payment reversal, billing correction) carry a `CreatedBy`/`ApprovedBy` pair
  (referencing `AppUser`, not `auth.users` directly) and a domain rule that
  rejects self-approval where configured. This is enforced in
  `Prime.Application`, not only in the UI, and is entirely independent of
  which service authenticated the user.

### 3.5 Cross-cutting concerns

- **Error format**: every API error follows the CLAUDE.md §63 shape
  (`code`, `message`, `details`, `traceId`). A global exception-handling
  middleware in `Prime.WebApi` maps domain/validation exceptions to this
  shape and strips stack traces from user-facing responses.
- **Auditing**: every create/update/delete/approve/reject/post/void/cancel/
  reverse on a tracked entity writes an `AuditLog` row (who/what/when/old/
  new/reason). Implemented as an EF Core `SaveChanges` interceptor in
  `Prime.Infrastructure` so it cannot be bypassed by forgetting to call it
  explicitly in a handler.
- **Soft delete / versioning**: entities listed in CLAUDE.md §49 are never
  hard-deleted. Enforced by omitting `DbSet.Remove` support for those
  aggregates at the repository layer and using `Status`/effective-dating
  instead.
- **Concurrency**: optimistic concurrency via a `xmin`-mapped or explicit
  `RowVersion` column on financial/assessment tables, combined with unique
  constraints and DB transactions for payment/posting operations (CLAUDE.md
  §66).
- **Money**: `decimal` in C#, `numeric(18,2)` (precision to be confirmed
  against actual LGU currency/rounding rules — flagged in
  DATABASE.md) in PostgreSQL. Never `float`/`double`.

### 3.6 GIS architecture

- PostGIS holds parcel polygons, property points, barangay boundaries, zone
  polygons, and road lines as `geometry` columns with spatial (GiST)
  indexes.
- `Prime.Infrastructure/GIS` exposes spatial queries (point/polygon lookups,
  "find parcel containing point", "properties within zone") behind an
  interface consumed by `Prime.Application`.
- The frontend GIS workspace (OpenLayers) requests vector/tile layers from a
  dedicated `/api/gis` surface; selecting a parcel resolves to a
  `PropertyId` and navigates to the Property Profile (CLAUDE.md §38/§54).
- **Coordinate reference system: stored in WGS84 (EPSG:4326)**, decided at
  the start of Phase 7 — PRS92 survey data is reprojected on import, and
  area is measured geodesically or in a per-LGU configured projected CRS
  (`Gis:MeasurementSrid`), never in degrees. See docs/GIS.md §2.

### 3.7 Valuation & assessment architecture

- A dedicated `ValuationService` in `Prime.Domain`/`Prime.Application`
  computes `MarketValue` from Land/Building/Machinery inputs, the applicable
  SMV, location/adjustment factors, and (for buildings/machinery)
  depreciation — per CLAUDE.md §30.
- An `AssessmentService` applies the effective-dated `AssessmentLevel` for
  the property's classification/actual use/property type to the computed
  market value to produce `AssessedValue`.
- Both services are **rule-driven, not hard-coded**: they resolve SMV rows,
  assessment level rows, and depreciation schedules from configuration
  tables filtered by effective date, never from constants in code
  (CLAUDE.md §7, §29, §30).
- Every invocation persists enough of its inputs (SMV id, assessment level
  id, method, basis, computed intermediate values) to satisfy the
  calculation-transparency requirement in CLAUDE.md §31 — i.e., the
  breakdown is reconstructable from stored data, not just the final number.
- Because valuation/assessment logic must be **single-sourced** (CLAUDE.md
  Rule 9), the billing engine calls the same `AssessmentService` output
  rather than recomputing assessed value independently.

### 3.8 Background processing

- **Hangfire** (PostgreSQL storage provider), wired in as of Phase 6, is
  used for:
  - General Revision (§33/§72) — CREATE JOB → QUEUE → PROCESS → VALIDATE →
    GENERATE RESULTS → REVIEW → APPROVE → POST, with progress tracked in
    `GeneralRevisionJob` (a job-status table the UI can poll —
    docs/DOMAIN-MODEL.md §3.13a).
  - Large CSV/Excel imports (§60/§73) and report generation/large exports
    — proposed, not yet built (Phase 13/11).
- Rationale: Hangfire integrates natively with ASP.NET Core, persists job
  state in the same PostgreSQL instance (no extra infrastructure like Redis
  required to start), provides a dashboard for operational visibility, and
  supports retry policies — matching the "trackable, retryable where safe,
  auditable" requirement in §73.
- **`Prime.Application` never references Hangfire directly** (Clean
  Architecture dependency direction, §2) — an `IBackgroundJobScheduler`
  interface in `Prime.Application/Common/Interfaces/` is what
  Application-layer services depend on to enqueue work;
  `HangfireBackgroundJobScheduler` in `Prime.Infrastructure/Jobs/` is the
  only place that touches Hangfire's own API. Any future background job
  (imports, exports, reports) should depend on this interface too, the
  same way `IApplicationDbContext`/`ICurrentUserService` abstract EF Core
  and the current-user context.
- A job class (e.g. `GeneralRevisionJobRunner`) is invoked by Hangfire
  outside any HTTP request — `Hangfire.AspNetCore`'s built-in
  `AspNetCoreJobActivator` resolves it from a fresh DI scope per
  execution, the same way a controller gets one per request, so plain
  constructor injection of scoped Application services is sufficient; no
  manual `IServiceScopeFactory` handling is needed in the job class
  itself. Because that scope has no HTTP request behind it, nothing
  populates `ICurrentUserService` the normal per-request way — a job
  entry point must call `ICurrentUserService.ActAsForBackgroundJob(userId)`
  as its first action so `CreatedBy` on rows it produces reflects who
  actually started the job (required for maker-checker, §46, to mean
  anything on those rows afterward).
- The Hangfire Dashboard's default authorization allows all requests —
  mapped Development-only in `Program.cs` until real authorization is
  designed for it (CLAUDE.md §67); `DOMAIN VERIFICATION REQUIRED` before
  enabling it anywhere else.
- Jobs that mutate assessment/billing data must be **idempotent** (safe to
  retry) and must themselves go through the same audit logging as
  interactive requests.

### 3.9 Document generation & storage — Supabase Storage

- Document templates (tax declarations, assessment notices, billing
  statements, official receipts) are configurable records (LGU
  name/office/logo/signatory/document number/legal reference), not files
  hard-coded into source, per CLAUDE.md §58.
- Actual files (titles, deeds, tax declarations, assessment documents,
  exemption documents, valuation documents, supporting records) are stored
  in **Supabase Storage**, in **private (non-public) buckets only** — e.g.
  `property-documents`, `exemption-documents`. No bucket holding taxpayer
  or property documents is configured public.
- **Access pattern**: the frontend does not fetch documents directly from
  Supabase Storage using end-user credentials. `Prime.WebApi` mediates every
  document read/write — it checks PRIME's own role/permission/maker-checker
  rules first, then either streams the file through the API or issues a
  short-lived **signed URL** (using the Supabase **service role key**,
  held only server-side in `Prime.Infrastructure`, never sent to the
  frontend) for direct, time-limited download. This keeps PRIME's
  authorization logic as the single source of truth for who can see a
  document (CLAUDE.md §67/§68), rather than duplicating that logic in
  Supabase Storage RLS policies.
- Storage-level RLS policies on the buckets are configured as
  **defense-in-depth** (deny-by-default; only the service role and, where
  genuinely needed, narrowly-scoped authenticated-role policies can read),
  not as the primary authorization mechanism.
- `Prime.Infrastructure/Storage` still exposes an interface
  (`IDocumentStorage` or similar) implemented against the Supabase Storage
  API, so the abstraction boundary from `Application` is unchanged even
  though the concrete backend is now fixed to Supabase rather than "local
  disk for dev, object storage for prod." For local development without
  network access, a local-disk implementation of the same interface may
  still be used as a development-only fallback.
- File metadata (bucket, path, content type, size, uploaded-by, associated
  entity) is stored in a PRIME `Document` table in Postgres — see
  DOMAIN-MODEL.md §3.19 for the updated `ExemptionDocument`/`Document`
  shape.

### 3.10 API design

- REST resources under `/api/...` matching CLAUDE.md §62.
- Every list endpoint supports pagination, filtering, sorting, and search
  parameters; the frontend never requests an unbounded property inventory
  (CLAUDE.md §71).
- Versioning strategy (URL-segment vs. header-based) — **open decision**,
  to be settled at the start of Phase 2 when the first controllers are
  written; not architecturally blocking today.
- All write endpoints run through the FluentValidation pipeline before
  reaching domain logic, and all authorization checks are declarative
  (policy-based) rather than embedded in controller bodies.

### 3.11 Deployment (high-level; detailed in a future DEPLOYMENT.md, Phase 14)

- Target: containerizable ASP.NET Core API + static-built React SPA, backed
  by **Supabase-hosted** PostgreSQL/PostGIS/Auth/Storage. The API and
  frontend still need to be hosted somewhere (Supabase does not host the
  ASP.NET Core API itself) — specific hosting target is an open decision,
  not required to begin Phase 2.
- Docker was found **not installed** in Phase 0. Status is now more nuanced
  than "optional": not required to begin Phase 2/3 (local native Postgres
  covers schema work), but relevant sooner than Phase 14 if the team wants
  the Supabase CLI's local stack for offline Auth/Storage development (see
  §3.4). Still recommended before Phase 14 regardless, for reproducible
  builds/deployment of the API and frontend.
- Configuration via environment variables / secret manager — never secrets
  committed to Git (CLAUDE.md §67/§104). This now explicitly includes:
  Supabase project URL, anon key, **service role key** (highest sensitivity
  — bypasses RLS, server-side only), JWT secret, and both the direct and
  pooled Postgres connection strings.

---

## 4. Explicit non-goals for this phase

- No source code, `.sln`, or project scaffolding is created in Phase 1.
- No specific package versions are pinned yet beyond "current LTS/stable" —
  exact versions are selected and recorded (with `dotnet --list-sdks` /
  `npm ls` evidence) in Phase 2 when the solution is actually scaffolded.
- No production infrastructure (hosting, CI/CD) is decided yet.

---

## 5. Open architecture decisions (to resolve by/at Phase 2)

1. ~~MediatR vs. plain service classes for the Application layer.~~
   **Resolved in Phase 4: plain service classes** — see §3.1 above
   (MediatR 14.x's licensing is incompatible with this project).
2. AutoMapper vs. Mapster vs. hand-written mapping for DTOs — resolved
   de facto in Phase 4 as "hand-written" (positional records + explicit
   `ProjectToDto` methods); not revisited as a separate package decision
   unless mapping boilerplate becomes a real burden.
3. API versioning scheme.
4. Exact `numeric` precision/scale for monetary columns (depends on
   confirmed LGU rounding rules — see DATABASE.md).
5. ~~Canonical spatial reference system for stored geometry~~ — resolved
   2026-09-24: EPSG:4326 storage + configurable measurement CRS (docs/GIS.md
   §2). Still open per LGU: *which* PRS92 zone to configure.
6. Whether local dev/CI integration tests run against a shared Supabase
   dev/staging project, or the team installs Docker + the Supabase CLI for
   a fully local Auth/Storage stack — affects Phase 2 setup instructions
   and whether Docker becomes a required (not just recommended) tool.
7. Exact Supabase project tier (affects backup frequency/PITR availability,
   connection pooler limits) — needed before Phase 14 backup/recovery
   planning, not before Phase 2.
8. Where the ASP.NET Core API and the built React SPA are actually hosted
   (Supabase hosts the database/auth/storage, not the API/frontend) —
   open, not blocking Phase 2.
