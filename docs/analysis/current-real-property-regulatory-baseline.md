# Current Real Property Regulatory Baseline (RPVARA era) — Reference Analysis for PRIME

Status: **analysis only**. Prepared 2026-09-24. No PRIME code, schema,
migration, form, API, valuation logic or workflow was changed.
Companion to [blgf-appraisal-assessment-analysis.md](blgf-appraisal-assessment-analysis.md)
(the 2004/2006 BLGF manual).

---

## 0. Method, access notes and classification legend

### 0.1 Method

Only official government sources were used: the Supreme Court E-Library,
the Official Gazette, the Department of Finance (DOF), the Bureau of Local
Government Finance (BLGF), and one LGU `.gov.ph` site hosting a copy of a
BLGF circular. Documents were downloaded and read in full where
retrievable; search-engine summaries were used **only** to locate
documents, never as evidence. Third-party sites (LawPhil, Scribd, law-firm
and commentary sites) were not relied on.

### 0.2 Access notes (important for how complete this baseline is)

| Issue | Effect |
|---|---|
| `blgf.gov.ph` sits behind an Azure WAF JavaScript challenge; several of its pages (Local Finance Circulars, Department Orders, news items) returned a "website is currently under maintenance" page on 2026-09-24. | PDFs were retrieved through a real (headless Chromium) browser session. Listing pages under maintenance could not be read. |
| `legacy.senate.gov.ph` was "temporarily unavailable". | RA 12001 was read from the Supreme Court E-Library instead (its TLS chain is incomplete; the page was fetched without certificate verification — read-only public text). |
| **The Local Assessment Manual (LAM), DOF Department Circular No. 004-2025, could not be found online** (not on BLGF's Manuals page, not in DOF's public Department Circular listing, not found by search). | **Every question whose answer lives in the LAM (forms, PIN/ARP numbering, transaction codes, approval chain, rounding) remains DOMAIN VERIFICATION REQUIRED.** This is the single largest gap in this baseline. |
| DOF Order No. 054-2024 (BLGF Overarching ICT Policy) could not be found online. | **Resolved 2026-09-24:** the user supplied a scanned copy (`docs/References/DOF Order No. 054-2024.pdf`); read page by page as images (no text layer). See S7 and §4.1. |

### 0.3 Classification legend

| Tag | Meaning |
|---|---|
| **CURRENT REQUIREMENT** | Stated in a current, in-force authoritative source (statute, IRR, DOF order/circular, BLGF circular). |
| **STILL APPLICABLE FROM OLDER MANUAL** | Stated in the old manual **and** independently confirmed by a current source (usually an unamended LGC provision). The current source is what controls. |
| **MODIFIED BY RA 12001 / CURRENT RULES** | Old rule changed by RA 12001, its IRR, or a current issuance. |
| **SUPERSEDED** | The old rule or document has been replaced. |
| **LGU-SPECIFIC** | Set by the LGU (ordinance or local practice) within statutory limits. |
| **DOMAIN VERIFICATION REQUIRED** | A current authoritative source probably governs it (most often the LAM) but could not be read, or sources conflict. |
| **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** | No retrieved authoritative source establishes it. |

---

## 1. Source matrix

| ID | Title | Issuer | Date | Version / status | Official location | Retrieved? | Sections used |
|---|---|---|---|---|---|---|---|
| S1 | Republic Act No. 12001 — "Real Property Valuation and Assessment Reform Act" (RPVARA) | Congress (consolidation of SB 2386 / HB 6558) | Approved 13 Jun 2024; **effective 5 Jul 2024** (per S3) | In force | Supreme Court E-Library, `elibrary.judiciary.gov.ph/thebookshelf/showdocs/2/97502`; Senate PDF `legacy.senate.gov.ph/republic_acts/ra%2012001.pdf` (unavailable at retrieval) | Yes (full text, E-Library) | §§1–39 (all) |
| S2 | Implementing Rules and Regulations of RA 12001 | DOF (signed by the Secretary of Finance 10 Dec 2024); circulated by **BLGF Memorandum Circular No. 001-2025** (6 Jan 2025) | Published 27 Dec 2024; **effective 11 Jan 2025** | In force | `blgf.gov.ph/wp-content/uploads/2025/03/BLGF-MC-No.-001.2025-IRR-of-RA-No.-12001-or-the-RPVARA-Reform-Act-6-Jan-2025-Approved-3.pdf` | Yes (29 pp.) | IRR §§1–66 (all) |
| S3 | BLGF Memorandum Circular No. 001-2026 — "Implementation of Republic Act No. 12001" | BLGF (Exec. Dir. C. Q. Agcaoili) | 6 Jan 2026 | In force (status report + direction) | `blgf.gov.ph/wp-content/uploads/2026/07/BLGF-MC-No.-001.2026-Implementation-of-Republic-Act-No.-12001-6-Jan-2026-Approved.pdf` | Yes (4 pp.) | §§I–XII |
| S4 | BLGF Memorandum Circular No. 003-2025 — Tax Amnesty on RPT under RA 12001 | BLGF | 6 Jan 2025 | In force; amnesty window **ended 5 Jul 2026** | LGU-hosted copy: `geronatarlac.gov.ph/wp-content/uploads/2025/01/BLGF-MC003.2025-Tax-Amnesty-on-RPVARA-Law.pdf` | Yes (1 p.) | Whole |
| S5 | Philippine Valuation Standards (PVS) 3rd Edition, incorporating IVS 2025 | DOF — **Department Order No. 022.2025** (15 Oct 2025 per S3); published by BLGF | 2025 | In force; **supersedes DO 37-2018 (PVS 2nd Ed.)** | `blgf.gov.ph/wp-content/uploads/2026/06/PHILIPPINE-VALUATION-STANDARDS-3rd-Edition-ONLINE-EDITION-COMPLIMENTARY-COPY.pdf` (250 pp.) | Yes | DO 022.2025 (pp. i–iii); Part II: PVS 100–106, 300, 400, 410 (PDF pp. 174–192); Part III GN 100–900 (index) |
| S6 | **Local Assessment Manual (LAM)** | DOF — **Department Circular No. 004-2025** | 5 Nov 2025 (per S3) | In force per S3; "Formerly Manual on Real Property Appraisal and Assessment Operations (MRPAAO)" | **Not found online** | **No** | — (existence and status only, via S3 and S5 PVS 106 §30.1) |
| S7 | "Instituting the ICT Standard Policy and Guidelines on the Adoption of Electronic Transactions for LGU Treasury and Assessment Operations, and BLGF Operations and Services" | DOF — Department Order No. 054.2024, signed by the Secretary of Finance; transmitted by BLGF MC No. 021.2024 (10 Oct 2024) and BLGF Regional Office III Regional MC No. 2024-012 (16 Dec 2024) | Signed 10 Jun 2024; effective "immediately upon filing and submission to" ONAR (§13) — **filing date not shown on the copy** | In force per S3 and the two transmittals ("for strict compliance"); S3's "054.2025" is a mis-citation — the order is 054.2024 | User-supplied scan: `docs/References/DOF Order No. 054-2024.pdf` (11 pp.: RO III transmittal, BLGF MC 021.2024, DO pp. 1–9). Not retrieved from an official site; content is an official issuance. | **Yes, in full** | §4.1 below |
| S8 | Republic Act No. 7160 — Local Government Code of 1991, Book II Title II (Real Property Taxation) | Congress | 10 Oct 1991 | In force **as amended by S1 §38**; applies suppletorily (S1 §35; S2 §62) | Official Gazette, `officialgazette.gov.ph/1991/10/10/republic-act-no-7160/` (original, **unconsolidated** text) | Yes | §§135, 199–231, 472 |
| S9 | Local Treasury Operations Manual (LTOM) 2nd Edition, Book IV | BLGF | 2019 (2nd printing 2020) | Current BLGF manual (listed on BLGF Manuals page) | `blgf.gov.ph/wp-content/uploads/2022/10/LTOM-Book-4-E-Copy.pdf` | Yes (138 pp.) | §§146–151, Forms 16–34 |
| S10 | Manual on Real Property Appraisal and Assessment Operations (MRPAAO) | DOF-BLGF, issued via Local Assessment Regulations No. 1-04 | 1 Oct 2004 / Jan 2006 | **Superseded by the LAM** per S3 ("Formerly MRPAAO"); still listed on BLGF's Manuals page as of 2026-09-24 | `blgf.gov.ph/wp-content/uploads/2015/08/ManualRPAandAO.pdf` (identical to `docs/References/ManualRPAandAO.pdf`) | Yes (prior analysis) | See companion analysis |
| S11 | BLGF "Manuals and Policies" page | BLGF | Retrieved 2026-09-24 | — | `blgf.gov.ph/manuals/` | Yes | Lists PVS 3rd Ed., MRPAAO, Mass Appraisal Guidebook, PVS 1st/2nd Ed., LTOM 2nd Ed., draft Idle Land policy. **No LAM listed.** |
| S12 | BLGF news: "Exposure Workshop on the Draft MRPAAO 2nd Edition" | BLGF | 2023 (per search listing) | Draft (context only) | `blgf.gov.ph/blgf-conducts-exposure-workshop-on-the-draft-manual-...-2nd-edition/` | Page under maintenance | — |
| S13 | DOF Department Order No. 074-2024 — income classification under RA 11964 | DOF | 2024 | In force | `blgf.gov.ph/wp-content/uploads/2025/06/DOF-Order-No.-074.2024.pdf` | Yes (p.1) | Tangential: LGU income class drives RPTAF subsidy / DICT priority (S1 §§22, 32) |

**Referenced but not obtained:** Local Assessment Regulations (LAR)
No. 1-92 (cited by PVS 103, S5 PDF pp.184–185); Mass Appraisal Guidebook
(new edition "submitted to DOF for promulgation", S3); Guidebook on Real
Property Tax Compliance and Impact Study and Assessment Examination and
Evaluation Manual (both "under review of the final draft", S3); any
current official FAAS/TD form templates.

---

## 2. Status of the old BLGF manual (Question 2)

| Finding | Classification | Source |
|---|---|---|
| The MRPAAO has been replaced as the operational assessment manual by the **Local Assessment Manual (LAM)**, promulgated by **DOF Department Circular No. 004-2025 (5 Nov 2025)**: "Local Assessment Manual (LAM)¹ … ¹Formerly Manual on Real Property Appraisal and Assessment Operations (MRPAAO)". | **SUPERSEDED** (old manual) / **CURRENT REQUIREMENT** (LAM) | S3 §II |
| Local assessors "shall maintain valuation records and forms prescribed under the Local Assessment Manual (LAM)". | **CURRENT REQUIREMENT** | S5 PVS 106 §30.1 (PDF p.186) |
| BLGF's Manuals page still hosts the MRPAAO and does not list the LAM. | Context only — does not change the supersession stated in S3 | S11 |
| The PVS 3rd Edition "shall complement all issuances of the DOF pertaining to rules and regulations for the classification, appraisal, and assessment of real property". | **CURRENT REQUIREMENT** | S5 DO 022.2025 ¶4 |
| Provisions of the old manual that merely restate **unamended LGC sections** remain in force *because of the LGC*, not because of the manual. | **STILL APPLICABLE FROM OLDER MANUAL** (for those restatements) | S1 §35; S2 §62; S8 |
| Provisions that exist **only** in the old manual (PIN/ARP formats, transaction codes and ranks, FAAS/TD layouts, three-signatory chain, "nearest tens" rounding, filing arrangements) cannot be assumed current. | **DOMAIN VERIFICATION REQUIRED** (against the LAM) | S3, S6 |

---

## 3. What RA 12001 and current rules changed (Question 1)

### 3.1 Comparison: OLD BLGF MANUAL vs CURRENT RPVARA / IRR / LAM / PVS / BLGF GUIDANCE

| Topic | Old manual (2004/2006, under LGC as then in force) | Current rule | Classification |
|---|---|---|---|
| Valuation standard | Three approaches; SFMV-based appraisal | All real property, taxable or exempt, valued on prevailing market values **in conformity with the PVS**; depreciation "in all instances" for depreciable assets, per the PVS (S1 §14; S2 §§21–22). PVS 3rd Ed. = IVS 2025 + Philippine Part II + GN 100–900 (S5). | **MODIFIED BY RA 12001 / CURRENT RULES** |
| Name and scope of the schedule | "Schedule of Fair Market Values" (SFMV) incl. SBUCC | "Schedule of Market Values (SMV)": "table of base unit market value for all kinds of real property, **except machinery**"; property not in the SMV appraised at current market value and assessed at the level for its actual use (S1 §4(p); S2 §4(r)) | **MODIFIED BY RA 12001** |
| Who approves the schedule | Enacted by **Sanggunian ordinance**; published (LGC §212, old manual Ch. III §5) | LGC §212 **repealed** (S1 §38(a)). Prepared by assessors per PVS within 12 months of BLGF notice; 2 public consultations within 60 days before submission; 2-week online publication before consultation; BLGF Regional Office review (45 days; NCR cities and lone municipality → Central Office), BLGF head (30 days), **Secretary of Finance certification** (30 days; inaction → existing SMV stays); remand/resubmission cycle; **effective 15 days after DOF publication** (S1 §§15–16; S2 §§23–34) | **MODIFIED BY RA 12001** |
| Sanggunian role after the SMV | Enacts SFMV, assessment levels | Receives the certified SMV and a **Revenue and Tax Impact Report** with **three options** for adjusting assessment levels and tax rates (within 30 days); enacts an ordinance for the general revision and may adjust levels/rates and cap/phase increases (S1 §17; S2 §§35–38, 41–42; S3 §IV) | **MODIFIED BY RA 12001**; levels/rates **LGU-SPECIFIC** |
| Amendment of the schedule | Sanggunian acts within 90 days on assessor's recommendation (LGC §214) | Revision during effectivity for significant market change (ROW, calamity, pandemic, analogous) or correction of errors, recommended to BLGF and reviewed per §§14–15 (S1 §19; S2 §43). *(Relationship to unrepealed LGC §214: DOMAIN VERIFICATION REQUIRED.)* | **MODIFIED BY RA 12001** |
| General revision periodicity | Every 3 years (LGC §219); 3-year calendar (old manual p.78–79) | LGC §219 **repealed**. First SMV update within 2 years of effectivity, i.e. **not later than 5 Jul 2026**; thereafter SMV update and general revision **every 3 years** on a BLGF-prepared schedule; suspension on national emergency / local calamity (S1 §19; S2 §§41–44). Once the certified SMV takes effect, LGUs conduct the general revision, **issue new TDs, and notify owners** (S3 §IV.1). | **MODIFIED BY RA 12001** |
| Tax increase from the new SMV | — | **First year: RPT increase capped at 6%** of RPT assessed before, per tax type (basic, SEF, idle land, special levies); LGU may cap later years by ordinance (S1 §29; S2 §55; S3 §IV) | **CURRENT REQUIREMENT**; later caps **LGU-SPECIFIC** |
| Assessment levels | Fixed by ordinance within LGC §218 maxima | LGC §218 "amended insofar as inconsistent"; IRR §54 requires §218 maxima to be observed; LGUs keep the power to set/adjust levels and rates (S2 §38) | **CURRENT REQUIREMENT** (§218 maxima) + **LGU-SPECIFIC** (actual levels) |
| Definitions | LGC §199 incl. (e) Appraisal, (g) Assessment Level, (o) Machinery | §199(e), (g), (o) **repealed**; replaced by RA 12001 §4 "Valuation/Appraisal", "Assessment level", "Machinery" (S1 §§4, 38) | **MODIFIED BY RA 12001** |
| Assessor qualification and appointment | LGC §472(a) | Assessor must be a **duly licensed appraiser** (S1 §4(e)); appointment from lists of three ranking eligibles (S1 §25; S2 §49); PRC licence under RA 9646 (S5 PVS 100 §20) | **MODIFIED BY RA 12001** |
| Organisation | Assessor's office per LGC | **Real Property Valuation Unit (RPVU)** mandatory in every province, city and the lone Metro Manila municipality within 2 years of the IRR; optional for component municipalities (S1 §8; S2 §9; S3 §XI) | **CURRENT REQUIREMENT** |
| Transaction data from other offices | RoD abstract yearly (LGC §209(a)); permits (§210) and plans (§211) within 30 days | RoD abstract **every 3 months** plus copies of all transfer/lease/mortgage contracts **monthly** (S1 §23; S2 §47); RoD, BIR, notaries, building officials and geodetic engineers transmit data **electronically to BLGF quarterly** (S1 §24; S2 §48). §§210–211 unchanged. | **MODIFIED BY RA 12001** (§209(a)); §§210–211 **STILL APPLICABLE** |
| Computerization | "Data computerization" encouraged (RPTA project) | **Mandatory automation** of RPT administration — tax mapping technology, software-enabled valuation systems, regular data cleansing, computerized records management — with full automation "within two (2) years from the effectivity of the Act" (S1 §22; S2 §46) | **CURRENT REQUIREMENT** |
| National database | — | BLGF **Real Property Information System (RPIS)**: electronic database of all transactions and declarations, construction costs, machinery prices; Data Privacy Act safeguards (S1 §§3(e),(g), 22, 27; S2 §§46, 51). BLGF RPIS and **CAMA with GIS** reported 95% complete (S3 §VI). | **CURRENT REQUIREMENT** |
| Records and forms | Prescribed by the manual (FAAS ×3, TD, TMCR, AR, ORC, ROA, NA) | "Forms prescribed under the LAM" (S5 PVS 106 §30.1) — **content not retrieved** | **SUPERSEDED** → **DOMAIN VERIFICATION REQUIRED** |
| Transfer/annotation/issuance of TDs | Manual Ch. VI; Annex A (MC 18-2004) | BLGF to "formulate and provide for uniform procedures on the different transactions in the assessor's office, including the transfer, annotation, and issuance of tax declarations" (S1 §5(i); S2 §5(i)) — procedure itself not retrieved (likely LAM) | **CURRENT REQUIREMENT** (mandate) / **DOMAIN VERIFICATION REQUIRED** (procedure) |
| Machinery valuation | Manual Ch. IV §7 (RCN, 5%/yr, 20% floor) | LGC §224 quoted as governing by PVS 300 §20.1; §225 unchanged; renewable-energy machinery at net book value (RA 9513) (S5 PVS 300) | **STILL APPLICABLE FROM OLDER MANUAL** (§§224–225) + **CURRENT REQUIREMENT** (PVS 300) |
| Effectivity of assessment | LGC §221 | Unchanged (S8 §221; LTOM §149.A) | **STILL APPLICABLE FROM OLDER MANUAL** |
| Back taxes | LGC §222 | Unchanged (S8 §222) | **STILL APPLICABLE FROM OLDER MANUAL** |
| Notice of assessment | LGC §223 | Unchanged (S8 §223) | **STILL APPLICABLE FROM OLDER MANUAL** |
| Appeals | LGC §§226–231 | Unchanged (S8; LTOM §148) | **STILL APPLICABLE FROM OLDER MANUAL** |
| Once-every-3-years limit on increases (LGC §220 proviso) | — | §220 "amended insofar as inconsistent" (S1 §38); effect on the proviso not stated | **DOMAIN VERIFICATION REQUIRED** |
| RPT amnesty | — | Penalties/surcharges/interest on RPT unpaid before 5 Jul 2024 waived if paid (one-time or instalment) **by 5 Jul 2026**; exclusions (auctioned, compromise, pending cases) (S1 §30; S2 §56; S4) | **CURRENT REQUIREMENT** (window now closed) |
| BIR use of values | Zonal values | SMV or gross selling price, whichever higher; zonal values continue until replaced by certified SMVs (S1 §§18, 29, 31; S2 §§39, 54, 57) | **MODIFIED BY RA 12001** |

---

## 4. Answers to the specific questions

### Q3. Currently prescribed FAAS and Tax Declaration forms

| Finding | Classification | Source |
|---|---|---|
| Forms are those "prescribed under the Local Assessment Manual (LAM)". | **CURRENT REQUIREMENT** | S5 PVS 106 §30.1 |
| The content/layout of the current FAAS and TD forms. | **DOMAIN VERIFICATION REQUIRED** (LAM not retrieved) | S6 |
| The 2004/2006 Attachments 1–4 (FAAS Land/Other Improvements, Building, Machinery; TD). | **SUPERSEDED** as a source (may or may not be reproduced in the LAM) | S3, S10 |
| LTOM treasury forms (Notices of Delinquency, Warrant/Notice of Levy, Certificates of Sale/Redemption, Final Deed of Sale) identify property by **Declared Owner, Tax Declaration No., TCT No., Location, Kind of Property, Assessed Value**. | **CURRENT REQUIREMENT** (treasury forms) | S9 Forms 16–34 (PDF pp.104–126) |

### Q4. PIN and ARP numbering

| Finding | Classification | Source |
|---|---|---|
| "All declarations of real property … shall be kept and filed under a **uniform classification system to be established by the provincial, city or municipal assessor**." | **CURRENT REQUIREMENT** (LGC §207, unamended) | S8 §207 |
| The assessor shall "install and maintain a **real property identification and accounting system**" and "prepare, install and maintain a system of **tax mapping**". | **CURRENT REQUIREMENT** | S8 §472(b)(4)–(5) |
| Automation must include "tax mapping technology". | **CURRENT REQUIREMENT** | S1 §22; S2 §46 |
| The 14-digit PIN (`PPP-DD-BBBB-SSS-LL`), building/machinery/mineral postscripts, retirement on subdivision/consolidation; the ARP/TD numbering with GR indicator. | **SUPERSEDED** as a source → **DOMAIN VERIFICATION REQUIRED** against the LAM | S10 Ch. II §1, Ch. VI §3 |
| Whether BLGF's RPIS/CAMA prescribes identifiers LGU systems must use. | **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** (retrieved) | S3 §VI describes modules only |

### Q5. Authority for appraisal, recommendation and approval by LGU type

| Finding | Classification | Source |
|---|---|---|
| Provinces and cities, including municipalities in the Metropolitan Manila Area, are "primarily responsible for the proper, efficient and effective administration of the real property tax". | **CURRENT REQUIREMENT** | S8 §200 |
| SMV preparation: provincial assessors together with municipal assessors; city assessors; the lone municipal assessor in Metro Manila; the PHIVIDEC-IA assessor. Submission: provincial and city assessors outside NCR → BLGF Regional Office; NCR cities and lone municipality → BLGF Central Office. | **CURRENT REQUIREMENT** | S1 §15; S2 §§28–29 |
| Provincial assessor exercises technical supervision and visitorial functions over component city/municipal assessors; functions delegable when the component LGU meets DOF minimum requirements. | **CURRENT REQUIREMENT** (§472(b)(12), unamended) | S8 §472(b)(12) |
| Assessor functions: appraisal and assessment of all real property in the LGU; semestral report of assessments, cancellations and modifications to the LCE and Sanggunian. | **CURRENT REQUIREMENT** | S8 §472(b)(7), (10) |
| The FAAS chain "Appraised/Assessed By → Recommending Approval → Approved By" with provincial-to-municipal delegation. | **SUPERSEDED** as a source → **DOMAIN VERIFICATION REQUIRED** | S10 Ch. VI |
| Valuation reviews should be by a competent, licensed appraiser. | **CURRENT REQUIREMENT** (valuation reports) | S5 PVS 106 §20.1 |

### Q6. Building valuation, including bracket/rate application

| Finding | Classification | Source |
|---|---|---|
| Buildings are valued by the **cost approach**: unit base construction cost per sq.m. (or cu.m.) × area (or volume) = reproduction/replacement cost new, **less depreciation** = depreciated cost. | **CURRENT REQUIREMENT** | S5 PVS 103 §§40.1–40.2 (citing LAR 1-92 §21(C)) |
| Depreciation must be taken into account "in all instances" for depreciable assets, in accordance with the PVS. | **CURRENT REQUIREMENT** | S1 §14; S2 §22 |
| Building unit values belong in the SMV ("all kinds of real property, except machinery"). | **CURRENT REQUIREMENT** | S1 §4(p) |
| Assessment levels for buildings: LGC §218(b) maximum brackets by fair market value ("Over / Not Over") for residential, agricultural, commercial/industrial, timberland; actual levels by LGU ordinance. | **CURRENT REQUIREMENT** (maxima) + **LGU-SPECIFIC** (levels) | S8 §218(b); S2 §§38, 54 |
| Whether a bracket's level applies to the **whole** market value or **marginally** per bracket. | **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** (statute gives the table only) → **DOMAIN VERIFICATION REQUIRED** (LAM/ordinance) | S8 §218 |
| Specific depreciation schedules, extra-item percentages, construction-type classes. | **LGU-SPECIFIC** (in the certified SMV) / **DOMAIN VERIFICATION REQUIRED** | S1 §4(p); S10 (superseded) |

### Q7. Machinery valuation and assessment

| Finding | Classification | Source |
|---|---|---|
| Brand-new machinery: fair market value = **acquisition cost**. Otherwise: **(remaining economic life ÷ estimated economic life) × replacement or reproduction cost**. | **CURRENT REQUIREMENT** | S8 §224(a); S5 PVS 300 §20.1 |
| Imported machinery: acquisition cost includes freight, insurance, bank and other charges, brokerage, arrastre and handling, duties and taxes, inland transportation, handling and installation; foreign cost converted at Central Bank rates. | **CURRENT REQUIREMENT** | S8 §224(b); S5 PVS 300 §20.1(b) |
| Depreciation allowance **not exceeding 5%** per year of use of original or replacement/reproduction cost, and remaining value **not less than 20%** while the machinery is useful and in operation. | **STILL APPLICABLE FROM OLDER MANUAL** (§225 unamended by S1 §38) | S8 §225 |
| Plant, machinery, equipment and facilities of renewable energy: **original cost less accumulated normal depreciation (net book value)** per RA 9513. | **CURRENT REQUIREMENT** | S5 PVS 300 §20.3 |
| Machinery is **excluded from the SMV** — valued individually. | **CURRENT REQUIREMENT** | S1 §4(p) |
| Machinery assessment levels: LGC §218(c) maxima by class; special classes §218(d). | **CURRENT REQUIREMENT** + **LGU-SPECIFIC** | S8 §218(c)–(d) |
| How §224's life-ratio formula and §225's 5%/20% rules combine in computation. | **DOMAIN VERIFICATION REQUIRED** (not reconciled in retrieved sources) | S8 §§224–225 |

### Q8. Rounding rules

| Stage | Finding | Classification |
|---|---|---|
| Market value (land) | "Rounded to the nearest tens" appears only in the old manual. | **SUPERSEDED** → **DOMAIN VERIFICATION REQUIRED** |
| Market value (buildings/machinery), assessed value | No rule in RA 12001, IRR, PVS 3rd Ed., LGC or LTOM Book IV. | **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** |
| Tax, discount, interest | LTOM §147 gives formulas and whole-peso examples but **no rounding rule**. | **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** |

### Q9. Transaction types/codes currently recognized

The old manual's list (SD, CS, DC, PC, DP, DT, TR, RC, GR, with ranks) is
**SUPERSEDED as a source** and must be verified against the LAM. The table
records which **statutory events** currently require assessment action,
independently of codes.

| Transaction | Current statutory basis (retrieved) | Code | Classification |
|---|---|---|---|
| New declaration / discovery | Owner's sworn declaration (§202); assessor declares for defaulting owner or unknown owner (§204); valuation on first declaration (§220(a)); back taxes up to 10 years (§222) | LAM | Basis **CURRENT REQUIREMENT**; code **DOMAIN VERIFICATION REQUIRED** |
| Improvement (new/added) | Declare within 60 days of completion or occupancy (§203); building permits sent to assessor within 30 days (§210); increase allowed for "new improvements substantially increasing the value" (§220 proviso — amendment effect unverified) | LAM | Same |
| Removal / destruction of improvements | Reassessment for "partial or total destruction" within 90 days, effective next quarter (§221) | LAM | Same |
| Subdivision / consolidation | Geodetic plans to assessor within 30 days (§211); assessor's identification system (§§207, 472(b)(4)) | LAM | Same |
| Transfer | Transferor notifies assessor within 60 days (§208); transfer-tax proof before cancelling old TD and issuing new (§135(b)); BLGF to set uniform transfer/annotation/issuance procedures (S1 §5(i)) | LAM | Same |
| Reassessment | Definition (§199(q)); abnormal causes (§221); on request of declared owner (§220(c)); after LBAA/CBAA decision with assessor concurrence (§229(c)) | LAM | Same |
| Correction | Gross illegality (§221); SMV error correction (S1 §19; S2 §43.2); PVS 106 valuation review | LAM | Same |
| Cancellation | Assessor reports "cancellations and modifications of assessments" semestrally (§472(b)(10)); auction/forfeiture leads to a new TD for purchaser/LGU (LTOM §150) | LAM | Same |
| Reclassification | Actual use governs (§217); "major change in its actual use" reassessment (§221) | LAM | Same |
| General revision | S1 §19; S2 §§41–42; S3 §IV (new TDs issued, owners notified) | LAM | Same |

### Q10. Untitled land, claimants, beneficial users, duplicate/conflicting records

| Finding | Classification | Source |
|---|---|---|
| Property listed, valued and assessed in the name of the owner, administrator, or anyone having legal interest; estates/heirs and co-owners (severally and proportionately liable); corporations like individuals; government property with beneficial use granted to a taxable person — in the name of the possessor/grantee, or the public entity if held for resale or lease. | **CURRENT REQUIREMENT** | S8 §205 |
| Assessor declares property of a defaulting owner, or against an **unknown owner**. | **CURRENT REQUIREMENT** | S8 §204 |
| Classified and assessed on actual use regardless of ownership or user. | **CURRENT REQUIREMENT** | S8 §217; S5 PVS 102/400 |
| A TD "does not and cannot by itself alone confer any ownership or legal title" (form note). | **SUPERSEDED** as a source (form note from old TD) → **DOMAIN VERIFICATION REQUIRED** | S10 Att. 4 |
| TDs for each claimant of untitled land; cross-annotations on duplicate declarations; preference to best title or possession. | **SUPERSEDED** as a source → **DOMAIN VERIFICATION REQUIRED** | S10 p.116, 140–141 |
| First-declaration documentary requirements for untitled land (survey plan, CENRO, affidavits, barangay certifications, NCIP). | **SUPERSEDED** as a source → **DOMAIN VERIFICATION REQUIRED** | S10 p.116 |

### Q11. Notices of Assessment, electronic service, proof of service

| Finding | Classification | Source |
|---|---|---|
| Written notice **within 30 days** when property is assessed for the first time or an existing assessment is increased or decreased, to the person in whose name the property is declared; delivery **personally, by registered mail, or through the punong barangay** to the last known address. | **CURRENT REQUIREMENT** (LGC §223 unamended) | S8 §223 |
| Appeal to the LBAA within **60 days from receipt** of the written notice; LBAA decides within 120 days; appeal to CBAA within 30 days; appeal does not suspend collection. | **CURRENT REQUIREMENT** | S8 §§226, 229, 231; S9 §148 |
| After the certified SMV and general revision, "new Tax Declarations (TDs) will be issued, and real property owners will be notified accordingly". | **CURRENT REQUIREMENT** | S3 §IV.1 |
| Back-tax interest runs from receipt of the notice if unpaid by the end of the following quarter. | **CURRENT REQUIREMENT** | S8 §222 |
| **Electronic service** of Notices of Assessment. | **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** — §223 lists three modes only; no retrieved BLGF issuance authorizes e-service; S7 (ICT policy) not retrieved | — |
| Required records of service (signed duplicates, registry return cards). | **SUPERSEDED** as a source (old manual p.136) → **DOMAIN VERIFICATION REQUIRED**. (Treasury delinquency notices use registered mail with return card or personal service — S9 §150, a different notice.) | S10; S9 |

### Q12. Prerequisites and documents for transfer-related Tax Declarations

| Finding | Classification | Source |
|---|---|---|
| The provincial assessor shall require **evidence of payment of the transfer tax** "before cancelling an old tax declaration and issuing a new one". | **CURRENT REQUIREMENT** (LGC §135(b), unamended; §135(a) amended by S1 §38) | S8 §135(b) |
| The Register of Deeds requires a certificate that real property taxes have been **fully paid** before registering a transfer. | **CURRENT REQUIREMENT** | S8 §209(b) |
| Transferor notifies the assessor within 60 days (mode of transfer, description, transferee name and address); acquirer files a sworn statement within 60 days. | **CURRENT REQUIREMENT** | S8 §§208, 203 |
| Transfer tax base now uses the SMV. | **CURRENT REQUIREMENT** | S1 §18(a)(2); S2 §39.1(b) |
| BIR capital gains tax / CAR before transfer of TD; CAR details on the new TD (BLGF MC 18-2004; RR 24-02). | **SUPERSEDED** as a source (old manual Annex A) → **DOMAIN VERIFICATION REQUIRED** (current BIR/BLGF rule) | S10 Annex A |
| Uniform procedure for transfer, annotation and issuance of TDs. | **CURRENT REQUIREMENT** that BLGF sets it (S1 §5(i)); content **DOMAIN VERIFICATION REQUIRED** | S1 §5(i) |
| Levy is annotated on the TD; after an unredeemed auction, a new TD is issued to the purchaser; forfeited property vests in the LGU. | **CURRENT REQUIREMENT** | S9 §150 (LGC §§258–263) |

### Q13. Historical / superseded assessment information to preserve

| Finding | Classification | Source |
|---|---|---|
| Assessor issues certified copies of assessment records "and all other records relative to its assessment" on request. | **CURRENT REQUIREMENT** | S8 §472(b)(9) |
| Semestral report of all assessments, cancellations and modifications. | **CURRENT REQUIREMENT** | S8 §472(b)(10) |
| Back taxes computed "on the basis of the applicable schedule of values in force during the corresponding period" (up to 10 years) — prior SMVs and assessments must remain available. | **CURRENT REQUIREMENT** | S8 §222 |
| Prior SMVs/zonal values stay in force until replaced by certified SMVs. | **CURRENT REQUIREMENT** | S1 §31; S2 §57 |
| RPT collectible within 5 years (10 for fraud), with suspensions; RPT history needed for that window. | **CURRENT REQUIREMENT** | S9 §149 (LGC §270) |
| Valuation records to be maintained per the LAM; documentation of valuation (IVS 106 / PVS 106). | **CURRENT REQUIREMENT** (content per LAM **DOMAIN VERIFICATION REQUIRED**) | S5 |
| Record of Superseded Assessment block (PIN, ARP, TD, AV, previous owner, effectivity); "This declaration cancels…". | **SUPERSEDED** as a source → **DOMAIN VERIFICATION REQUIRED** | S10 |
| Records retention periods. | **NOT ESTABLISHED BY AUTHORITATIVE SOURCE** (retrieved) | — |

### Q14. Requirements affecting a computerized Real Property Information System

| Finding | Classification | Source |
|---|---|---|
| LGUs **shall automate** RPT administration: tax mapping technology, software-enabled valuation systems, regular data cleansing, computerized records management; full automation within 2 years of effectivity (i.e. by 5 Jul 2026); DICT provides equipment, connectivity and an **interoperable** ICT platform. | **CURRENT REQUIREMENT** | S1 §22; S2 §46 |
| BLGF RPIS: national database of transactions and declarations, construction costs, machinery prices; **mandatory submission** of documents by LGUs and national offices; electronic submission mechanisms; BLGF may charge RPIS fees to others but **not LGUs**. | **CURRENT REQUIREMENT** | S1 §§5(k), 22; S2 §§5(k), 46 |
| BLGF RPIS modules: Valuation and Assessment, Data Warehousing, GIS (cadastral map viewing); CAMA: data management, valuation methods (traditional and AI), SMV development, SMV compliance review/certification — 95% complete; database cleansing before migration. | **CURRENT REQUIREMENT** (direction) — interface/integration obligations **DOMAIN VERIFICATION REQUIRED** | S3 §§VI–VII |
| **Data Privacy Act** (RA 10173) governs submission, disclosure and security; unauthorized processing/access/disclosure of RPIS data punishable (also RA 10175). | **CURRENT REQUIREMENT** | S1 §§3(g), 22, 27; S2 §§46, 51 |
| Quarterly RoD abstracts and monthly contract copies to assessors; quarterly electronic transmission by RoD, BIR, notaries, building officials and geodetic engineers to BLGF. | **CURRENT REQUIREMENT** | S1 §§23–24; S2 §§47–48 |
| DOF Order No. 054.2024 — minimum ICT standards (technology neutrality, interoperability, red-tape elimination, security, **audit trail**), EPCS and eOR requirements. Details in §4.1. | **CURRENT REQUIREMENT** | S7 §§5–9 |
| Tax-mapping and identification system maintained by the assessor. | **CURRENT REQUIREMENT** | S8 §§207, 472(b)(4)–(5) |
| Records and forms per the LAM. | **CURRENT REQUIREMENT** / content **DOMAIN VERIFICATION REQUIRED** | S5 PVS 106 §30.1 |
| Treasury: RPT = AV × rate − discount + interest per tax type (basic, SEF); discount = AV × rate × discount rate; interest = AV × rate × 2% × months delinquent, **max 36 months**. | **CURRENT REQUIREMENT** (BLGF manual procedure) | S9 §147 |

---

### 4.1 DOF Order No. 054.2024 — ICT standards for LGU treasury and assessment systems (S7)

Scope: "the ICT and electronic transactions of all local treasury and
assessment offices of LGUs" (§3). The Order is an umbrella policy: it sets
principles and payment/receipt requirements and defers detailed guidelines
to later BLGF issuances (§8).

| Finding | Classification | Source |
|---|---|---|
| ICT systems for LGU treasury and assessment operations are evaluated against five principles from §38 of the IRR of RA 8792: **technology neutrality** (no favouring a technology or vendor), **interoperability** ("connect and communicate with other entities forming part of the government network"), **elimination of red tape**, **security measures** ("guard against unauthorized access, unlawful disclosure of information, and … ensure the integrity of stored information"), and **auditability** ("**All systems installed shall provide an audit trail**"). | **CURRENT REQUIREMENT** | S7 §5.1–5.5 |
| LGUs engaging an Operator of Payment Systems (incl. banks/EMIs operating as OPS) must require the BSP Certificate of Registration (BSP Circ. 1049 s. 2019; RA 11127). | **CURRENT REQUIREMENT** (LGU procurement/contract duty, not a system feature) | S7 §6.1 |
| The Local Treasurer attests that the EPCS integrated with the Local Treasury Operations System meets DTI-DOF JDAO No. 02 s. 2006 — technical, legal and documentary requirements, **data retention protocol, confidentiality**. EPCS must comply with COA rules and standards. | **CURRENT REQUIREMENT**; JDAO 02-2006 and COA content **DOMAIN VERIFICATION REQUIRED** (not retrieved) | S7 §§6.2–6.3 |
| **Electronic Official Receipt (eOR) minimum data content** (per COA Circ. 2013-007 §3.3): issuing agency/office; location and location code; payor name; date **and time** of receipt; nature of collection; amount **detailed by nature of collection, coded to subsidiary-ledger revenue classification**; eOR number (**unique and sequential**, system-generated); **transaction number** (per accepted transaction, distinct from the eOR number — may cover eOR cancellation, inquiry); mode of payment; **Order of Payment Slip number or Assessment Number**. | **CURRENT REQUIREMENT** | S7 §7.1 |
| "The BLGF shall issue an **eOR numbering system format** … The LGU shall use the eOR numbering system assigned by the BLGF." | **CURRENT REQUIREMENT** (exists) / format **DOMAIN VERIFICATION REQUIRED** (not yet issued or not retrieved) | S7 §7.2 |
| Follow-on BLGF guidelines to be issued: LGU ICT system evaluation; EPCS and eOR adoption; BLGF office systems; eSRE/LIFT; **RPIS**; **CAMA with GIS**. | **CURRENT REQUIREMENT** (to be issued) / content **DOMAIN VERIFICATION REQUIRED** | S7 §8 |
| Treasurers and assessors must ensure their ICT systems comply with §§5–8; that the LGU has a **contract with the systems provider covering data ownership, control processes, integrity, security and confidentiality of data, and support**; and that EPCS is integrated into the LGU online system. | **CURRENT REQUIREMENT** (LGU duty; shapes vendor/deployment terms) | S7 §§9.1–9.3 |
| BLGF Central/Regional ICT Evaluation Teams evaluate systems; electronic collection systems and electronic books of accounts are evaluated per LTOM Book 4 §157, ISA on IT environments, and IT-audit issuances. | **CURRENT REQUIREMENT**; LTOM §157 not yet extracted → **DOMAIN VERIFICATION REQUIRED** | S7 §§10.1–10.2 |
| Existing LGU ICT systems and OPS engagements had **90 days** from effectivity to comply. | **CURRENT REQUIREMENT** (transitory; already elapsed if filed in 2024) | S7 §11 |
| Government must accept electronic documents and may issue approvals and receipts electronically (RA 8792 §27, recited in §1.7). | **CURRENT REQUIREMENT** (RA 8792) — but it does **not** establish electronic *service* of Notices of Assessment; Q11 is unchanged (**NOT ESTABLISHED**) | S7 §1.7 |
| No PIN/ARP format, FAAS/TD form, transaction code, rounding rule or approval chain is prescribed. | Q3–Q5, Q8–Q9 remain **DOMAIN VERIFICATION REQUIRED** (LAM) | S7 (whole) |

## 5. Consolidated status of the earlier analysis's open questions

| # (earlier doc §20) | Question | Status now |
|---|---|---|
| 1 | What did RA 12001 change? | **Resolved** — §3 above |
| 2 | Newer manual/forms? | **Partly resolved** — LAM (DC 004-2025) supersedes MRPAAO; content not retrieved |
| 3 | PIN/ARP scheme | **Open** — LGC §207 leaves the system to the assessor; LAM content unknown |
| 4 | LGU type / approving officer | **Partly resolved** — §5 table; FAAS signatory chain open |
| 5 | Building bracket application | **Open** — not established by the statute |
| 6 | Machinery 20% floor | **Resolved** — LGC §225 unamended (still applies); interplay with §224 open |
| 7 | Rounding | **Open** — not established by any retrieved source |
| 8 | Codes for improvements/cancellation | **Open** — statutory events identified; codes in LAM |
| 9 | Claimants / duplicates | **Partly resolved** — §205/§204; annotation practice open |
| 10 | Electronic NA | **Resolved as NOT ESTABLISHED** — §223 lists personal, registered mail, punong barangay |
| 11 | Transfer TD prerequisites | **Partly resolved** — §135(b), §209(b), §208; BIR CAR open |
| 12 | Superseded-record snapshot | **Open** (form content) — history-preservation obligations resolved (§5 Q13) |
| 13 | Condominiums | **Open** — not addressed by retrieved current sources |
| 14 | Linear infrastructure apportionment | **Open** — PVS 300 §30.2 defers to responsible national agencies' policies |
| 15 | Retention | **Open** — not established |

---

## 6. PRIME ARCHITECTURAL IMPACT

Interpretation for PRIME, based only on the findings above. No
implementation is proposed or performed here. "Firm" means the driver is a
CURRENT REQUIREMENT; "Pending" means it depends on the LAM or other
unretrieved sources.

| Module | Impact | Driver | Firmness |
|---|---|---|---|
| **Property Registry** | Must support listing in the name of owner, administrator, anyone with legal interest, estates/heirs, co-owners, grantee/possessor of government property, and **unknown owner** (§§204–205) — PRIME's `Taxpayer`/`PropertyTaxpayer` has no "unknown owner", administrator or beneficial-user roles. Separate owner vs. legal-interest roles. | S8 §§204–205 | Firm |
| **Parcel / GIS** | Tax mapping and an identification system are statutory duties (§§207, 472(b)(4)–(5)) and part of the automation mandate (S1 §22). BLGF's own RPIS/CAMA has a GIS module; integration or data exchange may be expected. PRIME's GIS (Phase 7) aligns in direction; identifier formats pending. | S1 §22; S3 §VI; S8 §207 | Firm (mandate) / Pending (identifier format, integration) |
| **RPU** | Machinery excluded from the SMV and valued individually (§4(p), §224) — separate valuation path per machine; the earlier concern about PRIME's one-machinery-per-RPU constraint depends on LAM form structure. | S1 §4(p); S8 §224 | Firm (valuation path) / Pending (cardinality) |
| **FAAS / Appraisal** | A FAAS-like approved appraisal/assessment record is expected ("forms prescribed under the LAM"), but its fields, signatories and numbering are **unknown**. PRIME should not model the 2004 FAAS layout as authoritative. | S5 PVS 106 §30.1; S6 | Pending |
| **Valuation** | PVS conformity for all valuation; depreciation mandatory for depreciable assets (buildings via cost approach — PRIME's `ValuationCalculator` currently skips building depreciation); machinery per §224 (**replacement/reproduction cost × remaining/estimated life** for non-new — PRIME uses *acquisition* cost × life ratio) and §225 (**20% floor** — PRIME floors at 0); renewable-energy machinery at net book value. SMV certification lifecycle (DOF-certified, 15-day effectivity, remand) differs from PRIME's Sanggunian-ordinance-style `Smv` model; SMV excludes machinery. SMV revision triggers (ROW, calamity, pandemic, error correction). | S1 §§4, 14–19; S2 §§21–44; S5 PVS 103, 300; S8 §§224–225 | Firm |
| **Assessment** | Actual use governs level (§217); §218 maxima, levels **LGU-SPECIFIC**; effectivity per §221 (1 Jan next year; next quarter for abnormal causes — a tax year can have two assessed values); back-tax periods on historical SMVs (§222); **general revision after each certified SMV, every 3 years**; §220 once-in-3-years proviso status pending; bracket application pending. | S8 §§217–222; S1 §19 | Firm (except bracket, §220 proviso) |
| **Tax Declaration** | New TDs issued at each general revision (S3); transfer TD requires transfer-tax proof (§135(b)); levy annotation and post-auction TD issuance (LTOM §150); TD layout/number pending (LAM). | S3 §IV; S8 §135(b); S9 §150 | Firm (events) / Pending (layout, numbering) |
| **Historical Records** | Historical SMVs and assessments must remain usable for 10-year back taxes and 5-/10-year collection prescription; certified copies of assessment records on request; semestral report of assessments, cancellations and modifications. PRIME's versioning principle aligns; specific superseded-record fields pending. | S8 §§222, 270, 472(b)(9)–(10); S9 §149 | Firm (obligations) / Pending (fields, retention) |
| **Billing** | **6% first-year cap per tax type** on RPT increases from the first new SMV, plus optional LGU caps/phasing for later years — not in `docs/BILLING.md`. Interest: 2%/month, **max 36 months** ("For delinquency from 1992 onward, the impositions under the LGC shall apply at 2% per month, with a maximum of 36 months", LTOM §147) — the design's `InterestRule.MaxMonths` can represent it. Discount formula per tax type (AV × rate × discount rate). Amnesty rules (window closed 5 Jul 2026) matter for historical bills. Per-tax-type breakdown (basic, SEF, idle land, special levies). | S1 §§29–30; S2 §§55–56; S9 §147 | Firm |
| **Collection** | **eOR (S7 §7):** receipts must carry the §7.1 minimum fields — notably date *and time*, location code, per-line amounts coded to the revenue classification (per tax type/fund), a unique sequential system-generated eOR number, a **separate transaction number** (also for cancellations/inquiries), mode of payment, and the Order of Payment / Assessment Number; eOR numbering must follow the **BLGF-assigned format** (pending) — so OR numbering must be configurable, not hard-coded. Electronic payment channels via BSP-registered OPS; EPCS integration and data-retention/confidentiality per JDAO 02-2006 and COA (pending). Also: payment under protest (held in trust; 50% distributed), refunds/credits, 5-year prescription with suspensions, compromise agreements (no waiver of interest), levy/auction/forfeiture/redemption (2%/month on bid price) and their forms. | S9 §§148–151 | Firm |
| **Reports** | Semestral report of assessments/cancellations/modifications (§472(b)(10)); Revenue and Tax Impact Report with three options (S1 §17); compliance reporting to BLGF (S1 §5(j)); mandatory data submissions to RPIS; Treasurer inputs (assessment roll → tax computation, LTOM §147.A.1). | S1 §§5, 17, 22; S8 §472 | Firm |
| **Workflow / Approvals** | SMV lifecycle workflow (preparation → consultations → BLGF regional/central review → DOF certification → publication → effectivity, with remand) is statutory and time-bound. Assessment approval chain is pending (LAM). Valuation review by licensed appraisers (PVS 106 §20). General revision suspension rules (S2 §44). | S1 §§15–16, 19; S2 §§23–34, 44; S5 | Firm (SMV) / Pending (assessment chain) |
| **Documents / Attachments** | Sworn statements (§§202–203), building permits and machinery registration certificates (§210), survey/subdivision plans (§211), RoD abstracts and contract copies (S1 §23), exemption evidence within 30 days (§206), transfer-tax proof (§135(b)), notice-of-assessment service proof (mode per §223). | S1 §23; S8 | Firm |
| **Audit Trail** | "**All systems installed shall provide an audit trail**" (S7 §5.5) — an explicit ICT standard for LGU treasury and assessment systems, evaluated by BLGF ICT Evaluation Teams (S7 §10); integrity of stored information (S7 §5.4). Also: Data Privacy Act compliance and **penalties for unauthorized processing, access, disclosure or use of RPIS data** (S1 §27) — access logging and least-privilege are legal, not just good practice; penal provisions for assessors failing to follow valuation standards or deadlines (S1 §26) make deviations and timelines audit-relevant. | S1 §§26–27; S2 §§50–51 | Firm |
| **Cross-cutting: interoperability** | Automation must work on a DICT-provided interoperable platform and feed BLGF's RPIS; S7 §5.2 requires systems to "connect and communicate with other entities forming part of the government network" and §5.1 technology/vendor neutrality. PRIME needs a documented export/integration layer (open formats, APIs). RPIS and CAMA-with-GIS guidelines are still to be issued (S7 §8). | S1 §22; S3 §§II, VI; S7 §§5, 8 | Firm (principle) / Pending (specs) |
| **Cross-cutting: deployment & vendor terms** | The LGU must contract with the systems provider on **data ownership**, control processes, integrity, security, confidentiality and support (S7 §9.2); systems are subject to BLGF ICT evaluation (S7 §10). PRIME's documentation should support that evaluation (security model, audit trail, data-export/ownership, backup/retention). | S7 §§9–10 | Firm |

### 6.1 Implications for work already in progress

- **Phase 8 (Billing) step 1 — uncommitted rule model:** the 6% first-year
  cap (per tax type) and LGU-ordained later caps were **not representable**
  in the first draft; **addressed 2026-09-24** by `TaxIncreaseCapRule`
  (docs/BILLING.md §3.7; its open interpretation points remain DOMAIN
  VERIFICATION REQUIRED). The 36-month interest cap is representable
  (`InterestRule.MaxMonths`).
- **Phase 9 (Collection), not started:** design the receipt model to the
  eOR minimum content (S7 §7.1) with configurable numbering awaiting the
  BLGF format (S7 §7.2).
- **Phase 5/6 (Valuation/Assessment) as built:** machinery calculation and
  the SMV model diverge from current statute/PVS as noted above; building
  depreciation is currently absent. **Machinery addressed 2026-09-24**
  (replacement cost for non-new machinery, §225 minimum remaining value;
  docs/DOMAIN-MODEL.md §3.9). SMV model and building depreciation remain.
- **Anything modelled on the 2004 FAAS/TD/PIN/ARP formats** should be treated
  as provisional until the LAM is obtained.

### 6.2 Recommended next step to close the gap (non-implementation)

Obtain the **Local Assessment Manual (DOF Department Circular No. 004-2025,
5 Nov 2025)** directly from BLGF (`records@blgf.gov.ph`, per S2/S3
letterhead) or through the eFOI portal, then re-run Q3–Q5, Q8–Q13 against
it. DOF Order No. 054.2024 has since been supplied (§4.1); still to obtain
for the collection side: the follow-on BLGF guidelines under S7 §8 (eOR
numbering format, EPCS/eOR adoption, RPIS, CAMA-with-GIS, ICT evaluation),
DTI-DOF JDAO No. 02 s. 2006, and COA Circular No. 2013-007. Separately, obtain the target LGU's
ordinances (assessment levels, tax rates, caps/phasing, SMV) since those
values are **LGU-SPECIFIC**.
