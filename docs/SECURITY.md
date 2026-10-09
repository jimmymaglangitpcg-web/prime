# PRIME — Security

How PRIME protects its data and what an installation must configure and check. Written for the administrators who
install and run PRIME and for auditors who review it. The design, decisions and test evidence are in
`docs/analysis/workflow-security.md` (Phase 12). Legal context: CLAUDE.md §67–§69, the Data Privacy Act (RA 10173)
and RA 12001's penalties for unauthorized processing of RPIS data.

Status: written 2026-10-08 at the end of Phase 12. Items marked **manual** cannot be checked by PRIME; an
administrator checks them at installation and after any change to the Supabase project.

## 1. The parts and who can reach them

| Part | Reached by | Protection |
|---|---|---|
| Web app (SPA) | Browsers | Static files; sign-in through Supabase Auth; headers set by its web server (§8.2) |
| API (`Prime.WebApi`) | The SPA, with a bearer token | Token validation, permissions, jurisdiction, maker-checker, rate limits, headers (§3–§8) |
| Database (Supabase PostgreSQL + PostGIS) | The API only, through its connection string | Row-level security against Supabase's own APIs (§9); append-only audit log (§6) |
| Supabase Auth | Browsers (sign-in), the API (token keys) | Project settings (§2.2) |
| Hangfire dashboard | Development only | Not served elsewhere |

## 2. Authentication

### 2.1 How a request is authenticated

- The SPA signs users in with Supabase Auth (e-mail and password, optional TOTP second factor) and sends the access
  token as `Authorization: Bearer …`.
- The API validates the token against the project's published signing keys (ES256): issuer, audience
  `authenticated`, lifetime and signature. PRIME stores no passwords.
- The API takes bearer tokens only and sets no cookie, so cross-site request forgery does not apply. A request
  carrying only cookies is refused (401).
- The first request of a new token creates a **Pending** PRIME account with no office and no permissions. Its holder
  can only send a sign-up request. A SYSTEM_ADMIN approves it, choosing the office and roles, or rejects it. A
  SYSTEM_ADMIN cannot decide their own request.
- A **disabled** account is refused on every request (401 `USER_DISABLED`), whatever its token. A user is disabled or
  enabled by one administrator's proposal and a second user's approval.
- Holders of the roles in `Security:MfaRequiredRoles` (default SYSTEM_ADMIN and ASSESSOR) must sign in with a second
  factor. Their requests with a single-factor token (`aal1`) are refused (403 `MFA_REQUIRED`), except "who am I".
- The browser signs a user out after `Security:IdleMinutes` (default 30) without activity. Sign-in and sign-out are
  audited as LOGIN and LOGOUT.
- **Development only:** with `DevAuth:Enabled` in the Development environment, a bypass signs every request in as a
  DEMO user (switchable with the `X-Prime-Dev-Act-As` header). Outside Development neither the bypass nor the header
  has any effect; a test checks this.

### 2.2 Supabase Auth project settings

PRIME checks what Supabase publishes, at start-up (a warning in the log) and on `/health` (`supabase-auth`). This
needs `Supabase:Url` and `Supabase:AnonKey` (the project's public key, not a secret).

| Setting | Required value | Checked by |
|---|---|---|
| Sign-in providers | E-mail only; anonymous sign-ins off; no social or phone providers | PRIME |
| Confirm e-mail | On (new addresses must be confirmed) | PRIME |
| Allow new users to sign up | On (accounts still need a SYSTEM_ADMIN's approval) | **manual** |
| Minimum password length | At least 12 | **manual** |
| Password requirements | Lower and upper case, digits and symbols | **manual** |
| Leaked password protection | On | **manual** |
| Multi-factor (TOTP) | Enabled | **manual** |
| Access token (JWT) expiry | 3600 seconds or less | **manual** |
| Auth rate limits (sign-in, sign-up, e-mails, token refresh) | Supabase defaults or stricter | **manual** |
| Site URL and redirect URLs | Only the SPA's own address(es) | **manual** |
| SMTP | A custom SMTP sender for the office's domain (the built-in one is rate-limited) | **manual** |

### 2.3 The first administrator

A new installation has no SYSTEM_ADMIN. After that person signs up in the SPA, run on the server:

```text
dotnet Prime.WebApi.dll bootstrap-admin <their e-mail>
```

It makes them an active, province-wide SYSTEM_ADMIN. It refuses to run once an active SYSTEM_ADMIN exists and never
starts the web server.

## 3. Authorization

- **Permissions.** About 40 permissions by module and verb (`property.edit`, `assessment.approve`, `config.approve`,
  `taxpayer.view-personal`, `audit.view` …), listed in `Prime.Application/Common/Security/Permissions.cs`. Every API
  action declares the permission it needs. An action that declares none is refused, and a test fails the build if one
  exists. A refusal is 403 `PERMISSION_DENIED`.
- **Roles.** A user's permissions are those of the roles they hold through their office assignment in force today.
  The role–permission matrix is configuration: it is changed on the Role Permissions screen, and a change takes
  effect when a second user approves it. The shipped matrix is a **provisional default**; check it against the
  province's actual positions and delegations (DOMAIN VERIFICATION REQUIRED).
- **Jurisdiction.** Every record tied to a property is filtered by the user's office jurisdiction. The provincial
  office sees the province. Outside the jurisdiction a taxpayer shows name and TIN only.
- **Frozen treasury code.** The billing and collection screens and endpoints need `treasury.legacy`, which no role
  holds (CLAUDE.md §0).

## 4. Maker-checker and workflow

- Every change listed in CLAUDE.md §46 needs a second user. The person who made a record cannot approve it, and an
  approval with no known acting user is refused (`APPROVING_USER_UNKNOWN`). This covers:
  - SMVs and their schedules, and assessment levels;
  - assessments, reassessments and general revision items;
  - exemption claims;
  - Tax Declaration approval and outright cancellation: one user requests the cancellation with a reason, a second
    approves or rejects it;
  - property transactions;
  - every configuration version, office assignments and delegations;
  - user status changes and role-permission changes.
- Approval chains route approvals per office, with provincial approval unless delegated (CLAUDE.md §117).
- Background jobs act as the user who started them.

## 5. Personal data

- An individual taxpayer's **TIN, contact number, e-mail and address** are masked in API responses for users without
  `taxpayer.view-personal`: the taxpayer registry, the property's owners and the ownership history. A TIN keeps its
  last three digits (`***-***-***-000`), a contact number its last four, an e-mail its first letter and domain; an
  address shows "(hidden)". Names stay visible: they are on the TD and the rolls. A corporation's or government
  entity's details are not masked.
- A user who cannot see an individual's details cannot correct them (`PERSONAL_DATA_FORBIDDEN`), so a masked value is
  never saved back.
- **Forms and notices are not masked.** The TD, FAAS, notices, rolls and other official forms print what the record
  holds. Previewing a form needs `forms.preview` and issuing one `forms.issue`; every issue and print is audited as
  EXPORT. Whether a role should preview forms at all is part of the matrix review (§3).
- `records.export` is reserved for bulk downloads (CSV, Excel). PRIME has none yet; Phase 11 reports will need it.
- **Logs** carry record and user ids, not names, TINs, addresses or tokens. Request bodies and the `Authorization`
  header are not logged. EF Core's sensitive-data logging is off, and Npgsql's error details (which would include key
  values) stay at their default, off. Do not turn either on outside a developer's machine.

## 6. Audit trail

- Every change to a record writes an `AuditLogs` row in the same transaction: user, table, record, action, old and
  new values, reason, IP address. Approvals, rejections, postings, voids and cancellations show as their action; child
  rows carry their parent. Listed exclusions (bulk results of audited jobs, number counters) are justified in
  `AuditSaveChangesInterceptor`.
- LOGIN, LOGOUT and EXPORT (form issues, register runs, prints) are logged too.
- Database triggers refuse UPDATE, DELETE and TRUNCATE on `AuditLogs` for every role.
- Auditors with `audit.view` use the Audit Trail screen and the record histories.

## 7. Secrets and configuration

- Only `appsettings.json` (no secrets) is in git. `appsettings.*.json`, `.env*` (except `.env.example`), the LAM
  copy and the LGU content pack are gitignored (CLAUDE.md §104, §118).
- Secrets on a server: the database connection string (`ConnectionStrings:PrimeDb`). Supply it as an environment
  variable (`ConnectionStrings__PrimeDb`) or a secret store, never a committed file.
- The Supabase **service-role key** is not used by PRIME and must never reach the SPA or the API's configuration.
  The anon key is public by design and safe only because of §9.
- The git history was scanned on 2026-10-08 (47 commits) and holds no secret. If a secret is ever committed, rotate
  it first, then remove it.

## 8. Web hardening

### 8.1 API

| Measure | Behaviour | Setting |
|---|---|---|
| HTTPS | Requests are redirected to HTTPS; HSTS (one year, subdomains) on HTTPS responses | — |
| Security headers | `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`. On outside Development | `Security:Headers` |
| Rate limits | 300 requests a minute per user (per IP before sign-in); 10 a minute for uploads, imports, form issues and register runs; 429 `RATE_LIMITED` with `Retry-After`. On outside Development | `RateLimits:*` |
| Reverse proxy | The client's address and scheme are taken from `X-Forwarded-For`/`-Proto` only when sent by a listed proxy | `Security:KnownProxies` |
| Request size | 10 MB by default; content packs 110 MB, market-data CSV 5 MB, GIS layer imports 50 MB | `Kestrel:Limits:MaxRequestBodySize` |
| Uploads | Empty files and wrong extensions refused before reading; zip entries checked for path traversal and size | — |
| CORS | Only the origins listed | `Cors:AllowedOrigins` |
| Errors | One error shape without stack traces or internals (CLAUDE.md §63, §106) | — |
| Injection | EF Core queries and parameterised raw SQL only (reviewed 2026-10-08) | — |

Sign-in attempts are limited by Supabase Auth (§2.2), not by PRIME.

### 8.2 The SPA's web server

The SPA is static files; its server must send these headers (adjust the origins):

```text
Strict-Transport-Security: max-age=31536000; includeSubDomains
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin
X-Frame-Options: DENY
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';
  img-src 'self' data: blob: https://tile.openstreetmap.org; font-src 'self' data:;
  connect-src 'self' https://<api host> https://<project>.supabase.co; frame-src 'self'; frame-ancestors 'none';
  object-src 'none'; base-uri 'self'; form-action 'self'
```

`style-src 'unsafe-inline'` is needed by the UI library's inline styles. The map tile host follows
`VITE_MAP_TILE_URL` when it is set. Issued forms render in a sandboxed iframe without scripts.

## 9. Supabase

- **Row-level security** is on for every `public` table, with no policy, so the anon and authenticated keys read
  nothing through Supabase's REST and GraphQL APIs. The one exception is PostGIS's `spatial_ref_sys` (public
  reference data owned by the extension). Supabase's `ensure_rls` event trigger turns RLS on for every new table.
  The `/health` check `row-level-security` reports any table without RLS and any policy.
- Checked on 2026-10-08: RLS on 171 of 172 tables; the anon key read 0 rows from `Taxpayers`, `PropertyTaxpayers`,
  `AppUsers` and `AuditLogs`; GraphQL is not enabled.
- **Storage:** no bucket is used yet. Document storage (CLAUDE.md §59) will use a private bucket with short-lived
  signed URLs; its policies are reviewed when it is built.
- **manual:** keep the project's database password and service-role key with the provincial ICT office only; enable
  Supabase's backups and point-in-time recovery (Phase 14).

## 10. Routine checks

| When | Check |
|---|---|
| Every release | `dotnet list package --vulnerable --include-transitive`; `npm audit --omit=dev` in `frontend/prime-web` |
| Every release | A secret scan of the new commits |
| Continuously | `/health`: database, PostGIS, `row-level-security`, `supabase-auth`, `background-jobs`, `database-tls` (Degraded until the database certificate is verified with `SSL Mode=VerifyFull`) |
| After Supabase changes | §2.2 manual items |
| Quarterly | The role–permission matrix and the list of active users and their offices; the audit trail for unusual exports |

Last run (2026-10-08): no vulnerable NuGet package; no production npm vulnerability; one build-time npm finding
(`source-map-js`) fixed.

## 11. Not yet verified

- A real Supabase sign-in, password reset and TOTP enrolment against the live project.
- The headers, limits and forwarded headers behind the production reverse proxy and HTTPS host.
- Backup and restore (Phase 14).
