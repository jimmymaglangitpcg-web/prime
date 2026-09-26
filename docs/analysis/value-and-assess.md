# PRIME — "Value and Assess" on screen (approved and implemented, 2026-09-26)

The goal: an assessor can value a unit, assess it and carry the assessment
through review, approval and posting from the screen, and can maintain the
valuation rules (SMVs, schedules, adjustment factors, assessment levels) the
calculation uses. Together with what exists, this gives the whole chain on
screen: record → value → assess → approve → post → TD → FAAS → notice → bill.

All values entered through these screens are the LGU's to supply. PRIME
invents none; the development database holds only DEMO rules.

## 1. What exists, and the gaps

| Area | Exists (API/service) | Gap |
|---|---|---|
| Valuation | Per-asset endpoints (land, building, machinery); `ComputeForRpuAsync` in the service; every run is saved as a `Valuation` with its lines | Valuing a whole unit is not exposed; the API's valuation data omits the lines, so the row-by-row calculation cannot be shown |
| Assessment | Create from a valuation (levels per classification/actual-use row), submit, approve (maker-checker or approval chain), reject, post (prepares a draft TD) | No preview before saving; the "previous assessment" is not checked to be the same unit's |
| SMV | Create and approve SMVs and schedules; list a SMV's schedules | No list of SMVs |
| Assessment levels | Create, approve, list | Creating a level closes the open level of the same classification/use/type, so **value brackets cannot coexist**; ranges match inclusively at both ends, so a value exactly on a boundary fits two brackets |
| Adjustment factors | Create, approve, list | — |
| Screens | The Assessments table per unit, with the appraisal record | No Value, Assess, Submit, Approve, Reject or Post actions; no rule administration |

## 2. Backend changes (small, no new tables)

1. **Value a unit:** `POST /api/rpus/{rpuId}/valuations` runs `ComputeForRpuAsync`
   and returns the saved valuation.
2. **Valuation lines in the API:** `ValuationDto` gains its lines (source,
   description, classification, actual use, quantity, unit, unit value, SMV
   schedule, market value, and the breakdown in calculation order) and the
   SMV's ordinance number and revision year.
3. **Assessment preview:** `POST /api/assessments/preview` takes the same
   request as create and returns the rows that would be assessed (market
   value, level found, assessed value), or the error (e.g.
   `ASSESSMENT_LEVEL_NOT_FOUND`), without saving. Create and preview share one
   calculation (CLAUDE.md Rule 9: a single source of assessment logic).
4. **Previous assessment:** it must be a posted assessment of the same unit
   (`PREVIOUS_ASSESSMENT_INVALID`). The screen proposes the unit's latest
   posted one.
5. **Level brackets:** a new level closes only the open levels of the same
   key whose value range overlaps it. Levels with separate ranges stay open
   side by side, and approving a level that overlaps another open, approved
   level of the same key is refused (`ASSESSMENT_LEVEL_OVERLAP`).
6. **Bracket boundaries:** match a value as "over the lower value, not over
   the upper value" (a lower value of 0 includes 0), in place of "inclusive
   at both ends". This follows the "Over / Not Over" form of the LGC §218
   level tables. **DOMAIN VERIFICATION REQUIRED** against the LGU's
   ordinance.
7. **List SMVs:** `GET /api/smv`, paged, with status, revision year and ordinance.

## 3. Screens

**On each unit (Property Profile → RPUs → the unit's row)**
- **Value:** values the unit and opens the valuation panel:
  - each row's calculation (area × unit value, adjustments, depreciation, as
    recorded), in order;
  - the SMV and schedule used, the method, and the total market value;
  - the unit's earlier valuations.
- **Assess** (from a valuation):
  - enter the assessment year, the effective date, the previous assessment
    (the latest posted one is proposed) and remarks;
  - a **preview** shows each classification/actual-use row, with its market
    value, level and assessed value, before the draft is created;
  - PRIME proposes no effectivity date: the rule for when an assessment
    takes effect is the LGU's to confirm (DOMAIN VERIFICATION REQUIRED).
- **Assessments table:** actions by status: Submit for review (Draft),
  Approve / Reject with a reason (Pending review), and Post (Approved, after
  a confirmation).
- **Before and after:** the previous and the new market and assessed values,
  and the difference (CLAUDE.md §51).
- After posting, a link to the draft Tax Declaration PRIME prepared, if it
  prepared one.

**Valuation Rules** (new admin page, `/admin/valuation`)
- **SMVs:**
  - a list, create (ordinance no. and dates, revision year) and approve;
  - per SMV, its schedules: list, create (classification, actual use,
    property type, zone, improvement kind, unit, market value, minimum and
    maximum, effective date) and approve.
- **Adjustment factors:** list, create and approve.
- **Assessment levels:**
  - a list filtered by classification, use and type;
  - create (ordinance, classification, actual use, property type, value
    range, percentage, effective date) and approve;
  - brackets are shown grouped by key.
- **No editing:** a change is a new version that supersedes the old from its
  effective date (CLAUDE.md §28–29). Approval is maker-checker: the creator
  cannot approve.

## 4. Testing maker-checker in the browser

In development every request is one fixed user, so no one can approve what
that user created, and the approval steps cannot be tried on screen. Proposal:
a **development-only "act as checker" switch**.
- It is enabled only where the development login bypass already is
  (Development environment and `DevAuth:Enabled`).
- A header selects a second fixed development user, and a visible banner
  shows "Acting as DEV CHECKER".
- It is never available anywhere else.

## 5. Delivery

1. **VA-1:** the backend changes in §2, with integration tests (unit
   valuation with lines, preview = create, previous-assessment check, brackets
   side by side, boundary matching, overlap refusal).
2. **VA-2:** Value and Assess on the unit, and the workflow actions and
   before/after in the Assessments table.
3. **VA-3:** the Valuation Rules admin page.
4. **VA-4:** the development checker switch, and a browser run of the whole
   chain on DEMO data: value → assess → submit → approve (as checker) →
   post → approve the prepared TD → print the land FAAS with values.

## 6. Open for review

1. Every Value click saves a valuation, as today (a valuation is history;
   the assessment names the one it used). The alternative is a preview-only
   valuation.
2. A preview before an assessment draft is created.
3. Level brackets side by side; a new level closes only the overlapping ones;
   an overlap is refused on approval.
4. Boundaries match "over the lower, not over the upper" (DOMAIN
   VERIFICATION REQUIRED).
5. No proposed effective date: the user enters the year and the effective
   date.
6. The previous assessment is proposed and must be a posted assessment of
   the same unit.
7. The rules are create and approve only, with no editing; a change is a
   new version.
8. A development-only "act as checker" switch, to try approvals on screen.

### 6.1 Decisions (user, 2026-09-26: "yes")

All eight proposals in §6 were accepted as written.

## 7. Implementation status (2026-09-26)

**Backend (VA-1).** No migration.
- `POST /api/rpus/{rpuId}/valuations` values the whole unit.
- `ValuationDto` carries `Lines`, each with its breakdown in calculation
  order, and the SMV's ordinance number and revision year. The one
  breakdown ordering (`ValuationBreakdown.Ordered`) is also used by the
  appraisal record.
- `POST /api/assessments/preview`: create and preview share one
  calculation (`CalculateAsync`).
- The previous assessment must be a posted assessment of the same unit
  (`PREVIOUS_ASSESSMENT_INVALID`). The general revision now links to the
  unit's latest *posted* assessment; before, it took the latest of any status.
- Assessment levels:
  - a new level closes only the overlapping open levels of its key;
  - approving an overlap is refused (`ASSESSMENT_LEVEL_OVERLAP`);
  - ranges match "over the lower, not over the upper" (a lower value of 0
    includes 0), in `AssessmentLevelService.RangesOverlap` and in the
    resolver.
- `GET /api/smv` lists SMVs (paged).
- Posting returns `TaxDeclarationNote`: whether a draft TD was prepared,
  and if not, why (e.g. no TD numbering scheme in force).
- The development-only act-as-checker switch: the header
  `X-Prime-Dev-Act-As: checker` selects `DevAuth:CheckerUserId` in
  `DevelopmentAuthenticationHandler`, which exists only in Development with
  `DevAuth:Enabled`.

**Screens (VA-2, VA-3).**
- Each unit's Assessments table has **Value and assess**:
  - a valuation drawer with each row's calculation, the SMV, method and
    total, and earlier valuations;
  - **Assess this valuation**: year, effective date (not proposed), the
    previous assessment (latest posted proposed) and remarks, with a preview
    and before-and-after, then the draft is created.
- The table has workflow actions (Submit, Approve, Reject with a reason,
  Post with a confirmation), a "Change in AV" column, and the posting
  message, including why no TD was prepared.
- **Valuation Rules** (`/admin/valuation`):
  - SMVs, with a schedules dialog;
  - adjustment factors;
  - assessment levels grouped by key as brackets;
  - create and approve only.
- The header has an **Act as checker (dev)** switch, with a warning banner
  while it is on.
- Also fixed, both found in the browser run:
  - The "Add Tax Declaration" dialog can now name the posted assessment the
    TD declares; that was not possible on screen before.
  - The dialog now proposes the current TD as the one replaced even when the
    TD list loads after the dialog opens. Before, it showed "None", and the
    TD could then not be approved.
  - The Property Profile note no longer says that assessments are done
    through the API.

**Verification.**
- Integration tests:
  - `ValueAndAssessTests` (11): unit valuation rows and SMV; preview equals
    create without saving; previous-assessment rule; brackets side by side
    with both boundary cases; overlap closing and refusal; range overlap
    cases; SMV list; posting note.
  - `DevActAsCheckerTests` (4).
  - The reassessment test now requires a posted previous assessment.
- Full suite: 101 domain, 37 application and 182 integration tests pass.
- The frontend production build and lint pass.
- Browser (Playwright), on the DEMO land unit:
  1. Valued: 2 strips, 600,000.00.
  2. Assessed with a preview: 20%, 120,000.00, +20,000.00 against 2026.
  3. Submitted.
  4. Approval as the creator was refused ("The assessment's creator cannot
     also approve it.").
  5. Approved as checker, then posted. The message said no TD was prepared,
     because the dev database has no TD numbering scheme in force.
  6. A TD (DEMO-TD-VA-2027B) was added by hand, declaring the 2027
     assessment and replacing R5, then submitted and approved as checker;
     R5 was cancelled.
  7. The land FAAS printed with the approved 2027 values.
  8. The Valuation Rules page was checked.
  There were no console errors.
- DEMO data left in the dev database:
  - a 2027 posted assessment on the DEMO land;
  - TD DEMO-TD-VA-2027B approved, and R5 cancelled;
  - a rejected TD DEMO-TD-VA-2027;
  - a stale draft TD DEMO-TD-DEMO-MUN-01-2026-0001, pending review. It
    names R2 as the TD it replaces, and R2 was cancelled by a later
    transfer, so it can no longer be approved.

**Gaps.**
- A draft TD prepared on posting names the TD current *at posting*. If
  another TD is approved before it (e.g. by a transfer), the draft goes
  stale and must be rejected and prepared again by hand.
- Rules have no admin for lookups (classifications, actual uses, zones,
  improvement kinds).
- Nothing in the API enforces permissions yet; that waits for Phase 12
  (RBAC).
