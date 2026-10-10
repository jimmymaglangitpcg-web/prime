# Phase 11 — Reporting (design)

| | |
|---|---|
| Phase | 11 (CLAUDE.md §57, §98; roadmap "Phase 11 — Reporting") |
| Status | Decisions recorded 2026-10-10 (all recommendations accepted); implementation starts during local testing, before Phase 14 H6–H8 |
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

(Starts when the decisions are recorded.)
