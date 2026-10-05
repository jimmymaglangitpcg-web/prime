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

### L6-3 — values under a proposed SMV, simulations and valuation testing (2026-10-05)

Done; migrations `SmvSimulations` (tables `SmvSimulationRuns`, `SmvSimulationScopes`, `SmvSimulationResults`) and
`ValuationTests` (tables `ValuationTestRuns`, `ValuationTestScopes`, `ValuationTestSales`) applied to the local database only.
**L6-3 is complete.**

- **One engine, a rate source and a compute-only mode** (Q8): `ValuationMode(ProposedSmvId, ComputeOnly)` on every
  `IValuationService.ComputeFor*Async`. The default prices from the approved SMV in force and stores the valuation, as before.
  Under a proposed SMV the engine reads that SMV's schedule rows, adjustment factors, construction costs, extra-item costs and
  depreciation tables whatever their status (not rejected, cancelled or voided; their dates still apply; of two versions for
  one key the newest wins) and only where the SMV covers the municipality. Compute-only returns the lines and totals with an
  empty id and changes nothing: no valuation row, no market value written on the land, building or machine. A proposed SMV is
  **always** compute-only (`PROPOSED_SMV_COMPUTE_ONLY`), so a value under an unapproved SMV can never be assessed or posted.
- **One assessment calculation**: the grouping by classification and use, the level per group and the taxability marking moved
  from `AssessmentService.CalculateAsync` into `AssessLinesAsync`, which the stored path and the simulation both call. Levels
  are those in force on the as-of date; a tax option's level table comes with L6-5.
- **Unit simulation**: `POST /api/rpus/{rpuId}/simulation?smvId=&asOf=` values one unit compute-only (as for a general
  revision: a building takes a new depreciation) and assesses it; the failure reason is returned, not raised. API only; no
  screen uses it yet.
- **Simulation runs** (`SmvSimulationRun`, background job): an SMV, an as-of date (the proposed effectivity) and a scope of
  cities/municipalities, refused for an SMV that is rejected or cancelled or does not cover the scope. The runner takes every
  active unit with an approved TD in PIN order, and records per unit the current posted assessment (market and assessed value,
  principal classification = the class of its largest row) beside the simulated values (market, assessed, taxable assessed,
  principal classification), or the reason it could not be valued or assessed. Results are saved per 100 units, with the
  change tracker cleared between batches; nothing else is written. The run's summary compares the units having both values:
  totals before and after, higher, lower, unchanged, classification changed. Results are jurisdiction-filtered.
- **Valuation testing** (Q9, Q10; `ValuationTestRun`, made in the request): every accepted, uncancelled land sale of the scope
  and optional period. The value is the land area × the SMV's unit value for the sale's class, sub-class and use, read
  through the engine's own rate selection (`IValuationService.LandRateAsync`; no zone, no lot adjustments). The price is the
  consideration, or the land part when a building was conveyed with it. Ratio = value ÷ price (4 decimals). Rows are frozen
  with the reason when a sale cannot be tested: no classification, no area, the land price cannot be separated from the
  building's, no unit value, or a rate unit PRIME cannot match to the sale's area. Square metres and hectares convert
  when the rate's unit reads as one of them. Median ratio and coefficient of dispersion (`ValuationTesting`, Domain) are given
  per sub-class, class, city/municipality and overall.
- **Benchmarks** are settings `ValuationTesting:MedianRatioLow`, `MedianRatioHigh` and `MaximumCoefficientOfDispersion`,
  shipped empty. When set, each group is marked within or outside them. **Deviation from Q10:** they are deployment
  settings, not a content-pack kind; a content kind can be added when the province supplies figures.
- **Prices are as recorded.** Adjustment to the base valuation date comes with the time-adjustment factors of L6-2; the
  report says so.
- **Report**: `VALUATION_TEST_REPORT` v1 (provisional, `FormSubjectType.ValuationTest`): header, groups, every sale without
  party names. The letterhead is the municipality's when the test covers one.
- **Screen** `/smv-testing` ("SMV Testing"): simulations (start, progress, summary, results with PIN, failed and
  classification-changed filters) and valuation tests (create, groups with benchmark marks, sales, print).
- **Found for L6-2:** a proposed SMV cannot be entered without an ordinance number and date (ordinance basis) or a
  certification reference (certified basis), which a proposed SMV does not have yet. The DEMO proposed SMV uses a DEMO
  ordinance; the preparation record of L6-2 should allow a proposed SMV without either.
- Tests: `SmvSimulationTests` (10: compute-only stores nothing, a proposed SMV is never stored, no rate before the proposed
  rows take effect, unit simulation, a run with a failure and its summary, start refusals, a valuation test with hectares and
  two exclusions plus its report, rate-unit conversion); `ValuationTestingTests` (11). 705 tests pass; frontend production
  build and lint clean.
- Browser, dev database: DEMO proposed SMV `DEMO-L63-PROPOSED` (2028, draft, one draft row at 1,500/sqm for DEMO_Residential,
  covering DEMO_Municipality) and four DEMO accepted sales of 2026-08-01. Simulation of DEMO_Municipality: the land unit
  900,000 → 450,000 (assessed 180,000 → 90,000; exempt, so no taxable value); the machinery unit failed with its reason. A
  results-table bug was found and fixed here: the table missed the run's last batch. Valuation test: 5 accepted sales,
  4 tested, median 1.0982, CoD 6.14% (matches the hand calculation); the earlier accepted sale has no matching use and is
  listed with its reason. The report was issued and printed. At 390 px the page has no horizontal overflow, and there were no
  console errors.

### L6-2a — preparation work file, consultations, review and certification (2026-10-05)

Done; migration `SmvPreparations` (tables `SmvPreparations`, `SmvConsultations`, `SmvPreparationEvents`; check
`CK_Smvs_CertifiedBasis` relaxed) applied to the local database only. L6-2b (sales analysis) and L6-2c (criteria, location
descriptions, SMV Forms 1–12) remain.

- **Work file** `SmvPreparation`: revision year (one open per year), title, date of valuation, base valuation date, team
  notes, status. Creating it creates the **proposed SMV**: a Draft `Smv` with certified basis, no certification reference
  yet, the planned effectivity and the coverage. This closes the gap found in L6-3. The database check now lets a Draft or
  Cancelled certified SMV lack its reference. Approval refuses a certified SMV without one (`SMV_CERTIFICATION_REQUIRED`), and
  refuses an SMV of a preparation until the certified SMV's publication is recorded (`SMV_NOT_PUBLISHED`). Direct SMV entry is
  unchanged: it still asks for the reference at creation.
- **Steps** (`SmvPreparationEvent`, `SmvPreparationFlow` in Domain): published for comment, submitted to the BLGF Regional
  Office, endorsed by the Regional Office, endorsed by the BLGF, remanded (reasons required), resubmitted, certified
  (reference required, unique), not certified, published (reference required), transmitted to the LCE and Sanggunian. Only an
  order that can have happened is accepted. A step cannot be dated in the future or before the latest one.
- **Effects on the SMV**, written only while it is a Draft: the stage dates and references. Submission sets the base valuation
  date to the date of submission (Book IV p.112), with a warning when the planned date differed. Publication sets the
  effectivity at `Smv:EffectivityDaysAfterPublication` (15) days after publication, with a warning for rows that take effect
  later.
- **Consultations** (date not in the future, mode, venue or link, attendance, minutes reference, notes): allowed before
  submission, and after a remand. Submission with fewer than `Smv:MinimumConsultations` (2) gives a warning, not a refusal.
- **Due dates** (reminders only): Regional Office review 45 days after submission, BLGF 30 after the Regional Office's
  endorsement, certification 30 after the BLGF's, resubmission 30 after a remand, decision 10 after resubmission, effectivity
  15 after publication. All are settings (`Smv:*`). [DOMAIN VERIFICATION against RA 12001's IRR.]
- **Offices** (Q2): only a province-wide user creates or changes a preparation (`SMV_PREPARATION_FORBIDDEN`, HTTP 403);
  municipal offices read it.
- Cancelling (with a reason) cancels the proposed SMV and frees the revision year.
- API `/api/smv/preparations` (list, create, get, put, consultations, events, cancel). Screens `/smv-preparation` (list,
  create) and `/smv-preparation/:id` (work file, proposed SMV with stage dates and links to its rows and to SMV Testing,
  consultations, steps offered by status, due-date reminder, warnings).
- Tests: `SmvPreparationFlowTests` (15) and `SmvPreparationTests` (4: proposal to publication with the stage dates, approval
  refused before publication then approved by a second user; refused order, date and duplicate year; cancellation; municipal
  read-only). 724 tests pass; frontend build and lint clean.
- Browser, dev database: DEMO preparation 2029 created, a consultation recorded, published for comment, submitted (warning:
  one consultation of two; base valuation date = 2026-10-01; due 2026-11-15). The DEMO municipal appraiser sees it read-only
  at 390 px, with no overflow and no console errors. Left in the dev database at Submitted.

### L6-2b — sales analysis, time adjustment and adoption (2026-10-05)

Done; migration `SalesAnalyses` (tables `SmvTimeAdjustmentFactors`, `SalesAnalyses`, `SalesAnalysisScopes`, `SalesAnalysisSales`,
`SalesAnalysisGroups`) applied to the local database only. L6-2c (criteria, location descriptions, SMV Forms 1–12) remains.

- **Source.** The LAM (Book IV pp.112–115) lists the forms. Annexes IV-C, IV-D, IV-G and IV-H give the steps: round to the
  nearest hundred, sort, interval percent of each value, average interval, ranges, count per range, combine ranges with few
  sales, midpoints highest to lowest as sub-class values. The worked example is "for illustration purposes only" and is not
  reproducible by one rule: its first two ranges are about ±4%, the rest ±6%, and its midpoints do not follow from its bounds.
  So PRIME applies one stated reading (`SalesAnalysisMath`): a range starts at the lowest value not yet in a range, its
  midpoint is that value × (1 + w) rounded to the increment, and its high is the midpoint × (1 + w). The width w is the
  assessor's, by default the average interval (Form 8's instructions give ±5% for agricultural land; that is entered, not
  coded). [DOMAIN VERIFICATION REQUIRED: the construction of the ranges.]
- **Time adjustment** (Q6): `SmvTimeAdjustmentFactor` per preparation (period, factor, source; periods may not overlap),
  entered by the assessor. A sale whose date no factor covers is not analysed, with the reason. With no factor at all,
  prices are used as recorded and the analysis says so. Adding or removing a factor recomputes every analysis of the
  preparation.
- **Other adjustment** per sale (percent, + or −): the road and distance deductions for agricultural land, whose annex
  percentages are examples and are not coded.
- **Analysis** (`SalesAnalysis`): one per class and optional use or crop in a preparation; market areas
  (cities/municipalities), optional sales period, unit (per sqm or per hectare, converted from the sale's area), rounding
  increment (100), range width. The accepted land sales are copied in (Form 2/6 data: date, location, TD, PIN, recorded
  sub-class, area, land price). "Refresh" adds sales accepted since, and leaves out, with a reason, those no longer accepted.
  The assessor can leave a sale out (with a reason) or adjust it. Sales whose land price cannot be told apart, or with no
  area, are not analysed.
- **Tables:** Table 1 (adjusted and rounded values, lowest to highest, with intervals and their average) and Table 2 (ranges
  with low, mid, high and sales) are computed on reading. Table 3 is the assessor's sub-classes: each runs from the low of one
  range to the high of another, names a sub-class, and is numbered from the highest. PRIME proposes the frequency-weighted
  mean of the merged midpoints and never adopts by itself (Q7).
- **Adoption** writes a **draft** row in the proposed SMV (class, use, sub-class, land, per sqm or per hectare, effective on
  the SMV's effectivity) through `ISmvService`, in one transaction with the group. An adopted group is frozen, and a
  sub-class is adopted once per class and use in a preparation. A correction is a new row on the Valuation Rules page.
- Changes are allowed only for the provincial office, while the preparation is preparing, published for comment, or remanded.
  After submission the analysis is read-only (the base valuation date is fixed).
- **Bug found and fixed:** since L6-2a a proposed SMV may have no reference, and the content pack's SMV lookups keyed SMVs by
  reference, so two unreferenced SMVs crashed every pack preview (five content-pack tests failed against the dev database).
  The lookups now skip unreferenced SMVs; regression test
  `ContentPackPreviewTests.ProposedSmvsWithoutAReference_DoNotDisturbThePreview`.
- API: `/api/smv/preparations/{id}/time-factors`, `/api/smv/preparations/{id}/analyses`, and `/api/smv/analyses/{id}` (with
  `/refresh`, `/sales/{saleId}`, `/groups`, `/groups/{groupId}/adopt`). Screens: factor and analysis cards on the preparation;
  `/smv-preparation/analyses/:id` with parameters, sales, Tables 1–3 and the sub-class editor.
- Tests: `SalesAnalysisMathTests` (10); `SalesAnalysisTests` (2: factors, exclusions, ranges and counts, leaving out,
  overlap refused, groups and proposals, adoption into the proposed SMV, read-only after submission; refresh and duplicate
  class). 737 tests pass; frontend build and lint clean.
- Browser, dev database: DEMO preparation 2030 (DEMO_Municipality) with a DEMO factor of 1.02 for 2026. An analysis of
  DEMO_Residential at ±10% gave the 4 DEMO sales 1,300 / 1,400 / 1,400 / 1,600; ranges 1,300–1,540 (3) and 1,600–1,980 (1);
  DEMO_Subdivision adopted at the proposed 1,400, now a draft row of the proposed SMV effective 2030-01-01. Fixed an overflow
  at 390 px (the notes input). No console errors.
- Dev data: many leftover test classes share the name "DEMO_Residential", so the class picker is ambiguous in the dev
  database; one empty DEMO analysis was made with the wrong one. Analyses cannot be deleted (working records; none needed so
  far).

### L6-2c — sub-class criteria, row descriptions, SMV Forms 1–12 (2026-10-05)

Done; migration `SmvFormsAndCriteria` (`SmvSchedules.LocationDescription`, `SmvSchedules.CropDescription`, table
`SmvSubClassCriteria`) applied to the local database only. The LAM's own layouts of the twelve forms remain (L6-2d, content in
the untracked `lgu-content/`, as in L5).

- **Sub-class criteria** (Form 1): `SmvSubClassCriterion` per SMV (class, sub-class, order, criteria text up to 4000). The
  text is the province's: it is printed, never used to classify. It is replaced as a whole while the SMV is a draft
  (`SMV_NOT_DRAFT` after), and only by the provincial office. API `GET/PUT /api/smv/{smvId}/sub-class-criteria`; edited
  on the preparation page.
- **Row descriptions:** an SMV row may carry a location description (Form 5: street, side, from–to) and a crop and
  productivity description (Form 9). They are printed and never used to select a rate; they are set when the row is
  created (Valuation Rules form, or the content pack's new optional CSV columns `location-description` and
  `crop-description`).
- **Forms** (provisional layouts, watermarked; `FormSubjectType.Smv` = the SMV, `FormSubjectType.SalesAnalysis` = an analysis):
  `SMV_FORM_1` criteria; `SMV_FORM_5` land values whose unit is not per hectare; `SMV_FORM_9` land values per hectare (by
  crop), with plants and trees; `SMV_FORM_10` construction costs; `SMV_FORM_11` depreciation with remaining value;
  `SMV_FORM_12` extra items; `SMV_FORM_2`/`6` statement of sales, `3`/`7` tabulation, `4`/`8` computation (Tables 1–3).
  The open rows of every status are listed with their status, and a proposed SMV is marked PROPOSED. Forms 5 and 9 split
  rows by the unit text (`ValuationTestService.RateUnitOf`, shared with valuation testing), so PRIME singles out no class.
  The analysis forms read the analysis through `ISalesAnalysisService`, so nothing is computed twice. The analysis page
  offers Forms 2–4, or 6–8 when values are per hectare.
- **Issue rule** (found in the browser): printing issues a frozen copy, and reprints return the same copy, so printing a
  proposed SMV would have frozen it mid-preparation. While the preparation is preparing, published for comment, remanded or
  cancelled, the forms are therefore preview-only (`FORM_SUBJECT_NOT_ISSUABLE`, with the reason shown). Once submitted, the
  copy as submitted is issued; after a remand the earlier copy is cancelled and issued again on resubmission. An SMV
  outside a preparation is issued once approved. The three copies issued in the dev database before this rule were
  cancelled with a reason.
- Tests: `SmvFormsTests` (2: criteria rules; the SMV's form data, the six forms rendered, issue refused while preparing and
  allowed after submission); `SalesAnalysisTests` now renders Forms 2–4. 739 tests pass; frontend build and lint clean.
- Browser, dev database (DEMO preparation 2030): criteria entered for DEMO_Subdivision; Form 1 shows them; Form 5 shows
  the adopted 1,400 per sqm row as Draft under "PROPOSED"; Form 4 shows Tables 1–3 of the DEMO analysis; the preview says
  why it cannot be issued yet; no console errors.
- [DOMAIN VERIFICATION: the columns of every form against Annexes IV-A to IV-L; the provisional layouts reproduce none of
  them.]

### L6-2d — the LAM layouts of SMV Forms 1–12, as content (2026-10-06)

Done; no migration. **L6-2 is complete.**

- The twelve layouts were written by PRIME from LAM 2025 Annexes IV-A to IV-L (rendered with poppler) into the untracked
  pack `lgu-content/zamboanga-sibugay` (`forms/SMV_FORM_n.lam.liquid`, catalogue `forms/forms.json`, pack version
  `+lam-smv-forms-1`), as L5 did for the other LAM forms. Nothing of them is in the repository (CLAUDE.md §118).
- **Data the layouts needed**, added to the committed form-data providers as plain shaping:
  - Form 9 as a matrix (kind of land × sub-class, with the productivity text per class);
  - Form 10 as a matrix (structural type × building design, a classification-specific cost noted);
  - Form 11 as a matrix (structural type × age band, then the minimum remaining value);
  - criteria split into lines (Form 1 numbers them);
  - for each sale, its document file number, its kind (use or crop) and month and year (Forms 2 and 6);
  - MV, area, date, TD and PIN on Table 1 (Forms 3, 4, 8).
- **Gaps between the annexes and PRIME's records:**
  - Form 7's road type and distances to the road and the poblacion are not recorded per sale; those columns print blank
    for hand entry.
  - The time adjustment has no column of its own: Form 7's adjusted value and Form 3's adjusted unit value include it,
    and the form says so.
  - The annex example of Form 4's ranges is shown as PRIME computes them, with the method stated under Table 2.
- Dev database: the pack was imported through the API (12 created) and the twelve LAM versions approved by the second
  user, effective 2026-10-06 (provisional). All eighteen renders checked (Forms 1, 5, 9–12 against the DEMO 2030
  proposed SMV and the DEMO 2099 SMV with building tables; Forms 2–4 and 6–8 against the DEMO analysis) with no template
  error; Forms 1, 2, 4, 5, 9, 10 and 11 were viewed.
- Tests: `SmvFormsTests` and `SalesAnalysisTests` pin the provisional layouts (`TestSeed.UseReferenceFormsAsync`), as the
  L5 tests do. 739 tests pass.
- Open for the Provincial Assessor: the layouts themselves; whether Form 7 should take a sale's road and distances (that
  would be new fields on the market transaction); the adoption date of the LAM forms.

### L6-4 — land value map and sub-market areas (2026-10-06)

Done; migration `SubMarketAreas` (table `SubMarketAreas`, GiST index, one current version per code) applied to the local
database only.

- **Value map** `GET /api/gis/value-map` (`LandValueMapService`). Each active parcel in the extent shows its property's land
  (the largest active one) and its principal strip (largest area), taken as the engine prices it: the priced class and
  sub-class, the use, the strip's or land's zone, and the property's barangay. The unit value comes from
  `IValuationService.LandRateAsync`, which now also takes the zone, so the map selects a rate exactly as a valuation would.
  The source is a chosen SMV (`smvId`, any status not rejected or cancelled, e.g. a proposed one) or the approved SMV in
  force on `asOf`. Each feature names the SMV that gave its rate, or the reason it has none (no land; no rate for its keys).
  A legend summarises the sub-classes with their parcel counts and values. Lot adjustments and independent appraisals are
  left out: the map shows unit values, not property values. No owner data.
- **Sub-market areas** (`SubMarketArea`, reference layer `submarketareas`): polygons keyed by their own code, with a name,
  imported by GeoJSON with a dry run and new versions on change, like disputed areas. Shown only (Book IV p.110: areas
  without parcels yet).
- **Screens:** the Tax Map has two new layers, off by default: "Land values" and "Sub-market areas". The Land value map
  card picks the SMV ("the approved SMV in force", or any SMV) and the colouring:
  - by sub-class: a stable colour per class and sub-class;
  - by value band: up to five quantile bands of the values loaded.

  Parcels without a value are grey. The card shows the legend with counts, and labels show sub-class and value when zoomed
  in, under the parcel number. The shared as-of date now applies to boundaries and values. The printable map carries the
  SMV and colouring, prints the value legend, and names the source of the values under "Data sources".
- Tests: `LandValueMapTests` (2: a parcel at the SMV in force and under a proposed SMV, no rate before the proposed rows
  take effect, a parcel without land, refusals; the sub-market areas layer imports and reads). 741 tests pass; frontend
  build and lint clean.
- Browser, dev database: DEMO parcels of 990-01-0001-001-01, -03 and -04. The first shows 1,000 per sqm under the SMV in
  force (DEMO-SMV-AE94B8) and 1,500 under DEMO-L63-PROPOSED as of 2028-01-01; the other two have no land and show grey.
  A DEMO sub-market area (`DEMO-SMA-01`) was imported. The print sheet shows the value legend and sources; no overflow at
  390 px; no console errors. Found and fixed here: the value label covered the parcel number ("1,000" over "01"), so it now
  sits below it.
- Open: an SMV's rows may differ by barangay or zone, so a parcel's colour is what its own keys select; the province's
  sub-market areas, if it draws them, come as content (a `gis-layer` entry with layer `submarketareas`).

### L6-5 — revenue compliance and tax impact study (2026-10-06)

Done; migration `RevenueImpactStudies` (tables `SmvSimulationResultLines`, `RevenueImpactStudies`, `RevenueImpactRates`,
`RevenueImpactOptions`, `RevenueImpactOptionLevels`) applied to the local database only.

- **Source.** RA 12001 §17; LAM 2025 Book IV pp.116–118, paraphrased:
  - **Revenue compliance, by the tax gap.** Tax potential = taxable assessed value × the rate. Total collection = the year's
    collection of that year's tax (no penalties, no prior years) + the discounts given. Tax gap = potential − total
    collection. Compliance rate = total collection ÷ potential (the taxpayers' view). Collection efficiency = actual
    collection ÷ potential (the collector's view).
  - **Tax impact.** Each taxable land parcel's tax at the new values under the existing levels and rates, then under tax
    options. The comparison gives the parcels whose tax falls, those whose tax rises (with the range of increases), and
    those reclassified.

  The arithmetic is `RevenueImpactMath` (Domain).
- **The study** (`RevenueImpactStudy`) rests on a completed simulation run (L6-3): the run's units are the units studied.
  - **Inputs (the Treasurer's figures, Q18):** existing rates, each with label and source, summed into the existing rate;
    the year's collection and discounts with their source; the reference date of the taxable values (default 1 January
    of the year). PRIME never reads the frozen treasury tables (CLAUDE.md §0) and computes no bill.
  - **Options:** up to three, each with its own total rate and a level table by class, optional use and bracket. A row
    no level matches keeps its level under the new values; the report counts and flags such rows. Options are study
    data, not configuration.
- **Computed on reading**, so nothing goes stale:
  - Compliance from the taxable rows of each active unit's posted assessment in force on the reference date, in the run's
    cities/municipalities.
  - Tax impact over the run's land units (every unit, if the study asks; Q12) that were valued and are taxable before or
    after: current taxable assessed value from the posted assessment's taxable rows; new taxable value from the
    simulation; each option applied to the simulation's rows.
  - Simulation results now keep their assessment rows (`SmvSimulationResultLine`). A run made before this falls back to
    one row of the unit's principal class, with a warning.
- **Report** `REVENUE_TAX_IMPACT_REPORT` v1 (provisional, `FormSubjectType.RevenueImpactStudy`): rates, compliance, the
  scenarios side by side with lower, higher (smallest, median, largest increase and the largest percent), unchanged and
  reclassified, the options' levels, the reclassified units. It is preview-only from the page: a study can still change,
  and an issue would freeze it.
- **Offices:** the provincial office creates and edits; municipal offices read.
- API `/api/smv/impact-studies` (list, create, get, put — the study is replaced as a whole — and `/{id}/units`, paged,
  filtered by change). Screens `/smv-impact` ("Tax Impact Study") and `/smv-impact/:id`.
- Tests: `RevenueImpactMathTests` (7) and `RevenueImpactStudyTests`. The test's DEMO study: rates 1% + 1%, collection
  1,500 + 100 → potential 2,000, gap 400, compliance 80%, efficiency 75%. The unit's tax goes 2,000 → 3,000 at the new
  values; option 1 (1.5%, a 15% level) gives 1,687.50; option 2 (2%, no level) gives 3,000 with one row at its existing
  level. The update replaces the study; refusals are covered. 749 tests pass; frontend build and lint clean.
- Browser, dev database: a DEMO simulation of DEMO-L63-PROPOSED run through the API, and a DEMO study created and filled
  in the UI (two DEMO rates, collection, two options). Compliance: taxable AV 160,000 on 2026-01-01, potential 3,200,
  gap 1,600, compliance 50%, efficiency 46.88%. The tax impact is empty in the dev database because its only DEMO land
  unit is exempt (the integration test covers the impact arithmetic). Report previewed; no overflow at 390 px. Fixed here:
  two antd deprecation warnings, and the report's footnote now shows only when a row kept its level.
- [DOMAIN VERIFICATION: the measures' definitions against RA 12001's IRR; whether the existing rate is one figure for the
  province or differs by municipality (a study per municipality can be made meanwhile).]

### L6-7 — SMV amendments (2026-10-06)

Done; migration `SmvAmendments` (`Smvs.AmendsSmvId`, `Smvs.AmendmentGround`, constraint `CK_Smvs_AmendmentBasis`;
`CK_Smvs_CertifiedBasis` now covers amendments) applied to the local database only. **L6 is complete.**

- **Source.** LAM 2025 Book IV Ch. III §4 (p.119–120), paraphrased: between revisions the provincial or city assessor
  recommends revisions of the SMV to the BLGF Regional Office when market values change significantly — roads or similar
  infrastructure, calamities, pandemics or declared emergencies and analogous circumstances, or the correction of errors
  and inequalities.
- **The amendment** is an `Smv` with basis `Amendment`, the SMV it amends (`AmendsSmvId`) and its ground
  (`SmvAmendmentGround`). Rules:
  - The amended SMV is approved and is not itself an amendment: every amendment hangs off one SMV, and a later amendment
    wins over an earlier one.
  - The amendment takes effect after the amended SMV, covers only municipalities it covers (none = the whole province,
    only when the amended SMV covers the whole province), and carries its revision year (so `{REV}` numbering and the
    revision in force do not move).
  - It is approved with its certification's reference, like a certified SMV; it may be entered without one while it is
    prepared, and a preparation (L6-2a) may carry it.
  - It carries unit values and adjustment factors only. Building, extra-item and depreciation tables are refused
    (`SMV_AMENDMENT_TABLES_UNSUPPORTED`): a new SMV changes them. A general revision applies the SMV itself, not an
    amendment (`SMV_NOT_APPLICABLE`); its amendments in force apply through it.
- **Precedence (Q17), in `SmvRateSelector`.** The latest-effective SMV with a matching rate is chosen as before, now
  counting its amendments' rates as its own. Within it, each row key (class, property type, improvement kind, sub-class,
  zone, barangay, use) takes the row of the latest member in force — amendment by effectivity, then the SMV — and only
  then the most specific row is picked. So an amendment's barangay row does not hide a more specific zone row of the
  amended SMV, and an amendment may add a key the SMV lacks. An amendment of an earlier SMV never beats a later SMV.
  Adjustment factors follow the same rule per code and classification (`ValuationService.FactorAsync`).
- **Simulation (L6-3).** A proposed amendment is simulated with the approved rows of the SMV it amends and of its
  approved amendments; its own rows win. The value map (L6-4) and the impact study (L6-5) follow through the same engine.
- **Review.** An amendment's unit values show the amended SMV's value each replaces, or "new key".
- **Content packs.** An `smv` item with basis `Amendment` names `amends` (the reference of an approved SMV already in
  PRIME), `amendmentGround` and its `certificationReference`; its revision year is the amended SMV's.
- Tests: `SmvRateSelectorTests` (+6: same key replaced, a less specific amendment row does not hide a more specific
  amended row, added key, latest amendment wins, an amendment of an earlier SMV does not beat a later SMV, a proposed
  amendment wins in its family); `SmvAmendmentTests` (refusals; the replaced value; a draft prices nothing; 500 → 600
  from the effectivity, the day before still 500; the amended SMV's factor applies to the amended row and the
  amendment's own factor wins: 660 → 720; a proposed amendment simulated over its family; building tables refused;
  approval and coverage rules); `ContentPackPreviewTests.SmvFiles_CheckAmendments`. 758 tests pass; frontend build and
  lint clean.
- Browser, dev database: a DEMO amendment `DEMO-AMD-L67` of `DEMO-L13-CERT` (ground: infrastructure, effective
  2099-08-01) created through the new form, its row approved by the dev checker. The value map prices parcel
  990-01-0001-001-01 at 3,000 (DEMO-L13-CERT) on 2099-07-31 and 3,600 (DEMO-AMD-L67) from 2099-08-01; on 2026-08-01 it is
  still 1,000 under DEMO-SMV-AE94B8. The unit-value list shows "Replaces 3,000". No console errors; no overflow at 390 px.
- Not done: SMV pickers load the newest 100 SMVs (the API's page limit). The dev database holds about 100 leftover test
  SMVs, so two old DEMO SMVs are not offered there; a province has a handful.
- [DOMAIN VERIFICATION: who certifies an amendment (the Secretary of Finance, as for an SMV, or the BLGF on the
  assessor's recommendation) and whether it is published and takes effect like an SMV; whether an amendment may change
  building cost or depreciation tables (PRIME refuses it until confirmed).]
