# Valuation Foundation — Design (Step L1)

| | |
|---|---|
| Date | 2026-10-01 |
| Status | **Approved 2026-10-01: all recommendations Q1–Q16 accepted (§8).** L1-1 to L1-6 done (§9); L1-7 next |
| Rules | CLAUDE.md §5–§7, §28–§32, §65, §75–§77, §97 (L1), §118 |
| Sources | LAM 2025 Book III Ch. II (appraisal, pp.69–80), Ch. III §4–§6 (assessment cases and effectivity, pp.84–85); Book IV Ch. I §2 (SMV contents, pp.107–110), Ch. III (certification and publication); RA 7160 §§219–225; RA 12001 |
| Commit status | Cites and paraphrases the LAM; reproduces none of its tables or figures. May be committed (§118) |
| Depends on | The Provincial Assessor's answers to Part C of the L0-4 question list (C1–C7). Every choice that depends on one is marked **[C*n*]**, with its provisional default |

## 1. Purpose

L1 makes PRIME value property the way the LAM says, under the rules in force on the right
date. Every later step needs this: records (L5), SMV preparation and general revision (L6),
and appeals (L7).

At present PRIME:
- values everything **as of today**;
- keys land rates by classification and actual use, with no sub-class;
- keys building rates the same way, not by structural type;
- applies **no building depreciation**;
- asks the appraiser to **type** machinery's replacement cost;
- has no method for property outside the SMV;
- cannot compute **back taxes**, which need a value for each earlier SMV period.

All the values involved — unit values, construction costs, depreciation rates, factors,
exchange rates, price indices, levels — are LGU or official data. They enter PRIME as
configuration through the content pack or the admin screens, under maker-checker (§118). The
repository ships DEMO data only.

## 2. What exists

| Area | Today | File |
|---|---|---|
| Engine | `ValuationService` resolves the inputs and calls the pure `ValuationCalculator`. Each valuation stores lines and a breakdown | `Features/Valuation/ValuationService.cs`, `DomainServices/ValuationCalculator.cs` |
| Valuation date | `clock.Today` everywhere. `Valuation.EffectiveDate` records it | same |
| SMV | `Smv`: ordinance number (required), date, effectivity, revision year; one maker-checker approval. **Global**: no coverage by municipality | `Entities/Smv.cs` |
| Rates | `SmvSchedule`: classification + actual use (both required) + property type + optional zone + optional improvement kind → unit value, min/max, effective-dated | `Entities/SmvSchedule.cs` |
| Land | Strips (classification, sub-class, use, zone, area), improvements (kind × quantity), adjustments by factor **code**, legacy `LocationFactor`. `RoadTypeId` and `IsCornerLot` are recorded but unused | `Entities/Land.cs` |
| Factors | `AdjustmentFactor`: SMV + code + signed percent (+ optional classification); added together | `Entities/AdjustmentFactor.cs` |
| Buildings | Use portions, each valued at the rate for its classification and use. Additional items carry an **entered** cost. Completion %. `StructuralTypeId`, `BuildingTypeId`, `YearConstructed/Completed` are recorded but not in the key. **No depreciation** | `ValuationCalculator.CalculateBuildingPortion` |
| Machinery | Brand-new: acquisition + installation + other cost. Otherwise: **entered** replacement cost × remaining life ÷ economic life, with the configured 20 % floor (LGC §225) | `CalculateMachinery` |
| Assessment | Takes a valuation and a **typed** `EffectiveDate`. Levels are resolved on that date, per line, with brackets. `AssessmentLevelService` admits **one open level per key**, so brackets cannot be entered through the API (known gap) | `Features/Assessments/AssessmentService.cs` |
| Effectivity | Typed by the caller. TD forms derive the quarter for printing only | — |
| General revision | The job values each RPU **today** and assesses it effective **today** | `GeneralRevisionJobRunner.cs` |
| Transaction types | Versioned catalogue (code, kind, rank, requirements), maker-checker | `Entities/Transactions` |

## 3. Gaps (from the gap analysis, verified against the text)

| # | LAM says | PRIME | Step |
|---|---|---|---|
| G-1 | Property declared for the first time is valued under the SMV in force in each period, back up to 10 years (Bk III pp.79–80) | Values as of today only | L1-1, L1-8 |
| G-2 | Assessments made after 1 January take effect on 1 January of the next year. Reassessment for destruction, a major use change, sudden inflation or deflation, gross illegality or another abnormal cause is made within 90 days of the cause, and takes effect at the start of the next quarter (Bk III p.85 §6) | Effectivity typed by hand; no 90-day record | L1-2 |
| G-3 | The SMV is certified by the Secretary of Finance and takes effect 15 days after publication; it is no longer enacted by ordinance (Bk IV Ch. III) | Ordinance number required; no lifecycle dates | L1-3 |
| G-4 | Land unit values are given per sub-class (by location); the land's actual use decides its classification and level (Bk III p.69; Bk IV pp.107–108) | No sub-class in the rate key; the rate is keyed by actual use | L1-3 |
| G-5 | Adjustments: corner lots on the highest-value street; depth and stripping beyond a standard depth, residential only and not subdivision lots; agricultural land by road type and distance to an all-weather road and to the poblacion; percentages are in the SMV (Bk III pp.76–78) | Flat percentages chosen by code; road type, corner and distances unused | L1-4 |
| G-6 | Buildings: floor area × base unit construction cost (BUCC) by structural type and sub-type, plus extra items priced from the SMV, depreciated by the SMV's table (Bk III p.72; Bk IV pp.108–109) | Rate by classification and use; entered extra-item cost; no depreciation | L1-5 |
| G-7 | Depreciation between general revisions only for a first declaration, an ongoing general revision, or the owner's request (Bk III p.73, BLGF opinions) | — | L1-5 |
| G-8 | Machinery: replacement cost derived from the acquisition cost, the exchange rates at acquisition and at valuation, and a price index; straight-line depreciation of at most 5 % a year; remaining value at least 20 % while useful and in operation; each machine individually (Bk III pp.73–75; LGC §225) | Replacement cost entered; no 5 % limit; no "not in operation" rule | L1-6 |
| G-9 | Buildings and structures not in the SMV are appraised on their construction cost or independently; special classes and special-purpose property by the market, income or cost approach (Bk III pp.71–72, p.76) | Valuation fails without a schedule | L1-7 |
| G-10 | Market value rounded to the nearest ten on the FAAS (Annexes) | Not rounded | L1-4 **[C5]** |
| G-11 | Separately owned crops or trees are valued and assessed apart from the land (Bk III p.72) | Land improvements on the land only | L1-4 |

## 4. Proposal

### 4.1 L1-1 — the valuation date

- Every valuation is made **as of a date**. `ValuationService.Compute*` takes `asOf`; the API
  takes an optional `asOf` (default: today, as now). `Valuation.EffectiveDate` is renamed in
  meaning to the **valuation date** (column kept; documented).
- Everything the engine looks up is resolved on that date: the SMV and its schedules, factors,
  construction costs, depreciation tables, exchange rates and price indices. The breakdown
  records the date and the SMV used (already per line).
- **Which date** (Q1): by default the valuation date is the **effectivity date of the
  assessment it is for**, so a period is always valued under the SMV in force in that period.
  This is what the back-tax rule needs, and it also covers the LAM's "ongoing general revision"
  case (p.80): a discovery made while a new SMV is about to take effect is valued under the new
  one.
- **General revision:** the job takes the revision's effectivity date (required) and values
  and assesses every RPU as of that date, not today.
- An assessment refuses a valuation made as of a different date than its own effectivity
  (`VALUATION_DATE_MISMATCH`), unless the method is one that does not depend on the date
  (an entered independent appraisal).
- Posted history is never recomputed (CLAUDE.md §76).

### 4.2 L1-2 — effectivity by rule

- **Rule kinds are code; which transaction uses which is configuration.** A transaction type
  gains an **effectivity rule** and its legal basis:
  - `NextJanuary` — made on 1 January: that day; otherwise 1 January of the next year
    (LGC §221; Bk III p.85);
  - `NextQuarter` — the first day of the quarter after the reassessment; the cause date is
    recorded and the reassessment is flagged when made more than the configured number of days
    after it (90, configuration with its legal basis);
  - `Periods` — back taxes (L1-8): each period's own start;
  - `Fixed` — a date given by the process (general revision: the revision's effectivity).
- An assessment carries its **transaction code** (an in-force transaction type), the
  **cause date** where the rule needs one, the **date made**, and the derived
  **effectivity date, year and quarter** (stored columns, never typed).
- **When an assessment is "made"** (Q3): the date of final approval. A draft shows the
  effectivity it would have if approved today; if final approval falls on a date that changes
  it (a December draft approved in January), the approval stops and asks for the valuation to
  be redone as of the new date. A manual override exists only with a reason, and it is
  audited.
- The 90-day window is a **warning on the record**, not a block (Q4).
- Existing assessments keep their dates. Year and quarter are back-filled from them.
- The TD's effectivity follows its assessment (already so for FAAS = TD + assessment).

### 4.3 L1-3 — the SMV model

**The SMV itself (G-3).**
- `Smv` gains its **basis** (`Certified` under RA 12001, or `Ordinance` for earlier SMVs) and
  optional dates and references for each stage: proposed, published for comment,
  consultations, submitted to the BLGF, certified (with the certification reference),
  published in the Official Gazette or a newspaper, and effective. The ordinance number
  becomes optional; a certified SMV needs its certification reference, an ordinance SMV its
  ordinance number.
- PRIME's own maker-checker approval stays. It confirms that the SMV was **entered**
  correctly; it does not stand in for certification.
- Tracking the preparation of a new SMV (sales data, forms, testing) is L6. L1 only records
  the stages of an SMV that exists.

**Coverage (Q2).** An SMV covers the whole province or a list of municipalities
(`SmvCoverage`, effective-dated). The engine uses the SMV in force on the valuation date that
covers the property's municipality. The province's own answer (C1) says which applies to
Zamboanga Sibugay; the model takes either.

**Land rates (G-4).**
- `SmvSchedule` gains an optional **sub-class** and an optional **barangay**, beside the
  existing optional zone. **Actual use becomes optional** in the rate key: under the LAM the
  unit value follows the land's class and sub-class, and the actual use decides the level.
- **Resolution order** (most specific first; Q5): sub-class + zone, sub-class + barangay,
  sub-class, then no sub-class with zone, barangay, or neither; within each, a row naming the
  actual use beats one that does not. The order is documented in code and tested. Existing
  DEMO schedules (no sub-class, actual use set) still resolve.
- **Valuation key and assessment key are separate** on each land strip and valuation line:
  the class and sub-class that price it (the SMV key), and the class and actual use that
  assess it. By default they are the same; the appraiser may set the valuation key apart, for
  example agricultural land in a predominantly commercial area is priced at the commercial
  unit value but assessed at the agricultural level (Bk III p.62, BLGF opinion).

**Building and machinery components** become their own SMV tables (§4.5, §4.6), not more
columns on `SmvSchedule`.

**Assessment-level brackets.** `AssessmentLevelService` is changed to accept several open
levels per key when their value ranges do not overlap, so a building's level by market-value
bracket can be entered (fixes the known gap; uses the existing `RangesOverlap`).

**Content pack.** New file kinds load an SMV with its coverage, land values, factor rules,
BUCC, extra-item costs and depreciation table as **Draft** versions for a second user to
approve, like the other legal configuration (L0-2). The certified SMV arrives as a
spreadsheet (F3); a sheet per table converts to these CSVs.

### 4.4 L1-4 — land adjustments, rounding, separate improvements

- **Factor rules (G-5).** `AdjustmentFactor` gains a **rule kind**:
  - `Flat` — today's signed percent, chosen by the appraiser;
  - `ByRoadType` — rows of road type → percent, applied from the land's road type;
  - `ByDistance` — rows of distance band (over … up to … km) → percent, for a named
    reference ("all-weather road", "poblacion"), applied from the distance recorded on the
    land;
  - `Corner` — a percent applied when the land is a corner lot. The base street is the one
    with the highest unit value, so the appraiser records it as the strip's zone or barangay;
  - `Depth` — the standard depth and a percent per strip beyond it, applied to strips
    marked with their depth band; refused on commercial and industrial land and on
    subdivision lots (the classes it applies to are part of the rule).
- The land gains the **distances** used (km to an all-weather road, to the poblacion) and a
  **subdivision-lot** flag.
- Adjustments **add**, as today (Formula 4: MV ± MV × factor). Whether they compound is not
  settled by the text (Q6).
- **Rounding (G-10) [C5].** A configured rounding step for market value, applied to each
  line's adjusted market value, with its legal basis; **off** until the office confirms.
  Assessed value keeps centavos unless the office says otherwise.
- **Separately owned improvements (G-11).** An `OtherImprovement` RPU on the land can be
  valued with the SMV's improvement rates (trees, plants), in its owner's name. Today that
  RPU type cannot be valued.
- **Timberland (Bk III p.71).** No code: the province's transaction type for a first
  declaration of timberland carries the DENR/LMB certification as a mandatory requirement
  (configuration, already supported).

### 4.5 L1-5 — buildings

**Tables in the SMV (G-6).**
- `SmvBuildingCost` (BUCC): SMV × structural type (sub-types are rows of the structural-type
  lookup, e.g. as the SMV lists them) × building kind (the building-type lookup) × optional
  classification → cost per square metre, effective-dated.
- `SmvExtraItemCost`: SMV × extra-item type (the building-component lookup) → cost per unit
  (with the unit), effective-dated.
- `SmvDepreciationRate`: SMV × structural type × age band → depreciation percent, with the
  remaining value the SMV sets. How the printed rate reads (cumulative percent for the age, or
  a yearly rate within the band) is a property of the table **[C3]** (Q8).

**Calculation** (per use portion, as now, so each portion keeps its own classification and use
for the level **[C6]**):
1. core = floor area × BUCC for the building's structural type and kind;
2. extra items = Σ quantity × SMV unit cost (an item without an SMV cost needs an independent
   appraisal, §4.7; entered costs remain for legacy valuations only);
3. total = core + extra items, × completion percent;
4. depreciation = total × the rate for the building's age on the valuation date, never below
   the remaining value; **adjusted market value** = total − depreciation;
5. the schedule's limits, then rounding if configured.

The breakdown shows each figure, matching the FAAS layout (core, extra items, total, rate,
depreciated value, adjusted MV).

**Age** = valuation year − the year the building was completed, else constructed, else
occupied (Q9).

**When depreciation may change (G-7; Q10).** The transaction type says whether it allows a
new depreciation (first declaration, general revision, owner's request). Otherwise PRIME
keeps the **depreciation percent of the building's last posted valuation**, so a transfer or
a correction does not quietly change the value. The breakdown says which applied.

**Building FAAS fields.** The building records its property type for the FAAS
(residential/commercial/industrial condominium, townhouse) through the existing building kind;
age is derived. Condominium valuation itself is L4.

### 4.6 L1-6 — machinery

**Reference tables (configuration, effective-dated, maker-checker):**
- `ExchangeRate`: currency → peso rate on a date, with source (BSP);
- `PriceIndex`: series (an origin country's, or the local one) × year → index, with source.

**Machine fields:** acquisition currency and foreign amount; imported or local; origin
country; the acquisition-cost items the LAM lists (freight, insurance, bank charges,
brokerage, arrastre, duties, inland transport, installation) as one itemised list; date
installed; "in operation" status. The import, registration and supplier particulars for the
FAAS (Annex I-F) are descriptive and go with L5.

**Calculation [C4] (Q11).** Per machine, as of the valuation date:
1. brand-new: acquisition cost (unchanged);
2. replacement cost: acquisition cost × (exchange rate on the valuation date ÷ on the
   acquisition date) × the price index, for imported machinery; acquisition cost × the local
   index for local machinery;
3. years of use N = from installation (else acquisition) to the valuation date;
4. depreciation = replacement cost × the smaller of N ÷ economic life and the configured
   maximum yearly rate × N (LGC §225: at most 5 % a year; configuration with its legal basis,
   like the 20 % floor);
5. remaining value at least the configured floor **while in operation**; a machine not in
   operation is valued without the floor (Q12);
6. the **entered replacement cost** stays as a method for legacy records and machinery
   without index data, shown as such in the breakdown.

### 4.7 L1-7 — other methods

- **Independent appraisal** (G-9): for a building, structure, extra item or machine the SMV
  does not cover, and for special classes and special-purpose property. The appraiser enters
  the value, the approach (market, income or cost), the basis (text) and the evidence
  (document references), and may list named inputs (e.g. gross income, capitalisation rate)
  that the breakdown keeps. PRIME **does not compute** an income or cost approach from a
  built-in formula until the office supplies its worksheet (Q13). Review is the assessment's
  existing maker-checker approval.
- The method registry is the existing `ValuationMethod` enum, extended (`IndependentAppraisal`,
  `EnteredReplacementCost`, `DerivedReplacementCost`).

### 4.8 L1-8 — back taxes

For a property declared for the first time or newly discovered (G-1):
- The appraiser enters the **year it should have been declared from** (e.g. completion,
  acquisition, occupation), with its basis. PRIME limits the start to the configured number of
  years before the year of initial assessment (10, configuration with its legal basis).
- PRIME splits the range into **periods**, cut at each SMV effectivity date inside it. For each
  period it makes a valuation as of the period's start and an assessment effective then, each
  chained to the one before, and the last one current. The levels in force in each period apply.
- **Buildings and machinery (Q14).** The LAM's text differs: Bk III p.78 says a building
  declared for the first time uses the **current** BUCC and machinery its acquisition cost,
  while p.79 says the SMVs of the corresponding periods control. Recommended: land by period,
  buildings at the current BUCC for every period, machinery by period (its age changes), as a
  setting per property kind.
- **Records (Q15).** One FAAS and TD per period, each cancelling the previous, so the FAAS/TD
  effectivity "commences in the first year and every year of general revision" (p.80). The
  current TD lists the back-tax years.
- A discovery during an ongoing general revision is covered by §4.1: the current period is
  valued as of the new SMV's effectivity.
- PRIME gives the assessed value per period. Computing and collecting the tax stays with the
  treasury (CLAUDE.md §0).

## 5. Data and migrations

All additive (CLAUDE.md §105):
- new columns: SMV basis and stage dates (ordinance number made optional); schedule sub-class
  and barangay (actual use made optional); land distances and subdivision flag; strip
  valuation key and depth band; factor rule kind and rule rows; assessment transaction code,
  cause date, date made, effectivity year and quarter; machinery currency, foreign amount,
  origin, cost items, installed date, in operation;
- new tables: `SmvCoverage`, `SmvBuildingCost`, `SmvExtraItemCost`, `SmvDepreciationRate`,
  `AdjustmentFactorRow` (road type / distance band / depth band rows), `ExchangeRate`,
  `PriceIndex`, `MachineryCostItem`;
- back-fill: existing SMVs become `Ordinance` basis; existing assessments get year and quarter
  from their date and no transaction code; existing DEMO data keeps valuing.

## 6. Delivery steps

| Step | Scope | Verification |
|---|---|---|
| L1-1 | Valuation date through the engine, API and GR job; mismatch check | "a past date uses the rules then in force" (§75); GR before the SMV's effectivity uses the new SMV |
| L1-2 | Effectivity rules on transaction types; assessment transaction code, cause date, date made, year/quarter; approval re-derivation; 90-day flag | Each rule at its boundaries (31 Dec/1 Jan, quarter ends); December draft approved in January |
| L1-3 | SMV basis and stages; coverage; schedule sub-class/barangay/optional use; valuation vs assessment key; level brackets through the API; content-pack kinds; admin screens | Resolution order table test; existing DEMO data unchanged |
| L1-4 | Factor rule kinds and rows; land distances and subdivision flag; rounding setting; `OtherImprovement` valuation | Each rule kind; depth refused on commercial; rounding off/on |
| L1-5 | BUCC, extra-item and depreciation tables; building calculation; eligibility and carry-over | A depreciated building reproduces the FAAS figures (DEMO numbers) |
| L1-6 | Exchange rates, price indices; machine fields and cost items; derived replacement cost; 5 % cap; in-operation floor | Imported and local machines; cap and floor boundaries |
| L1-7 | Independent appraisal (value, approach, basis, evidence, inputs) | Building outside the SMV values; breakdown keeps the inputs |
| L1-8 | Back-tax periods, chained assessments and TDs | The LAM's back-tax structure (§7 exit criteria) |

Each step: build, full tests, `npm run build` and lint, browser check where there is UI, then
the documentation (CLAUDE.md §107). UI per step: the admin screens for the new tables, and
the Value-and-assess drawer gains the valuation date, transaction code and cause date, the
building depreciation, machinery derivation and independent appraisal inputs, and the
back-tax periods.

## 7. Exit criteria

1. A back-tax case with the same structure as the LAM's worked example (Bk III pp.79–80: five
   SMV periods, one level, a ten-year limit) produces one assessment per period with the
   period's unit value, using **DEMO figures** in the committed test (Q16).
2. A depreciated building produces the FAAS figures (core, extra items, total, rate,
   depreciated value, adjusted MV).
3. A general revision run before the new SMV's effectivity values under the new SMV.
4. Every existing DEMO valuation still values; posted history is unchanged.

## 8. Review questions

Marked **[C*n*]** where the Provincial Assessor's answer to question C*n* may change the
default; the model takes either answer as configuration.

| # | Question | Recommendation |
|---|---|---|
| Q1 | Which date values an assessment? | Its **effectivity date**, so each period uses its own SMV; general revision uses the revision's effectivity date |
| Q2 | SMV coverage in a province-wide deployment **[C1]** | An SMV covers the province or a list of municipalities; the engine picks the one in force covering the property's municipality |
| Q3 | When is an assessment "made" for the 1 January rule? | At final approval. Drafts show the provisional effectivity; an approval that would change it asks for revaluation |
| Q4 | The 90-day reassessment window | Record the cause date and flag late reassessments; do not block |
| Q5 | Land rate resolution order | Sub-class + zone → sub-class + barangay → sub-class → zone → barangay → none; a row naming the actual use beats one that does not. Existing DEMO rows keep resolving |
| Q6 | Several adjustment factors on one strip | Add them (Formula 4), as today; compounding only if the office says so |
| Q7 | Rounding of market value **[C5]** | A setting, off until confirmed; when on, applied to each line's adjusted market value |
| Q8 | How the SMV's depreciation table reads **[C3]** | Store the table with a "cumulative" or "yearly within band" flag and compute accordingly; the province's table decides which |
| Q9 | Building age | Valuation year − year completed, else constructed, else occupied |
| Q10 | Depreciation between general revisions | Only where the transaction type allows it (first declaration, general revision, owner's request); otherwise keep the last posted valuation's depreciation percent |
| Q11 | Machinery formula **[C4]** | Replacement cost from acquisition cost × exchange-rate ratio × price index (imported) or × local index; depreciation = smaller of N/EL and 5 % × N; floor 20 %; entered replacement cost kept as a method |
| Q12 | Machinery no longer in operation | No 20 % floor (LGC §225 sets it "for so long as the machinery is useful and in operation") |
| Q13 | Income and cost approaches | An independent appraisal records the approach, basis, evidence and named inputs; no built-in formula until the office supplies its worksheet |
| Q14 | Back taxes for buildings and machinery (LAM p.78 vs p.79) | Land by period; buildings at the current BUCC for every period (p.78 §3.A); machinery by period; a setting per property kind |
| Q15 | FAAS/TD records for back taxes | One FAAS/TD per period, each cancelling the previous; the current TD lists the back-tax years |
| Q16 | The LAM's worked example in tests | Commit a test with the same structure and **DEMO figures** (§118 forbids reproducing the LAM's tables); optionally, a check that reads the example's figures from the untracked `lgu-content/` folder |

Not asked here, decided by earlier rules: values stay configuration (§7); posted history is
never recomputed (§76); collection stays with the treasury (§0); C7 (taxable and exempt parts)
is L3.

**Decisions (2026-10-01):** the user accepted every recommendation above ("yes to all").
Defaults marked [C*n*] stay provisional until the Provincial Assessor's Office answers.

## 9. Implementation log

### L1-1 — the valuation date (2026-10-01)

**Built**
- **Engine:** `ValuationService.ComputeFor{Land,Building,Machinery,Rpu}Async` take an
  optional `asOf` (default today). The SMV schedules and adjustment factors in force on that
  date are used; the valuation records it (`Valuation.EffectiveDate`, shown as "valued as of").
  Messages name the date instead of "today".
- **API:** `POST /api/rpus/{id}/valuations?asOf=yyyy-MM-dd`, and the same on the land,
  building and machinery valuation endpoints.
- **Assessment (Q1):** an assessment is refused when its valuation was made as of another
  date (`VALUATION_DATE_MISMATCH`).
- **General revision:** the start request requires the revision's `effectiveDate`, stored on
  the job (migration `GeneralRevisionEffectiveDate`, one nullable column, **local database
  only**). The job values and assesses every RPU as of that date. Jobs from before L1-1 keep
  their run date.
- **UI:** the Valuation drawer has a "Value as of" date and lists valuations by that date.
  "Assess this valuation" takes the effective date and year from the valuation and does not
  let the date be changed. The valuation picker now shrinks at phone width.

**Fixed on the way (existing defects that dated valuation exposed):**
- **End dates of SMV schedules and assessment levels.** Superseding a schedule or a level
  ends it the day before its successor, an inclusive end like the rest of PRIME's
  configuration. But the valuation engine and the level lookup read it as exclusive, so on
  the last day before every change there was no rate and no level. Both lookups now read it
  as inclusive.
- **Two schedules in force for one key.** Approving a new SMV's schedule does not end an
  earlier SMV's open-ended one. The engine now takes the schedule that took effect last, so
  a new SMV supersedes from its effectivity. Coverage and SMV choice proper come in L1-3.

**Verified**
- 2 new integration tests (`ValuationDateTests`; DEMO SMVs at 1,000/sqm from 2026 and
  1,500/sqm from a 2027 revision):
  - the same unit values at 500,000 on 2026-12-31 (the old SMV's last day) and at 750,000
    on 2027-01-01 under the new SMV; a date before any SMV is refused with the date in the
    message; no date means today;
  - a general revision run before the new SMV's effectivity values and assesses as of
    2027-01-01 at 750,000 (AV 150,000), chained to the posted assessment.
- The assessment-flow test now also checks `VALUATION_DATE_MISMATCH`. Existing tests in eight
  files were changed to value as of their assessment's date, the shared seed helper among them.
- Full suite: 474 tests pass (140 domain, 56 application, 278 integration). `npm run build`
  and `npm run lint` are clean.
- Browser (Playwright, on a separate API and dev server so the user's running API was not
  disturbed): DEMO-BILL-AE94B8's land valued as of 2026-01-01 (600,000); the assessment
  dialog shows effective date 2026-01-01, locked, and previews AV 120,000 at 20 %; a date in
  2020 is refused with the date in the message. No script errors.
- **Existing issue noted, not fixed:** at phone width, expanding a unit on the property
  profile widens the page to about 1,670 px (the nested unit-detail tables). It predates L1
  and is unrelated to the drawer.


### L1-2 — effectivity by rule (2026-10-01)

**Built**
- **Rules (code):** `EffectivityRule` = `NextJanuary`, `NextQuarter`, `Periods`, `Fixed`, and the
  pure `EffectivityRules` (Domain): the date each derived rule gives for the day an assessment
  is made, the quarter, and the reassessment-window check (the last day of the window is in
  time).
- **Which transaction uses which (configuration):** a transaction type gains an effectivity
  rule, the rule's legal basis (required with a rule) and, for `NextQuarter` only, the number
  of days after the cause. Admin screen (Forms & Numbering → Transaction types) and the content
  pack's `transaction-types` catalogue (`effectivityRule`, `effectivityLegalBasis`,
  `causeWindowDays`) carry them; versions are approved by a second user as before.
- **Assessment:** carries the transaction type and, frozen from it, its code, rule and window;
  the cause date; the late flag; the date made; an override reason; and the effectivity year
  and quarter as **stored columns computed by the database** from the effective date (never
  typed; existing rows filled by the migration). Migration `EffectivityRules`, additive,
  **local database only**.
- **Create / preview:** with a type whose rule derives the date, the effective date is left
  out and derived as if made today; a different date needs a reason
  (`EFFECTIVITY_OVERRIDE_REASON_REQUIRED`). `NextQuarter` needs the cause date
  (`CAUSE_DATE_REQUIRED`, `CAUSE_DATE_IN_FUTURE`) and flags a reassessment made after the
  window. Without a type, or with `Periods`/`Fixed`, the date is entered
  (`EFFECTIVE_DATE_REQUIRED`). A general revision is `Fixed` at the revision's date under the
  configured general-revision code. The assessment year defaults to the effectivity year.
  `GET /api/assessments/effectivity` answers "if made today".
- **Approval (Q3):** before any step is signed, a derived effectivity is re-derived for today;
  if it changed (a December draft approved in January), approval stops with
  `EFFECTIVITY_CHANGED` and asks for the unit to be valued and assessed again. Final approval
  stamps the date made and re-checks the window. An overridden date is not re-derived.
- **TD:** the transaction code of a TD prepared on posting (and of a TD declaring an
  assessment) is the assessment's own; older general-revision assessments still get the
  general-revision code.
- **UI:** the Valuation drawer has a Transaction picker (and the cause date for a
  reassessment); a rule's date replaces "Value as of" and an alert states the effectivity,
  the rule and its basis, and any late flag. The assess dialog warns when the valuation's date
  differs from the rule's and then asks for the override reason. The unit's assessments table
  shows the quarter, the transaction code, "overridden" and "late reassessment" tags, and the
  date made. The drawer's controls moved from the header into the body so they wrap at phone
  width.

**Deviation:** the transaction type is **optional** on an assessment, as the design's "never
typed" otherwise requires every LGU to configure its types first. Without one the effective
date is entered, as before L1-2; existing callers and data keep working. Making it mandatory
is a one-line setting later, once the province's catalogue is loaded.

**Verified**
- 30 new unit tests (`EffectivityRulesTests`: 1 January and quarter boundaries, leap day,
  day 90/91 of the window) and 5 integration tests (`EffectivityRuleTests`, DEMO types,
  clock moved by hand): a draft of 31 December takes effect 2027-01-01, is refused on
  2 January (`EFFECTIVITY_CHANGED`, nothing signed) and approved on 1 January with date made
  2027-01-01; an override needs a reason and is not re-derived; a reassessment with a cause on
  day 91 is flagged; catalogue validation; the TD transaction code follows the assessment.
- Full suite: 501 tests pass (162 domain, 56 application, 283 integration). `npm run build`
  and `npm run lint` clean.
- Browser (Playwright, separate API :5231 and dev server :5174): a DEMO type `DEMO-RA12`
  (NextQuarter, 90 days) created as admin and approved as the dev checker; on
  DEMO-BILL-AE94B8's land, choosing it with a cause on 2026-06-01 locks "Value as of" to
  2027-01-01 and shows the late flag; valued (600,000), assessed (AV 120,000), and the draft
  is listed as "2027-01-01 (Q1) DEMO-RA12 late reassessment". No script errors; at 390 px the
  drawer controls wrap. Dev DB now holds that type and a Draft assessment.

### L1-3 — the SMV model (2026-10-01)

**Built**
- **The SMV (G-3):** a basis (`Certified` under RA 12001, or `Ordinance`), the ordinance number
  and date now optional (required for an ordinance SMV; a check constraint enforces it), the
  certification reference (required for a certified SMV, unique), the stage dates (proposed,
  published for comment, consultations, submitted to the BLGF, certified, published) and the
  publication reference. `Reference` = ordinance number, else certification reference; it is
  what valuations, factors and the FAAS data show. PRIME's approval stays, and the screen says
  it confirms the entry, not the certification. Existing SMVs became `Ordinance`.
- **Coverage (Q2):** `SmvCoverages` lists the municipalities an SMV covers; none = the whole
  province. Set when the SMV is created (while a draft); a change of coverage is a new SMV.
  A barangay unit value must lie in a covered municipality (`BARANGAY_OUTSIDE_SMV_COVERAGE`).
- **Unit values (G-4):** a rate may name a sub-class and a barangay; its actual use is
  optional. The pure `SmvRateSelector` (Domain) chooses (Q5): first the **SMV** — the latest-
  effective approved SMV in force on the valuation date that covers the property's municipality
  and has a matching rate — then within it sub-class + zone → sub-class + barangay → sub-class →
  zone → barangay → neither, a rate naming the actual use first at each level. Superseding a
  rate is now confined to its own SMV, since SMVs are chosen by effectivity and coverage.
- **Valuation key (§4.3):** a land strip may name the class and sub-class that **price** it;
  the valuation line keeps its own class and actual use for the level and records the priced
  class (`PricedClassificationId/PricedSubClassificationId`, shown as "priced as").
- **Level brackets:** already accepted side by side since the value-and-assess step
  (`RangesOverlap`, covered by `ValueAndAssessTests`); the design's "known gap" was out of date.
  Nothing to change; one stale test comment corrected.
- **Content pack:** new versioned kinds `smv` (JSON), `smv-schedules` and `assessment-levels`
  (CSV, codes and PSGC codes). Each item becomes a Draft through its service for a second user
  to approve. An SMV with the same reference and content is unchanged; with other content it is
  refused (`SMV_EXISTS`: never edited). Rates may name SMVs, lookups and barangays the same pack
  adds; unknown ones are refused. `adjustment-factors` moves to L1-4, where factor rule kinds
  change its shape. The provenance key column widened to 200 characters. The DEMO pack gained a
  DEMO certified SMV for Town A (2099), two unit values, one level, a sub-class and an actual use.
- **Screens:** Valuation Rules → SMVs: basis, reference, coverage, stages (expandable), and the
  new-SMV dialog with basis, stage dates and coverage; unit values with sub-class, barangay
  (via municipality) and optional actual use, with the resolution order stated. The land strip
  dialog has "priced as" class and sub-class; the strips table and the valuation drawer show it.
- Migration `SmvModel`, additive plus three NOT NULL relaxations (SMV ordinance number and date,
  rate actual use), **local database only**.

**Deviations**
- Coverage is fixed per SMV instead of effective-dated: under RA 12001 an SMV is certified for
  its LGU, so a different coverage is a different SMV. Recorded here; revisit if the province's
  answer to C1 says otherwise.
- `adjustment-factors` pack files are deferred to L1-4 (rule kinds), not L1-3.
- Frozen treasury code touched only to keep compiling: `BillingRuleService` shows the SMV's
  reference instead of its (now optional) ordinance number.

**Open (existing behaviour, not changed):** creating a draft rate or level ends the open one it
supersedes at once, before the draft is approved. A rejected draft would leave a gap. It
predates L1; worth fixing when the approval of configuration is reworked (Phase 12).

**Verified**
- 5 unit tests (`SmvRateSelectorTests`: the full order, actual use first, set parts must match,
  a later SMV supersedes a more specific older rate, an SMV without a match does not hide an
  older one).
- 2 integration tests (`SmvModelTests`): basis validation and certification uniqueness; an SMV
  for another town is ignored and cannot take a barangay outside it; with the covering SMV a
  plain strip takes the barangay rate (2,500), a sub-class strip the sub-class rate (2,200 over
  the barangay's), a strip priced as commercial 5,000 while assessed under its residential level
  (AV 344,000 on 1,720,000). 1 preview test (`SMV_EXISTS`, invalid certified SMV, unknown town,
  `SMV_UNKNOWN`, `CODE_UNKNOWN`, bad columns). The DEMO pack import asserts the draft SMV,
  coverage, rates and level, and a second import changes nothing.
- Full suite: 509 tests pass (167 domain, 56 application, 286 integration). `npm run build`
  and `npm run lint` clean.
- Browser (Playwright, API :5231, dev server :5174): a certified DEMO SMV `DEMO-L13-CERT`
  (2099, coverage DEMO_Municipality) created as admin and approved as the dev checker; a
  barangay unit value of 3,000/sqm for DEMO_Barangay_1 with no actual use, approved by the
  checker; DEMO-BILL-AE94B8's land valued as of 2099-01-01 at 1,800,000 under that SMV. No
  overflow at 390 px; no script errors. Dev DB now holds that SMV and rate.

### L1-4 — land adjustments, rounding, separate improvements (2026-10-01)

**Built**
- **Factor rule kinds (G-5):** `AdjustmentFactor.RuleKind` = `Flat` (as before), `ByRoadType`,
  `ByDistance` (with a `DistanceReference`: all-weather road or poblacion), `Corner`, `Depth`
  (with the standard depth), and a table of `AdjustmentFactorRows` (road type, distance band
  "over … not over …" km, or depth band → signed percent). The service checks each kind's shape
  (rows required or forbidden, no overlapping bands, no repeated road type or band). The pure
  `AdjustmentRules.Evaluate` (Domain) gives a strip its percentage from the land's facts.
- **Applying them:** the appraiser still names factors by code on the land or a strip; the rule
  decides the percentage. A factor that cannot apply stops the valuation with the reason
  (`ADJUSTMENT_NOT_APPLICABLE`): a corner factor on land not recorded as a corner lot, a road or
  distance factor without the road type or distance, a depth band without a row, any depth factor
  on a subdivision lot. A depth factor leaves strips without a band untouched. A depth factor
  names its class (residential), so it is not found for commercial or industrial strips, as the
  existing lookup already did. Percentages add (Formula 4, Q6).
- **Land facts:** `DistanceToAllWeatherRoadKm`, `DistanceToPoblacionKm`, `IsSubdivisionLot`;
  `LandStrip.DepthBand`. `PUT /api/land/{id}/appraisal-inputs` changes road type, frontage,
  corner, distances and subdivision status with a reason (audited). Before this, road and corner
  could be set only when the land was registered.
- **Rounding (G-10, [C5]):** `Valuation:MarketValueRoundingStep` (with
  `Valuation:MarketValueRoundingLegalBasis`, required with a step; checked at start-up) rounds
  every row's market value, half away from zero; the breakdown keeps the value before rounding
  and the step. **Not set: off**, until the office confirms.
- **Separately owned improvements (G-11):** `LandImprovement.SeparateRpuId` names an
  `OtherImprovement` RPU recorded on the land (`SEPARATE_RPU_INVALID` otherwise). The land's
  valuation leaves those rows out; valuing the other-improvement RPU values them at the SMV's
  rates for their kind (source type Land, so the land levels and the land FAAS apply).
- **Content pack:** `adjustment-factors` is now read (JSON: smv, code, name, ruleKind, percent,
  classification, distanceReference, standardDepth, rows with roadType / over / upTo / depthBand /
  percent), imported as Draft versions keyed by SMV and code. The DEMO pack gained a corner
  factor and a distance factor. `building-costs`, `extra-item-costs`, `depreciation-rates`
  (L1-5), `exchange-rates` and `price-indices` (L1-6) are reserved as not yet read.
- **Screens:** Valuation Rules → Adjustment factors shows each factor's rule in words, and the
  new-factor dialog has the rule kind, reference or standard depth, and a row table. The land
  section has "What the adjustment factors read" with an audited edit dialog, a depth band on
  strips, and "owned by" on trees and plants. The SMV picker in the factor dialog is searchable.
- Migration `LandAdjustmentRules`, additive (existing factors become `Flat`), **local database
  only**.

**Deviation:** the design said road-type and distance factors are "applied from the land's
road type / distance"; they are still named on the land by the appraiser, and the rule supplies
the percentage. This keeps one way of attaching factors and makes the appraiser's choice
visible on the FAAS. Automatic application can be added if the office wants it.

**Verified**
- 15 unit tests (`AdjustmentRulesTests`: each kind, band edges "over / not over", missing
  facts, depth on subdivision lots, overlap check, rounding half away from zero and off).
- 5 integration tests (`LandAdjustmentRuleTests`, DEMO SMV 1,000/sqm): dirt road -10, 3 km to
  the poblacion -5, corner +15 and depth band 1 -20 give 500,000 and 80,000 (580,000); each
  refusal with its reason, the audited change of inputs; factor table checks; a rounding step of
  100,000 set through configuration rounds 80,000 to 100,000; a mango tree owned by an
  other-improvement unit is valued under that unit (20,000) and not with the land.
- Full suite: 529 tests pass (182 domain, 56 application, 291 integration). `npm run build` and
  `npm run lint` clean.
- Browser (Playwright, API :5231, dev server :5174): a corner factor (+10 %) and a distance
  factor with two bands created as admin and approved as the dev checker; on DEMO-BILL-AE94B8's
  land, corner lot and 1.5 km to the poblacion recorded with a reason, the corner factor added,
  and the land valued as of 2026-06-01 at 660,000 (500,000 + 50,000 and 100,000 + 10,000).
  No script errors. Dev DB now holds factors DEMO-L14-CNR, DEMO-L14-KM and that land state.

### L1-5 — buildings: construction cost, extra items, depreciation (2026-10-01)

**Built**
- **SMV building tables (G-6):** `SmvBuildingCost` (SMV × structural type × optional building
  kind × optional classification → cost per sqm), `SmvExtraItemCost` (SMV × component type →
  cost per unit, with the unit) and `SmvDepreciationSchedule` (SMV × structural type → age bands,
  how they read, and the minimum remaining percent). Each is effective-dated configuration with
  maker-checker approval (`/api/building-costs`, `/api/extra-item-costs`,
  `/api/depreciation-schedules`), one open approved version per scope.
- **How a depreciation table reads (Q8, [C3]):** `Cumulative` (the band's percent is the total
  for an age in it) or `YearlyWithinBand` (the band's percent per year of age in it, added band by
  band). Bands must follow each other without gap or overlap; only the last may be open. An age
  below the first band is not depreciated; an age beyond a closed last band is refused. The
  result never exceeds 100 minus the minimum remaining percent (the breakdown says when it is
  capped). Pure rules in `BuildingDepreciation`.
- **Calculation (§4.5):** where an approved SMV in force covering the property has construction
  costs, each use portion is valued as floor area × BUCC (the most specific row for the
  building's kind and the portion's classification) + its extra items (quantity × the SMV's
  cost; items not tied to a portion spread by floor area) = total, × completion, less
  depreciation = market value; then rounding if configured (`ValuationCalculator.CalculateBuildingByCost`).
  The breakdown lists each figure in FAAS order, with each extra item just before their total.
  The valuation names that SMV. What the SMV does not give stops the valuation with the reason:
  `BUILDING_COST_NOT_FOUND`, `EXTRA_ITEM_COST_NOT_FOUND`, `EXTRA_ITEM_QUANTITY_REQUIRED`,
  `DEPRECIATION_TABLE_NOT_FOUND`, `BUILDING_AGE_UNKNOWN`, `DEPRECIATION_NOT_APPLICABLE`
  (an independent appraisal is L1-7).
- **Legacy path kept:** under an SMV without construction costs a building is valued as before
  (rate by classification and use, entered cost of additional items, no depreciation), so every
  existing DEMO valuation still values (exit criterion 4).
- **Age (Q9):** valuation year − year completed, else constructed, else the year of the date
  constructed or occupied; never below zero.
- **When depreciation may change (G-7, Q10):** `TransactionType.AllowsNewDepreciation`. A new
  depreciation for the age applies in a general revision (the job passes it), for a transaction
  whose type allows it, or when the building has no posted valuation that recorded a
  depreciation percent. Otherwise the percent of the last posted valuation is carried over
  (breakdown `DepreciationCarriedOver`). The valuation endpoints take `?transactionTypeId=`; the
  valuation records it, and an assessment of a depreciated building made under another
  transaction is refused (`VALUATION_TRANSACTION_MISMATCH`). The Value-and-assess drawer sends the
  chosen transaction when it values.
- **Extra items by quantity:** an additional item may now be recorded with a quantity alone (priced
  from the SMV); the check `CK_BuildingComponents_AdditionalItemCost` accepts a cost or a quantity.
  `Building.Depreciation` (numeric 9,6, a rate) holds the depreciation percent of the last
  cost-based valuation; `DepreciatedValue` its market value.
- **Content pack:** `building-costs`, `extra-item-costs` and `depreciation-rates` are read (JSON,
  like adjustment factors), imported as Draft versions keyed by SMV and scope. The DEMO pack gained
  a structural type, building kind and component type, and one row of each table.
- **Screens:** Valuation Rules → Building costs (three tables, create and approve); Transaction
  types show and set "Buildings take a new depreciation"; the valuation breakdown shows flags as
  Yes/No and percents with %; the component dialog says a quantity is priced from the SMV.
- Migration `BuildingCostTables`, additive (three tables and rows, two columns, the relaxed check),
  **local database only**.

**Deviations:** the pack files are JSON, not CSV (as for adjustment factors in L1-4); a building
under an SMV without construction costs keeps the older rate method rather than being refused.

**Verified**
- 15 unit tests (`BuildingDepreciationTests`: age sources, both readings at band edges, the cap,
  ages beyond the table, band checks, the FAAS figures and a carried-over percent).
- 6 integration tests (`BuildingCostValuationTests`, DEMO figures): 100 sqm × 10,000 + 20 m of
  fence × 1,500 = 1,030,000; age 10 at 2 % a year for 1–5 and 3 % from 6 = 25 %; depreciation
  257,500; market value 772,500; assessed at 50 % = 386,250 (exit criterion 2). Carried over (25 %)
  for a transaction that does not allow it and with none; renewed (31 % at age 12) for one that does
  and in a general revision; assessment under another transaction refused; each refusal; a
  cumulative table capped at 70 % by a 30 % remaining value; band checks and maker-checker.
  Content-pack tests updated (36 records, 24 files).
- Full suite: 550 tests pass (197 domain, 56 application, 297 integration). `npm run build` and
  `npm run lint` clean.
- Browser (Playwright, API :5231, dev server :5174): the DEMO pack previewed (one new record for
  each new kind) and imported (36 records); on Building costs a BUCC (12,000/sqm), a fence cost
  (1,500 per linear m) and a yearly depreciation table (1–10 at 1.5 %, 11+ at 2 %, 20 % remaining)
  created under DEMO-L13-CERT and approved by the dev checker; DEMO-BLDG-562763 given its year
  (2089, with a reason), a use portion and 20 m of fence, and valued as of 2099-01-01 at 96,900
  (84,000 + 30,000 = 114,000, less 15 %). No sideways scroll at 390 px; no script errors. The
  DEMO pack import was then removed from the dev database (the pack tests expect it absent), with
  the fence that used its item type; the building now values at 71,400. Dev DB keeps: the
  `BUILDING` property type (demo pack `demo-l15`), the three DEMO-L13-CERT rows (the fence cost
  removed), and the building's year and portion.

### L1-6 — machinery: derived replacement cost, 5 % cap, in operation (2026-10-01)

**Built**
- **Observations:** `ExchangeRate` (currency, date, pesos per unit, source) and `PriceIndex` (series,
  year, value, source). Each is created as a Draft and approved by a second user, never edited, and
  unique once approved (per currency and date, per series and year). Unlike effective-dated rules they
  are entered in any order; a valuation reads the latest approved rate on or before the date it
  needs. `/api/exchange-rates`, `/api/price-indices`.
- **Machine fields:** imported or local, acquisition currency (ISO 4217), cost in that currency,
  origin country, price index series, date installed, in operation (default yes) and an itemised
  list of acquisition cost items (`MachineryCostItem`: freight, insurance, bank charges, brokerage,
  arrastre, duties, inland transport, installation, other). They are set when the machine is
  created or through `PUT /api/machinery/{id}/valuation-inputs` with a reason (audited), which
  replaces the item list.
- **Derived method (Q11; LAM Bk III pp.73–75, Formulas 7–12):** for a machine that is not
  brand-new and names a price index series, replacement cost = (acquisition cost + freight +
  insurance) × (rate at valuation ÷ rate at acquisition, imported only) × (index of the valuation
  year ÷ index of the acquisition year) + the other expenses (installation, duties … and the older
  installation and other cost fields) at their recorded cost. Years of use = completed years from
  installation, else acquisition. Depreciation = replacement cost × the smaller of years ÷ economic
  life and `Valuation:MachineryMaximumYearlyDepreciationPercent` (5, LGC §225, configuration with its
  legal basis) × years, at most 100 %. The minimum remaining value (20 %) applies only while the
  machine is in operation (Q12). Method `DerivedReplacementCost`; the breakdown keeps every input
  (rates, indices, factor, years, the cap and whether it bound). Missing data stops the valuation:
  `PRICE_INDEX_NOT_FOUND`, `EXCHANGE_RATE_NOT_FOUND`, `MACHINERY_VALUATION_INPUTS_MISSING`,
  `MACHINERY_DEPRECIATION_LIMIT_NOT_CONFIGURED`.
- **Entered method kept:** a machine without a series is valued from its entered replacement cost
  and remaining life as before; it now also gets no minimum when not in operation (Q12). Brand-new
  machinery adds its cost items to the acquisition cost.
- `Machinery.Depreciation` (numeric 9,6) holds the depreciation percent of the last derived valuation.
- **Content pack:** `exchange-rates` (CSV: currency, rate-date, pesos-per-unit, source, remarks) and
  `price-indices` (CSV: series, year, value, source, remarks) are read; each row becomes a Draft; a
  row PRIME already holds is unchanged; an approved observation with another value is refused. No
  kind is left "not yet read". The DEMO pack gained two DEMO USD rates (dated 2099) and one DEMO index.
- **Screens:** Valuation Rules → Machinery indices (rates and indices, create and approve); on a
  machinery unit, a "Replacement cost" column (acquisition cost / derived: series, currency / entered),
  an operation tag and a "Valuation inputs" dialog (with the cost item list and a reason); the
  breakdown shows the flags as Yes/No and lists depreciation before the depreciated value.
- Migration `MachineryDerivedCost`, additive (machine columns, cost items, rates, indices), **local
  database only**.

**Decisions within the approved design (DOMAIN VERIFICATION with the province, [C4]):**
- The LAM's "price index (international price or trending factor)" is applied as the ratio of the
  index of the valuation year to that of the acquisition year. If the province supplies trending
  factors directly, they need their own table (series, acquisition year, valuation year → factor).
- The other acquisition expenses are added untrended; the LAM converts the cost, insurance and
  freight and says the appraisal includes the other expenses, without saying whether they are trended.

**Verified**
- 12 unit tests (`MachineryDerivationTests`: completed years, conversion and trending, the cap at
  its edges, the minimum in and out of operation, no limit configured, the entered method out of
  operation, brand-new cost items).
- 4 integration tests (`MachineryDerivedCostTests`, DEMO XTS figures): 1,000,000 + 50,000 freight +
  10,000 insurance = 1,060,000 × 60/50 × 110/100 + 40,000 installation = 1,439,200; 5 years with a
  10-year life → 25 % (capped from 50 %) → 1,079,400. A local machine of 18 years: 90 %, held at
  20 % (200,000) in operation and 10 % (100,000) once recorded as not in operation (audited reason);
  missing index, missing or draft-only rate, the latest earlier rate used, the entered method without
  a series; validation, maker-checker and no second approved value. Content-pack tests updated (39
  records, 26 files; the broken pack now has a malformed exchange-rates file).
- Full suite: 566 tests pass (209 domain, 56 application, 301 integration). One full run had
  `EndToEndFlowTests.CreatePropertyToVerifiedBalance` fail once; it passed alone and in two later
  full runs (intermittent, not investigated further). `npm run build` and `npm run lint` clean.
- Browser (Playwright, API :5231, dev server :5174): XTS rates (50 on 2020-01-01, 60 on 2026-01-01)
  and DEMO-L16 indices (100 for 2020, 110 for 2026) created and approved by the dev checker; a
  machinery unit DEMO-MACH-L16 added on DEMO-BILL-AE94B8 with one machine (1,000,000, acquired
  2020-01-15, 10-year life); its valuation inputs set with a reason (imported, XTS, installed
  2020-03-01, freight, insurance, installation); valued as of 2026-01-01 at 1,079,400 with the full
  breakdown. Machinery indices tab without sideways scroll at 390 px; no script errors. These stay in
  the dev database.
