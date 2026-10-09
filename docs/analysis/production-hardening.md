# Phase 14 — Production hardening (design)

| | |
|---|---|
| Phase | 14 (CLAUDE.md §101, §107; roadmap "Phase 14 — Production Hardening") |
| Status | Decisions recorded 2026-10-08 (§8.1); H1, H2 and H3 done; H4 and H5 in progress (§9) |
| Sources | CLAUDE.md §49, §64, §66, §67, §70, §71, §74, §75, §79, §80, §83, §101, §104, §105, §107; `docs/ARCHITECTURE.md` §3.3, §3.11, §5; `docs/DATABASE.md`; `docs/GIS.md` §4; `docs/SECURITY.md` §9–§11 |
| Depends on | Phases 0–10 and 12 (built); Phases 11 and 13 deferred (user, 2026-10-08) |
| Order | After Phase 12, with 11 and 13 set aside; Phase 15 (manual) follows |

The goal is that the province can run PRIME for years: deployed on a known host, backed up with a restore that has
actually been performed, safe under concurrent use, fast at provincial volume, and checked by an automated regression
suite. This phase adds no assessment features.

## 1. Scope

In scope: deployment, backup and recovery, database safety, concurrency, performance at volume, calculation testing
(§75), API, UI and accessibility reviews, a regression suite and continuous integration, and the operational documents
`DEPLOYMENT.md`, `TESTING.md` and `BUSINESS-RULES.md`.

Out of scope: new features. Document storage (§59) is not built and is not hardening; it stays a later feature. The
frozen treasury code (§0) is neither extended nor removed here (Q11). Reports (Phase 11) and import (Phase 13) remain
deferred; the exit criteria apply to what is built.

## 2. What PRIME has

| Area | State |
|---|---|
| Build and tests | .NET 10 solution; about 780 xUnit tests (Domain ~190, Application ~27, Integration ~380) against the local PostgreSQL 16 + PostGIS; frontend `tsc -b`, `vite build`, `oxlint` |
| Frontend tests | None committed. Browser checks were Playwright scripts kept in session scratchpads, not in the repository |
| CI | None. No `.github/workflows`; builds and tests run by hand |
| Deployment | Not decided (ARCHITECTURE §3.11, §5 items 7–8). No Dockerfile; Docker not installed locally. Supabase hosts the database and Auth; nothing hosts the API or the SPA |
| Configuration | `appsettings.json` committed with empty secrets; `appsettings.*.json` gitignored. The local staging file connects to the Supabase pooler with `Trust Server Certificate=true` |
| Security (Phase 12) | Permissions, sign-up approval, MFA rule, rate limits, security headers and HSTS outside Development, upload checks, RLS check, audit completeness, personal-data masking (`docs/SECURITY.md`) |
| Not verified (SECURITY.md §11) | Real Supabase sign-in, password reset and TOTP; headers and forwarded headers behind the production proxy; backup and restore |
| Health | `/health`: PostgreSQL, PostGIS, row-level security, Supabase Auth settings. No check of the Hangfire server |
| Logging | Serilog to the console only |
| History protection | `AuditLogs` append-only by trigger. Other §49 tables (TDs, assessments, valuations, transactions, issued forms, registers, ownership, SMVs) are protected only by the application not offering a delete |
| Concurrency | Row version (`xmin`) on `Parcel` only (and the frozen `Payment`). 138 unique indexes guard numbers and keys. Approvals and postings check status in the application but do not lock the row or compare a version |
| Money | `decimal` everywhere; `numeric(18,2)` for amounts, other scales for rates and areas |
| Calculation tests | Valuation, adjustments, depreciation, brackets, back taxes and historical effectivity are tested; rounding is tested only for adjustment rules |
| Volume | All checks on a few hundred DEMO records. GIS index use proven with `EXPLAIN` on a tiny table (GIS.md: "re-check with real data volumes in Phase 14") |
| Docs | `README.md` is stale (describes billing and collection as in scope and Phases 5–6 as without UI). `DEPLOYMENT.md`, `TESTING.md`, `BUSINESS-RULES.md` do not exist |

## 3. Gaps

| # | Gap | Rule |
|---|---|---|
| G1 | No hosting target, no build artefact, no deployment procedure | §101; ARCHITECTURE §3.11 |
| G2 | Backups not confirmed; no restore performed; no recovery targets | §79; roadmap exit criterion |
| G3 | Historical tables can be deleted by any database session | §49, §76 |
| G4 | Two users can approve or post the same record at the same moment; edits are last-write-wins | §66 |
| G5 | Performance unknown at provincial volume; general revision never run on a large set | §71, §72 |
| G6 | §75 matrix only partly covered (rounding, zero, very large amounts, bracket boundaries per kind) | §75 |
| G7 | No automated UI or end-to-end regression; §74's E2E flow is tested through the API only | §74 |
| G8 | No CI; vulnerability scans are manual | §101; SECURITY.md §10 |
| G9 | Accessibility never reviewed | §83 |
| G10 | Database TLS certificate not verified; Hangfire not in `/health`; logs not retained | §67, §69, §70 |
| G11 | Operational documents missing; README stale | §86, §101 |

## 4. Proposal

### 4.1 Deployment (G1)

- One container image for the API, built from a committed `Dockerfile` (multi-stage: SDK build, ASP.NET runtime). The
  SPA is built with `vite build` and served as static files, either by the API (`UseStaticFiles` with an SPA fallback)
  or by the reverse proxy (Q1).
- Configuration only from environment variables or the host's secret store: the pooled connection string (runtime), the
  direct connection string (migrations only), Supabase URL and anon key, CORS origins, known proxies. Nothing secret in
  the image.
- Migrations are applied by a separate, manual release step (`dotnet ef database update` with the direct connection, or
  an idempotent SQL script generated by `dotnet ef migrations script --idempotent` and reviewed before it is run).
  The API never migrates on start.
- `docs/DEPLOYMENT.md`: environments (local, staging, production), the release checklist, migrations, rollback (redeploy
  the previous image; migrations are additive so the older image still runs), the proxy and TLS settings, and the
  verification of SECURITY.md §11 items on the real host.

### 4.2 Backup and recovery (G2)

- Confirm on the Supabase dashboard the project's plan, its backup frequency and retention, and whether point-in-time
  recovery is enabled; record them in `DEPLOYMENT.md` with the date checked.
- Add a second, province-held copy: a scheduled logical backup (`pg_dump -Fc`, schema and data) stored outside Supabase
  under the provincial ICT office's control, encrypted, with a stated retention (Q4).
- **Restore drill**, performed and recorded, not just documented:
  1. Restore the latest logical backup into an empty database (local PostgreSQL, and a scratch Supabase project if the
     plan allows).
  2. Restore Supabase's own backup (or a point in time) into a separate project, if the plan offers it.
  3. Verify each restore with a script: migration history matches, row counts per table match the source at backup
     time, `/health` passes against it, the append-only and delete triggers are present, and a sample of §111 questions
     (current TD, assessment on a past date, who approved) return the same answers.
  4. Record the date, the duration (measured recovery time) and the data loss window (measured recovery point).
- The client tools must be at least the server's major version; the local `pg_dump` is version 16, so the Supabase
  server version is checked first.

### 4.3 Database safety (G3, G10)

- A `BEFORE DELETE` trigger, as on `AuditLogs`, on every table listed by §49 and §76 that holds history: Tax
  Declarations, assessments and their lines, valuations, property transactions, issued forms and register runs, notices,
  sworn statements, ownership (`PropertyTaxpayers`), SMVs and their schedules, assessment levels, approvals and their
  requests, and the frozen treasury tables. One migration, one shared function; a test per table proves a delete is
  refused. Tables where deletion is part of a designed flow (drafts, child lines replaced while still in draft) are
  listed and justified in the migration, not silently excluded.
- A review of constraints: foreign keys present on every reference, check constraints on amounts, percentages and date
  ranges where the domain demands them, effective-date indexes on every effective-dated table.
- Verify the Supabase server certificate (`SSL Mode=VerifyFull` with the Supabase root certificate) instead of
  `Trust Server Certificate=true` (Q12).
- A Hangfire health check (server running, failed-job count) tagged `ready`.

### 4.4 Concurrency (G4)

- Add a row version (`xmin`, no physical column, the `Parcel` precedent) to every record that goes through a workflow
  status or is edited in place: assessments, valuations, Tax Declarations, property transactions, exemptions, SMVs,
  assessment levels, approval and cancellation requests, general revision runs, offices, delegations, numbering
  schemes and form versions.
- Every approve, reject, post, cancel and void is a conditional update: the expected status and the version must still
  match, otherwise `409` with a stable code (`CONCURRENCY_CONFLICT`) and nothing written. The frontend sends the version
  it displayed and on a 409 reloads and tells the user.
- Number allocation (TD, NOA, PIN, FAAS) is already guarded by unique indexes; the review adds a parallel test for each
  allocator.
- Tests run each sensitive transition twice in parallel against the database and assert exactly one succeeds.

### 4.5 Performance at volume (G5)

- A DEMO volume generator (a WebApi command, refused outside Development, every name prefixed `DEMO`) that creates a
  province-sized set: offices, barangays, properties with parcels and geometry, RPUs, valuations, assessments and TDs
  (Q7). It runs against the local database only, never against Supabase.
- Measure, with the volume loaded: property and owner search, the Property Profile, TD and assessment lists, the tax map
  at municipality and barangay zoom, register runs (TMCR, assessment roll), a general revision job over one
  municipality and over the province, and the audit trail viewer.
- For each slow query: `EXPLAIN (ANALYZE, BUFFERS)`, then an index, a projection or paging fix. Every list endpoint is
  checked for a server-side page-size ceiling.
- Targets (Q7) recorded in `TESTING.md` with the measured figures.

### 4.6 Calculation testing (G6)

- Walk the §75 matrix against the existing tests and fill the holes, per kind (land, building, machinery): zero values,
  the largest value `numeric(18,2)` holds, fractional areas and rates, rounding at each step that rounds, exact bracket
  boundaries (at, one centavo below, one above), depreciation floors and ceilings, several classifications on one unit,
  and a past date using the rules then in force.
- Where the rounding rule is a configuration or a legal question, the test pins the current behaviour and the rule is
  listed as DOMAIN VERIFICATION REQUIRED in `BUSINESS-RULES.md`.

### 4.7 Regression suite and CI (G7, G8)

- Commit a Playwright end-to-end suite under `frontend/prime-web/e2e/`, against the dev API with DevAuth and a seeded
  DEMO database: the §74 flow (property → owner → parcel → RPU → appraise → assess → approve and post as a second user
  → TD and FAAS → NOA → assessment roll and ROA), sign-up approval, a refused permission, a maker-checker refusal, and a
  municipal user seeing only their jurisdiction.
- Accessibility checks in the same suite with axe on the main screens (Q10), plus a manual keyboard pass recorded in
  `TESTING.md`.
- GitHub Actions workflow (Q9): on push and pull request, `dotnet build`, the test suite against a PostGIS service
  container, `npm ci`, `tsc -b`, `vite build`, `oxlint`, `dotnet list package --vulnerable`, `npm audit --omit=dev`, and
  a secret scan. The end-to-end suite runs on demand and before a release. No LGU or LAM content is used in CI; the
  tests use DEMO data only (§118).

### 4.8 API and UI review (G9, G11)

- API: every error follows §63; no endpoint returns an unbounded list; validation on every write; OpenAPI shows each
  endpoint's permission; Swagger off outside Development.
- UI: loading, empty and error states on every page; confirmation on every irreversible action; status badges and
  breadcrumbs consistent; printable views checked in Chrome and Edge.
- Logs: Serilog with a rolling file (or the host's collector) and a stated retention (Q13); a check that no personal
  data or token is logged at Information level.

### 4.9 Documents (G11)

- `docs/DEPLOYMENT.md` (§4.1, §4.2 and the drill record), `docs/TESTING.md` (suites, how to run them, volume results,
  accessibility results), `docs/BUSINESS-RULES.md` (each rule with legal basis, source, effective date, jurisdiction and
  system representation, CLAUDE.md §6; citing the LAM, not reproducing it, §118), and a rewritten `README.md`.

## 5. Data and migrations

- `HistoryDeleteGuards`: the shared trigger function and one trigger per history table. Additive; reversible by dropping
  the triggers.
- Row versions use `xmin`: no column is added, so the model change has an empty or near-empty migration.
- No data is changed. The volume generator writes to the local database only.
- Migrations are applied locally first; Supabase only when the user asks (§105).

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| H1 | Database safety: delete guards, constraint and index review, Hangfire health check, certificate verification | M |
| H2 | Concurrency: row versions, conditional transitions, 409 handling in the UI, parallel tests | M |
| H3 | Calculation matrix (§75) and `BUSINESS-RULES.md` | M |
| H4 | Volume generator, performance measurements and fixes | M |
| H5 | Regression suite: Playwright E2E, axe checks, CI workflow, API and UI review fixes | M |
| H6 | Deployment: Dockerfile, SPA serving, environment configuration, `DEPLOYMENT.md`, verification on the chosen host (SECURITY.md §11) | M |
| H7 | Backup and recovery: plan check, province-held backup, restore drill performed and recorded | S–M |
| H8 | Whole-system quality gate (§107), README and `TESTING.md`, regression run, roadmap update | S |

H1–H5 need no outside decision beyond §8 and can start at once. H6 and H7 need the hosting target and access to the
Supabase project settings.

## 7. Exit criteria

1. Every item of the §107 quality gate passes for the system as built (Phases 0–10, 12).
2. A delete on any history table is refused by the database, proven by a test per table.
3. Two parallel approvals or postings of the same record: exactly one succeeds, proven by tests.
4. With the province-sized DEMO set, the measured response times meet the targets in Q7, and a province-wide general
   revision completes as a background job.
5. The §75 matrix is covered by tests, listed in `TESTING.md`.
6. The E2E suite and CI pass on a clean checkout.
7. PRIME runs on the chosen host behind HTTPS, with the SECURITY.md §11 items verified there.
8. A restore drill has been performed from both the Supabase backup (if the plan allows) and the province-held backup,
   with the verification script passing and the measured recovery time and recovery point recorded.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Where are the API and the SPA hosted? | A decision for the province's ICT office. Recommendation: one Linux VM (or a container service) in the same region as the Supabase project (Singapore), running the API container behind Caddy or nginx with TLS, the SPA served by the proxy. Alternative: an on-premises Windows server with IIS at the Provincial Capitol; workable, but adds latency to Supabase and depends on the office's connection |
| Q2 | Supabase plan and point-in-time recovery | Confirm the current plan on the dashboard. For production, a paid plan with daily backups at least; point-in-time recovery if the budget allows. The free plan is not acceptable for production records |
| Q3 | Restore drill target | Both: the province-held logical backup into a local database and into a scratch Supabase project; Supabase's own restore into a separate project where the plan offers it. Repeat the drill every six months, recorded in `DEPLOYMENT.md` |
| Q4 | Recovery targets and the province-held copy | Recovery point: 24 hours without PITR, minutes with it. Recovery time: one working day. Province-held `pg_dump` daily, kept 30 daily, 12 monthly and yearly copies indefinitely (government records, §80); encrypted, on storage the ICT office controls. Final figures are the province's to set |
| Q5 | Delete protection | Database triggers on every history table, as on `AuditLogs`, rather than revoking privileges (Supabase's `postgres` role owns the tables, so revoking from it is not reliable) |
| Q6 | Concurrency scope | Row versions and conditional transitions on every workflow and editable record (§4.4), 409 `CONCURRENCY_CONFLICT`, the UI reloads and explains. Alternative: only on approval and posting; leaves edits last-write-wins |
| Q7 | Volume and targets | The province's real counts are DOMAIN VERIFICATION REQUIRED. Meanwhile test with 250,000 land properties and 400,000 RPUs across 16 municipalities. Targets: search and lists under 1 s at the 95th percentile, Property Profile under 1.5 s, tax map view under 2 s, province-wide general revision within one night as a background job |
| Q8 | End-to-end tests | Playwright, committed under `frontend/prime-web/e2e/`, running against DevAuth and a seeded DEMO database. No frontend unit-test framework for now |
| Q9 | CI | GitHub Actions on push and pull request (build, tests with a PostGIS container, frontend build and lint, vulnerability and secret scans); E2E on demand and before releases. Confirm the GitHub repository is private |
| Q10 | Accessibility standard | WCAG 2.1 AA as the review target: automated axe checks in the E2E suite plus a recorded manual keyboard pass of the main workflows. Fix serious and critical findings; list the rest |
| Q11 | Frozen treasury code | Leave it frozen and hidden (`treasury.legacy` granted to no one); include its tables in the delete guards; removal stays a separate decision (§0) |
| Q12 | Database TLS | Verify the server certificate (`SSL Mode=VerifyFull` with Supabase's root certificate) in staging and production |
| Q13 | Production logs | Serilog to the console for the host's collector plus a rolling file kept 90 days; `/health` failures alert the ICT office (by the host's monitor). Audit records stay in the database indefinitely |
| Q14 | Order of the steps | H1 → H2 → H3 → H4 → H5 → H6 → H7 → H8. H6 and H7 move earlier if the hosting decision (Q1) comes quickly |

### 8.1 Decisions

2026-10-08, the user: Q3–Q14 accepted as recommended. Q1 and Q2 decided as follows.

- **Q1 — Vercel.** The SPA goes on Vercel (a static `vite build` with an SPA rewrite to `index.html`). Vercel has no
  first-party .NET runtime; it can run the API only as a container on Vercel Functions, which scale to zero when idle.
  PRIME's API runs Hangfire background jobs (general revision, §72–§73), which need an always-running process, so the
  API is not put on Vercel Functions. **API host: Render** (user, 2026-10-08): a Docker web service in the Singapore
  region on a paid, always-on instance (a free instance spins down when idle, which stops background jobs). H6
  delivers the Dockerfile and Render settings: secrets as Render environment variables (pooled connection string,
  Supabase URL and anon key, CORS origin of the Vercel URL); migrations as a manual release step over the direct
  connection; `Security:KnownProxies`/forwarded headers and HSTS checked behind Render's TLS proxy; `/health` as
  Render's health check; logs to Render's log stream, since the container disk is ephemeral; and the LGU content
  root on a persistent disk or uploaded through the admin screens. With the SPA and API on different origins, H6
  sets the production CORS origins, the SPA's API base URL and the CSP `connect-src` accordingly.
- **Q2 — Supabase plan unchanged for now.** Consequences recorded for H7: if the project is on the Free plan, Supabase
  keeps no restorable backups and pauses a project after a week of low activity. The province-held `pg_dump` (§4.2) is
  then the only backup, and the restore drill uses it alone. Moving to a paid plan stays a go-live condition, to be
  revisited before go-live.

## 9. Implementation log

### H1 — Database safety (done 2026-10-08)

- **Delete guards.** Migration `HistoryDeleteGuards`: function `prime_history_no_delete()` and triggers
  `history_no_delete_row` (BEFORE DELETE, per row) and `history_no_truncate` (BEFORE TRUNCATE) on 77 history tables.
  Two tables carry a conditional row trigger instead, for the two designed deletes found in the code: `RegisterRuns`
  (a monthly run with no entries is removed before issue; refused once an `IssuedForms` row names the run) and
  `SwornStatementItems` (removable while the statement is a Draft). Refusals raise SQLSTATE 42501. The other 94
  tables (reference, configuration and working data such as drafts, studies and analyses) are listed as unguarded in
  `HistoryDeleteGuardTests`; a new table fails that test until it is classified.
- Before the triggers, every application delete was traced (`Remove`, `RemoveRange`, `ExecuteDelete`). The
  `IssuedForms` remove in `FormService` only detaches unsaved rows after a lost race; it never reaches the database.
  The full suite passing with the triggers in place shows no flow deletes history through EF orphan removal either.
- `PropertyRegistrationFlowTests` committed rows and deleted them afterwards, which the guards now refuse. It reuses
  one fixed `TEST_` province and reference set instead, and its per-run rows stay, identified by the run's id.
- **Schema review** (catalog queries): every foreign key column leads an index. Uuid references without a foreign key
  are polymorphic by design (`SubjectId`, `RecordId`, `SourceId`, `RuleId`, `EntityId`, `RelatedEntityId`,
  `ImportBatchId`), external (`SupabaseUserId`), the audit log (no foreign keys by design) or frozen treasury
  (`CashierUserId`), except four real gaps, now fixed by migration `ReferenceForeignKeys` (restrict):
  `ApprovalRecords.UserId` → `AppUsers`, `ApprovalRecords.SignerOfficeId` → `Offices`,
  `NoticeOfAssessmentItems.RpuId` → `RealPropertyUnit`, `NoticeOfAssessmentItems.TaxDeclarationId` →
  `TaxDeclarations`. No orphans in the dev database; **check Supabase for orphans before applying it there.**
- Effective-dated columns without an index: `Valuations.EffectiveDate`, `PropertyExemptions.EffectiveDate`, and the
  dates of general revision programmes and jobs and territorial change jobs. Those tables are reached through indexed
  foreign keys today; whether a date index helps is measured in H4 with the volume set, not guessed here.
- **Health.** `background-jobs` (Hangfire: Unhealthy with no server, Degraded with failed jobs) and `database-tls`
  (Degraded when a remote database is reached with an `SSL Mode` other than `VerifyFull`; healthy for a local
  database). The staging string (`SSL Mode=Require;Trust Server Certificate=true`) reports Degraded until it carries
  Supabase's root certificate: download it from the Supabase dashboard (database settings, SSL), keep it outside the
  repository, and set `SSL Mode=VerifyFull;Root Certificate=<path>`. Done in H6 with the deployment.
- Verified: 794 tests pass (Domain 286, Application 56, Integration 452; new: 6 guard tests, 6 TLS cases, and the
  health test asserts `background-jobs`); live `/health` on the dev API shows all five checks Healthy; a direct
  `DELETE` of a TD in `prime_dev` is refused. Migrations applied to the local database only (81); Supabase stays at
  79 until the user asks.

### H2 — Concurrency (done 2026-10-09)

- **Row versions.** `IVersioned` (`Prime.Domain.Common`) with `uint RowVersion`; one model convention in
  `PrimeDbContext.ApplyRowVersions` maps it to PostgreSQL's `xmin` for every implementing table, so no physical
  column is added. 42 tables: every `EffectiveDatedConfiguration` (numbering schemes, form definitions, approval
  chains, adjustment factors, ceilings, exemption and transaction types, SMV building tables, office jurisdictions and
  assignments, checklist definitions), assessments, assessment levels, valuations, TDs and their cancellation
  requests, property exemptions, property transactions, discovery summonses, SMVs and schedules, SMV preparations,
  approval delegations, offices, notices of assessment and of cancellation, sworn statements, general revision
  programmes, independent appraisals, revenue impact studies, taxpayers, properties, parcels, land, buildings,
  machinery, RPUs, sign-up requests, role-permission changes and assessment-roll submissions. Migration
  `RowVersions` is model-only: `dotnet ef migrations script` emits no DDL (the `ParcelConcurrencyToken` precedent).
  The property is `RowVersion`, not `Version`, because `FormDefinition.Version` is the form's version number; Parcel's
  token was renamed to match, and its API field stays `version`.
- **Not versioned, on purpose:** background-job rows (general revision jobs, territorial change jobs, SMV simulation
  and valuation test runs). Their workers save progress repeatedly while a user may cancel; a row version would make
  the worker's progress save fail instead. A user's cancel racing a worker is therefore not covered by H2; it is
  looked at with the volume runs in H4. The frozen treasury tables keep what they had (Payment's row version).
- **Conditional transitions.** With the row version, EF adds `WHERE xmin = @loaded` to every update, so of two
  requests that load the same record and both change it, the second matches no row and nothing it did is saved. The
  `DbUpdateException` catch sites that turn unique-index races into their own `_CONFLICT` codes now let a
  `DbUpdateConcurrencyException` through, and `ExceptionHandlingMiddleware` returns **409 `CONCURRENCY_CONFLICT`**.
- **If-Match.** A write to a record route (`{id}`) may carry the version the screen displayed (`If-Match: "42"`;
  `W/"42"` and a bare number are accepted, `*` checks nothing, a malformed value is 400 `INVALID_IF_MATCH`).
  `IfMatchFilter` (WebApi) puts it in the request's `IConcurrencyExpectation`. The first time the request reads that
  record from the database (`ConcurrencyMaterializationInterceptor`, a singleton reaching the request's expectation
  through `PrimeDbContext`), its version must match, otherwise 409 before any rule is evaluated. Checking at the read
  matters: a stale "Approve" on a TD someone else already approved was first refused as 400 "not pending review",
  which the screen did not treat as a reason to reload. A record the request saves without reading it is checked at
  the save (`ConcurrencyExpectationInterceptor`). Without the header nothing is compared (older clients, tests,
  background jobs); the row version still guards the time between load and save.
- **Limit.** A version changes when the row itself changes. An edit that only replaces child rows (e.g. a study's
  options) does not change the parent's version, so two such edits are not detected by If-Match; the parent's own
  fields, status and every workflow decision are.
- **DTOs and frontend.** `rowVersion` is returned on assessments, TDs, transactions, exemptions, offices, taxpayers,
  sworn statements, SMV preparations, impact studies, land, machinery and the approvals queue. `apiPost`/`apiPut`
  take `{ ifMatch }`; the screens send it on assessment, TD and transaction steps, exemption decisions, office edits,
  taxpayer details, sworn statement save/file/cancel, SMV preparation save/cancel, impact study save, and land and
  machinery input edits. A `CONCURRENCY_CONFLICT` from any mutation reloads every query (`MutationCache` in
  `main.tsx`); the screen's own error alert shows the API's message.
- **Found and fixed:** `ExceptionHandlingMiddleware` serialized `ApiError` in PascalCase (`"Code"`, `"Message"`), so
  the frontend never saw the code or message of a middleware error (domain-rule 400s and 500s before H2). It now
  writes camelCase like the controllers.
- **Number allocation.** Every number (TD, NOA, FAAS, PIN, transaction) comes from the one `NumberSequenceAllocator`
  (upsert with a row lock); one parallel test covers it: 12 concurrent issuers in one scope get 1–12, no gap, no
  duplicate.
- Verified: 805 tests pass (Domain 286, Application 56, Integration 463; new `ConcurrencyTests`: every workflow and
  editable table carries an `xmin` token, If-Match parsing, four parallel approvals of one TD give exactly one 200
  and one APPROVE audit row (observed `[200, 409, 409, 409]`), a stale and a malformed If-Match are refused and change
  nothing, a second editor of a taxpayer is refused instead of overwriting, parallel numbering). In the browser
  (Playwright against `vite dev` and the API, after `tsc -b` and lint): a checker's screen showing a TD pending
  review, approved meanwhile by someone else, gets the 409 message, the list reloads to Approved and the Approve
  button disappears; an uncontested approval sends `If-Match` and succeeds. Migration applied to the local database
  only (82); Supabase stays at 79 until the user asks.

### H3 — Calculation matrix (done 2026-10-09)

- **Matrix.** The §75 cells (zero, large amounts, decimal values, rounding, bracket boundaries, depreciation limits,
  adjustments, several classifications on one unit, historical rules, determinism) were walked per kind against the
  existing tests. `docs/TESTING.md` §2 names the test for each cell. The holes were filled by
  `Prime.Domain.Tests/DomainServices/CalculationMatrixTests` (19) and `Prime.IntegrationTests/CalculationMatrixTests` (12).
- **Rules register.** `docs/BUSINESS-RULES.md` (R1–R23): each calculation rule with its basis, effective date and
  jurisdiction, system representation and status. The open domain questions ([C3]–[C6], rounding, the adjustment
  floor) are marked DVR there.
- **Found and fixed: centavo rounding.** The engine computes at full precision; only PostgreSQL rounded a row to the
  centavo, on insert into `numeric(18,2)`. Both general revision runners value a unit and then assess it in the same
  `DbContext`, so EF returned the tracked, unrounded valuation, while a manual assessment of the same valuation read
  the stored, rounded value. The two could differ by a centavo of assessed value, and a value a fraction of a centavo
  over a bracket's upper limit took the next level in one path and not in the other (1,000.31/sqm × 999.6901 sqm =
  1,000,000.003931: 30 % in the general revision path, 20 % on its stored value). Stored rows also did not always add
  up to the stored total. Now `Money.ToCentavo` (Domain, half away from zero) is applied to every row by
  `ValuationCalculator.ToCentavo`, after any configured step, through `ValuationService.Rounded`, which every row
  passes through. The total is the sum of the rounded rows, and the breakdown keeps the value before rounding. The
  assessed value uses the same helper, as does the FAAS machinery row, which had used banker's rounding for its
  depreciation figures. The two regression tests fail with the step removed and pass with it. Posted valuations are
  not recomputed (CLAUDE.md §97).
- **Found and fixed: out-of-range amounts.** No input is bounded above, so a mistyped area could produce a value beyond
  `numeric(18,2)` (PostgreSQL 22003 at save) or beyond `decimal` (`OverflowException`), and either was a 500. Now
  `Money.Checked` refuses a row or total beyond 9,999,999,999,999,999.99 with `ValueOutOfRangeException` (a
  `DomainException`: 400 `VALUE_OUT_OF_RANGE`, nothing saved). `ExceptionHandlingMiddleware` maps an
  `OverflowException` or SQLSTATE 22003 to the same 400 as a safety net. The general revision programme runner now
  records a `DomainException`'s own message on the failed item instead of the generic one.
- **Found and fixed: adjustments below −100 %.** Each factor is limited to more than −100 %, but their sum is not.
  Two −60 % factors made a negative market value, which the `CK_ValuationLines_MarketValue` check refused with a 500.
  Now refused as `ADJUSTMENT_TOTAL_OUT_OF_RANGE`, naming the factors. Whether a floor applies instead is DVR.
- **Left for H4:** the older `GeneralRevisionJobRunner` (Phase 6) has no per-unit catch, so one unit's exception
  stops the whole job. It is looked at with the job runs at volume. The unused private `ValuationService.Persist`
  (single-row) is left as it was.
- Verified: 836 tests pass (Domain 305, Application 56, Integration 475). No migration and no frontend change: the
  breakdown key `MarketValueBeforeRounding` already existed for the rounding step, and the valuation screen labels it
  generically ("Market value before rounding", up to six decimals). Not checked in a browser, since no screen changed.
- **Supabase brought up to date (2026-10-09):** `HistoryDeleteGuards`, `ReferenceForeignKeys` and `RowVersions`
  applied (79 → 82, same as local). Checked first: no orphan rows for the four new foreign keys. Afterwards: delete
  triggers on 79 tables, all four foreign keys present. From now on each step's migrations are applied to the local
  database and to Supabase as part of the step (user instruction, 2026-10-09).

### H4 — Volume and performance (in progress, 2026-10-09)

- **Volume set.** `generate-volume [n]` (`Prime.WebApi/Commands/GenerateVolumeCommand.cs`) builds a separate local
  database, `prime_volume`, from a copy of `prime_dev`. It refuses to run unless the environment is Development, the
  host is local and the database name contains "volume". The set is 250,000 DEMO land properties and 400,000 RPUs with
  approved TDs and PIN assignments in 16 DEMO municipalities, with DEMOVOL configuration (SMV `DEMOVOL-SMV-2027`, levels,
  numbering) and about 1.2 million audit rows. It takes 11.5 minutes and makes a 3.5 GB database. It never runs against
  Supabase.
- **Measurements.** `tests/perf/measure.mjs` (Node) times each case 20 times with different inputs from
  `tests/perf/samples.sql`, after one warm-up call, and reports p50, p95 and max. The figures are in `docs/TESTING.md` §3.
- **Found and fixed: search.** Property search (PIN, lot, title, survey and tax map numbers) and owner search (names, TIN) used `ILIKE '%…%'` over unindexed columns: 0.75–1.2 s per
  search, and the full count ran on every page. Now trigram indexes (`pg_trgm`, migration `SearchTrigramIndexes`), a
  count capped at 10,000 (`PagedResult.TotalIsLowerBound`, shown in the UI as "more than 10,000"), and the sort chosen
  by the capped count (PIN order when there are 5,000 matches or fewer). PIN fragment search: p95 1,007 → 92 ms;
  no match: 1,481 → 19 ms.
- **Found and fixed: audit trail.** Page 1 counted all 1.2 million rows (p95 722 ms) and a record's history scanned the
  table. Now the capped count and an index on `AuditLogs.RecordId` (migration `AuditRecordIndex`): page 1 p95 32 ms,
  a property's history 36 ms. Deep paging into one table (page 100) stays at p95 about 0.75 s: offset paging over a large
  table, still under the 1 s target.
- **Found and fixed: registers.** The assessment roll of a barangay loaded each unit's parties, PIN and values one query
  at a time: 140 s at volume. Now one prefetch per run (`Registers.cs`, `Prefetched`; `PropertyParties.Rows`/`Ordered`;
  `UnitPin.ForUnit`): 2.5 s for the run.
- **Found and fixed: general revision runners slowing down.** Both runners kept every loaded entity tracked, and each save
  scanned the whole change tracker. On a 3,200-unit DEMO run the rate fell from 311 to 74 units a minute within ten
  minutes. They now clear the tracker after each item and keep a steady rate.
- **Found and fixed: a run executed twice at the same time.** Hangfire.PostgreSql hides a fetched job for 30 minutes
  (its invisibility timeout) and then gives it to another worker, whether or not the first is still running. The
  province-wide Value run was resumed at the 30th and 60th minute while it was still running. From then on two runners
  worked through the same items, with a transaction error and one unit drafted twice. Now `UseSlidingInvisibilityTimeout`
  (`Infrastructure/DependencyInjection.cs`): the worker renews its claim (seen renewed every 6 minutes) and the job is
  handed on only when its server stops.
- **Found and fixed: resuming.** Hangfire runs a job again when its server stops mid-run. The programme runner restarted
  the run from the beginning; it now resumes. A Value run skips the items it finished. A batch action keeps only the
  items still in the state it acts on, and the counts are rebuilt. A run that already finished is not run again. If a
  run stopped after drafting a unit's assessment but before saving the item, the resumed run cancels that draft instead
  of leaving a second one. The older `GeneralRevisionJobRunner` (Phase 6) now catches an error per unit, so one unit
  no longer stops the job. Verified at volume by stopping the API mid-run three times; each time the run resumed.
- **General revision rate.** Province-wide programme over the volume set (one runner, local database): Compile of
  400,000 units in 5.5 minutes; Value at about 760 units a minute (900 in a sampled window), so 400,000 units take
  about 9 hours. EF command logging over 150 units: 23 statements and about 30 ms of database time per unit. There is
  no N+1 query and no repeated loading of static data; the largest share is the three audit inserts. The rest is
  application time. Each unit goes through the ordinary valuation and assessment services, so every check applies per
  unit (Q14), and is saved on its own so the run can resume. A large speed-up would need several workers on separate
  partitions of a run, a design change left until a measurement on the hosted stack (H6) shows the night is not
  enough. On Render with Supabase, each statement crosses the network, so the rate will be lower than measured here.
- **Effective-date indexes (from H1).** Not added: no measured query at volume was limited by an effective-date filter.
- **Still to do in H4:** after the Value run: submit one municipality's items and open the approval queue, post one
  barangay and run its assessment roll, then a final run of `measure.mjs` against the posted volume set; then
  `docs/TESTING.md` §3 completed and the roadmap.

### H5 — Regression suite, CI, API and UI review (in progress, 2026-10-09)

- **End-to-end suite.** Playwright 1.63 with axe (`@axe-core/playwright`, `playwright-core` pinned to one version by an
  npm override), committed under `frontend/prime-web/e2e/` with `playwright.config.ts` and `tsconfig.e2e.json` (so
  `tsc -b` checks it). 17 tests, all passing, about 3.5 minutes: the §74 flow on screen, the four access scenarios of
  §4.7, axe on 11 screens, and a keyboard-only registration. Details and how to run them: `docs/TESTING.md` §4.
- **DEMO E2E set.** `seed-e2e` (`Prime.WebApi/Commands/SeedE2eCommand.cs`): idempotent, Development and local database
  only; distinct "DEMO E2E" names, because `prime_dev` holds hundreds of identically named test classifications and a
  test cannot pick one on screen. The PIN and TD numbers are typed in the tests, so the set needs no numbering scheme.
- **Fresh applicants.** The development sign-in now accepts `applicant-<anything>` as a new applicant who has never
  signed in, with an id derived from the key (`DevelopmentAuthenticationHandler.FreshApplicant`). A sign-up decision
  cannot be undone, so the fixed `applicant` could try the sign-up screens only once (in `prime_dev` it is disabled).
  Like the rest of the bypass it exists only in Development.
- **CI.** `.github/workflows/ci.yml` (Q9): backend build, migrations and tests against a PostGIS 16 container, vulnerable
  package check; frontend type-check, build, lint and audit; gitleaks over the history; the end-to-end suite on demand.
  The API's Development settings file is not in git, so the jobs pass the connection string and `DevAuth__Enabled` as
  environment variables. Not run yet: it needs a push. Whether the integration tests pass on a database built from
  migrations alone had never been checked. First runs (2026-10-09): migrations applied, 22 integration tests failed on
  the empty reference data; reproduced on a fresh local `prime_ci` (PostGIS created by the `postgres` superuser). CI now
  runs `seed-e2e` before the tests, and a test that assumed the order of two same-transaction payments ignores it. Run
  `a619919`: **all jobs green** (the end-to-end job is on demand and has not been run on GitHub yet).
- **Found and fixed (UI): actions offered to users who cannot take them.** The maker (an encoder) was shown Approve on
  an assessment and on a TD, and the click ended in a 403. Assessment submit, approve, reject and post and TD submit,
  approve and reject now show only to a user whose roles hold the API's permission for them (`useCan`). Separation of
  duties stays with the API: the assessor who prepared an assessment still sees Approve and is refused, with the reason
  shown, which the suite checks. Extended the same day to the other decision actions: configuration approvals
  (`ApproveButton`, transaction and exemption types, checklist steps: `config.approve`), office jurisdictions,
  assignments and delegations (`users.approve`), exemption claims (`exemption.approve`), property transactions
  (`transaction.prepare` / `transaction.approve`) and issuing notices (`notice.issue`). Role-permission changes, user status
  changes and sign-up decisions were already gated; the frozen collection screens are left as they are.
- **Found and fixed (UI): a new taxpayer shown as its id.** After "Register a new taxpayer" in the party dialog, the
  Taxpayer field showed the raw id; the new taxpayer is now an option and shows by name.
- **Found and fixed (UI): unit values never shown.** The land and building panels read the records' own market and
  assessed value fields, which nothing writes (the values live in the valuations and assessments), so they always said
  "Not yet valued/assessed". They now show the unit's current posted assessment.
- **Found and fixed (UI): the parcel dialog** asked again for the province, municipality and barangay; it now proposes
  the property's.
- **Found and fixed (UI): sideways scroll.** With a unit expanded, the property profile was 1,658 px wide at any window
  width (the units table grew to its widest content). The units table now has a fixed layout and the tables inside a
  unit scroll within it; no horizontal page scroll at 1,400, 1,280 or 1,024 px.
- **Found and fixed (UI): raw status names** ("PendingReview") in the assessment, TD and transaction tables; one
  `WorkflowStatusTag` with readable labels and one colour per status.
- **Found and fixed (accessibility): contrast** on 10 of 11 screens (§5 of `TESTING.md`); none left.
- **Logs (Q13).** Serilog now also writes a daily rolling file, `logs/prime-<date>.log`, 90 kept, shared between processes
  (`appsettings.json`); `logs/` is ignored by git. On Render the container disk is ephemeral: H6 points the path at a
  persistent disk or relies on the console stream. A scan of the file found no personal data, token or password.
- **Reviewed, no change needed:** Swagger is served only in Development; list endpoints over growing tables are paged or
  capped (approval queue and register runs at 200, others scoped to one record or a date range). EF warns that the H4
  capped count uses `Take` without `OrderBy`; counting does not depend on order, so it is harmless.
- **Still to do in H5:** run the end-to-end job on GitHub once; a person's manual keyboard pass; printable views in
  Chrome and Edge.
