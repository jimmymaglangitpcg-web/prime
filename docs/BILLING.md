# PRIME — Billing (Phase 8)

Status: **§3 rule model implemented** (Phase 8 step 1 — entities, the
`BillingRules` migration, and create/get/approve endpoints under
`/api/billing/...` (list supports `?asOf=`), with tax types at `/api/reference/tax-types`).
§4 (bill) and §5 (engine) are still outlines. All values used in
development and tests are **DEMO** values (CLAUDE.md §81) until official
ordinance values are supplied.

## 1. Scope

CLAUDE.md §39/§44/§52/§95: generate a Real Property Tax bill for an RPU
and tax year from its **posted** assessment, applying configurable,
effective-dated rules — basic RPT, additional levies, discounts, penalties
and interest — with a line-by-line breakdown that can be reproduced later.
Payments, allocation and collection are Phase 9; delinquency reporting is
Phase 10.

## 2. Legal / domain items — DOMAIN VERIFICATION REQUIRED

PRIME does not decide any of these. The rule model (§3) must be able to
represent them; the values come from the LGU's ordinances and the
applicable national law. The provisions below are the ones commonly cited
for these rules in the Local Government Code (RA 7160). **They must be
checked against the official text, and against RA 12001 and its IRR
(which amended the real property valuation and assessment provisions),
before any real value is configured.** Nothing here is used as a system
value.

| Rule | Commonly cited basis (verify) | System representation |
|---|---|---|
| Basic RPT rate (differs for provinces vs. cities/municipalities in Metro Manila; actual rate set by ordinance up to a ceiling) | RA 7160 §233 + local ordinance | `TaxRate` (tax type = basic RPT) |
| Special Education Fund additional levy | RA 7160 §235 + local ordinance | `TaxRate` (tax type = SEF) |
| Other additional levies (e.g. on idle lands, special levies) | RA 7160 §236–§245 + ordinance | `TaxRate`, optionally limited to a classification |
| Payment in installments and their due dates | RA 7160 §250 + ordinance | `PaymentSchedule` + installments |
| Discount for advance / prompt payment (ceiling in law, actual rate by ordinance) | RA 7160 §251 + ordinance | `DiscountRule` |
| Interest on unpaid tax (rate per month, cap on months) | RA 7160 §255 | `InterestRule` |
| Any other surcharge/penalty an ordinance imposes | Local ordinance | `PenaltyRule` |
| **Cap on the RPT increase from a new SMV** — first year of the approved SMV, max 6% of the RPT assessed before, **per tax type** (basic, SEF, idle land, special levies); later-year caps optional by ordinance | **RA 12001 §29 ¶3; IRR (BLGF MC 001-2025) §55** — verified against official text, see docs/analysis/current-real-property-regulatory-baseline.md | `TaxIncreaseCapRule` (§3.7) |
| Exemptions | RA 7160 §234 + others | Out of Phase 8 — `PropertyExemption` (§43), later |
| Rounding of tax amounts | Not established — LGU/COA practice | Engine setting, **to confirm** |
| How a partial month counts for interest | Not established — to confirm | `InterestRule.MonthCounting` |

Open question for the LGU/legal reviewer: whether RA 12001 or its IRR
changed any billing-side rule above, and from which effective date. The
one change identified so far — the increase cap — is modelled in §3.7.

## 3. Rule model (proposed)

Every rule table shares these fields (the `AssessmentLevel`/`Smv`
precedent — ordinance fields inline, since no `Ordinance` table exists):

```text
LegalBasis        text, required — e.g. "RA 7160 §233; Ord. No. 2026-012 §3"
OrdinanceNumber   required
OrdinanceDate     optional
EffectiveDate     required
EndDate           null = open; INCLUSIVE last day (AssessmentLevel convention)
Status            WorkflowStatus: Draft → Approved (maker-checker, §46)
ApprovedBy / ApprovedAt
Remarks
```

Only **Approved** rules are ever applied. Approving a rule closes the
previously approved rule with the same *scope key* (sets its `EndDate` to
the day before the new `EffectiveDate`); a new rule must start after the
one it replaces. Rules are never edited after approval and never deleted —
a correction is a new rule (§49/§76).

> Note — deliberate difference from `AssessmentLevel`: there, *creating a
> draft* already closes the currently-open level, so an abandoned draft
> would end a live rule. Billing rules close their predecessor only **on
> approval**. (The `AssessmentLevel` behaviour is flagged as a follow-up.)

### 3.1 `TaxType` (reference table, §27)

LGU-configurable lookup (Code, Name, SortOrder, IsActive), e.g. DEMO rows
`DEMO_BASIC`, `DEMO_SEF`. Each bill line names its tax type, so
collections can later be reported per fund (§41 "tax-type collection").

### 3.2 `TaxRate` — basic RPT and additional levies

```text
TaxTypeId           required
ClassificationId    optional — null = applies to every classification
Rate                percent of assessed value, numeric(9,6), 0–100
```
Scope key: (TaxTypeId, ClassificationId). A classification-specific rate
takes precedence over the general (null) one for the same tax type.

### 3.3 `PaymentSchedule` + `PaymentScheduleInstallment`

```text
PaymentSchedule:              (common rule fields)
PaymentScheduleInstallment:   Sequence (1..n), DueMonth, DueDay, SharePercent
```
Shares must total exactly 100. Due dates are month/day within the tax
year. Scope key: one schedule at a time.

### 3.4 `DiscountRule`

```text
TaxTypeId     optional — null = all tax types on the bill
Kind          PROMPT_PAYMENT   — installment paid on/before its due date
              ADVANCE_PAYMENT  — whole year paid on/before a cutoff
Rate          percent of the tax amount it applies to
CutoffMonth, CutoffDay, CutoffYearOffset   (ADVANCE_PAYMENT only;
              offset −1 = the year before the tax year, 0 = the tax year)
```
Scope key: (Kind, TaxTypeId). Whether both kinds can stack is an
**engine setting to confirm** — default: no stacking; the larger single
discount applies.

### 3.5 `InterestRule` — on unpaid, overdue tax

```text
TaxTypeId          optional
RatePerMonth       percent per month of the unpaid overdue amount
MaxMonths          cap on months counted (null = no cap)
MonthCounting      FRACTION_COUNTS_AS_FULL_MONTH | COMPLETED_MONTHS_ONLY
```
Scope key: (TaxTypeId).

### 3.6 `PenaltyRule` — ordinance surcharges, if any

```text
TaxTypeId          optional
Rate               percent of the overdue amount, or
FixedAmount        numeric(18,2)          (exactly one of the two)
AppliesAfterDays   grace days after the due date
```
Scope key: (TaxTypeId). May have no approved rule at all — then no
penalty lines are produced.

### 3.7 `TaxIncreaseCapRule` — cap on the tax increase from a new SMV

Source text (IRR §55, mirroring RA 12001 §29 ¶3): *"For the first year of
effectivity of the approved SMV … any increase in real property taxes
shall be limited to a maximum of six percent (6%) of the real property
taxes assessed on such properties prior to the effectivity of the first
SMV under the Act: Provided, That the cap shall be applicable to each type
of real property tax, including Special Education Fund, Idle Land Tax, and
other Special Levies … Provided, further, That the LGU may impose, by way
of an ordinance, a cap on the increase in real property taxes for the
succeeding years."*

```text
SmvId               required — the SMV revision whose increases are capped
TaxTypeId           optional — null = every tax type, EACH ON ITS OWN
Basis               STATUTORY_FIRST_YEAR | LOCAL_ORDINANCE
Baseline            TAX_BEFORE_SMV | PREVIOUS_TAX_YEAR
MaxIncreasePercent  percent of the baseline tax, numeric(9,6), ≥ 0
EffectiveDate / EndDate   REQUIRED bounded window (inclusive)
```
Scope key: (SmvId, TaxTypeId, Basis). Unlike the other rules a cap is not
open-ended, so approval does not close a predecessor; instead approved
windows of one scope may never overlap (service check +
`EX_TaxIncreaseCapRules_NoOverlap`, a PostgreSQL `EXCLUDE USING gist`
constraint, which needs the `btree_gist` extension).

Enforced: a cap applies per tax type, never to the bill total; a statutory
cap uses the pre-SMV baseline and covers at most one year from its
effective date; the window cannot start before the SMV's effectivity date;
the SMV must be Approved/Posted before the cap is approved; maker-checker
as for every rule. **The percentage is configuration** — entered with its
legal basis and approved by a second user, not a code constant.

Engine intent (step 2): for each tax type, capped tax =
min(computed tax, baseline tax × (1 + MaxIncreasePercent/100)); the bill
line records the cap rule, baseline and uncapped amount, so the reduction
is explainable (CLAUDE.md §31).

**DOMAIN VERIFICATION REQUIRED** before real use:
- **Baseline year.** The Act says tax assessed "prior to the effectivity
  of this Act"; the IRR says "prior to the effectivity of the first SMV
  under the Act". Which tax year's figure PRIME should use (and whether
  the IRR reading governs) must be confirmed.
- **Which increases are capped.** The IRR title limits the cap to increases
  "resulting from the increase in real property values and assessments".
  Whether tax rises from new improvements, reclassification or new
  discovery are excluded is not stated.
- **Properties with no prior tax** (new discoveries, new improvements):
  no baseline exists; treatment not stated. Default intent: no cap applies,
  flagged on the bill for review.
- **"First year of effectivity"** vs. the tax year: how the SMV's first
  year maps to billed tax years (LGC §221 January-1 effectivity) must be
  confirmed; PRIME stores the window as explicit dates.
- **Order of application** with discounts, penalties and interest
  (cap on the gross annual tax is the default intent).
- Whether a statutory cap is needed for SMVs after the first under the Act
  (the text speaks of "the first SMV"); later caps are ordinance-only.

## 4. Bill (step 3 — outline only)

```text
TaxBill:        Id, PropertyId, RpuId, TaxDeclarationId, AssessmentId,
                TaxYear, AssessedValue (frozen), BillDate, AsOfDate,
                Status (Draft → Posted → Cancelled/Superseded), BillNumber
TaxBillDetail:  Id, TaxBillId, InstallmentSequence, TaxTypeId,
                ComponentType (BASIC_RPT | ADDITIONAL_LEVY | DISCOUNT |
                               PENALTY | INTEREST),
                RuleTable + RuleId (the rule applied), RateApplied (frozen),
                BaseAmount, Amount, DueDate, Explanation
```
- **Total = Σ details**, always (DOMAIN-MODEL.md §2) — never stored
  separately from its lines.
- Every line freezes the rule id **and** the rate it used, so a bill keeps
  its meaning after the rule is superseded (the `Assessment` precedent).
- Discounts, penalties and interest depend on *when* payment happens, so a
  bill is computed **as of a date**; recomputing for a later date produces
  a new bill version, never an edit (§76). Idempotency: one current bill
  per (RPU, tax year, as-of date).
- Money: `decimal` / `numeric(18,2)`, rounding at line level with
  `MidpointRounding.AwayFromZero` (the `AssessmentService` precedent) until
  the LGU confirms otherwise.

## 5. Engine (step 2 — outline only)

A pure, deterministic `BillingCalculator` (no database access): input =
assessed value, classification, tax year, as-of date and the set of
applicable approved rules; output = the lines with explanations. The
database-facing `BillingService` resolves the rules and persists the
result. This keeps every financial case in §75 unit-testable.

## 6. Out of scope for Phase 8

Payments and allocation (Phase 9), delinquency aging and reports
(Phase 10), exemptions (§43), the Treasurer's billing UI beyond a
statement of account, and role gating (Phase 12).
