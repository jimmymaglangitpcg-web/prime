# Province-wide Operation — Design (Step LP)

| | |
|---|---|
| Date | 2026-09-29 |
| Status | **Approved 2026-09-29: all recommendations Q1–Q13 accepted.** LP-1 to LP-3 done (§9); LP-4 next |
| Rules | CLAUDE.md §9, §46, §47, §68, §85, §117 (deployment scope), §118 (no LGU data in git) |
| Commit status | Contains no LAM text or LGU data; may be committed |

## 1. Purpose

PRIME is deployed once for the whole province (CLAUDE.md §117). One instance
serves the Provincial Assessor's Office and the municipal assessors' offices.
Municipal offices prepare FAAS and Tax Declarations. The Provincial Assessor
approves them, unless approval has been delegated to the municipal assessor
for a period (LAM Book I Ch. I).

Today PRIME assumes one LGU and one office. This step adds offices,
jurisdiction, office-aware approval with delegation, per-office branding and
signatories, and the province's consolidated view. It comes before the
valuation work (L1) because every record L1 and later steps produce is
prepared in one office and approved in another.

The province's actual answers (which offices, which delegations, which
approval steps, which signatories) are being collected with the L0-4 question
list. **This design is built so that those answers are configuration only**:
nothing in the code names an office, a municipality or a person.

## 2. What exists

| Area | Today | Single-LGU assumption |
|---|---|---|
| Users | `AppUser` (Supabase id, name, email, status); just-in-time provisioning | No office |
| Roles and permissions | `Role`, `Permission`, `UserRole`, `RolePermission` tables exist, **empty and not enforced**. Every endpoint only requires sign-in. The dev bypass gives one SYSTEM_ADMIN user (plus the `X-Prime-Dev-Act-As: checker` second user) | Anyone signed in can do anything (Phase 12 planned) |
| Where a record belongs | `Property` carries `ProvinceId`, `MunicipalityId` and `BarangayId`. RPUs, valuations, assessments, TDs, transactions, notices and PIN assignments hang off a property. `SwornStatement` has `MunicipalityId`; `RegisterRun` a barangay or a taxpayer | A natural anchor exists, but no query uses it to limit access |
| Taxpayers | One registry, with an optional address | Shared by everyone |
| Approval of records | `ApprovalChain`: **one chain in force per subject type** (Assessment, TaxDeclaration, PropertyTransaction). `SignNextStepAsync` signs the next step; the creator and earlier signers cannot sign. `ApprovalRecord` freezes each signature | One chain for the whole deployment; any user may sign any step |
| Approval of configuration | `ConfigurationApproval`: Draft → Approved by a second user, effective-dated | Global scope |
| Numbering | `NumberingScheme` patterns with `{MUNIDX}`, `{BRGYIDX}`, `{SECT}` tokens; `NumberSequence` counts per scope key | Already per municipality or barangay where the pattern says so |
| Branding | `Lgu:` settings in appsettings (name, office, province, address, legislature) | One letterhead |
| SMV and assessment levels | Global | Their scope by municipality is decided in L1 |
| Lists, search, dashboard, GIS | Unscoped | Everyone sees the province |

## 3. Proposal

### 3.1 Offices and jurisdiction

- **`Office`**: code, name, kind (`Provincial`, `Municipal`), head's
  position title, address, contact, status. The kinds are fixed; the offices
  themselves are configuration. The province has exactly one provincial
  office.
- **`OfficeJurisdiction`**: office + municipality + effective from/to. The
  provincial office's jurisdiction is the whole province and needs no rows.
  A municipality is covered by exactly one municipal office on any date,
  enforced by an exclusion constraint. History is kept, so a later
  reorganisation does not rewrite the past.
- Offices and jurisdictions are loaded through the content pack (a new
  `offices` kind) or entered on an admin screen, under maker-checker. The
  manifest already accepts an `office` field on approval chains and refuses
  it "until LP". This step turns it on.

### 3.2 Users in offices

- **`OfficeAssignment`**: user + office + roles (§9 codes) + from/to. A role
  is held within an office (CLAUDE.md §9). A user has one active assignment
  at a time, and the history stays.
- SYSTEM_ADMIN and AUDITOR may be assigned **province-wide** (no office).
- The request context (`ICurrentUserService`) gains the current office, its
  kind, the municipalities it covers today, and the roles held there.
- The §9 roles are seeded as fixed codes. The full permission matrix remains
  Phase 12. LP enforces only what it needs: jurisdiction (§3.3) and who may
  sign which approval step (§3.4).
- Development: the dev login gains named DEMO users per office (for example
  a DEMO municipal appraiser, a DEMO municipal assessor and a DEMO provincial
  assessor), chosen with a header. This replaces the single "act as
  checker" switch. The header stays Development-only.

### 3.3 Jurisdiction of records

- A record's jurisdiction is its **municipality**: `Property.MunicipalityId`
  for everything that hangs off a property; `SwornStatement.MunicipalityId`;
  a register run's barangay's municipality.
- **Reads.** A municipal user sees only records in the municipalities their
  office covers today. Provincial users, and province-wide roles, see
  everything. Implemented as **EF Core global query filters** on the
  property-rooted entities, driven by the request's office scope. Background
  jobs and system processes bypass them explicitly (`IgnoreQueryFilters`, as
  the job's recorded user). A record outside the user's jurisdiction answers
  **404**, the same as a missing record, so its existence is not revealed.
- **Writes.** Creating or changing a record requires its municipality to be
  in the user's jurisdiction. A transaction touching properties in two
  municipalities (a consolidation across a boundary) is refused for
  municipal users and left to the province.
- **Taxpayers** stay one provincial registry, because an owner can hold
  property in several municipalities and must not be duplicated. A municipal
  user sees a taxpayer's full details only when the taxpayer is a party to a
  property in their jurisdiction. Search still finds an exact TIN or name
  match, with name and TIN only, so the user links the existing record
  instead of creating a duplicate (CLAUDE.md §61, §68).
- **Tests.** A sweep enumerates every read and write endpoint and checks
  that a municipal user of office A gets 404 for a record of office B.

### 3.4 Approval: office chains, provincial approval, delegation

**Chains per office.** `ApprovalChain` gains `OfficeId`. A chain with no
office is the provincial default. For a record, PRIME uses the chain in force
of the office whose jurisdiction covers the record, and falls back to the
default. A record already in a chain finishes under that chain, as today.

**Who signs each step.** `ApprovalChainStep` gains:
- `SignerOffice`: `Preparing` (the municipal office covering the record) or
  `Provincial`;
- `RequiredRole` (optional), e.g. ASSESSOR;
- `IsFinalApproval` on the one step that makes the record final.

The existing rules stay: the creator cannot sign, and one person cannot sign
two steps.

**Delegation.** A dated record, not a change of chain:

- `ApprovalDelegation`: the municipal office delegated to; the delegating
  official (the Provincial Assessor, frozen name and position); the
  instrument (reference and date); the subject types covered (FAAS/TD,
  property transactions) and, optionally, property kinds; valid from/to;
  revoked at/by/reason.
- Renewal is a new record that points to the one it renews. Revocation ends
  it early, with a reason. It cannot be sub-delegated: only a user holding
  the **ASSESSOR role in the delegated office** may sign under it.
- Created by provincial staff and approved by a second provincial user
  (maker-checker, CLAUDE.md §46). A delegation is never deleted.

**Routing.** When the final step's signer office is `Provincial` and a
delegation covering the record's office and subject type is in force **on
the signing date**, the municipal assessor signs that step instead. The
`ApprovalRecord` stores the delegation's id, so the printed FAAS or TD shows
"under delegation {instrument}", and the history answers who approved and
under what authority (CLAUDE.md §111).

**Returning a record.** The province does not edit municipal records. It
**returns** them with remarks, through the existing rejection, which sends
the record back to Draft for the municipal office to correct.

### 3.5 Configuration: provincial or per office

| Configuration | Scope | Who may change it |
|---|---|---|
| Offices, jurisdictions, delegations | Province | Provincial office, maker-checker |
| Approval chains | Per office, with a provincial default | Provincial office |
| Signatories and letterhead | Per office | Provincial office, or the office itself for its own letterhead |
| Numbering schemes | Province (patterns carry the municipal and barangay tokens) | Provincial office |
| Forms, transaction types, lookups, geography | Province | Provincial office |
| SMV, assessment levels, adjustment factors | Province for now; municipal scope decided in L1 | Provincial office |

### 3.6 Letterhead and signatories per office

- The `Lgu:` settings move into `Office`: display name, office name,
  address, contact. The province-level items stay settings: the province
  name, the provincial legislature's name and the time zone.
- A form prints the letterhead of the **office whose jurisdiction covers the
  record**. Provincial documents (for example a consolidated roll) print the
  provincial office.
- Signatures come from the frozen `ApprovalRecord`s, which already carry the
  name and position. Printed position titles come from the chain steps, per
  office.
- **Logos wait for document storage** (CLAUDE.md §59, not built yet). Until
  then letterheads are text only. PRIME never prints a seal the LGU has not
  supplied (§85).

### 3.7 Consolidated view and municipal reports

- Provincial users see every municipality. Lists, search, the dashboard and
  the GIS workspace gain an office/municipality filter.
- **Transmittals.** Within one instance, "reporting to the province" becomes
  a dated **transmittal**: the municipal office closes a period, and PRIME
  freezes the list of FAAS and TDs issued and cancelled in it (an issued-form
  snapshot, as for registers). The province acknowledges receipt. LP builds
  this one transmittal. The other reports (assessment rolls by municipality,
  the BLGF reports) come with Phase 11, once the office answers question A5.

### 3.8 Existing data

The migration is additive (CLAUDE.md §105):

- one **DEMO provincial office** and one DEMO municipal office per existing
  DEMO municipality;
- existing users assigned to the DEMO provincial office;
- existing approval chains become provincial defaults;
- existing approval records keep no delegation.

Real offices then arrive from the content pack.

## 4. Security and privacy

- The jurisdiction filter applies to every read and write. The endpoint sweep
  (§3.3) is part of the quality gate.
- Out-of-jurisdiction requests answer 404 and are logged as authorization
  failures (CLAUDE.md §69), without personal data.
- Audit entries gain the acting office.
- Taxpayer details are limited as in §3.3 (Data Privacy Act; CLAUDE.md §68).
- Delegation, office and jurisdiction changes are maker-checker and audited.

## 5. Delivery steps

| Step | Scope | Verification |
|---|---|---|
| LP-1 | `Office`, `OfficeJurisdiction`, `OfficeAssignment`; role codes seeded; current-office context; admin screen; content-pack kind `offices`; dev users per office; DEMO migration (§3.8) | Integration tests; content-pack preview/import; browser |
| LP-2 | Jurisdiction filters and write guards (§3.3); taxpayer visibility rule; the endpoint sweep | Sweep: office A never reads or writes office B's records; provincial user sees all |
| LP-3 | `ApprovalDelegation` with renew, revoke and maker-checker; admin screen | Tests: dates, overlap, revocation, no sub-delegation |
| LP-4 | Office-aware chains; step signer office and role; delegated final approval; delegation shown on approval records and printed forms; "awaiting my approval" list | Tests: municipal prepare → provincial approve; the same with a delegation in force; after expiry; after revocation; self-approval refused |
| LP-5 | Per-office letterhead (§3.6); forms render the record's office | Rendered form checks; browser |
| LP-6 | Consolidated filters; transmittal of issued and cancelled FAAS/TDs (§3.7) | Tests and browser |

Each step: build, full tests, `npm run build` and lint, browser check where
there is UI, documentation (CLAUDE.md §107).

## 6. Out of scope

- The full role and permission matrix, real login and MFA (Phase 12). LP
  enforces only jurisdiction and approval signers.
- BLGF and LAM reports (Phase 11).
- The scope of SMVs and assessment levels by municipality (L1).
- Logos (after document storage, §59).
- Frozen treasury code (CLAUDE.md §0). It is not given offices.

## 7. Depends on the Provincial Assessor's answers

| Question list item | Enters PRIME as |
|---|---|
| A1 offices and coverage | Offices and jurisdictions (content pack or admin) |
| A2 delegations | Delegation records |
| A3 approval steps | Approval chains per office |
| A4 signatories | Office letterhead and chain step positions; user accounts |
| A5 reports | Transmittal contents; Phase 11 reports |
| A6 province may edit? | §3.4 "returning a record" (default: return, not edit) |
| A7 users | Office assignments |

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Office kinds | `Provincial` and `Municipal` only. Add a component-city kind only if the province has one (A1 will tell) |
| Q2 | Offices per user | One active office assignment at a time, dated history. SYSTEM_ADMIN and AUDITOR may be province-wide |
| Q3 | How to limit reads to the jurisdiction | EF Core global query filters on property-rooted entities, bypassed only explicitly by system jobs; plus the endpoint sweep test |
| Q4 | Response for another office's record | 404, as if it did not exist; logged as an authorization failure |
| Q5 | Taxpayer registry | One provincial registry. Full details only for parties to properties in the user's jurisdiction; exact TIN/name matches visible (name and TIN only) to prevent duplicates |
| Q6 | Delegation model | A dated record per municipal office: instrument, subject types (optionally property kinds), from/to, renewable, revocable, not sub-delegable (only that office's ASSESSOR signs), maker-checker |
| Q7 | Which date decides a delegated approval | The signing date |
| Q8 | Province correcting municipal records | Return with remarks (the existing rejection); no direct edit (default of A6) |
| Q9 | Letterhead | Per office, text only until document storage exists; province-level items stay settings |
| Q10 | Municipality → province reporting in LP | One dated transmittal of issued and cancelled FAAS/TDs; the other reports in Phase 11 |
| Q11 | Existing data | DEMO provincial office plus DEMO municipal offices; existing users to the province; existing chains become provincial defaults |
| Q12 | Authorization before Phase 12 | Seed the §9 role codes now. Enforce only jurisdiction and approval-signer roles in LP; the full permission matrix stays in Phase 12 |
| Q13 | Development login | Named DEMO users per office chosen by a Development-only header, replacing the single "act as checker" switch |

## 9. Implementation log

### LP-1 — offices, jurisdiction, assignments (2026-09-29)

**Built**
- **Domain:** `Office` (code, name, kind `Provincial`/`Municipal`, head's
  position, address, contact, status). `OfficeJurisdiction` and
  `OfficeAssignment` are effective-dated configuration: Draft, then approved
  by a second user through the shared `ConfigurationApproval`. Approval ends
  the previous version for the same municipality or user, and a database
  index holds one open approved version per municipality or user.
  `OfficeAssignmentRole` links an assignment to `Role`. `RoleCodes` lists
  the nine §9 codes; SYSTEM_ADMIN and AUDITOR may be province-wide.
- **Migration `Offices`:** four new tables, nothing else changed; seeds the
  nine role codes. One provincial office is enforced by a filtered unique
  index. Applied to the **local database only** (CLAUDE.md §105).
- **Rules:**
  - an office's code and kind never change;
  - only an active municipal office is given municipalities;
  - an office still covering municipalities or with staff cannot be
    deactivated;
  - a user cannot approve their own assignment, and the creator cannot
    approve either;
  - ending an assignment needs a reason, is audited and needs no second
    user (it removes access).
- **Office context:** `IOfficeContext` gives each request the acting user's
  assignment in force today: office, roles, and covered municipalities
  (none for province-wide; empty when unassigned). LP-2 uses it for
  filtering.
- **API:**
  - `/api/offices` (list, get, create, update);
  - `/api/office-jurisdictions` (list, create, approve);
  - `/api/office-assignments` (list, create, approve, end);
  - `/api/roles`, `/api/users` and `/api/me`.
- **Content pack kind `offices`** (JSON catalogue):
  - an office record is created, or its details updated (Changed), on
    import;
  - each municipality it lists becomes a **Draft** jurisdiction, unless the
    office already covers it or a draft says so;
  - municipalities the same pack adds are accepted;
  - refused: a second provincial office, municipalities under the
    provincial office, unknown municipalities, a municipality listed twice,
    a missing date, a changed kind.
  The DEMO pack has one DEMO office covering its two towns (12 files).
- **Development login (Q13):** built-in DEMO users (overridable by
  `DevAuth:Users`, since appsettings.*.json is not in git): `admin`
  (province-wide SYSTEM_ADMIN), `checker` (provincial ASSESSOR),
  `mun-appraiser` and `mun-assessor`. The existing header selects one by
  key; `/api/dev/users` lists them. `DevOfficeSeeder`, Development only,
  creates a DEMO provincial office, one DEMO municipal office per uncovered
  DEMO municipality, and the users' assignments.
- **UI:**
  - **Offices** admin page with tabs Offices, Coverage and Staff;
  - the header shows the signed-in user's office;
  - a Development-only picker replaces the "Act as checker" switch (a
    stored checker choice carries over).

**Deviation from §3.8:** the DEMO offices come from the Development-only
seeder, not from the migration, so a production database never receives
DEMO offices.

**Verified**
- 4 new integration tests (`OfficeTests`): unique codes and one provincial
  office; jurisdiction maker-checker and supersession by date, with
  deactivation refused while in use; assignment maker-checker (including
  not approving one's own), the office scope before and after approval and
  after ending, province-wide only for SYSTEM_ADMIN/AUDITOR; the dev header
  acting as the DEMO municipal appraiser through `/api/me`.
- 1 new content-pack test: offices apply on import, jurisdictions wait for a
  second user, a re-import is a no-op, and a rename is shown as Changed
  while mistakes are refused. DEMO pack counts updated (22 records, 12
  files).
- Full suite: 447 tests pass. `npm run build` and `npm run lint` are clean.
- Browser (Playwright): the header shows "Province-wide" for the usual user
  and the DEMO municipal office when acting as the DEMO municipal
  appraiser. The Staff tab lists the DEMO assignments, the Coverage tab the
  DEMO jurisdiction, and a new office saves. No console errors; no
  horizontal overflow at 390 px. The check left office `DEMO-UI-LP1` in the
  dev database.

### LP-2 — jurisdiction of records (2026-09-29)

**Built**
- **`IJurisdiction`** (Application) and its request-scoped `JurisdictionState`
  (Infrastructure). `JurisdictionMiddleware` runs after user provisioning:
  - a municipal user is restricted to the municipalities their office covers
    today;
  - a user with no office in force is restricted to none;
  - provincial and province-wide users are not restricted;
  - outside a request (background jobs, start-up seeders, tests calling
    services directly) nothing is restricted.
- **EF Core global query filters** (`PrimeDbContext.ApplyJurisdictionFilters`)
  read that state on every query:
  - by `Property.MunicipalityId` on properties, RPUs, parcels, land,
    buildings, machinery, property parties, PIN assignments, valuations,
    assessments, TDs, notices and their items, and property transactions;
  - by `MunicipalityId` on sworn statements;
  - by the barangay's municipality on register runs (the ORC, kept by
    owner, lists only the rows the user may see).

  A record outside the jurisdiction reads as missing (404, Q4).
- **Province-wide uniqueness:** the checks for the PIN, the RPU number, the
  TD number, a parcel number in a section, and the "index number locked by
  permanent PINs" rules use `IgnoreQueryFilters()` at the top of their own
  query. Inside a subquery it would lift every filter of the whole query, so
  it is never used there.
- **Writes:** registering a property, creating a sworn statement or a
  register run in a municipality outside the jurisdiction →
  `JURISDICTION_FORBIDDEN` (HTTP 403). Everything created under a property
  finds the property through a filtered lookup, so it is refused as "not
  found". A transaction across a boundary is therefore impossible for a
  municipal user and stays with the province.
- **Issued forms:** fetching, listing or cancelling an issued form checks
  that its subject is visible.
- **Taxpayers (Q5):** one provincial registry. A restricted user sees full
  details only for parties to properties in the jurisdiction. Others show
  name and TIN only (`limited`, marked "other jurisdiction" in the list).
  Search outside the jurisdiction finds only an exact TIN or surname/
  corporate-name match.
  **Narrowed from a first draft:** a taxpayer not yet linked to any property
  also shows name and TIN only. Allowing more would need an unfiltered
  subquery, and the approved Q5 rule does not ask for it. The creator
  already has the full record from creating it.
- **Not filtered:** the frozen treasury tables (`TaxBill`,
  `PaymentAllocation`; CLAUDE.md §0), and a transaction's related-property
  links (reached through the filtered transaction). The test lists these
  exceptions with their reasons.

**Verified**
- 5 new integration tests (`JurisdictionTests`):
  - **model:** every entity with a required `PropertyId` has a jurisdiction
    filter or a listed reason;
  - **rows:** restricted to an empty municipality, every filtered table
    returns nothing, while an unfiltered lookup still finds the rows;
  - **services:** another municipality's property, RPU, parcel, TD and
    ownership history are "not found"; registering a property there is
    refused; an RPU or owner cannot be added under its property; its RPU
    number still counts as taken;
  - **taxpayers:** the Q5 rule above;
  - **pipeline:** the DEMO municipal appraiser's property list holds only its
    municipalities, and the province-wide user sees at least as many.
- Full suite: 452 tests pass. `npm run build` and `npm run lint` are clean.
- Live API on the dev database: the usual user sees 43 properties across
  four DEMO/TEST municipalities; the DEMO municipal appraiser sees only the
  13 of DEMO_Municipality.
- Browser (Playwright): the usual user opens a property of another
  municipality; the appraiser gets "No property was found with the given
  id." and a list of their municipality only; taxpayers show only their
  parties. No console errors.

**Also changed:**
- The frontend no longer retries 4xx responses. A record outside the
  jurisdiction now shows "not found" at once instead of after about seven
  seconds of retries.
- The Properties list scrolls horizontally inside its table at phone width.
  This overflow existed before LP-2 for every user.

### LP-3 — delegation records (2026-09-29)

**Built**
- **`ApprovalDelegation`** (Domain/Entities/Offices):
  - the municipal office delegated to;
  - the delegating official's name and position, frozen;
  - the instrument's reference and date;
  - the records covered (Tax Declarations, assessments, property
    transactions), stored as a text array;
  - optionally, the property kinds covered (empty = every kind);
  - a required `ValidFrom`/`ValidTo` period;
  - status Draft / Approved / Rejected (with reason);
  - `RenewsDelegationId`;
  - revocation: `RevokedFrom`, when, by whom, why.
  Migration `ApprovalDelegations`: one new table with check constraints;
  **local database only**.
- **Rules (`ApprovalDelegationService`):**
  - only a provincial or province-wide user enters, approves, rejects and
    revokes (`DELEGATION_FORBIDDEN`, 403), and the one who entered it cannot
    approve it;
  - only to an active municipal office;
  - approved delegations of one office may not overlap in both period (up
    to any revocation) and records covered, checked on entry and again on
    approval;
  - a renewal names an approved delegation of the same office and starts
    later;
  - revocation needs a reason and takes effect today or later, so approvals
    already signed under it stand;
  - a draft is rejected, not revoked;
  - nothing is deleted.
- **`FindInForceAsync(office, record type, property kind, date)`** returns the
  delegation in force on that date, for LP-4's routing (Q7: the signing date
  decides). The list shows each delegation's state: Draft, Rejected,
  Scheduled, In force, Expired, Revoked.
- **API** `/api/approval-delegations`: list, get, create, approve, reject,
  revoke. **UI:** a Delegations tab on the Offices page (enter, approve,
  reject, revoke; renewals chosen from approved delegations).
- Not in the content pack: delegations (question A2) are entered on the
  screen under maker-checker, as they come in.

**Verified**
- 3 new integration tests (`ApprovalDelegationTests`):
  - municipal users refused, the maker cannot approve, a second provincial
    user can;
  - lookups by record type, date bounds and the provincial office;
  - overlap refused, renewal rules, delegation limited to land;
  - revocation not retroactive, in force until the day before, not twice;
    rejecting drafts only.
- Full suite: 455 tests pass. `npm run build` and `npm run lint` are clean.
- Browser (Playwright):
  - a DEMO delegation entered by the usual user;
  - the DEMO municipal assessor's approval refused with the message;
  - approved by the checker, shown "In force";
  - revoked from today, shown "Revoked";
  - no console errors, no overflow at 390 px.
  The revoked DEMO delegation `DEMO-UI-LP3 …` remains in the dev database.
