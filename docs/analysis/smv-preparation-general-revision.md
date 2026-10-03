# L6 — SMV preparation and general revision (design)

| | |
|---|---|
| Step | Phase 10, step L6 (CLAUDE.md §97); replaces the earlier step 10e |
| Date | 2026-10-02 |
| Status | **Approved 2026-10-02: all recommendations Q1–Q18 accepted (§8.1).** Implementation in progress (§9) |
| Rules | CLAUDE.md §7, §28, §31, §33, §46, §49, §60, §68, §72–§73, §76, §97 (L6), §116–§118 |
| Sources | LAM 2025 Book IV (pp.103–125): Ch. I SMV (3-year cycle, Tables 14–15, contents, land value map), Ch. II preparation (mass appraisal, date of valuation and base valuation date, SMV Forms 1–12, valuation testing, revenue and tax impact study, review), Ch. III certification, publication, use, amendment, Ch. IV general revision (effectivity, expenses, GRI 1–19, suspension); Book I pp.9, 22–25 (abstracts, reports to BLGF); Annexes I-M, I-N, I-O, I-R, I-S (pp.178–185) and IV-A to IV-L (pp.213–237); RA 12001 §§15–17, 19 and its IRR; LGC §§212, 219, 221, 223, 275 |
| Depends on | L1 (SMV model and certification stages, valuation as-of date, building and machinery tables), L2 (GR-year numbering, temporary PINs), LP (offices, approval chains, delegation), L3 (exemptions, taxability per line), L5 (TMCR, rolls, ORF, NOA); province answers B2, C1, E1, E6 (L0-4) |
| Feeds | L7 (appeals against GR assessments), L8 (treasury inputs and outputs), Phase 11 (BLGF reports), Phase 13 (import of market data and existing SMVs) |
| Commit status | Cites and paraphrases the LAM; reproduces none of its tables, form layouts, criteria or example values. May be committed (§118). Form layouts, checklists and every LGU value stay in the untracked `lgu-content/` |

## 1. Scope

The plan's L6 item, findings F3–F7 and G8 of the gap analysis (untracked, `docs/lam/`):

1. **Market data** (F7): the abstracts of registered transactions, building permits and machinery registrations, and
   the sales analysed for the SMV.
2. **SMV preparation** (F3, F4): date of valuation and base valuation date, sales adjusted to the base date, the
   analysis behind each unit value, the proposed SMV and its certification path, SMV Forms 1–12.
3. **Valuation testing** (F5): median value-to-price ratio and coefficient of dispersion of the proposed values.
4. **Land value map** (F6).
5. **Revenue compliance and tax impact study** (F5) for the Sanggunian.
6. **General revision** (G8, CLAUDE.md §33, §72): a revision programme over a scope, with per-property results, field
   review, bulk approval, Tax Declarations, Notices of Assessment, the roll after the 60-day period, the ORFs and the
   completion report.
7. **SMV amendments** between revisions (Book IV pp.119–120).

Already done and not repeated: the SMV's certification stages and dates (L1-3, finding F2); building costs, extra
items and depreciation schedules per SMV (L1-5); machinery indices (L1-6); independent appraisals (L1-7); the
valuation as-of date and the general revision's fixed effectivity (L1-1, L1-2); GR-year TD numbering (L2-1).

## 2. What the LAM requires (paraphrased)

**Cycle (Book IV pp.104–107; RA 12001 §§15–17).** A preparatory year of continuous market-data gathering and field
validation; Y1 preparation, publication of the proposed SMV for comment, at least two public consultations (in person,
online or hybrid), finalisation and submission to the BLGF Regional Office; Y2 review (Regional Office 45 days, BLGF
head 30 days), certification by the Secretary of Finance (30 days; otherwise the existing SMV stays), publication,
effectivity 15 days after publication, transmittal to the LCE and Sanggunian, the revenue and tax impact report with
three options for levels and rates, the ordinance adjusting them; Y3 general revision, new TDs, notices, roll; Y4
collection. A remanded SMV is revised (optionally after one more consultation) and resubmitted within 30 days; the
Secretary decides within 10 days (Ch. III).

**Contents (pp.107–110).** Lands: classes, sub-classes with their criteria (SMV Form 1), market and unit values, the
land value map, adjustment factors. Buildings: structural types, utility classification, base unit construction cost,
extra items, depreciation. Machinery: classification, value formulas, depreciation.

**Preparation (pp.112–115).** Mass appraisal from sales, income, expenses and costs. The date of valuation is in
January of the first year; the **base valuation date** is the date the SMV is submitted to the BLGF, and every sale
used is adjusted to it for market trends. Sales are listed (Forms 2, 6), tabulated lowest to highest after rounding
(Forms 3, 7; agricultural sales first adjusted for road type and distance), turned into sub-class unit values by an
interval-and-frequency procedure (Forms 4, 8), and scheduled (Forms 5, 9 by location or crop and productivity class;
Forms 10–12 for buildings). Values of adjoining LGUs are checked.

**Valuation testing (pp.115–116).** Accuracy by the median value-to-price ratio (value ÷ price) within each sub-market
group; uniformity by the coefficient of dispersion (average absolute deviation of the ratios from the median, as a
percent of the median). The manual gives indicative benchmarks and notes there is no international standard.

**Revenue and tax impact (pp.116–118; RA 12001 §17).** With the Treasurer, after transmittal: a revenue compliance
study by the tax-gap approach (tax potential = taxable assessed value × rate, against the year's actual collection
excluding penalties and prior years, with discounts added back) and a tax impact study (each taxable land parcel's tax
under the new values at existing levels and rates, then under tax options; parcels whose tax falls, rises — with the
range of increases — or that are reclassified).

**Use and amendment (pp.119–120).** The certified SMV is the basis for assessment, general revision and level and rate
adjustment; it controls except for property of a kind it does not cover. Between revisions the assessor recommends
amendments for new roads, calamities, pandemics or error correction.

**General revision (pp.121–125).** Every three years on the certified SMV. Effectivity per LGC §221; the NOA within 30
days (§223) is mandatory. The GRI: an LCE office order; existing FAAS and TDs compiled; new FAAS pre-filled from them,
filed by barangay with buildings and machinery behind their land; pre-TMCR; base maps, temporary PINs and section maps;
field instructions and route assignments; ocular inspection reconciling class, use, owners and possessors; independent
appraisal where the SMV has no value; FAAS finalised with adjusted market values and the post-TMCR; assessed values;
FAAS and TD endorsed by the municipal assessor for the provincial assessor's approval; permanent PIN and TD numbers in
tax-map order; NOAs served; **the roll prepared only after 60 days from NOA delivery** and furnished to the Treasurer;
ORFs; a completion report to the LCE and Sanggunian (and the GR status report to the BLGF, Book I p.25). The GR may be
suspended during a declared calamity (30 days, extendable) or national emergency.

**Abstracts and reports (Book I pp.22–25).** Abstracts of registered real property transactions (sales, transfers,
conveyances, leases, mortgages), of building permits (LGC §290) and of machinery installation certificates (LGC §210),
kept to support the SMV's review; a yearly report of lowest, median and highest recorded sales to the BLGF by
31 January; the GR status report on completion.

## 3. What PRIME has

| Area | Today | Gap |
|---|---|---|
| SMV | `Smv` with basis (ordinance or certified), stage dates, certification and publication references, coverage by municipality; schedule rows by class, sub-class, use, zone, barangay; building, extra-item, depreciation tables per SMV; maker-checker on entry | No preparation record, consultations, remand cycle or deadlines; no sub-class criteria; no location description on a row; no amendment link |
| Market data | Building permit number and date on a building; engineering registration number on machinery; CAR number on a transfer | No sales, no price on a transfer, no abstracts, no validation |
| Valuation engine | One engine, effective-dated; uses **approved** SMVs in force; every computation is **persisted** as a valuation | Cannot value under a proposed (draft) SMV, nor compute without storing |
| Testing, impact | — | Everything |
| Land value map | Parcels, zones, sections, roads, barangays, disputed areas as layers | No value or sub-class layer |
| Tax rates, collections | Only in the frozen treasury code | Must not be built on (CLAUDE.md §0); need treasury-supplied inputs |
| General revision | `GeneralRevisionJob`: a caller-supplied list of RPU ids, Hangfire run, Draft assessments tagged with the job; counts of processed and failed. **No screen** | No scope selection, no per-property record or failure reason (CLAUDE.md §33 asks for old value, new value, reason, effective date, approver), no field review, no bulk approval or posting, no NOA batch, no 60-day gate, no completion report, no suspension |
| After posting | A posted assessment prepares a Draft TD (numbered by the TD scheme, `{GRYEAR}` available); NOA per assessment or combined per owner; register runs for TMCR, rolls, ORF, ROA | One by one only |

## 4. Proposal

Seven sub-steps. Each is usable on its own; §6 gives the order.

### 4.1 L6-1 — market data and abstracts

- **`MarketTransaction`**: one record per transaction used as market evidence. Kind (sale, other conveyance, lease,
  mortgage; configurable lookup), source (registry abstract, deed presented on a transfer, sworn statement, field data
  collection sheet), document reference and file number, transaction date, municipality, barangay, street or sitio,
  optional link to a PRIME property, TD and land; classification, sub-class, actual use or crop; area and unit (sqm or
  hectare); consideration; unit price (computed); **use for analysis** (yes/no, with the reason when excluded — not
  arm's length, related parties, partial interest …); field validation (by, on, note). Entered, or prefilled when a
  transfer is approved and the deed's consideration was recorded (a new optional field on the transfer's tax
  clearance), or imported (Q4).
- **`BuildingPermitAbstract`** and **`MachineryRegistrationAbstract`**: permits and installation certificates received
  (number, date, issuer, owner, location, description, declared cost), linked to the building or machinery once
  declared. A worklist of those not yet linked doubles as a **discovery lead list**.
- **Reports**: the three abstracts and the yearly lowest–median–highest sales report (Annex I-R; its frequency is
  question E6), through the forms foundation.
- **Privacy**: prices and parties are personal data (CLAUDE.md §68); a `MarketData.View` permission, jurisdiction
  scoping as for properties, and no prices in logs.

### 4.2 L6-2 — SMV preparation

- **`SmvPreparation`**: the work file of one revision cycle for one LGU (the province, under §117): revision year,
  **date of valuation**, **base valuation date**, the proposed SMV it produces (a Draft `Smv`), team notes, status
  (Preparing → Proposed → Published → Consulted → Submitted → UnderReview → Certified / Remanded → Published →
  Effective). The stage dates already on `Smv` are filled from it.
- **Consultations**: date, mode (in person, online, hybrid), venue or link, attendance count, minutes reference;
  submission warns (does not refuse) with fewer than the configured minimum (`Smv:MinimumConsultations`, default 2,
  RA 12001 §15).
- **Review and remand**: received-by-BLGF dates, remand date and reasons, resubmission; statutory periods as settings
  (Regional Office 45, BLGF head 30, Secretary 30, resubmission 30, decision 10, effectivity 15 days after
  publication; RA 12001 and its IRR) used only for reminders and due dates, never to change data by themselves.
- **Sales analysis** per class (and per crop for agricultural land): the valid sales of the market areas chosen, each
  **adjusted to the base valuation date** by a time-adjustment factor the assessor enters per month or quarter (the
  LAM names no method, Q6); for agricultural land, the road and distance deductions the assessor enters (the annex
  percentages are examples). PRIME then performs the tabulation and interval-frequency arithmetic of Forms 3–4 and
  7–8 with the assessor's parameters (rounding increment, range width), shows the ranges with their frequencies, lets
  the assessor merge ranges and name sub-classes, and records the **adopted unit value per sub-class** with the
  analysis that produced it (Q7). Adopting creates or updates Draft schedule rows in the proposed SMV.
- **SMV content additions**: sub-class **criteria** per SMV (Form 1; text supplied by the province); a **location
  description** on a schedule row (Form 5's street and side; printed, not used to select a rate); a crop and
  productivity description for agricultural rows (Form 9).
- **Forms 1–12** as form definitions with data providers in the repository; the LAM layouts authored in
  `lgu-content/` as in L5; PRIME provisional layouts, watermarked, until then.
- **Adjoining LGUs** check: a checklist note with references.

### 4.3 L6-3 — values under a proposed SMV, and valuation testing

- **Engine**: the valuation engine gains an explicit **rate source** — "the approved SMV in force" (today's
  behaviour, the default) or "this proposed SMV" (its rows and tables whatever their status) — and a **compute-only**
  mode that returns the lines and totals without storing a valuation. One engine stays the single source of the
  arithmetic (CLAUDE.md §102 Rule 9); the persisted path is unchanged (Q8).
- **`SmvSimulationRun`** (background job, §73): a proposed SMV, a scope (municipalities), an as-of date (the proposed
  effectivity), and the level sets to apply (current, or a tax option's, §4.5). Results per RPU in
  `SmvSimulationResult` — current posted market and assessed values, simulated values, classification change — kept
  apart from valuations and never posted.
- **Valuation testing**: for each sale marked for analysis, VPR = simulated value ÷ time-adjusted price, the value
  being the sale's area × the proposed unit value of its sub-class (land; Q9). Median VPR and CoD per sub-class,
  class and market area, with the number of sales; benchmarks are settings shipped **empty** and loaded with the
  province's content (the LAM's figures are indicative, Q10). Results print as a testing report.

### 4.4 L6-4 — land value map

A GIS layer derived from data, not drawn by hand: each land parcel coloured by the sub-class (or unit-value band) of
its land under a chosen SMV — the current one or a proposed one — with legend, labels (sub-class and unit value) and
the existing zone, road and section layers for the factors the LAM lists. Optional **sub-market area** polygons
(drawn or imported, Book IV p.110) for areas without parcels yet. Printable through the existing map print (Q11).

### 4.5 L6-5 — revenue compliance and tax impact study

- **`RevenueImpactStudy`**: linked to a proposed (or newly certified) SMV and a simulation run. **Inputs supplied by
  the Treasurer** and recorded on the study with their source: basic and SEF rates per LGU, the year's current
  collection excluding penalties and prior years, discounts granted (question E1). PRIME never reads the frozen
  billing or collection tables (CLAUDE.md §0) and never computes a bill.
- **Revenue compliance**: tax potential from the posted taxable assessed values, tax gap, compliance rate, collection
  efficiency, as the LAM defines them.
- **Tax impact**: per taxable land parcel (option: every taxable RPU, Q12), tax under current values, under new values
  at existing levels and rates, and under **up to three options**, each option holding its own level table and rates
  as study data (not configuration; nothing is approved by being in a study). Summaries: parcels with lower tax,
  higher tax with the range of increases, reclassified parcels. The report (RTIR) prints through a form.

### 4.6 L6-6 — general revision programme

Replaces the bare job with a programme that follows the GRI and records every property (CLAUDE.md §33, §72).

- **`GeneralRevision`**: revision year, effectivity date (the GR transaction type's fixed rule), the certified SMV
  (approved in PRIME), scope (municipalities, default all in the office's jurisdiction), office order and ordinance
  references (GRI 1), status (Planned → Compiling → Fieldwork → Valuing → Reviewing → Noticing → Rolls → Completed;
  Suspended with dated suspension records — calamity period and extensions, or national emergency until lifted).
- **Checklist**: the GRI steps as a **checklist template loaded as content** (the LAM's text stays out of the
  repository), each step with completion date, user and evidence; the steps PRIME controls are **gates checked by
  the system** (all items valued, FAAS and TDs approved, NOAs served, 60 days elapsed, roll run, ORFs run, report
  issued) (Q13).
- **`GeneralRevisionItem`** per RPU in scope, created server-side from the scope (active units with a current TD):
  previous posted assessment (class, use, market and assessed values), new valuation and assessment, difference,
  status (Pending, Valued, Failed with the **reason**, FieldReview, Assessed, Approved, Posted, Declared, Noticed,
  Served), field-inspection assignment (appraiser, barangay route), inspected on, notes (GRI 9–11). Field changes are
  made through the normal screens; the item is then re-valued. Units the SMV cannot value are flagged for an
  independent appraisal (L1-7, GRI 11).
- **Runner**: chunked by barangay in **PIN order**, idempotent per item, resumable, re-runnable for failed or changed
  items (the territorial-change job's pattern). Draft assessments as today, linked to the item.
- **Bulk approval and posting**: a reviewer approves or returns a batch (a barangay, a filter) after seeing the
  comparison; each item still gets its own approval record through the configured chain — municipal preparation,
  provincial approval unless delegated (LP; GRI 14) — and maker-checker holds per item (Q14).
- **TDs and PINs**: posting prepares the Draft TDs as today, in PIN order so TD numbers follow the tax map (GRI 15);
  bulk TD approval through the same chain. Re-PINning to the LAM's parcel digits at this GR, if the province chooses
  it (question B2), uses L2's tools and is not part of L6.
- **Notices**: combined NOAs per owner generated in bulk for the posted items, issued in bulk, service recorded in
  bulk from a list (mode, date, proof); undelivered notices listed.
- **Roll after 60 days (GRI 17)**: the GR's assessment roll for a municipality runs only when every item's notice is
  served and the configured period (`GeneralRevision:RollWaitDays`, default 60) has passed since the latest delivery;
  otherwise it lists what blocks it. An override with a reason is allowed and recorded (Q15). Then the ORF run
  (GRI 18), and pre- and post-TMCR runs tagged with the GR (GRI 5, 12).
- **Completion report** (GRI 19) and the **GR status report** (Annex I-S): from the programme's data — counts and
  totals by class before and after, notices served, items independently appraised, appeals filed (L7) — through
  forms.
- **Existing job**: kept as history; the old start endpoint is retired once the programme exists (Q16).

### 4.7 L6-7 — SMV amendments

An amendment is a new `Smv` with basis **Amendment**, the SMV it amends, the ground (infrastructure, calamity,
pandemic or emergency, correction of errors), and only the rows it changes; it follows the same certification stages.
From its effectivity its rows take precedence over the amended SMV's rows for the same key; elsewhere the amended SMV
still applies (Q17).

## 5. Data and migrations

Additive only; nothing existing is dropped or rewritten.

- `MarketTransactions`, `MarketTransactionKinds` (lookup), `BuildingPermitAbstracts`, `MachineryRegistrationAbstracts`;
  `TransferTaxClearances.Consideration` (nullable).
- `SmvPreparations`, `SmvConsultations`, `SmvReviewEvents`, `SalesAnalyses` (+ ranges, adopted values),
  `TimeAdjustmentFactors`; `SmvSubClassCriteria`; `SmvSchedules.LocationDescription`, `SmvSchedules.CropDescription`.
- `Smvs.AmendsSmvId`, `Smvs.AmendmentGround`, `SmvBasis.Amendment`.
- `SmvSimulationRuns`, `SmvSimulationResults`; `ValuationTests` (+ rows).
- `SubMarketAreas` (spatial layer).
- `RevenueImpactStudies`, `StudyTaxOptions` (+ option levels and rates), `StudyResults`.
- `GeneralRevisions`, `GeneralRevisionScopes`, `GeneralRevisionSuspensions`, `GeneralRevisionChecklistSteps`,
  `GeneralRevisionItems`; `RegisterRuns.GeneralRevisionId` (nullable).
- Settings: `Smv:*` periods and minimum consultations, `ValuationTesting:*` benchmarks (empty), `GeneralRevision:RollWaitDays`.

Existing `GeneralRevisionJobs` and the assessments they produced stay as they are.

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| L6-1 | Market data and abstracts; transfer consideration; discovery leads; abstracts and sales report forms | M |
| L6-2 | SMV preparation work file, consultations, review and remand, sales analysis and adoption, criteria and location descriptions, SMV Forms 1–12 | L |
| L6-3 | Engine rate source and compute-only mode; simulation runs; valuation testing | M |
| L6-4 | Land value map layer; sub-market areas | S |
| L6-5 | Revenue compliance and tax impact study with treasury inputs and three options | M |
| L6-6 | General revision programme: scope, items, runner, field review, bulk approval and posting, TDs, notices, 60-day roll gate, ORF, TMCR, completion and status reports, suspension, screens | L |
| L6-7 | SMV amendments | S |

Recommended order (Q1): L6-1 → **L6-6** → L6-3 → L6-2 → L6-4 → L6-5 → L6-7. Market data should start accumulating at
once (the preparatory year is "continuous gathering"); the general revision is the assessor's core output and the
current code has no screen and no per-property record, and the province's next GR depends only on a certified SMV
being entered; the preparation tools (L6-2 to L6-5) matter from the next SMV cycle.

Each step: build, tests, browser check of its screens, design-doc log, roadmap and memory (CLAUDE.md §108).

## 7. Exit criteria

1. DEMO sales entered, one imported batch, one prefilled from a transfer; an excluded sale keeps its reason; the
   yearly sales report lists lowest, median and highest per class.
2. A DEMO preparation adjusts sales to its base valuation date, runs the interval analysis for one class, adopts
   three sub-class values into a proposed SMV, records two consultations, and prints Forms 2–5.
3. A simulation under the proposed SMV values a DEMO municipality without creating a single valuation; the testing
   report shows median VPR and CoD per sub-class.
4. The land value map colours DEMO parcels by sub-class under the current and the proposed SMV.
5. A DEMO study with treasury inputs shows the tax gap and three options with the parcels whose tax falls, rises
   (with ranges) or that are reclassified.
6. A DEMO general revision of one municipality: items created from the scope, one failure with its reason, one field
   review re-valued, a batch approved by a second user through the chain, TDs numbered in PIN order, combined NOAs
   served, the roll refused before the waiting period and run after it, ORF and completion report issued; every
   item shows old value, new value, difference, effective date and approver.
7. A DEMO amendment changes one street's unit value from its effectivity; other rows still come from the amended SMV.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Order of the sub-steps | L6-1, then L6-6 (general revision), then L6-3, L6-2, L6-4, L6-5, L6-7 (§6) |
| Q2 | Who prepares the SMV in the province-wide deployment? | The Provincial Assessor's Office owns the preparation and the proposed SMV (Book IV p.104); municipal offices contribute market data for their jurisdiction and see the preparation read-only |
| Q3 | Price on a transfer | An optional "consideration" on the transfer's tax clearance; when present, approval prefills a market transaction for the assessor to validate (never used unvalidated) |
| Q4 | Bulk market data | A CSV import for market transactions within L6-1 (registry abstracts come in bulk), with the upload–validate–preview–confirm flow of CLAUDE.md §60; the general import remains Phase 13 |
| Q5 | Abstract columns (Annexes I-M, I-N, I-O, I-R, I-S are images; this session could not view them) | Model the fields in §4.1 now; install poppler (`pdftoppm`) on this machine so the annex pages can be viewed and the forms matched — or the province supplies filled samples. Your choice |
| Q6 | Adjusting sales to the base valuation date (no method in the LAM) | Time-adjustment factors entered by the assessor per month or quarter on the preparation, with their source; PRIME applies them and shows both prices [DOMAIN VERIFICATION] |
| Q7 | Interval-frequency analysis (Forms 4, 8) | PRIME computes the steps with the assessor's parameters and records the assessor's merges and adopted values; it never adopts a value by itself |
| Q8 | Valuing under a proposed SMV | Add a rate source and a compute-only mode to the one engine (no second calculator, no rolled-back transactions) |
| Q9 | Value used in valuation testing | Land: the sale's area × the proposed unit value of its sub-class, without lot adjustments; buildings are not tested by sales [DOMAIN VERIFICATION] |
| Q10 | Testing benchmarks | Settings shipped empty; the province's figures loaded as content; results shown either way |
| Q11 | Land value map | Derived from parcels and the chosen SMV; sub-market area polygons optional |
| Q12 | Tax impact base | Taxable land parcels as the LAM says, with an option to include all taxable RPUs |
| Q13 | GRI checklist | Template as content (not committed); PRIME-controlled steps are system gates |
| Q14 | Bulk approval of GR assessments and TDs | Batch action that writes one approval record per item through the configured chain; the batch is refused for items the approver created (maker-checker) |
| Q15 | Roll after the 60-day period | Gate per municipality: all notices served and the period elapsed since the latest delivery; override allowed with a reason, recorded and shown on the roll run |
| Q16 | The existing `GeneralRevisionJob` and its endpoint | Keep the table and its assessments as history; retire the start endpoint when L6-6 ships |
| Q17 | Amendment precedence | An amendment's rows win over the amended SMV's for the same key from the amendment's effectivity |
| Q18 | Tax rates and collections | Entered (or imported from a treasury file) on each study with their source; never read from the frozen treasury tables [E1] |

### 8.1 Decisions (user, 2026-10-02: "confirm all")

Q1–Q18 accepted as recommended. Order: L6-1 → L6-6 → L6-3 → L6-2 → L6-4 → L6-5 → L6-7. Q5: the annex images are to be
viewed with poppler installed on the development machine (or from samples the province supplies). Where a question
waits on the Provincial Assessor (B2, C1, E1, E6), the recommendation is the provisional default until the answer
comes.

## 9. Implementation log

### L6-1 — market data and abstracts (2026-10-02)

Done; migration `MarketData` (four tables, a lookup table, one nullable column; the amounts check on transfer
clearances re-created with the new column) applied to the local database only.

- **Market transactions** (`MarketTransaction`): source, mode of conveyance (lookup `ConveyanceMode`, content kind
  `conveyance-modes`; DEMO values in `samples/content-demo`), date, document and file number, grantor, grantee and
  address, place, optional PRIME property, PIN, TD, lot and titles as stated, land and/or building conveyed with their
  class, sub-class, use or crop, kind and structural type, areas, consideration and the part for the land.
  `MarketPrices.Compute` gives the land price per sqm or hectare and the building price per sqm only when they can be
  told apart. Review: Accepted (needs a unit price and a classification) or Excluded (needs a reason), with the field
  validation date; any later edit returns the record to Unreviewed. Cancelled with a reason, never deleted.
- **Transfer prefill** (Q3): `TransferTaxClearance.Consideration` (optional, on the clearance dialog). Approving a
  transfer with it adds one Unreviewed market transaction (source TransferDeed) from the property, its lands, the
  ending owners and the new owners; a machinery-only transfer adds none.
- **Import** (Q4): CSV through the content-pack parser, preview with each row's errors, import with the previewed
  file's SHA-256 fingerprint, all rows in one transaction, refused while any row is invalid or already recorded (same
  municipality, date, document and consideration); at most 5,000 rows; rows carry the batch name.
- **Abstracts**: `BuildingPermitAbstract` and `MachineryRegistrationAbstract` with the annex columns (viewed with
  poppler, Q5), unique number per municipality among uncancelled records; linked to the declared building or
  machinery (picker by property and unit), with a suggestion when a building's permit number or a machine's
  registration number matches. Unlinked ones, other than demolitions, are the **discovery leads**.
- **Reports**: `MarketDataReportRun` (kind, municipality, period) printed through the forms foundation as frozen
  snapshots: abstract of transactions, of permits, of machinery certificates (with the declared machinery's assessed
  value at the end of the period), and the lowest–median–highest sales report from accepted sales
  (`MarketPrices.Spread`). Four PRIME provisional templates, watermarked; the LAM annex layouts are content.
- **Jurisdiction**: query filters on the four tables by municipality; writes outside it are refused. The form
  service's subject check now also covers the Notice of Cancellation and the discovery summons (L3-4) and this
  report. A dedicated permission waits for Phase 12.
- Screens: **Market Data** page (sales and transactions, building permits, machinery registrations, discovery leads,
  abstracts and reports), CSV import dialog, consideration on the transfer clearance dialog, new numbering kinds
  unchanged.
- Tests: `MarketPricesTests` (9), `MarketDataTests` (8), a transfer-prefill test in `PropertyTransactionTests`, content
  pack counts updated; 671 tests pass. Browser, dev database (DEMO): a sale entered and accepted (3,500/sqm); a CSV with
  a bad row previewed with its errors, the corrected file imported (two unreviewed rows); permit DEMO-BP-L61-001 shown
  as a lead and linked to the building on DEMO-BILL-AE94B8; the sales report and the transactions abstract prepared and
  printed for DEMO_Municipality. The transfer clearance's consideration field was type-checked and tested through the
  API, not clicked in the browser.

### L6-6a — programme, scope, items, runs, suspension (2026-10-02)

Done; migration `GeneralRevisionProgrammes` (four tables; two nullable columns on `GeneralRevisionJobs`) applied to the
local database only.

- **Programme** (`GeneralRevisionProgramme`; named so to avoid clashing with the `Features.GeneralRevision`
  namespace): revision year, effective date, the SMV applied (approved, in force on the effective date, covering every
  municipality in scope — else `SMV_NOT_APPLICABLE`), scope, office order and ordinance references, status Planned →
  InProgress → (Completed in L6-6c) or Cancelled. One revision per year and municipality.
- **Items** (`GeneralRevisionItem`): one per active unit with a current approved TD in scope, with PIN and unit number
  (the order of every run), the latest posted assessment and its values, the run outcome (Pending, Assessed, Failed
  with the reason), the valuation and Draft assessment of the last run, the new values. Review, approval and posting are
  read from the assessment, not copied.
- **Runs**: still `GeneralRevisionJob` (assessments reference it), now with the programme and a mode. Compile adds the
  units not yet compiled; Value values each chosen item as of the effective date (`generalRevision: true`) and drafts
  its assessment with the run as `RevisionReference` and the unit's latest posted assessment as the previous one. A unit
  valued under another SMV than the programme's fails (GRI 2). Each item is saved as done; an unexpected error fails that
  item (or stops the run, keeping what was done). Valuing an item again cancels its earlier Draft; an item whose
  assessment is in review or beyond is refused. One active run per programme.
- **Suspension** (Book IV p.125): local calamity for `GeneralRevision:CalamitySuspensionDays` (30), extensions of the
  same length following it, national emergency until lifted; no run starts while one is in force.
- **Cancellation**: only before any assessment goes for review; the drafts are cancelled with it.
- Screens: **General Revision** list and create (searchable SMV); programme page with counts, value totals before and
  after, assessments by status, runs with live progress, suspensions, references, and the units table in PIN order with
  before/after values, change, outcome and failure reason, filters, and "value selected again".
- Tests: `GeneralRevisionProgrammeTests` (5); 676 tests pass. Browser, dev database (DEMO): a 2027 revision of
  DEMO_Municipality on DEMO-SMV-AE94B8 compiled two units and valued them as background jobs — the land unit assessed
  (Draft), the machinery unit failed with its reason; the land unit valued again (first draft cancelled); a calamity
  suspension disabled the runs until lifted.
- The old `POST /api/general-revision` (list of units) still works; it is retired in L6-6c (Q16).

### L6-6b — field review, batch approval and posting, Tax Declarations (2026-10-03)

Done; migration `GeneralRevisionReviewAndBatches` (field-review columns on `GeneralRevisionItems`, `Reason` on
`GeneralRevisionJobs`, the `Mode` column widened, new table `GeneralRevisionRunIssues`) applied to the local database only.

- **Field review** (GRI 9–11): items are assigned in bulk to an appraiser with a route; an inspection is recorded per
  item (date, findings, whether changes were found). Corrections are made through the property's own screens; "changes
  found" puts the item back to Pending so the next value run values it again (its draft gives way). Refused while the
  item's assessment is in review or beyond: it must be rejected first. Filters: not assigned, awaiting inspection,
  inspected, by inspector.
- **Batch runs** (Q14): new run modes Submit, Approve, Reject (with a reason), Post, SubmitTaxDeclarations,
  ApproveTaxDeclarations. A run takes the chosen items, or every item of the programme (optionally of one barangay), that
  are in the state it acts on, in PIN order, and runs as a background job **as the user who started it**. Each item goes
  through the ordinary single-record service call (`IAssessmentService`, `ITaxDeclarationService`), so the configured
  chain, delegation, maker-checker, effectivity and TD checks are exactly those of the single-record screens: there is
  no second approval path. One approval record per item per step, as Q14 asks.
- **Issues**: an item an action refuses is recorded with the code and message (for example
  `CANNOT_APPROVE_OWN_ASSESSMENT`) and stays as it was; the change tracker is cleared after a refusal so nothing the
  refused call added (an approval record signed before a later check failed) is saved with the next item. A post that
  prepared no TD (no TD numbering scheme in force) is done, with a **note** (`TAX_DECLARATION_NOT_PREPARED`) on the run.
- **TDs in PIN order** (GRI 15): posting prepares each draft TD at posting time and numbers it then, so posting in PIN
  order numbers the TDs in tax-map order. The units table shows each item's TD (number, status) and filters by it.
  Approving the new TD cancels the previous one, as for any TD.
- **Fix to L6-6a**: valuing a Failed item again stopped the run (`CK_GeneralRevisionItems_Failed`: the reason was cleared
  but the status kept); the item is now reset to Pending before it is valued. An item without an assessment no longer
  shows an unrelated TD that declares no assessment.
- Screens: batch action buttons (on the selection, or on every unit in that state), a confirmation that says what each
  action does, the reject reason, run issues (refused or note), assign-inspection and record-inspection dialogs, the TD
  and field-review columns and filters; the units table refreshes when a run ends.
- Tests: `GeneralRevisionProgrammeTests` now 9 (batch submit/approve/post with maker-checker per item and the TDs;
  reject and value again; field review; post without a TD scheme; re-valuing a failed item). Browser, dev database
  (DEMO): compile → value → assign to the DEMO municipal appraiser with a route → inspection with changes → value again →
  submit → approve as the maker refused and listed → approve as the checker → post (TD `DEMO-GR-TD-2027-0001`) → submit
  the TD → approve it as the other user (Approved).
- Dev database note: while regenerating this step's migration the local database was rolled back one migration too far,
  which dropped L6-6a's tables and the DEMO 2027 programme recorded above; the migrations were re-applied and a DEMO 2027
  programme rebuilt (with a DEMO TD numbering scheme, `DEMO-GR-TD-{YEAR}-{SEQ:4}`, effective 2026-10-03). The earlier
  runs remain as rows of `GeneralRevisionJobs` without their programme.
- Test isolation: `LamNumberingTests`, `BackTaxTests` and `ExemptionTaxabilityTests` approve a TD numbering scheme starting
  today and failed whenever the database already held an approved scheme starting on that day (the DEMO scheme above);
  they now set the database's approved schemes aside inside their rolled-back transaction, as other tests do. 680 tests pass.
- Not verified in a browser: the reject dialog (covered by an integration test). The chain with a provincial final step
  under delegation was not exercised in the browser (the dev database has no assessment or TD chain in force today); the batch uses
  the same `ApproveAsync` the chain tests cover.

### L6-6c — notices, roll gate, records, checklist, completion and reports (2026-10-03)

Done; migrations `GeneralRevisionRecords` (`RegisterRuns.GeneralRevisionProgrammeId`, `RegisterRuns.RollGateOverrideReason`) and
`GeneralRevisionCompletion` (`GeneralRevisionItems.ExclusionReason` with its check, tables `GeneralRevisionChecklistStepDefinitions`
and `GeneralRevisionChecklistSteps`) applied to the local database only. **L6-6 is complete.**

- **Notices in bulk** (LGC §223): run modes GenerateNotices and IssueNotices. A posted unit needs a notice when it had no
  previous assessment or its assessed value changed; the units of one sole declared owner share one combined notice, a unit
  with several owners gets its own notice addressed to all its parties. Both go through `INoticeService`, so its checks hold
  (a refused notice is an issue on each of its units). Service is recorded in bulk for chosen issued notices (one mode,
  receipt date and proof; "received by" defaults to each addressee), notice by notice through the ordinary call; refusals are
  listed. The revision's notices are listed with status, issue overdue, receipt and appeal deadline. [DOMAIN VERIFICATION:
  whether a general revision requires a notice for a unit whose assessed value did not change; PRIME follows §223.]
- **Roll gate** (GRI 17; Q15), per city/municipality: every unit posted and declared by an approved new TD (the roll lists TDs
  in force), every notice the units need served, and `GeneralRevision:RollWaitDays` (60) elapsed since the latest receipt. A
  taxable or exempt roll of the revision is refused while the gate is closed unless a reason is given; the reason is stored
  on the run and written into its remarks, so the printed roll shows it.
- **Register runs of the revision**: assessment rolls (taxable, exempt), pre- and post-TMCR per barangay with units, and
  Ownership Record Forms for every current owner of the posted units, as of the revision's effective date by default, through
  `IRegisterService` and tagged with the revision.
- **Units taken out**: an item can be excluded with a reason before its assessment goes for review (its draft is cancelled)
  and put back; excluded units are not valued, not counted by the gates or the roll, and listed with the reason in the
  completion report.
- **Checklist as content** (Q13): `GeneralRevisionChecklistStepDefinition` (code, order, title, description, optional gate),
  effective-dated, Draft until a second user approves; content kind `general-revision-checklist` (docs/analysis/lgu-content-pack.md)
  or the admin card on the General Revision page. A revision copies the steps in force when its checklist is loaded. A step
  naming a gate is met when PRIME finds the condition (Compiled, Valued, Approved, Posted, TaxDeclarationsApproved,
  NoticesServed, RollWaitElapsed, AssessmentRollRun, OwnershipRecordsRun, CompletionReportIssued); a manual step is marked
  done with a date and evidence. The sample pack ships four DEMO steps with invented titles.
- **Completion**: refused while a run is active or anything blocks it: units compiled, valued, approved, posted and declared,
  notices served, a taxable roll run of the revision for every barangay with units, and every manual step of a loaded
  checklist marked done. A roll run before the waiting period with a recorded reason counts. Completion closes the revision.
- **Reports**: `GR_COMPLETION_REPORT` and `GR_STATUS_REPORT` (provisional templates, subject = the programme): references,
  units by outcome, values before and after by classification from the lines of the assessments replaced and made, notices,
  register runs, units taken out; appeals are noted as recorded from L7. Previewed at any time, issued (frozen) only once the
  revision is completed. [DOMAIN VERIFICATION: the status report's fields, LAM Annex I-S.]
- **Retired** (Q16): `POST /api/general-revision`; `GET /api/general-revision/{id}` still reads the earlier jobs.
- Screens: "Notices and records" (notice batch buttons, notices table with bulk service, roll gate per municipality, register
  runs with the override reason and print), "Completion" (conditions, checklist, completion, report buttons), take-out and
  put-back on each unit, checklist template card with add and approve. The page refreshes units, notices and gates when a
  run ends.
- Tests: `GeneralRevisionProgrammeTests` now 13 (notices per owner, gate closed then overridden, bulk service, gate open after
  the period, ORF per owner; unchanged value needs no notice; take-out and put-back; checklist, completion and reports);
  content-pack import counts updated. 684 tests pass. Browser, dev database (DEMO 2099 revision on DEMO-L13-CERT, assessed
  value 60,000 to 180,000): submit, approve as checker, post, generate and issue the notice, roll refused without a reason and
  run with one (printed with the reason), service recorded (appeal deadline and the 60-day date shown), ORF run; then the
  failed machinery unit taken out, the TD submitted and approved, two DEMO checklist steps added and approved by the checker,
  checklist loaded (the gate step met by PRIME), the manual step marked done, the revision completed and the completion report
  issued.
- Not verified in a browser: the status report print (covered by an integration test), the exempt roll, pre- and post-TMCR
  runs of the revision.
- Dev data: DEMO revision 2099 (`b5e7f82e-…`) is Completed; the DEMO 2027 revision (`9f0b33c0-…`) is in progress.

