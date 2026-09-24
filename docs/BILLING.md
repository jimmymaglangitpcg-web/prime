# PRIME — Billing (Phase 8)

Status: **implemented through Phase 8 step 3** — rule model (§3), bill
(§4), engine (§5), and bill service/API/UI (§6). All values used in
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

## 3. Rule model (implemented)

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

## 4. Bill (step 3 — implemented)

```text
TaxBill:         Id, PropertyId, RpuId, TaxDeclarationId, AssessmentId,
                 TaxYear, AsOfDate, RulesAsOfDate,
                 AssessedValue, ClassificationId, DiscountStackingAllowed (frozen),
                 Notes, Status (Draft → Posted → Cancelled),
                 PostedAt/By, CancelledAt/By, CancellationReason,
                 SupersededByBillId, audit fields
TaxBillTaxType:  TaxBillId, TaxTypeId, TaxRateId, RatePercent,
                 ComputedAnnualTax, CapRuleId, CapBaselineTax, CapLimit,
                 AnnualTax
TaxBillDetail:   TaxBillId, LineNumber, InstallmentSequence, DueDate,
                 TaxTypeId, Component (Tax | Discount | Penalty | Interest),
                 RuleId, RatePercent (frozen), BaseAmount, Amount, Months,
                 Explanation
```
- Basic RPT vs. an additional levy is told apart by the line's tax type,
  not by `Component`, so no tax type is special-cased in code.
- **Total = Σ details**, always — never stored.
- Every line freezes its rule id **and** rate, so a bill keeps its meaning
  after a rule is superseded. `RuleId` has no FK because it spans four rule
  tables; rules are never deleted. `TaxBillTaxType` has real FKs to its
  `TaxRate` and cap rule.
- Bills are never edited or deleted (§76). A recomputation is a new bill.
- Constraints (migration `TaxBills`, additive):
  - One live (non-cancelled) bill per (RPU, tax year, as-of date):
    `UX_TaxBills_Rpu_TaxYear_AsOf_Live`.
  - At most one **posted** bill per (RPU, tax year):
    `UX_TaxBills_Rpu_TaxYear_Posted`.
  - Discounts are the only negative lines.
  - Annual tax is at most the computed annual tax.
  - Status and timestamp consistency.
- Money: `numeric(18,2)`, with rounding per line (§5).
- Bill numbers are **not** assigned yet. Document numbering must be
  configurable (CLAUDE.md §7) and is still to be designed; bills are
  identified by id until then.

## 5. Engine (step 2 — implemented)

`Prime.Domain.DomainServices.BillingCalculator` — pure and deterministic
(no database access; the `ValuationCalculator` pattern). Input
(`BillingCalculationInput`): assessed value, classification, tax year,
as-of date, the approved rules in force, and cap baselines. Output
(`BillingCalculationResult`): per tax type the computed and capped annual
tax, plus bill lines that each carry the rule id, the rate used, the base,
the amount and a readable explanation. `Total` is always the sum of the lines.
The database-facing `BillService` (§6) decides which rules are in force
and saves the result.

What it does, per tax type:

1. **Annual tax** = AV × rate, rounded. A classification-specific rate wins
   over the general one; a rate for another classification is ignored.
2. **Cap** (§3.7): if a cap applies (tax-type-specific wins over general)
   and a baseline of the cap's kind was supplied, annual tax =
   min(annual tax, round(baseline × (1 + MaxIncreasePercent/100))). With
   no baseline the cap is **not** applied and a note says so. A zero
   baseline is applied literally (limit 0).
3. **Installments**: each share = round(annual × share%); the **last
   installment takes the remainder**, so installments always add up to the
   annual tax.
4. Per installment, as of the as-of date (a bill assumes full payment on
   that date; nothing is assumed already paid — payments are Phase 9):
   - **not overdue** (as-of ≤ due date): prompt-payment discount; advance
     discount if as-of ≤ its cutoff (tax year + offset, month, day). If
     both apply, only the larger one is given unless the
     `AllowDiscountStacking` option is on.
   - **overdue**: never a discount. Penalty once days past due exceed the
     rule's grace days: rate × the installment's tax, or the fixed amount.
     Interest = tax × RatePerMonth × months, where months are counted from
     the due date's day of month (31 Mar → 30 Apr is one completed month),
     rounded up for a partial month or not according to the rule's
     `MonthCounting`, and capped at `MaxMonths`.
   - Penalty and interest are charged on the installment's tax only (not
     on each other).

Refused, with the reason, before calculating (`Validate`): negative
assessed value; any rule that is not Approved; no applicable tax rate; two
rules with the same scope (e.g. two general prompt-payment discounts);
installment shares that do not total 100; an advance discount with no
cutoff; a **fixed-amount penalty with no tax type**, because charging a
fixed amount "for every tax type" would add it once per tax type, and
nobody has confirmed that reading.

Rounding: 2 decimals per line, `MidpointRounding.AwayFromZero`.

**DOMAIN VERIFICATION REQUIRED** (engine choices made without a legal source;
each can be changed in one place):
- Rounding mode and level (per line vs. per total).
- Discount stacking (defaults to off, passed in as an option).
- Whether interest compounds or includes penalties (currently it does neither).
- The start of the interest month count (currently the due date itself, with
  delinquency beginning the day after).
- Whether a fixed penalty applies per installment (currently yes).
- Which date picks the rules in force for a tax year (default chosen in §6.1).

Tests: `tests/Prime.Domain.Tests/DomainServices/BillingCalculatorTests.cs`
(46 cases, all using DEMO values) cover zero, large amounts, fractional rates,
midpoint rounding, uneven installment splits, caps, each discount kind with
and without stacking, interest month counting and the MaxMonths cap,
penalty grace days, mixed past-due and future installments, traceability,
determinism and every refusal.

## 6. Bill service, API and UI (step 3)

`BillService` resolves the inputs, calls `BillingCalculator` and saves the
result. It does no arithmetic of its own.

API:
- `POST /api/bills {rpuId, taxYear, asOfDate}` → Draft bill.
- `POST /api/bills/{id}/post`
- `POST /api/bills/{id}/cancel {reason}`
- `GET /api/bills/{id}`
- `GET /api/properties/{id}/bills`
- `GET /api/properties/{id}/statement-of-account`

UI: a **Billing** tab on the Property Profile (generate, post with
confirmation, cancel with a reason, full per-line breakdown) and a printable
**Statement of Account** page (`/properties/:id/statement-of-account`).

### 6.1 Defaults chosen for now (2026-09-25; user: "decide what is efficient, I'll change it later")

Each default is changeable in one place, and each is **DOMAIN
VERIFICATION REQUIRED**:

| Decision | Default | Where to change |
|---|---|---|
| Date the rules and the assessment are taken as in force | **1 January of the tax year** (LGC §221 January-1 effectivity) — frozen on the bill as `RulesAsOfDate` | `BillService.RulesAsOfDate` |
| Which assessment is billed | The latest **Posted** assessment for the RPU effective on or before that date. No proration for mid-year (e.g. next-quarter) effectivity | `BillService.GenerateAsync` |
| Classification and taxability | From the RPU's current Tax Declaration. A non-taxable TD is refused (`BILL_PROPERTY_EXEMPT`); exemptions are out of scope | `BillService.GenerateAsync` |
| Which caps apply | Approved caps in force on the rules date whose SMV is the one the assessment's valuation used (machinery has no SMV, so it is never capped) | `BillService.GenerateAsync` |
| Cap baseline source | PRIME's own **posted** bills for the RPU. `TaxBeforeSmv` → latest posted bill for a tax year before the SMV's effectivity year. `PreviousTaxYear` → the posted bill for tax year − 1. With no such bill the cap is **not applied** and the bill's notes say so. Pre-PRIME tax history (data migration, Phase 13) will be needed for real use | `BillService.CapBaselinesAsync` |
| Discount stacking | Off (`Billing:AllowDiscountStacking` in appsettings); frozen on each bill | configuration |
| Recomputing a bill | A new bill. Posting it cancels the previously posted bill for the same RPU and tax year (`SupersededByBillId`); the statement of account lists only posted bills | `BillService.PostAsync` |
| Maker-checker on posting | **Not enforced** (posting is not an approval of a rule); role gating is Phase 12 | `BillService.PostAsync` |

Errors: `BILL_ASSESSMENT_NOT_FOUND`, `TAX_DECLARATION_NOT_FOUND`,
`BILL_PROPERTY_EXEMPT`, `BILL_DUPLICATE` (409),
`BILL_PAYMENT_SCHEDULE_NOT_FOUND` (not exactly one in force),
`BILL_RULES_INVALID` (the calculator's own refusals), `BILL_NOT_DRAFT`,
`BILL_POST_CONFLICT` (409), `BILL_ALREADY_CANCELLED`, `BILL_NOT_FOUND`.

Tests: `tests/Prime.IntegrationTests/BillingFlowTests.cs` (8). The flow
was also verified live in a browser against the dev database on
2026-09-25, using the property `DEMO-BILL-AE94B8`: generate → breakdown →
post → statement of account, total 1,990.00, matching a hand calculation.

## 7. Out of scope for Phase 8

Payments and allocation (Phase 9), delinquency aging and reports
(Phase 10), exemptions (§43), the Treasurer's billing UI beyond a
statement of account, and role gating (Phase 12).
