# Phase 11 — Reporting (design)

| | |
|---|---|
| Phase | 11 (CLAUDE.md §57, §98; roadmap "Phase 11 — Reporting") |
| Status | Decisions recorded 2026-10-10 (all recommendations accepted); R1–R3 done; R4 detail Q13–Q21 accepted 2026-10-10 (§10.4); R4a done 2026-10-11 |
| Sources | CLAUDE.md §55, §57, §71, §73, §76; LAM 2025 Book I Ch. I §5 (reportorial requirements, pp.24–25), Ch. I on idle lands (pp.11–13) and the province's reports (p.15); LAM Annexes I-P, I-Q, I-R, I-S; LAM gap analysis J5–J7 (untracked, `docs/lam/`) |
| Depends on | Assessments, registers and forms (Phases 6, 10), jurisdiction (LP), permissions and audit (Phase 12) |
| Order | After Phase 12; Phase 13 skipped for now (user, 2026-10-08) |

The LAM's report layouts are LAM content. This document cites and paraphrases them (CLAUDE.md §118); their layouts
enter PRIME as content from `lgu-content/`, like the LAM forms of step L5.

## 1. Scope

The roadmap's goal: the report catalogue of CLAUDE.md §57 in PDF, Excel, CSV and print, with large reports run as
background jobs and nothing unbounded loaded into memory. It adds two things the roadmap's wording leaves out:
- the **dashboard** of CLAUDE.md §55, which is still a placeholder (the roadmap's note that it "reads real data" is
  wrong and is corrected with this phase);
- the **reports the LAM requires** of the assessor (gap J5–J7): the quarterly and monthly reports on real property
  assessments to the BLGF, the half-yearly report to the local chief executive and the Sanggunian, and the inventory
  of idle lands.

Out of scope: treasury reports (collections, delinquency; CLAUDE.md §0) and the frozen billing and collection
reports, which stay as they are.

## 2. What PRIME has

| Area | PRIME today |
|---|---|
| Official registers | TMCR, pre-TMCR, Assessment Rolls (taxable, exempt), Ownership Record Form, Record of Assessment: dated runs (`RegisterRun`) printed through versioned forms, frozen when issued |
| Other printed reports | General revision status and completion reports; market data abstracts and the lowest-to-highest sales report (Annex I-R); SMV Forms 1–12; land value map; tax map sheets; the monthly roll submission to the province (LP-6) |
| Formats | HTML forms printed by the browser (print or "save as PDF"). No CSV or Excel export anywhere outside the frozen collection screens; no server-side PDF |
| Lists on screen | Properties, taxpayers, transactions, notices, market data, audit trail: paged on the server, filtered by jurisdiction |
| Dashboard | Placeholder text (`App.tsx`) |
| Statistics | None: no summaries by barangay, classification, actual use or zone |
| BLGF / LAM reports | QRRPA, MRRPA, half-yearly report, idle lands: none |
| Audit | EXPORT rows for form issues, register runs and browser prints (P12-3) |
| Background jobs | Hangfire runs general revision, SMV simulation and territorial changes; no report job |
| Document storage | Not built (CLAUDE.md §59) |

## 3. Gaps

| # | Gap | Effect |
|---|---|---|
| G1 | No summary reports (§57 Property and Assessment: by barangay, classification, actual use, zone; market and assessed value summaries) | Totals are compiled by hand |
| G2 | No CSV or Excel export | Offices cannot reuse PRIME data in spreadsheets; BLGF uploads are typed by hand |
| G3 | No dashboard | §55 unmet; no overview of pending work |
| G4 | No MRRPA or QRRPA | The LGU's monthly and quarterly BLGF reports (Book I pp.24–25) are compiled outside PRIME |
| G5 | No half-yearly report of assessments and cancellations to the LCE and Sanggunian; no inventory of idle lands | LAM duties (Book I) unsupported |
| G6 | No lists of TDs, reassessments, assessment history or parcels across properties | §57 items missing |
| G7 | No classification map | §57 GIS item missing (the value map and tax map exist) |
| G8 | Audit reports only on screen | §57 Audit items cannot be exported |

## 4. Proposal

### 4.1 Report framework

- **A report is a C# query, not a query builder.** Each report is a class in `Prime.Application/Features/Reports`
  with a code, a title, its parameters (as-of date or period, municipality, barangay, classification …), its
  permission and a query that returns rows of typed columns. Users cannot write queries (Q4).
- **Aggregation in the database.** Summaries are `GROUP BY` queries; lists are read in pages or streamed. Nothing
  loads a province's records into memory (CLAUDE.md §71).
- **"As of" means history.** A report as of a date uses the posted assessments and approved TDs in force on that
  date, with the rules then in force. Asked again for the same date, it gives the same answer (CLAUDE.md §76; Q9).
- **Jurisdiction and personal data.** Every report reads through the same jurisdiction filters as the screens. A
  report that shows an individual's TIN, contact details or address masks them for users without
  `taxpayer.view-personal` (P12-5).
- **One API.** `GET /api/reports` lists the reports a user may run. `POST /api/reports/{code}/preview` returns the
  first page of rows on screen. `GET /api/reports/{code}/export?format=csv|xlsx` downloads the whole report.
- **Formats** (Q1–Q3):
  - **Screen:** paged table.
  - **CSV:** streamed row by row (UTF-8 with BOM, so Excel opens it correctly).
  - **Excel (.xlsx):** written by a streaming library, one sheet, with a header block (LGU, office, title,
    parameters, date run, user).
  - **Print and PDF:** the report's HTML layout, printed by the browser as the forms are. Reports with an official
    layout (MRRPA, QRRPA …) print through the forms engine from a versioned layout, loaded as content.
- **Large reports** (Q3). A report whose estimated row count exceeds `Reports:SyncRowLimit` (default 20,000) runs as
  a Hangfire job. Its file is kept in a `ReportFile` table, with the user, parameters, row count and an expiry
  (`Reports:FileRetentionDays`, default 7), until document storage exists. The screen shows the job's progress and a
  download link.
- **Audit.** Every export writes an EXPORT row: the report, its parameters, the format and the row count (P12-3).
- **Permissions** (Q10). Running a report on screen needs `records.view`. Downloading CSV or Excel needs
  `records.export`. The dashboard needs `prime.use`.

### 4.2 Report catalogue

| Group | Report | New / existing | Notes |
|---|---|---|---|
| Property | Property inventory | New | One row per property: PIN, location, owners (names), parcels, area, RPUs, current TD |
| | Properties by barangay / classification / actual use / zone | New | Count, land area, market value and assessed value, taxable and exempt |
| Assessment | Tax Declaration list | New | Filter by status, period, barangay, transaction code |
| | Market value and assessed value summaries | New | By kind (land, building, machinery, other) and classification, as of a date |
| | Assessment history | New | The assessments of a unit or barangay over a period, with old and new values and the reason |
| | Reassessments | New | Reassessments in a period, with the change and its cause |
| | Assessment Rolls, Record of Assessment, TMCR, ORF | Existing (forms) | Add CSV/Excel export of a run's rows |
| | General revision status and completion | Existing (forms) | Unchanged |
| LAM / BLGF | MRRPA (monthly; Annex I-Q) | New | Per LGU and kind: RPUs and assessed value at the start of the month, assessed and cancelled during it, and at its end; taxable and exempt |
| | QRRPA (quarterly; Annex I-P) | New | Per classification: land area, RPUs, market and assessed values by kind, rates of levy and the collectible basic tax, SEF and idle-land tax (Q5) |
| | Lowest-to-highest sales (Annex I-R) | Existing (L6) | Add export |
| | Status of general revision (Annex I-S) | Existing (L6) | Unchanged |
| | Half-yearly report of assessments and cancellations to the LCE and Sanggunian | New | Content not specified by the LAM: proposal in Q11 |
| | Monthly FAAS/TD report to the province | Existing (LP-6) | Unchanged |
| | Inventory of idle lands | Deferred | Needs an idle-land designation on land, which PRIME does not record (Q7) |
| GIS | Parcel inventory | New | Parcels with area, PIN, section, barangay, whether mapped |
| | Classification map | New | A map layer coloured by the land's classification, beside the value map |
| | Tax map, value map | Existing | Unchanged |
| Audit | User activity, record changes, approval history, export history | Existing (viewer) | Add CSV/Excel export of a filtered view |

### 4.3 Dashboard (CLAUDE.md §55)

- **Tiles** (the user's jurisdiction, as of today):
  - properties, parcels and total land area;
  - total market value, and assessed value split taxable and exempt;
  - items awaiting the user's approval (the existing queue);
  - general revision progress (open programmes, items valued, posted and declared).
- **Charts:** properties and assessed value by classification and by barangay (top ten and "others").
- **Lists:** the ten most recent transactions and approvals.
- **Pending appeals** is left out while L7 is deferred.
- **Freshness.** Figures are read from the database on each visit and cached for one minute per jurisdiction (Q8).
  They are real data only; an empty database shows zeros, never sample figures.

### 4.4 Tax rates for the QRRPA

The QRRPA reports the collectible basic real property tax, SEF and idle-land tax: assessed value times the rates the
LGU's ordinances set (Book I p.24). PRIME does not bill (CLAUDE.md §0). The collectible is a report figure, not a
bill. Proposal (Q5):
- the rates are entered as effective-dated configuration per LGU: basic rate, SEF rate and idle-land rate, with the
  ordinance as legal basis, approved by a second user;
- they are DEMO until the province supplies them.

## 5. Data and migrations

- `ReportFile`: a large report's output (bytes, format, row count, parameters, user, created, expires), deleted after
  its expiry by a daily job. This is the one exception to "never delete": a report file is a derived copy, and its
  run stays in the audit log.
- `LevyRate` (Q5): LGU, kind (basic, SEF, idle land), classification (optional), rate, legal basis, effective date,
  end date, status, under the configuration approval.
- No change to assessments or registers.
- Settings: `Reports:SyncRowLimit`, `Reports:FileRetentionDays`.

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| R1 | Framework: report registry, preview, CSV and Excel export, EXPORT audit, permissions; first reports: property inventory and the four "properties by …" summaries | M |
| R2 | Dashboard (§4.3) | M |
| R3 | Assessment reports: TD list, market and assessed value summaries, assessment history, reassessments; CSV/Excel export of register runs and the sales report | M |
| R4 | LAM/BLGF: levy rates (configuration), MRRPA, QRRPA, half-yearly report; provisional print layouts, LAM layouts as content | M |
| R5 | Large reports as background jobs (`ReportFile`, progress, download, expiry) | S |
| R6 | GIS: parcel inventory, classification map; audit exports | S |

Each step is built, tested (including against a seeded set of DEMO records large enough to exercise paging and
streaming) and checked in a browser before the next.

## 7. Exit criteria (from the roadmap, made concrete)

1. Each report in §4.2 marked New runs on DEMO data on screen and exports to CSV and Excel; those with a layout also
   print.
2. A report as of a past date gives the values then in force, unchanged by later assessments.
3. A municipal user's report covers their jurisdiction only; personal data is masked without the permission.
4. A province-wide export of a large DEMO set streams or runs as a job without loading all rows into memory; its
   download is audited as EXPORT.
5. The dashboard shows real figures for the user's jurisdiction and zeros on an empty database.
6. The MRRPA balances: the start of the month plus the month's assessments, less its cancellations, equals the end
   of the month, for each kind, taxable and exempt.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Excel library | MiniExcel (Apache-2.0): streams rows, so large exports use little memory. Excel files are data exports; official layouts print through the forms. Alternative: ClosedXML (MIT), richer formatting but in memory |
| Q2 | PDF | The browser's "save as PDF" from the printed HTML, as for the forms now. No server-side PDF library for now; revisit if unattended PDF generation is needed |
| Q3 | Large reports | Stream up to `Reports:SyncRowLimit` (20,000 rows); beyond, a background job with the file kept 7 days in `ReportFile` |
| Q4 | Report definitions | In code, one class per report, with print layouts as versioned content; no user-defined queries |
| Q5 | Tax rates for the QRRPA collectibles | Effective-dated levy rates per LGU, entered with their ordinance and approved by a second user; DEMO until supplied. The collectible is shown as a report figure only. Alternative: leave the collectible columns blank for the treasurer to fill |
| Q6 | Electronic submission of the QRRPA (LIFT) and MRRPA (BLGF system) | Their upload formats are not in the LAM: DOMAIN VERIFICATION REQUIRED. Meanwhile, Excel in the annex's column order, for the office to upload or transcribe |
| Q7 | Inventory of idle lands | Deferred: it needs an idle-land designation on land (LGC §§236–239) and confirmation that the province levies the idle-land tax. Add it as its own step when confirmed |
| Q8 | Dashboard freshness | Live queries with a one-minute cache per jurisdiction; no nightly snapshot |
| Q9 | Basis of "as of" | Posted assessments and approved TDs in force on the date (effective-dated), as the registers already use |
| Q10 | Permissions | Reuse `records.view` (run on screen) and `records.export` (download CSV/Excel); no new permission |
| Q11 | Half-yearly report to the LCE and Sanggunian | The LAM names it without a layout: DOMAIN VERIFICATION REQUIRED. Proposal: assessments made and cancelled in the half-year, by kind and classification, counts and assessed values, taxable and exempt (the MRRPA's columns for six months) |
| Q12 | Order of the steps | R1 → R2 → R3 → R4 → R5 → R6. R4 (BLGF reports) could come right after R1 if the province needs them first |

### 8.1 Decisions

2026-10-08: Phase 11 deferred by the user before any question was decided. The design is kept; Q1–Q12 are
answered when the phase is resumed.

2026-10-10: the user accepted every recommendation of Q1–Q12:

- Q1: MiniExcel for Excel exports; official layouts print through the forms.
- Q2: PDF by the browser's "save as PDF" from the printed HTML; no server-side PDF library.
- Q3: stream up to `Reports:SyncRowLimit` (20,000 rows); larger reports run as a background job, the file kept 7 days in `ReportFile`.
- Q4: report definitions in code, one class per report; print layouts as versioned content; no user-defined queries.
- Q5: effective-dated levy rates per LGU with their ordinance, under maker-checker, DEMO until supplied; the collectible is a report figure only.
- Q6: QRRPA (LIFT) and MRRPA (BLGF) upload formats DOMAIN VERIFICATION REQUIRED; meanwhile Excel in the annex's column order.
- Q7: inventory of idle lands deferred until the idle-land designation and the province's levy are confirmed.
- Q8: dashboard from live queries with a one-minute cache per jurisdiction.
- Q9: "as of" = posted assessments and approved TDs in force on the date.
- Q10: reuse `records.view` and `records.export`; no new permission.
- Q11: half-yearly report as proposed (assessments made and cancelled in the half-year, by kind and classification, counts
  and assessed values, taxable and exempt), DOMAIN VERIFICATION REQUIRED until the province confirms its content.
- Q12: steps in the order R1 → R2 → R3 → R4 → R5 → R6.

2026-10-10, also: a UI theme step (PRIME logo, sidebar grouped from 25 items into 7, colours and type) follows R2, so
the theme is applied together with the new dashboard. It gets its own design document (CLAUDE.md §108) before any code.

Phase 11 starts during the user's local testing, once H4 is finished; Phase 14 H6–H8 are deferred to before
deployment (production-hardening.md §8.1).

## 9. Implementation log

### R1 — framework and property reports (2026-10-10)

Built:
- **Report registry** (`Prime.Application/Features/Reports`): `IReport` (code, title, group, parameters, typed columns,
  `RunAsync` over a window of rows), `IReportService` (catalogue, preview, export), `ReportsOptions`
  (`Reports:SyncRowLimit` 20,000, `Reports:FileRetentionDays` 7 for R5).
- **API** (`ReportsController`): `GET /api/reports` and `POST /api/reports/{code}/preview` need `records.view`;
  `GET /api/reports/{code}/export?format=csv|xlsx` needs `records.export` and uses the strict rate limit (Q10).
- **FAAS in force on a date in SQL** (`IFaasInForceQuery`, Infrastructure `FaasInForceQuery`): the registers' rule
  (approved and effective by the date, not cancelled by then, the latest per unit; the declared assessment, else the
  unit's posted one in force; taxable/exempt split by assessment lines), written once for the database. It applies the
  jurisdiction itself, since raw SQL bypasses EF's query filters. A test compares it with the Assessment Rolls, taxable
  and exempt, on two dates around a cancellation.
- **Reports**: property inventory (PIN order, owners on record on the date, parcels, units and land TDs in force, land
  area, values, totals) and properties by barangay, classification, actual use and zone (properties, units, land area,
  market and assessed values taxable and exempt, totals counting each property once).
- **Files** (Infrastructure `Reporting/ReportFileWriters.cs`): CSV in UTF-8 with a BOM, titles and rows only, money to
  the centavo; Excel with MiniExcel 1.46.0 (Apache-2.0): header block (LGU, office, title, parameters, run date and
  user), typed numbers with number formats and column widths, totals and notes. Text cells that look like formulas are
  prefixed with an apostrophe (CSV injection).
- **Audit**: each download writes an EXPORT row to table `Reports`: the report, its parameters, the row count and the
  format.
- **Screen** `/reports` (menu "Reports", `records.view`): choose a report, set the date, municipality and barangay, page
  through it with its totals and notes, download CSV or Excel when the user holds `records.export`. A report over the
  download limit says so and asks for a narrower scope (the background run comes in R5).

Measured on the DEMO volume database (`prime_volume`: 250,000 properties, 400,000 units), provincial user, through
the API on the development laptop:

| Read | Province-wide | Largest municipality |
|---|---|---|
| A "properties by …" summary | 7–9 s | about 1.8 s |
| Property inventory, one page with totals | 7.3 s | 2.3 s |
| Download: barangay summary (Excel, 509 rows) | 10 s | — |
| Download: inventory of the municipality (CSV, about 19,500 rows) | — | 6 s |
| Download: inventory of one barangay (CSV or Excel, 642 rows) | — | under 1 s |

Found and fixed on the way:
- The first version wrote the in-force rule in LINQ. EF made it a correlated sub-query per TD: 104 s for one
  province-wide summary, and the others hit the 30 s command timeout. The SQL version reads each table once, joined by
  hash, and a summary is one statement (`GROUPING SETS` give the groups and the total with each property counted once).
- The scope (jurisdiction, municipality, barangay, a page's properties) is applied inside every sub-query, so a
  municipality or a page of the inventory reads only its own rows.
- PostgreSQL overestimated the joined rows (800 million for 400,000) and JIT-compiled the query: 160 s instead of 8 s.
  Report reads now run in a transaction of their own with `SET LOCAL jit = off` and `SET LOCAL work_mem = '64MB'`
  (4.4 s in psql for the same query).

Verified: 850 tests pass (12 new in `ReportsTests`); the screen checked in a browser as a provincial assessor and a
view-only user (no download buttons), axe without violations, no sideways page scroll at phone width (the report table
scrolls in its own focusable area), CSV and Excel downloaded with the server's file names (CORS now exposes
`Content-Disposition`).

Open after R1:
- Province-wide figures take several seconds on the laptop. The dashboard (R2) will cache them for a minute per
  jurisdiction (Q8). The hosted figures are measured at H6.
- On the volume database the values are zero: its general revision assessments were valued but never posted. Values
  are checked by the integration tests on posted DEMO assessments.
- No print layout in R1: these reports are data lists, downloaded or read on screen. Printed layouts come with R4.

### R2 — dashboard (2026-10-10)

Built:
- **API** `GET /api/dashboard` (`prime.use`, Q10), `DashboardService` (`Prime.Application/Features/Dashboard`): the user's
  jurisdiction as of today in the LGU's time zone.
  - Tiles: active properties and their parcels; properties and units with a FAAS in force; land area; market value and
    assessed value, taxable and exempt. The "awaiting my approval" tile reads the existing approvals queue, which depends
    on the user, not only the jurisdiction.
  - Charts: assessed value by classification and by barangay, largest first, the ten largest and an "Others" row.
  - General revision: each planned or in-progress programme covering a municipality of the jurisdiction, with its items
    there valued, posted and declared (an approved TD declares the revision assessment), failed and excluded.
  - Lists: the ten latest transactions, and the ten latest approvals of TDs, assessments and transactions with the
    approver's name. No owner names or other personal data.
- **One pass for the figures.** `IFaasInForceQuery.SummaryAsync` now takes several groupings: the totals, the classes and
  the barangays come from one statement (`GROUPING SETS ((barangay), (classification), ())`), each row saying which grouping
  it belongs to. The single-grouping form used by the R1 reports is unchanged.
- **Cache** (Q8): the totals, charts and revision progress are kept for one minute in memory, per jurisdiction and date.
  The lists are read on each request. Several API instances would each keep their own cache; that is acceptable for a
  one-minute cache.
- **Screen** `/` replaces the placeholder: the tiles; the two charts as horizontal bars (one series, so no legend; value at
  the bar's tip; hover or keyboard focus shows properties, market and assessed value; a Chart/Table switch for the same
  rows as a table); "Others" is a text row without a bar, so the named groups keep a readable scale; progress bars per
  revision; the two lists, with the PIN linking to the property for users with `property.view`. Pending appeals are left
  out while L7 is deferred (§4.3).

Verified: 855 tests pass (5 new in `DashboardTests`: figures for one municipality, zeros for an empty jurisdiction, the
one-minute cache, the ten-plus-Others chart rule, the API for a view-only user); production build and lint clean; in a
browser as the provincial checker, a municipal assessor (their municipality only) and a view-only user; axe on the
dashboard without serious or critical findings (the e2e accessibility check now waits for the charts); no sideways scroll
at phone width.

Open after R2:
- Not measured on the volume database. Its first province-wide read is expected to take about as long as one R1 summary
  (7–9 s on the laptop), then a minute from cache. Hosted figures are measured at H6.
- The local development database holds many DEMO classes and barangays left by the integration tests, so its charts
  show mostly "DEMO_Residential" and "Demo Barangay" rows.


### R3 — assessment reports and run downloads (2026-10-10)

Built:
- **Parameters.** A report may now take a period (from and to; without dates, the year to date, and the period's end is
  the report's date), a Tax Declaration status, a transaction code and a PIN or its first part. The screen shows only the
  ones a report lists.
- **Tax Declaration list** (`TD_LIST`): the TDs recorded in the period (on the day approved; not yet approved, on the
  day drafted), by municipality, barangay, status, transaction code or PIN, in TD number order. Each row has the unit's
  kind, owners on record that day, class and use codes, taxability, effectivity, status, the values, and what the TD
  cancels and what cancelled it. The values are those of the declared assessment, else the unit's posted assessment in
  force on the TD's effectivity (the Record of Assessment's rule). The totals give the count only; the value summary
  gives sums.
- **Market and assessed value summary** (`VALUE_SUMMARY`): the FAAS in force on the date per kind of unit and
  classification (the TD's), with a subtotal per kind and the total, taxable and exempt. One statement:
  `IFaasInForceQuery.KindSummaryAsync` adds `GROUPING SETS ((kind, classification), (kind), ())` to the R1 query, which
  now also returns each unit's kind.
- **Assessment history** (`ASSESSMENT_HISTORY`) and **reassessments** (`REASSESSMENTS`): the posted assessments made
  in the period, by municipality, barangay or PIN, each beside the one it follows (`PreviousAssessmentId`), with the
  change in assessed value and the reason (the transaction type, or "General revision", and the remarks). An
  assessment is made on its `MadeOn` date; one made before that date was recorded counts from its approval, else its
  posting, else its entry. Reassessments are those made under a transaction type of the reassessment kind, with the
  cause date and whether they were made after the type's window.
- **Run downloads** (`IRunExportService`; `GET /api/reports/register-runs/{id}/export` and
  `/api/reports/sales-report-runs/{id}/export`, `records.export`, strict rate limit): the rows of a TMCR, pre-TMCR,
  Assessment Roll, ORF or ROA run, and the groups of a lowest-to-highest sales report, as CSV or Excel. A run already
  issued is written from its frozen snapshot, so the file holds what was printed. A run not yet issued is read now by
  the form's own data provider, and the header says so. The columns are the snapshot's fields; the official layouts stay
  with the forms. Owner and administrator addresses are hidden from users without `taxpayer.view-personal`, because a
  file leaves the system (P12-5). The abstracts (Annexes I-M to I-O) stay print-only. Each download writes an EXPORT row
  with the run's id.
- **Screens:** the new parameters on `/reports`; CSV and Excel buttons on each register run and on sales report runs,
  for users with `records.export`.

Verified: 862 tests pass (7 new in `ReportsTests`: value summary with subtotals, TD list with its filters and period
check, the posted-value fallback, history and reassessments, a register run downloaded unissued and issued (snapshot
kept after a later edit), refusals; the API test covers the new endpoints' permissions). Production build and lint
clean. In a browser as the provincial checker: each new report runs and pages, a filtered TD list downloads as Excel,
a register run downloads as CSV; no page errors; axe without serious or critical findings on the TD list and the empty
reassessments result; no sideways scroll at phone width; a view-only user sees no download buttons.

Measured on `prime_volume` (401,197 TDs; 1,150 posted assessments), provincial user, Debug API on the laptop, warm:

| Read | Province-wide | Titay (largest municipality) |
|---|---|---|
| Value summary, preview | 3.1 s (7.2 s cold) | 0.9–1.2 s |
| TD list, first page | 0.7 s | 2.4 s |
| Assessment history, first page | 0.9 s | 0.6 s |
| Download: value summary (Excel) | 3.0 s | — |
| Download: TD list of one barangay (Excel, 137 KB) | — | 1.0 s |

Open after R3:
- The TD list of one municipality is slower than the province's: PostgreSQL underestimates the date filter (840 rows
  estimated for 400,000) and probes `Property` once per TD. A sub-query form saves about a third; left for H6, where
  hosted figures are measured.
- A municipality's whole TD list (30,816 rows on the volume set) is over the download limit until R5's background run.
- The volume set has no reassessments; the report is checked by the integration test.

### R4a — levy rates (2026-10-11)

Built (Q5, Q18):
- **`LevyRate`** (configuration, effective-dated, maker-checker; migration `LevyRates`): the levy (basic tax, SEF,
  idle-land tax), the municipality or the whole province, the classification or every class, the rate as a percent of
  assessed value, the ordinance as legal basis. The keys form its `Code`, so a new approved rate for the same keys
  closes the old one the day before it takes effect. Approved rows are refused DELETE and TRUNCATE, like the other
  configuration (CLAUDE.md §49); row version and RLS as the other tables.
- **`ILevyRateService`**: create, approve (not by the maker), list, and `InForceAsync(date)`, which returns a
  `LevyRateTable` whose `Find` gives the most specific rate: the municipality's for the class, its rate for every class,
  then the province's likewise. A municipal office sets only its municipalities' rates; a province-wide rate needs the
  whole province in jurisdiction.
- **API** `/api/levy-rates` (list `prime.use`; create `config.edit`; approve `config.approve`).
- **Screen**: a "Levy rates" tab on Valuation rules. No rate is built in; the repository ships none.

Verified: 866 tests pass (4 new in `LevyRateTests`: second-user approval before a rate is in force, a new rate taking
over with the old kept, the precedence order, jurisdiction and validation refusals; the history guard test lists the
table). Production build and lint clean. Migration applied to the local database and Supabase (87/87). In a browser: the
maker creates a DEMO rate (effective 2099), the checker approves it, a view-only user sees the tab without "New rate";
no page errors, axe without findings.

## 10. Step R4 in detail (2026-10-10; Q13–Q21 accepted)

R4's line in §6 (levy rates, MRRPA, QRRPA, half-yearly report) was approved with Q5, Q6 and Q11. Reading the annexes
for the build shows choices that line does not settle. They are set out here before any code (CLAUDE.md §108).

### 10.1 What the LAM asks (paraphrased; Book I pp.24–25, Annexes I-P and I-Q)

- **MRRPA** (monthly, uploaded to the BLGF's reporting system): per LGU and kind of property, the number of RPUs and
  their assessed value, taxable and exempt, in four blocks: in force at the end of the previous month, assessed during
  the month, cancelled during the month, and in force at the end of the month. Prepared and certified by the assessor.
- **QRRPA** (quarterly; the first three by the 15th of the month after the quarter, the year-end one by 28 February;
  uploaded to LIFT): per classification row, the land area, the RPUs by kind, the market value by kind (residential
  buildings split at a value threshold), the assessed value by kind, the rates of levy (basic, SEF, idle land) and
  the collectibles. The rows come in groups: taxable properties by classification (the special classes by sub-kind),
  exempt properties by kind of exemption, properties with restrictions (under agrarian reform, under litigation,
  others) by classification, and idle lands. The header gives the LGU, the period and the number of barangays in the
  report; a footer records the type of the last revision.
- **Half-yearly report** to the local chief executive and the Sanggunian: named, no layout (Q11).

The layouts themselves are LAM content: they come in from `lgu-content/` as form versions (CLAUDE.md §118), and PRIME
ships provisional layouts of its own.

### 10.2 What PRIME has for it

| Need | PRIME |
|---|---|
| FAAS in force on a date, by kind, class, taxable/exempt | `IFaasInForceQuery` (R1, R3) |
| Kind of exemption of an exempt line | `AssessmentLine.PropertyExemptionId` → `ExemptionType` (configuration) |
| Restrictions (agrarian reform, litigation) | Not recorded as such; TD annotations have configurable types (lis pendens, court orders …) |
| Idle land | Not recorded (Q7, deferred) |
| Levy rates | None in scope. The frozen billing code has its own rates; R4 does not build on it (CLAUDE.md §0) |
| A threshold for residential buildings' market value | None |

### 10.3 Review questions

| # | Question | Recommendation |
|---|---|---|
| Q13 | What "assessed" and "cancelled during the month" mean in the MRRPA | By the FAAS in force at the two month-ends, as the rolls read them: a unit's FAAS in force at the end and not at the start is "assessed"; one in force at the start and not at the end (or replaced) is "cancelled". The report then balances by construction (exit criterion 6). A TD approved in the month but effective later appears in the month it takes effect; a note counts those. Alternative: count TD approvals and cancellations in the month, which does not balance when effectivity is in the future |
| Q14 | Rows of the MRRPA for a province | One block per municipality (the "LGU" column) with a row per kind (land, building, machinery, other improvements) and a provincial total; a municipal user gets their municipality only |
| Q15 | How the QRRPA's rows are found | A row mapping kept as configuration, loaded with the LAM layout from `lgu-content/` and approved by a second user: classification → taxable row; exemption type → exempt row; annotation type → restriction group. A unit not mapped lands in an "others" row of its group, and a note counts them. PRIME ships a DEMO mapping only |
| Q16 | The residential-building value threshold of the QRRPA's market value columns | A dated system parameter with its legal basis, entered by the office, approved by a second user. Until it is set, the two columns are shown as one with a note. The figure on the annex is not written into code (CLAUDE.md §5, §7). DOMAIN VERIFICATION REQUIRED: its legal basis |
| Q17 | Idle-land rows and the idle-land collectible | Rows and collectible left empty, with a note, until the idle-land designation is built (Q7). The idle-land rate can still be entered |
| Q18 | Levy rates and collectibles | Rates per municipality (or the whole province), kind (basic, SEF, idle land), effective-dated, with the ordinance as legal basis, under maker-checker (Q5). The QRRPA uses the rates in force on the quarter's last day. Collectible = taxable assessed value × rate, per row, rounded to the centavo; exempt rows have none. Shown as report figures only (CLAUDE.md §0) |
| Q19 | The half-yearly report | The MRRPA's four blocks over the half-year (January–June, July–December), by kind and classification, from the same code, DOMAIN VERIFICATION REQUIRED until the province confirms it (Q11) |
| Q20 | The year-end QRRPA | The fourth quarter's report, as of 31 December; no separate report |
| Q21 | Delivery | R4a: levy rates (migration, admin screen, maker-checker). R4b: MRRPA and the half-yearly report (screen, CSV/Excel, provisional print layout). R4c: QRRPA with the row mapping and the threshold parameter (migration). The LAM layouts are loaded as content when the office loads them |

### 10.4 Decisions

2026-10-10: the user accepted every recommendation of Q13–Q21. R4 is built as R4a (levy rates), R4b (MRRPA and the
half-yearly report), R4c (QRRPA).
