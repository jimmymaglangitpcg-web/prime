# Phase 12 — Workflow and security (design)

| | |
|---|---|
| Phase | 12 (CLAUDE.md §99; roadmap "Phase 12 — Workflow & Security"); next after Phase 10 by the phase order of 2026-10-02 |
| Status | Done (2026-10-08): P12-1 to P12-5; decisions in §8.1 and §8.2 |
| Sources | CLAUDE.md §9, §45–§49, §63, §66–§69, §104, §106; `docs/ARCHITECTURE.md` §3.4 (Supabase Auth + PRIME RBAC); the Data Privacy Act (RA 10173) and RA 12001's penalties for unauthorized processing of RPIS data (CLAUDE.md §68) |
| Depends on | LP (offices, office assignments with roles, approval chains, delegations, jurisdiction) |
| Feeds | Phase 13 (imports run under these permissions), Phase 14 (production hardening reviews what this phase builds), Phase 15 (the manual's administration chapter) |

No legal source fixes which role may do what in PRIME. The default role–permission matrix shipped here is a
**provisional default**, configurable by the administrator under maker-checker, and is DOMAIN VERIFICATION REQUIRED
against the province's actual positions and delegations (CLAUDE.md §9).

## 1. Scope

The roadmap's Phase 12 tasks:
- the full role–permission matrix for the roles of CLAUDE.md §9, enforced;
- maker-checker on every transaction listed in §46;
- an audit-trail completeness review;
- a hardening pass (§67): HTTPS, lockout, password policy, input validation, injection, XSS and CSRF, rate limiting,
  session expiry, secret storage;
- a personal-data exposure review (§68);
- a Supabase review: RLS deny-by-default on every `public` table, Storage bucket policies, the service-role key kept
  server-side;
- real sign-in;
- `docs/SECURITY.md`.

## 2. What PRIME has

| Area | PRIME today |
|---|---|
| Authentication | Outside Development the API validates Supabase-issued JWTs (ES256, JWKS by discovery; issuer, audience and lifetime checked). In Development a bypass handler treats every request as a DEMO user, switchable by the `X-Prime-Dev-Act-As` header (usual user, checker, municipal users). Every controller inherits `[Authorize]` from `ApiControllerBase`. |
| Users | `AppUser` is created on first sight of any valid Supabase token (just-in-time provisioning), active, with no office. `AppUser.Status` is not checked on requests. |
| Roles | The 9 roles of §9 are seeded. Roles are held per **office assignment** (LP: `OfficeAssignment` + `OfficeAssignmentRole`, effective-dated, approved by a second user). The older `UserRoles` table is unused (0 rows). |
| Permissions | `Permission` and `RolePermission` tables exist, empty. **No endpoint checks a role or a permission.** Roles are only read by approval chains (a step's required role) and the delegation rules. |
| Jurisdiction | Records are filtered by the user's office jurisdiction; province-wide-only writes return `_FORBIDDEN` (403). |
| Maker-checker | Enforced in the services, always on: assessments, TDs, transactions, SMVs and their rows, assessment levels, territorial changes, office assignments, delegations, exemption claims, signatures, and configuration through a shared `ConfigurationApproval` helper (factors, building tables, indices, forms, numbering, chains, content packs …). Approval chains route approvals per office with provincial approval unless delegated. |
| Audit | `AuditSaveChangesInterceptor` writes one `AuditLog` row (user, table, record, old and new values, IP, reason) in the same save for every change to an `IAuditable` entity. Approvals, rejections, postings and cancellations are logged as **Update**. Child rows that are plain `Entity` (e.g. assignment roles, scopes, lines) are logged only through their parent's change, if at all. Reads and exports are not logged. There is no audit screen. 8,333 rows in the dev database. |
| Sign-in UI | None. The frontend attaches the Supabase session's token if there is one; locally everything runs on the bypass. |
| Hardening | HTTPS redirection; CORS from configuration; a uniform error shape without stack traces; the Hangfire dashboard only in Development; forms render under a restrictive CSP. No rate limiting, no HSTS or security headers, no request-size limits beyond the defaults, no dependency vulnerability check in the workflow. |
| Secrets | `appsettings.*.json` and `.env*` are gitignored; only `appsettings.json` (no secrets) is tracked. The Supabase service-role key appears only as an empty variable in `.env.example`. |
| Supabase | RLS is on for all 172 `public` tables except PostGIS's `spatial_ref_sys`, with 0 policies (deny-by-default for the anon and authenticated keys). Verified after the last migration. |
| Documents | `Document` metadata exists; storage is not built (no upload path, no bucket). |

## 3. Gaps

| # | Gap | Risk |
|---|---|---|
| G1 | Any authenticated user can call any endpoint: no role or permission check | A VIEW_ONLY or AUDITOR user can approve, post, import or change configuration |
| G2 | Anyone who can obtain a Supabase token (e.g. by self sign-up, if enabled in the project) is provisioned as an active user; disabled users are not refused | Access without being invited; no way to cut a user off |
| G3 | No sign-in, sign-out, password reset or MFA screens | PRIME cannot run outside Development |
| G4 | Approvals and other state changes are audited as plain updates; some child rows are not audited; exports and record prints are not logged; no audit viewer | §48's APPROVE/REJECT/POST/VOID/CANCEL/EXPORT/LOGIN not distinguishable; auditors must query the database |
| G5 | No rate limiting, no HSTS or security headers, default upload limits | Brute force against the API, clickjacking of the SPA host, oversized uploads |
| G6 | Taxpayer personal data (TIN, contact details, address) is returned to every user who can see the property | §68 / Data Privacy Act exposure |
| G7 | Maker-checker skips its check when the acting user is unknown (`AppUserId` null) | Today only background jobs (which act as their starter); becomes a hole if a request is ever unattributed |
| G8 | Dead weight: `UserRoles` (unused) | Confusion about where roles live |

## 4. Proposal

### 4.1 P12-1 — permissions, enforced on every endpoint

- **Permission catalogue in code.** A permission is a capability the code checks, e.g. `property.edit`,
  `assessment.approve` or `config.approve`. The codes are constants in `Prime.Application` and are seeded into
  `Permissions` with a name and module. They are not LGU rules. About 40 permissions, by module and verb rather than
  one per endpoint (Q1): `view`, `edit` (prepare), `approve`, `post`/`issue` and `manage`, plus a few special ones
  (`taxpayer.view-personal`, `records.export`, `audit.view`, `users.manage`).
- **Role–permission matrix as configuration.** `RolePermissions` holds which role has which permission. A provisional
  default ships as DEMO configuration (§8 Q2 lists it). The administrator changes it on an admin screen, and a change
  takes effect when a second user approves it (§46).
- **Where roles come from.** A user's permissions are the union over the roles of their office assignments in force
  today (LP), so a role is held within an office and its jurisdiction (§117). `UserRoles` is retired; the table is
  kept empty and unused, then dropped later with the user's approval (Q3).
- **Enforcement.**
  - Every controller action declares its permission with `[RequirePermission("…")]`. A policy provider resolves it
    against the user's permissions (cached for the request) and refuses with 403 and a §63 error
    (`PERMISSION_DENIED`).
  - The default authorization policy becomes **deny unless an action declares a permission**: an action without one is
    refused, and a test fails the build.
  - Reads need the module's `view` permission (Q4).
- **Unchanged.** The services keep their own checks: maker-checker, approval-chain roles, delegation and jurisdiction.
  The permission check runs in front of them; a permission never overrides them.
- **Tests.** A test enumerates every action and asserts it declares a permission. A matrix suite has each role call
  one gated action per module and asserts allowed or 403. The DEMO dev users get assignments matching their roles, and
  the dev act-as switch keeps working.

### 4.2 P12-2 — users and sign-in

- **Self sign-up, approved by a system administrator** (Q5, decided).
  - Anyone may sign up in the Supabase project (e-mail confirmation required). The first sign-in provisions an `AppUser`
    with status **Pending** and no office assignment.
  - A pending user can only read `GET /api/me` and submit a **sign-up request**: name, position, the office they work
    in, the roles they ask for, and a note. The screen says "awaiting approval".
  - A **SYSTEM_ADMIN** reviews the request (worklist and dashboard count) and approves it, choosing the office and roles
    (which may differ from those asked), or rejects it with a reason.
  - Approval activates the account and creates the office assignment with its roles, **in force at once**: the
    applicant is the maker and the approving administrator the checker. A SYSTEM_ADMIN cannot approve their own
    sign-up.
  - Later changes to a user's offices and roles follow LP's office assignments (a second user approves).
  - Rejected and pending requests are kept with their history; a rejected user may submit again.
  - Bootstrap: the first SYSTEM_ADMIN of a new installation is created by a documented one-off command, not through
    the API.
- **Disabled users refused.** An `AppUser` with status Inactive is refused (401 with `USER_DISABLED`) on every
  request, whatever their token. Disabling is an administrator action under maker-checker, effective at once (Q6).
- **Frontend sign-in** with the Supabase client:
  - sign-in by email and password;
  - sign-out;
  - password reset by email link;
  - TOTP MFA enrolment and challenge, required for the roles the setting `Security:MfaRequiredRoles` lists (default:
    SYSTEM_ADMIN and ASSESSOR; Q7). The API refuses those roles' requests when the token's assurance level is not
    `aal2`.
- **Supabase project settings.** Lockout, password policy and the token lifetime are Supabase Auth settings. PRIME
  documents the required values in `docs/SECURITY.md` (minimum length, leaked-password check, rate limits on sign-in,
  access-token lifetime) and checks what it can at start-up (Q8).
- **Session.** The access token expires per Supabase (default 1 hour) and is refreshed by the client. An idle timeout
  (`Security:IdleMinutes`, default 30) signs the user out in the browser (Q9).
- **Sign-in events.** The frontend reports sign-in and sign-out to `POST /api/session` and the API logs them as
  LOGIN/LOGOUT audit rows. Supabase keeps its own auth log; PRIME's row ties the event to the `AppUser`.
- **Development** keeps the bypass, only in the Development environment and only with `DevAuth:Enabled` (as now).

### 4.3 P12-3 — audit completeness

- **Actions named.** When a save changes an entity's workflow status, the interceptor logs it as APPROVE, REJECT,
  POST, VOID or CANCEL instead of UPDATE (still one row, same transaction). A service can set the reason through the
  existing `ICurrentUserService.Reason`, and rejecting and cancelling already require one in most services; the review
  lists those that don't.
- **Every table audited** (Q10). Plain `Entity` children are logged too, with their parent's id, so a change to an
  assignment's roles or a transaction's requirements is visible. Exclusions are listed and justified: the audit log
  itself, Hangfire tables, frozen cached results such as simulation result lines (written by jobs in bulk; the run
  is audited) and the EF history table.
- **Exports and prints.** Issuing or printing a form, running a register and every CSV/Excel download write an EXPORT
  row: what, which record, the format and the user. Viewing a record is not logged (Q11).
- **Audit viewer.** For `audit.view` (AUDITOR, SYSTEM_ADMIN): filter by user, table, record, action and date; open a
  row's before and after values. Plus a record's history on the Property Profile (§50 "Audit History") and on SMVs,
  TDs and transactions. Read-only, paged on the server.
- **Append-only, protected.** No API path changes or deletes audit rows. In the database the application role is
  denied UPDATE and DELETE on `AuditLogs` by a migration trigger (Q12).

### 4.4 P12-4 — hardening

- **Rate limiting** (ASP.NET Core rate limiter), per user, or per IP before sign-in: a general limit, a strict one on
  uploads, imports and exports, and settings for the numbers (Q13).
- **Headers** outside Development:
  - HSTS;
  - `X-Content-Type-Options: nosniff`;
  - `Referrer-Policy: no-referrer`;
  - `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` on API responses;
  - for the SPA host, a recommended header set in `docs/SECURITY.md`.
- **Uploads.** Request-size limits per endpoint (content packs, market data CSV); content-type and extension checks;
  zip entries checked against path traversal (already done for content packs — reviewed).
- **CSRF.** Not applicable while the API takes bearer tokens only and never cookies; recorded so, with a test that a
  cookie-only request is refused.
- **Injection and XSS.** EF Core parameterises queries; the review lists every raw SQL call and checks it is
  parameterised. The forms renderer's CSP stays, and Liquid output is escaped by default (reviewed).
- **Dependencies and secrets.** `dotnet list package --vulnerable` and `npm audit --omit=dev` are run and recorded.
  The git history is scanned for secrets (connection strings, keys, JWTs); a finding is rotated, not just removed.
- **Hangfire dashboard.** Stays Development-only. Elsewhere it is off (Q14).
- **Supabase.**
  - Confirm RLS deny-by-default on every `public` table and that new tables inherit it (a check in the migration
    workflow).
  - Confirm that requests with the anon key to the REST and GraphQL APIs fail on taxpayer tables (exit criterion).
  - Storage policies are reviewed when document storage is built (Q15).

### 4.5 P12-5 — personal data

- **Masked by default.** TIN, contact number, e-mail and the full address of an individual taxpayer are returned masked
  (e.g. `***-***-123`) unless the user has `taxpayer.view-personal` (Q16). Names stay visible: they are on the TD and
  the rolls, which are public records.
- **Exports** that contain personal data need `records.export` and are logged (P12-3).
- **Logs.** The review checks that structured logs carry ids, not names, TINs or tokens; the request logger drops
  bodies and the `Authorization` header.
- **The dev act-as header** is ignored outside Development (already so) — covered by a test.

### 4.6 Workflow (CLAUDE.md §45–§46)

- **What stays as built.** Maker-checker is always on in PRIME; §46's "where configured" is met by the approval
  chains, which decide who approves.
- **Changes:**
  - **G7:** a sensitive approval is refused when the acting user is unknown. Background jobs already act as their
    starter.
  - **Missing approvals:** the review walks §46's list against the code and adds approval only where a sensitive
    change has none (expected: none or few; listed in the log).
  - **Status names:** the review lists every workflow status set and checks §45's statuses are used consistently. It
    does not rename existing statuses (Q17).

## 5. Data and migrations

- Seed `Permissions` (catalogue) and a provisional `RolePermissions` default (marked DEMO; Q2).
- `AuditLogs`: trigger denying UPDATE and DELETE; index on (`TableName`, `RecordId`) and (`UserId`, `Timestamp`) for the
  viewer.
- No change to `UserRoles` beyond leaving it unused (drop later, with approval; Q3).
- Settings: `Security:MfaRequiredRoles`, `Security:IdleMinutes`, `RateLimits:*`.

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| P12-1 | Permission catalogue, matrix (configurable, maker-checked), `[RequirePermission]` on every action, deny-by-default, permission tests | L |
| P12-2 | Users: self sign-up with SYSTEM_ADMIN approval (office and roles), pending users limited, disabled users refused; sign-in, sign-out, reset, MFA screens; idle timeout; sign-in events | M |
| P12-3 | Audit: named actions, child tables, exports, viewer, record history, append-only trigger | M |
| P12-4 | Hardening: rate limits, headers, upload limits, CSRF note, injection review, dependency and secret scans, Supabase checks | M |
| P12-5 | Personal data masking, export permission, log review; `docs/SECURITY.md` | S |

## 7. Exit criteria (from the roadmap, made concrete)

1. The permission suite proves, for each role, one allowed and one refused action per module, and that every API
   action declares a permission.
2. A self-approval of an assessment, a TD and a configuration version is refused; an approval with no acting user is
   refused.
3. A user who signed up sees only "awaiting approval" until a SYSTEM_ADMIN approves the request with an office and roles, and then works at once; a SYSTEM_ADMIN cannot approve their own sign-up; a disabled user is refused at
   once; an ASSESSOR without MFA is refused.
4. An approval shows as APPROVE in the audit viewer with the user, the before and after values; a register run and a
   form print show as EXPORT; an UPDATE or DELETE on `AuditLogs` fails in the database.
5. A VIEW_ONLY user sees a taxpayer's TIN masked; a user with `taxpayer.view-personal` sees it.
6. Requests over the rate limit get 429; responses carry the security headers outside Development.
7. The git history scan finds no secret; the anon key cannot read a taxpayer table through Supabase's REST or GraphQL
   API.
8. `docs/SECURITY.md` describes all of the above and the Supabase project settings PRIME requires.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Permission granularity | By module and verb (~40), not one per endpoint |
| Q2 | Default matrix | Provisional, DOMAIN VERIFICATION: SYSTEM_ADMIN — users, offices, configuration (edit), audit view, no assessment approval; ASSESSOR — everything in assessment, approve and post, configuration approve; APPRAISER — properties, land, buildings, machinery, valuation, transactions (prepare), market data; ASSESSMENT_ENCODER — properties, taxpayers, TDs and transactions (prepare), no approve; ASSESSMENT_REVIEWER — view all, approve assessments and transactions (where the chain says), no configuration; GIS_OFFICER — parcels, maps, layers, PINs; REPORTING_OFFICER — view all, registers, forms issue, exports; AUDITOR — view all, audit view, no edits; VIEW_ONLY — view only, personal data masked |
| Q3 | `UserRoles` | Retire: roles come only from office assignments; drop the table later with your approval |
| Q4 | Do reads need a permission? | Yes, the module's `view`; jurisdiction filters still apply |
| Q5 | Self sign-up | **Decided:** on; a SYSTEM_ADMIN approves each account, choosing its office and roles in the same step (was: off, invited users only) |
| Q6 | Disabling a user | Administrator action, second-user approved, effective immediately; history kept |
| Q7 | MFA | Required for SYSTEM_ADMIN and ASSESSOR (setting); optional for others |
| Q8 | Password and lockout policy | Supabase Auth settings, documented with required values; PRIME does not store passwords |
| Q9 | Session | Supabase token lifetime (1 hour, refreshed) plus a 30-minute idle sign-out in the browser |
| Q10 | Audit of child rows | Every table, with listed and justified exclusions |
| Q11 | Logging reads | No (volume); exports, prints and register runs yes |
| Q12 | Protecting the audit log | Database trigger denying UPDATE and DELETE on `AuditLogs` |
| Q13 | Rate limits | General 300 requests/minute per user; uploads, imports and exports 10/minute; sign-in limits at Supabase; all settings |
| Q14 | Hangfire dashboard | Development only; job status is already visible in PRIME's own screens |
| Q15 | Document storage (§59) | Not built yet; a separate step (before Phase 13) with a private bucket, short signed URLs and policies reviewed then |
| Q16 | Personal data masking | TIN, contact, e-mail, full address masked without `taxpayer.view-personal`; names visible (public records) |
| Q17 | Workflow statuses | Keep existing status sets; the review checks consistency with §45 and lists differences, no renames |
| Q18 | Frozen billing and collection code | Gated like the rest (`treasury.legacy` permission, granted to no role by default), not extended (CLAUDE.md §0) |

### 8.1 Decisions (user, 2026-10-06)

- **Q5 changed:** self sign-up is allowed; every new account needs approval by a user holding the existing
  **SYSTEM_ADMIN** role (no new role). The approval activates the account and gives its office and roles in one step,
  in force at once; the applicant counts as the maker and the administrator as the checker (§4.2).
- **Q1–Q4 and Q6–Q18:** as recommended.

### 8.2 Question from P12-4 (decided 2026-10-08: as recommended; built in P12-5)

| # | Question | Recommendation |
|---|---|---|
| Q19 | A TD is cancelled directly by one user (`td.approve`, with a reason); §46 lists TD cancellation under maker-checker. How should it be checked? | Make the direct cancellation a request that a second user approves, like a delegation, with the reason kept on both. Cancellation through a property transaction stays as it is. Alternative: withdraw the direct cancellation and cancel only through a cancellation transaction. |

## 9. Implementation log

### P12-1 — permissions, enforced on every endpoint (2026-10-06)

Done; migration `RolePermissionChanges` applied to the local database only.

- **Catalogue** (`Prime.Application/Common/Security/Permissions.cs`): 40 permissions by module and verb, plus the
  never-granted `undeclared`. `PermissionCatalogSeeder` (all environments) keeps the `Permissions` table in step. A
  permission seen for the first time gets its provisional default grants (`Permissions.DefaultGrants`, Q2); existing
  grants are never touched, so approved changes stand.
- **Where permissions come from:** `IPermissionService` takes the roles of the user's office assignment in force today
  (LP) and joins `RolePermissions`, once per user per request. No assignment means no permissions. `UserRoles` is left
  unused (Q3).
- **Enforcement.**
  - Every one of the 395 API actions carries `[RequirePermission(…)]`; only `GET /api/me` is `[SignedInOnly]`.
  - `PermissionDeclarationConvention` gives any action without a declaration the never-granted requirement, so it is
    refused.
  - `PermissionResultHandler` answers 403 in the §63 shape (`PERMISSION_DENIED`, naming the missing permission, or
    `PERMISSION_NOT_DECLARED`).
  - The user and office middlewares now run before `UseAuthorization`, which reads them.
  - Reads need the module's view permission; look-up lists and configuration reads need `prime.use` (Q4). The frozen
    treasury endpoints need `treasury.legacy`, which no role holds (Q18).
- **The matrix is configuration.**
  - `RolePermissionChange` holds a proposed new set of permissions for one role: what it gains and what it loses, with
    a reason, and at most one open proposal per role.
  - It applies only when a second user approves it. The author, or an unknown user, is refused
    (`CANNOT_APPROVE_OWN_ROLE_PERMISSION_CHANGE`). Rejecting needs a reason.
  - API `/api/role-permissions` (matrix; `changes`, approve, reject); screen `/admin/role-permissions` ("Role
    Permissions").
- **Screens:**
  - `GET /api/me` returns the user's permissions.
  - The menu hides entries whose view permission the user lacks (`useCan`); the API refuses them anyway.
  - A refused call shows its message ("You do not have the permission this action needs: …").
- **Development users:**
  - The usual dev user now holds SYSTEM_ADMIN plus APPRAISER, ASSESSMENT_ENCODER, GIS_OFFICER and REPORTING_OFFICER, so
    it prepares everything but approves nothing. The checker (ASSESSOR) approves.
  - `DevOfficeSeeder` adds configured roles missing from a dev user's DEMO assignment. It now inserts the role rows
    through their own set: through the parent, EF updated rows that did not exist.
- **Tests** (`PermissionTests`):
  - every action declares a known permission;
  - each of the 9 roles holds exactly its default grants, and a user without an assignment holds none;
  - over HTTP: a DEMO appraiser is refused an assessment approval with `PERMISSION_DENIED`, the usual user too, and the
    checker passes the gate;
  - `/api/me` lists the permissions;
  - matrix changes: unchanged, unknown permission, missing reason, a second open proposal, self-approval and double
    decision are all refused; approval applies the change, and the seeder leaves it alone.
  - `CollectionFlowTests` now asserts that the frozen collection API is gated (403, `treasury.legacy`).
  - 762 tests pass; frontend build and lint clean.
- **Browser** (dev database):
  - The usual user opened the matrix and proposed VIEW_ONLY + `audit.view`; no Approve button was shown on their own
    proposal.
  - The checker approved it, and the default was then restored the same way. A proposal for AUDITOR was rejected with
    a reason.
  - A DEMO municipal appraiser sees no Content Packs or Collection entries and no "Change a role" button; opening
    Collection shows the permission message.
  - No console errors; no overflow at 390 px.
- [DOMAIN VERIFICATION: the default matrix against the province's positions and delegations (Q2).]

### P12-2 — users and sign-in (2026-10-07)

Done; migration `SignUpAndUserStatus` applied to the local database only.

- **Account status.** `AppUserStatus` is Active, Inactive or Pending. The numbers continue the earlier values, so
  existing users stay Active. A token seen for the first time provisions a **Pending** user with no office assignment.
  Pending users therefore hold no permissions and reach only the `[SignedInOnly]` endpoints: `GET /api/me` and their
  own sign-up request.
- **Sign-up request** (`SignUpRequest`; Q5):
  - The applicant sends their full name, position, office, the roles they ask for and a note. They can have at most
    one open request (`UX_SignUpRequests_Open`).
  - A user with `users.signup` (SYSTEM_ADMIN by default) approves it, choosing the office and roles. The approval
    activates the account and creates the office assignment, in force at once: the applicant is the maker, the
    administrator the checker. Alternatively they reject it with a reason, and the applicant may ask again.
  - Deciding one's own request is refused (`CANNOT_DECIDE_OWN_SIGN_UP`), and so is a decision by an unknown user
    (`DECIDING_USER_UNKNOWN`).
  - API `/api/sign-up` (`options`, `mine`, submit) and `/api/sign-up-requests` (list, approve, reject). Screen
    `/admin/sign-up-requests` ("Sign-up Requests"), with the waiting count in the menu.
- **Disabling and enabling** (`UserStatusChange`; Q6):
  - One user proposes it with a reason (`users.manage`). It takes effect when a second user approves it
    (`users.approve`). Self-approval, a change to one's own account and a second open proposal are refused.
  - A disabled user is refused on every request with 401 `USER_DISABLED`, in `AppUserProvisioningMiddleware`.
  - API `/api/users/{id}/status-changes` and `/api/user-status-changes`. Screen: Offices → Accounts.
- **MFA** (Q7):
  - `Security:MfaRequiredRoles` defaults to SYSTEM_ADMIN and ASSESSOR. A holder of one of those roles whose token is
    not `aal2` is refused with 403 `MFA_REQUIRED` everywhere except `/api/me`.
  - `/api/me` reports `status`, `mfaRequired`, `mfaSatisfied` and `idleMinutes`.
  - In Development the bypass reports `aal2` unless the `X-Prime-Dev-Aal: aal1` header asks otherwise.
- **Frontend** (`AuthGate`, `lib/auth.ts`, `pages/auth/`). The gate decides what a visitor sees:
  - the sign-in screen (sign in, create an account, forgot password);
  - the password-reset page from the e-mailed link;
  - the "account disabled" notice;
  - the sign-up form, then "Awaiting approval";
  - the TOTP enrolment or challenge step;
  - otherwise PRIME.

  The gate also does the following:
  - signs the user out after `Security:IdleMinutes` (default 30) without activity (Q9);
  - clears cached queries on sign-out;
  - reports sign-in and sign-out to `POST /api/session`, logged as LOGIN and LOGOUT audit rows (`AuditAction` 9
    and 10).
- **Bootstrap.** `dotnet Prime.WebApi.dll bootstrap-admin <email>` makes a signed-up user the first SYSTEM_ADMIN
  (province-wide). It is refused once an active SYSTEM_ADMIN exists and never starts the web server.
- **Development.** The DEMO user `applicant` (office "none") signs in as a new pending user. A local
  `appsettings.Development.json` with its own `DevAuth:Users` list needs that entry added.
- **Moved to P12-4:** the start-up check of the Supabase Auth project settings (Q8). The required values are written
  in `docs/SECURITY.md` (P12-5).
- **Tests** (`UserAccountTests`, 7):
  - request, approval with the office and roles in force at once, own and unknown decisions refused;
  - rejection, then a new request;
  - disabling by a second user only;
  - the bootstrap, only while there is no SYSTEM_ADMIN;
  - over HTTP, a pending user limited to their sign-up and a disabled user refused;
  - an ASSESSOR with an aal1 token refused but still able to read `/api/me`;
  - LOGIN and LOGOUT audited.

  769 tests pass; the frontend build and lint are clean.
- **Browser** (dev database, DEMO users):
  - the sign-in screen at 390 px, with no overflow;
  - the applicant saw only the request form, sent it and saw "Awaiting approval";
  - the administrator saw "Sign-up Requests 1" and approved with the requested office and role pre-filled;
  - after a reload the applicant worked at once (20 menu entries);
  - the administrator proposed disabling the applicant, with no Approve button on their own proposal. The checker
    approved it, and the applicant then saw "Your PRIME account has been disabled".
  - an ASSESSOR with an aal1 token got the "Second sign-in step" screen.

  The only console errors were the disabled user's expected 401s.
- **Not verified:**
  - a real Supabase sign-in, sign-up e-mail, password reset or TOTP enrolment. These need accounts in the Supabase
    project and are left for the Supabase review in P12-4.
  - The dev database keeps the DEMO applicant approved and then disabled.

### P12-3 — audit completeness (2026-10-08)

Done; migration `AuditCompleteness` applied to the local database only. Most of the code was written on 2026-10-07
and not logged; this session reviewed it, fixed the gaps below, and verified it.

- **Named actions.** `AuditSaveChangesInterceptor` logs a change of any `…Status` enum property as the action it
  sets, matched by the value's name: Approved and Certified → APPROVE, Rejected and Returned → REJECT, Posted → POST,
  Voided and Void → VOID, Cancelled → CANCEL, Reversed → REVERSE. Other values (Submitted, PendingReview, Superseded
  …) stay UPDATE. A record without a status that is cancelled by setting `CancelledAt` (the market-data records) is
  CANCEL. Rows written before this step keep UPDATE: the log is append-only.
- **Readable values.** Before and after values store enums by name ("Approved", not 3). Rows written before
  2026-10-08 keep their numbers.
- **Every table** (Q10). Plain `Entity` children are audited too, with `ParentTableName` and `ParentRecordId` (the
  principal of the foreign key their parent navigates through, else their first required foreign key). The
  exclusions, each justified in the interceptor: the audit log itself; SMV simulation results and their lines,
  valuation-test sales and general-revision run issues (written in bulk by a job whose run is audited); and number
  counters (`NumberSequence`; the issued number is audited on the record that carries it). Hangfire's tables and
  the EF history table are not EF entities.
- **Reasons.** The review of every reject, cancel and return path outside the frozen treasury code found:
  - assessment rejection accepted an empty reason. It now needs one (max 1000), like a TD's.
  - five paths stored their reason on the record but not on the audit row: the three market-data cancellations,
    rejecting a role-permission change, and cancelling an SMV preparation. They now set `ICurrentUserService.Reason`.

  The other paths (TDs, transactions, exemptions, notices, notices of cancellation, sworn statements, forms,
  general revision, delegations, roll submissions, sign-up and status changes) already required a reason and
  recorded it.
- **Exports** (Q11). `SecurityEventLog` writes EXPORT rows (module "Exports", `AuditAction` 11) with what was
  exported and the format:
  - Issued, on the server, by `[AuditExport]` on issuing a form, running a register and running a general-revision
    register;
  - Print, Csv, Excel or Pdf, reported by the browser to `POST /api/audit/exports` (any signed-in user). The pages
    report through `usePrintAudit`, which listens to `beforeprint` so the browser's own print command counts too.
    They are the form document, the GIS print and the tax map sheet.

  PRIME has no server-side file downloads, and its only CSV is an import. The frozen billing and collection prints
  are not instrumented (CLAUDE.md §0). Viewing a record is not logged.
- **Viewer** (`audit.view`). `GET /api/audit-logs` filters by user, table, record (optionally with its child rows),
  property, action, module and period, paged on the server, newest first. It also offers `/{id}` and `/tables`. No
  endpoint changes or deletes a row.
  - Screen `/admin/audit` ("Audit Trail"): rows open to their before and after values; clicking a user filters to
    their rows. A user without `audit.view` sees "Not permitted" instead of a failed table.
  - Record histories (`AuditHistoryCard`, shown only with `audit.view`): the Property Profile's "Audit history" tab
    (the property and its owners, parcels, RPUs, land, buildings, machinery, valuations, assessments, TDs and
    transactions, with their child rows), each SMV, and each transaction. TDs are covered by the property's history.
- **Append-only** (Q12). Triggers on `AuditLogs` raise `insufficient_privilege` on UPDATE, DELETE and TRUNCATE, for
  every role. New indexes: (`UserId`, `Timestamp`) and `ParentRecordId`.
- **Also fixed:** the Property Profile showed the frozen Billing and Payments tabs to users without
  `treasury.legacy`, whose API calls were already refused. The tabs are now hidden like the menu entries.
- **Tests:**
  - `AuditTrailTests` (3): named actions, enum names in values, a child row with its parent, a record's history
    with its children; the database refusing UPDATE, DELETE and TRUNCATE; a register run and a print as EXPORT,
    a bad format refused, the viewer refused without `audit.view`.
  - `AssessmentFlowTests`: a rejection needs a reason and is audited as REJECT with it.
  - `MarketDataTests`: a cancellation is audited as CANCEL with its reason.

  773 tests pass; the frontend type-check, lint and production build are clean.
- **Browser** (dev database, DEMO users):
  - the Audit Trail filtered to APPROVE (9 rows), one opened to its before and after values;
  - the Property Profile's Audit history (7 rows);
  - printing an issued form added one EXPORT row;
  - the municipal appraiser had no menu entry and saw "Not permitted";
  - no overflow at 390 px.
- **Open, not part of P12-3:** the workflow review of §4.6 (an unknown acting user refused on every sensitive
  approval, G7; §46's list walked against the code; status names checked against §45). Role-permission
  changes, user-account decisions and a few others refuse an unknown user today; the rest have not been walked.
  Proposed for P12-4.

### P12-4 — hardening and the workflow review (2026-10-08)

Done; no migration.

- **Rate limits** (Q13; `Security/RateLimiting.cs`, section `RateLimits`).
  - A general limit of 300 requests a minute per signed-in user, or per IP address before sign-in.
  - A strict limit of 10 a minute on uploads, imports and exports: content pack upload and import, market data import
    and its preview, GIS layer import, form issue and both register runs (`[EnableRateLimiting("strict")]`). The
    browser's print reports (`POST /api/audit/exports`) are not strict, so an audited print is never refused.
  - Over a limit: 429 with `Retry-After` and a §63 body (`RATE_LIMITED`). The limiter runs after authentication, so a
    user is counted as themselves.
  - On everywhere except Development (`RateLimits:Enabled` overrides), where the DEMO users and the test suite share
    one identity. Sign-in attempts are limited by Supabase Auth.
- **Headers** (`Security/SecurityHeadersMiddleware.cs`). Outside Development (`Security:Headers` overrides), every
  response carries `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY` and
  `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`. HTTPS responses also carry HSTS (one year,
  subdomains). Issued forms are HTML inside JSON. They render in a sandboxed iframe (no scripts) under the SPA's
  policy and the renderer's own meta policy, so the API's policy does not affect them. The SPA host's headers go in
  `docs/SECURITY.md` (P12-5).
- **Proxies.** `Security:KnownProxies` lists the reverse proxies whose `X-Forwarded-For` and `X-Forwarded-Proto` are
  trusted. The audit log's IP address, the per-IP limit and HSTS then see the client. The list is empty by default,
  so no forwarded header is trusted.
- **Uploads.**
  - The server's body limit is 10 MB (`Kestrel:Limits:MaxRequestBodySize`). Content packs keep 110 MB and the market
    data CSV 5 MB; GIS layer imports get 50 MB.
  - An empty file, or a file without the expected extension (`.zip`, `.csv`), is refused before it is read
    (`UPLOAD_EMPTY`, `UPLOAD_TYPE_INVALID`). The declared content type is not trusted; the services still check the
    content.
  - Zip entries: the content-pack unpacking already checks path traversal and size (`ContentPackUploadTests`).
- **CSRF.** Not applicable: outside Development the API accepts bearer tokens only and sets no cookie. A test shows
  that under real authentication a cookie-only request, and the Development act-as header, are refused with 401.
- **Injection.** Every raw SQL call was reviewed: `NumberSequenceAllocator` and `CollectionLock` (Npgsql parameters),
  `GeometryMeasurementService` and the PostGIS and RLS health checks (EF `SqlQuery` with interpolation, which
  parameterises). None concatenates input.
- **XSS.** Fluid renders with `HtmlEncoder.Default`, and no template (built-in or in the dev content pack) uses `raw`.
  The form iframe is sandboxed without `allow-scripts`, and its document carries `default-src 'none'`. The SPA has no
  `dangerouslySetInnerHTML`.
- **Dependencies** (2026-10-08).
  - `dotnet list package --vulnerable --include-transitive`: none in any project.
  - `npm audit --omit=dev`: none.
  - `npm audit` with dev tools: one high in `source-map-js` 1.2.1 (via postcss, build time only). `npm audit fix`
    moved it to 1.2.2 within its range; the production build still passes.
- **Secrets.** The whole history (47 commits) was scanned for connection-string passwords, JWTs, Supabase service
  and personal access keys, private keys, and AWS and GitHub tokens. The only match is the README's `Password=...`
  placeholder, and `.env.example` has empty values. Nothing to rotate. This was a pattern scan; gitleaks is not
  installed.
- **Hangfire dashboard.** Development only (Q14), unchanged.
- **Supabase** (read-only checks on 2026-10-08; Supabase at 75 migrations).
  - RLS is on for 171 of 172 `public` tables, with 0 policies. The exception is PostGIS's `spatial_ref_sys` (public
    reference data owned by the extension). Supabase's `ensure_rls` event trigger turns RLS on for every new table,
    so later migrations inherit it.
  - With the anon key, the REST API returned 0 rows from `Taxpayers`, `PropertyTaxpayers`, `AppUsers` and
    `AuditLogs`. The GraphQL API is not enabled (`pg_graphql` off).
  - New health check `row-level-security`, in all environments. It is unhealthy if a `public` table other than
    `spatial_ref_sys` lacks RLS or if any policy exists. It is healthy on a database without Supabase's `anon` role
    (local).
  - New check of the Auth settings Supabase publishes (Q8, moved from P12-2): e-mail sign-in on, e-mail confirmation
    required, no anonymous sign-ins, no other providers. Outside Development it runs at start-up (a warning in the
    log) and on `/health` (`supabase-auth`, degraded on a difference). It needs `Supabase:AnonKey`, the project's
    public key. The live project passes. Password length, leaked-password protection, lockout and token lifetime are
    not published; they go in `docs/SECURITY.md` as manual checks.
  - Storage: no bucket yet (Q15).
- **Workflow review (§4.6).**
  - **G7 closed.** `MakerChecker.Refusal` (Application/Common) refuses a decision when the acting user is unknown
    (`APPROVING_USER_UNKNOWN`) or made the record. Its users:
    - the configuration approval helper, so every effective-dated configuration (factors, building tables,
      numbering, forms, chains, jurisdictions, checklist steps, exemption and transaction types, assessment-level
      ceilings …);
    - machinery indices, SMVs and their schedules, assessment levels;
    - assessments, TDs, property transactions;
    - delegations, office assignments, territorial changes and exemption claims.

    `SignNextStepAsync` refuses an unknown signer before anything else, so a chain step can no longer be signed
    "(unknown user)". Role-permission changes and user-account decisions already refused an unknown user. The frozen
    treasury code (billing rules, payment cancellations, remittances) is not changed (CLAUDE.md §0); only
    `treasury.legacy` reaches it, and no role holds that permission.
  - **§46's list walked.** These all need a second, known user: SMV, assessment level, assessment, general revision
    (its assessments and TDs go through the same approvals, item by item), reassessment (an assessment), exemption,
    TD approval, property transaction and configuration. Market-data review and the province's acknowledgement of a
    monthly roll are not §46 items; they record the acting user.
  - **One gap: TD cancellation.** `POST /api/tax-declarations/{id}/cancel` (permission `td.approve`) lets one user
    cancel an approved TD in one step, with a reason. §46 lists "Tax Declaration approval and cancellation". A
    cancellation through a property transaction of the cancellation kind is already maker-checked. It is not changed
    here; see Q19 (§8.2), built in P12-5.
  - **Status names (§45, Q17).** `WorkflowStatus` has exactly §45's statuses (Draft, Submitted, PendingReview,
    Approved, Rejected, Posted, Cancelled, Voided), and every maker-checked record uses it. The other status sets
    describe a record's own lifecycle, not an approval, and stay as they are: SMV preparation, exemption claims,
    notices, sworn statements, general revision programmes and items, roll submissions, territorial change items,
    jobs, user accounts, and `RecordStatus` for registry records. No renames.
- **Tests.**
  - `SecurityHardeningTests` (7):
    - the general and strict limits, with 429 and `Retry-After`;
    - limits off in Development by default;
    - headers when switched on, HSTS only over HTTPS, none by default in Development;
    - uploads of the wrong type refused;
    - under real authentication, a cookie or the act-as header signs nobody in, and the headers are on by default;
    - the Supabase Auth settings check lists each difference.
  - `HealthEndpointTests`: `/health` reports `row-level-security`.
  - An unknown approver is refused for an SMV, an assessment level, an assessment and a TD (`EndToEndFlowTests`), a
    configuration version (`AssessmentLevelCeilingTests`) and a delegation (`ApprovalDelegationTests`).
  - Test set-up that approved with no acting user now approves as a separate DEMO checker
    (`TestSeed.AsCheckerAsync`): the shared seed and 21 classes. When a set-up assertion fails, its transaction stays
    open. Once the shared seed failed this way, the suite ran out of database connections; that is how the change
    first showed itself.

  780 tests pass (438 integration, 286 domain, 56 application); the frontend production build passes.
- **Not verified:** the headers and limits behind a real reverse proxy and HTTPS host (no deployment yet); a real
  Supabase sign-in (carried from P12-2).

### P12-5 — TD cancellation, personal data and `docs/SECURITY.md` (2026-10-08)

Done; migration `TaxDeclarationCancellationRequests` applied to the local database. Q19 was decided as recommended.

- **TD cancellation under maker-checker** (Q19).
  - `TaxDeclarationCancellationRequest`: one user (`td.prepare`) asks for an approved TD to be cancelled outright,
    with a reason (`POST /api/tax-declarations/{id}/cancellation-requests`). Nothing changes until a second, known
    user (`td.approve`) approves it, which cancels the TD with the request's reason, or rejects it with a reason
    (`POST /api/tax-declarations/cancellation-requests/{id}/approve|reject`).
  - The requester cannot decide their own request (`CANNOT_DECIDE_OWN_TAX_DECLARATION_CANCELLATION`), and an unknown
    user cannot either. There is one open request per TD (`UX_TaxDeclarationCancellationRequests_Open`). A request
    for a TD outside the user's jurisdiction is not found.
  - Approval checks the TD again: still approved, and no court claim blocking it.
  - Requests are kept, with their history (`GET …/{id}/cancellation-requests`). The approval is audited as APPROVE on
    the request and CANCEL on the TD, both with the reason.
  - The one-step `POST /api/tax-declarations/{id}/cancel` is removed. Cancellation through a property transaction of
    the cancellation kind is unchanged: the transaction has its own approval.
  - On the Property Profile, an approved TD offers "Request cancellation" (with `td.prepare`). A pending request
    shows "Cancellation requested", with the reason on hover, and "Approve cancellation" / "Reject cancellation"
    for users holding `td.approve`.
- **Personal data** (Q16).
  - `PersonalData` masks an individual's TIN (last three digits kept), contact number (last four), e-mail (first
    letter and domain) and address ("(hidden)") for a user without `taxpayer.view-personal`.
  - Masked in the taxpayer registry (get, search, the create and update responses) and in the property's owners and
    ownership history. `TaxpayerDto.PersonalDataMasked` and `PropertyOwnerDto.AddressMasked` say so; the registry
    shows a "masked" tag and no "Edit details".
  - Names, and the details of corporations and other non-individuals, are not masked.
  - Correcting an individual's details is refused without the permission (`PERSONAL_DATA_FORBIDDEN`), so a masked
    value cannot be saved back.
  - Forms, notices and the appraisal record are not masked: they are official records and print what the record
    holds. They are gated by `forms.preview` and `forms.issue`, and every issue and print is audited as EXPORT.
  - `records.export` stays reserved for bulk downloads; PRIME has none yet.
- **Logs reviewed.** The 23 log statements carry ids only: no names, TINs, addresses or tokens. There is no request
  or body logging. EF Core sensitive-data logging and Npgsql error details are off (defaults). The Development act-as
  header has no effect outside Development (P12-4 test).
- **`docs/SECURITY.md`** written for administrators and auditors. It covers authentication and the required Supabase
  Auth settings (manual items marked), authorization, maker-checker, personal data, the audit trail, secrets, API and
  SPA headers, Supabase, routine checks, and what is not yet verified.
- **Development.** A DEMO `viewer` user (VIEW_ONLY, provincial office) was added to the act-as list. A local
  `appsettings.Development.json` with its own `DevAuth:Users` needs it added; this one was updated.
- **Also fixed:** the Taxpayer Registry table overflowed at 390 px; it now scrolls inside its card.
- **Tests:**
  - `PersonalDataTests` (2): the masks; a VIEW_ONLY user sees an individual masked in the registry, search, owners
    and history, sees a corporation in full and cannot correct the details; an encoder sees everything.
  - `PartiesAndTdLifecycleTests`: request, the duplicate refused, own and unknown decisions refused, rejection with a
    reason, a second request approved by the other user, the history and the APPROVE/CANCEL audit rows.
  - `JurisdictionTests`: the taxpayer test acts as a user who may see personal data.

  782 tests pass (440 integration, 286 domain, 56 application). The frontend type-check, lint and production build
  pass.
- **Browser** (dev database, DEMO users):
  - On TD DEMO-GR-TD-2099-0001, the usual user (encoder) requested a cancellation and then saw no decision buttons.
  - The checker saw the request and rejected it with a reason; the TD stayed approved, offering "Request
    cancellation" again. The dev database keeps the two rejected requests.
  - The DEMO viewer saw DEMO-E2E's TIN masked, a "masked" tag and no "Edit details"; the usual user saw it in full.
  - No overflow at 390 px after the fix; no console errors.
- **Supabase** (user's go-ahead, 2026-10-08): the four Phase 12 migrations (`RolePermissionChanges`,
  `SignUpAndUserStatus`, `AuditCompleteness`, `TaxDeclarationCancellationRequests`) were applied; Supabase is at 79,
  like the local database. All are additive. Afterwards, RLS was on for 175 of 176 `public` tables (the four new ones
  included, through `ensure_rls`), with 0 policies.
