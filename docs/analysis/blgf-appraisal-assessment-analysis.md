# BLGF Manual on Real Property Appraisal and Assessment Operations — Reference Analysis for PRIME

Status: **analysis only** (2026-09-24). No PRIME code, schema, migration or
configuration was changed as a result of this document.

## 0. About this analysis

### 0.1 Source

| Item | Value |
|---|---|
| File analysed | `docs/References/ManualRPAandAO.pdf` — the only file in `docs/References/`. The request named `BLGF_Manual_Real_Property_Appraisal_Assessment.pdf`; no file by that name exists, and this file's content matches the description. |
| Title | *Manual on Real Property Appraisal and Assessment Operations* |
| Issuer | Department of Finance — Bureau of Local Government Finance, cover dated **January 2006**; issued through **Local Assessment Regulations No. 1-04, October 01, 2004** (PDF p.7), under Sec. 200/201 of R.A. 7160 and Art. 291 of its IRR (PDF pp.7–8). |
| Size | 279 PDF pages; 10 chapters, Annexes A–G, Attachments 1–12 (forms). |

### 0.2 Citation convention

Citations give the **manual's printed page** and the **PDF page**, e.g.
"p.145 (PDF 156)". The printed page number is consistently 11 lower than
the PDF page index. Chapter/section numbers follow the manual.

### 0.3 How to read this document

| Marker | Meaning |
|---|---|
| **[REF]** | Stated by the reference document (paraphrased closely, terminology preserved; direct quotes in quotation marks). |
| **[INTERP]** | This analysis's architectural interpretation for PRIME — not a statement of the reference. |
| **NOT ESTABLISHED BY REFERENCE** | The document does not settle the point. |

### 0.4 Currency caveat — DOMAIN VERIFICATION REQUIRED

The manual dates from 2004/2006 and implements R.A. 7160 as it stood then.
**It predates R.A. 12001 (Real Property Valuation and Assessment Reform
Act, 2024) and its IRR**, which amended the valuation and assessment
framework, and any later BLGF issuances. Every rule below is what *this
manual* states; whether it still applies must be confirmed against current
law, BLGF issuances and the target LGU's ordinances before PRIME relies on
it. The manual itself also lets LGUs keep an existing numbering system
until their next general revision (p.170, PDF 181).

---

## 1. Terminology (preserved from the reference)

| Term | Reference meaning | Source |
|---|---|---|
| **FAAS** — (Real Property) Field Appraisal and Assessment Sheet | "Primary and principal record of all real property information that is necessary for real property tax assessment." Three forms: Land and Other Improvements; Buildings and other Structures; Machinery. | p.143–144 (PDF 154–155) |
| **TD** — Tax Declaration (of Real Property) | Establishes "a permanent assessment record and provide[s] the property owner with information relative to the assessment". | p.155 (PDF 166) |
| **ARP / ARPN** — Assessment of Real Property (Number) | Number assigned to each assessment of an RPU; "The TD Number which is the ARP Number". | p.144, 156, 159, 170 (PDF 155, 167, 170, 181) |
| **PIN** — Property Identification Number | 14-digit parcel identifier assigned on the tax map; buildings/machinery add a 4-digit postscript. | Ch. II §1, p.35–43 (PDF 46–54) |
| **RPU** — Real Property Unit | Used throughout ("FAAS shall be prepared for every real property unit"; "A tax declaration shall be prepared for every real property unit (RPU)"). | p.143, 156 (PDF 154, 167) |
| **TMCR** — Tax Map Control Roll (Pre-TMCR / Post-TMCR) | "Basic document in controlling the PIN in each section map." | p.158 (PDF 169); p.42–43 (PDF 53–54) |
| **AR** — Assessment Roll (Taxable / Exempt) | "Permanent listing of all assessments of real property made"; basis for the Treasurer. | p.160 (PDF 171) |
| **ROA** — Record of Assessment(s) | Records each assessment transaction; source of statistics. | p.166 (PDF 177) |
| **ORC** — Ownership Record Card | Per-owner listing of properties. | p.164 (PDF 175) |
| **NA** — Notice of Assessment | Notifies owner of market and assessed value; due-process requirement under Sec. 226 of R.A. 7160. | p.167–168 (PDF 178–179) |
| **SFMV** — Schedule of Fair Market Values | Enacted by ordinance; basis of appraisal. Includes the **SBUCC** — Schedule of Base Unit Construction Cost for buildings, "an integral part of the SFMV". | Ch. III §5; p.117 (PDF 128) |
| **Market Value / Fair Market Value** | "The price at which a property may be sold by a seller who is not compelled to sell and bought by a buyer who is not compelled to buy". | Def. 34, p.5 (PDF 16) |
| **Assessment Level** | "The percentage applied to the market value to determine the taxable value of the property". | Def. 11, p.3 (PDF 14) |
| **Assessed Value** | "The market value of the property multiplied by the assessment level. It is synonymous to taxable value". | Def. 13, p.3 (PDF 14) |
| **Actual Use** | "The purpose for which the property is principally or predominantly utilized by the person in possession thereof". | Def. 3, p.2 (PDF 13) |
| **Reassessment** | "Assigning of new assessed values … as the result of a general, partial, or individual reappraisal". | Def. 64, p.8 (PDF 19) |
| **Administrator / Beneficial User** | Administrator: person authorized by the owner to manage the property. Beneficial User: person granted use of government property and liable for RPT under Sec. 234(a). | p.146 (PDF 157) |
| **Transaction Code** (a.k.a. Update Code) | Code of "the transaction necessitating the assessment or reassessment". | p.145, 170 (PDF 156, 181) |
| **Kind Code / Classification Code / Use Code** | L/B/M; RACIMST (+ special sub-codes); AR/AC/…/AGOCC. | p.169–170 (PDF 180–181) |

---

## 2. The assessment record system [REF]

- Records are **technical** (FAAS ×3, TD, TMCR) and **non-technical**
  (AR taxable/exempt, ORC, ROA, NA). p.143, 160 (PDF 154, 171).
- "The FAAS when approved is the source of information for all other
  assessment records." p.143 (PDF 154). TD: "All information to be entered
  in the Tax Declaration form shall be extracted from the corresponding
  FAAS." p.156 (PDF 167). AR, ORC, ROA and NA are likewise extracted from
  the FAAS (p.161, 165, 169).
- "A FAAS is prepared each time there is an assessment transaction."
  p.144 (PDF 155).
- "Only one FAAS shall be prepared for each real property unit (rpu)
  declared under an existing and active Tax Declaration." p.50 (PDF 61).
- The FAAS number, ARP number and TD number are the same number: "TD No.:
  Indicate the TD number which is also the FAAS number" (p.156, PDF 167);
  "TD No.: Indicate the Tax Declaration Number which is the ARPN" (p.159,
  PDF 170).

**[INTERP]** The reference's unit of record is the **approved FAAS per RPU
per assessment transaction**; the TD is its owner-facing counterpart
carrying the same number. PRIME today splits this across `Valuation`,
`Assessment` and `TaxDeclaration`, with no single "FAAS" record, no
transaction code on the assessment, and separate numbers (`RpuNumber`,
`TaxDeclarationNumber`).

---

## 3. FAAS for Land and Other Improvements [REF]

Instructions p.145–149 (PDF 156–160); form Attachment 1, p.230–231
(PDF 241–242).

| Block | Fields |
|---|---|
| Header | Transaction Code; ARP No.; PIN (14 digits "as recorded in the TMCR"); OCT/TCT/CLOA No. + date of entry; Survey No. (e.g. Cad-774, Psd-10011); Lot No.; Blk. |
| Owner | Name (with middle name/initial), mailing address, TIN, telephone |
| Administrator/Beneficial User | Name(s), address(es), TIN(s), telephone(s) |
| Property Location | No./Street, Brgy/District, Municipality, Province/City |
| Property Boundaries | North/East/South/West. If tax mapped: assessor's lot no. with survey lot no. in parentheses, e.g. "09 (50)"; if surveyed only: cadastral/survey lot nos. and/or owners; if not surveyed: owners per their TDs; streets/rivers by name; adjoining parcel in another municipality → its name. |
| Land Sketch | "The plan or sketch of the lot" — form notes "(Not necessarily drawn to scale)". |
| Land Appraisal (multi-row) | Classification (RACIMST); Sub-Classification (e.g. R-1, C-2, per SFMV); Area (ha for agricultural, sq.m. for urban); Unit Value (per approved SFMV); Base Market Value = area × unit value; Totals. |
| Other Improvements (multi-row) | Kind (productive and non-productive, other than buildings); Total Number; Unit Value (per SFMV); Base Market Value; Totals. |
| Market Value (Adjustments) | Base Market Value; Adjustment Factors (as provided in the SFMV — e.g. sunken/low area, accessibility to market/poblacion with distance, blighted area, corner influence, location along road/kind of road/distance); % Adjustment; Value Adjustment; Market Value. |
| Property Assessment (multi-row) | Actual Use; Market Value ("rounded to the nearest tens"); Assessment Level ("consistent with the actual use … as fixed in the LGU ordinance"); Assessed Value = MV × level; Totals. |
| Taxability | Taxable ☐ / Exempt ☐ (Sec. 234 of the LGC or other laws) |
| Effectivity | "Effectivity of Assessment/Reassessment" — **Qtr. / Yr.** ("the year or quarter of the year when the assessment/reassessment shall take effect as basis for the collection of taxes") |
| Signatories | Appraised/Assessed By (name, date); Recommending Approval (name, date); Approved By (Provincial/City/Municipal Assessor, date) |
| Memoranda | Cause of assessment/reassessment (e.g. general revision, RPTA project, other legal cause) |
| Record entry | Date of Entry in the Record of Assessment; By |
| Record of Superseded Assessment | PIN; ARP No.; TD No.; Total Assessed Value; Previous Owner; Effectivity of Assessment; AR Page No.; Recording Person + date |

---

## 4. FAAS for Buildings and Other Structures [REF]

Instructions p.149–153 (PDF 160–164); form Attachment 2, p.232–233
(PDF 243–244).

| Block | Fields |
|---|---|
| Header | Transaction Code; ARP No.; PIN |
| Owner / Administrator-Beneficial User | as for land |
| Building Location | house no./street, barangay/district (name + index no.), municipality (name + index no.), province/city |
| **Land Reference** | Land owner's name; OCT/TCT/CLOA No.; Survey No.; Lot No.; Blk. No.; **TD/ARP No. of the land**; Area of the land |
| General Description | Kind of Bldg.; Structural Type; Bldg. Permit No. + date issued; Condominium Certificate of Title (CCT); Certificate of Completion issued on; Certificate of Occupancy issued on; Date Constructed/Completed; Date Occupied; Bldg. Age; No. of Storeys; Area of 1st–4th flr.; Total Floor Area |
| Floor Plan | "Attach the building plan or sketch of floor plan. A photograph may also be attached if necessary" |
| Structural Materials (checklist) | Per structure part (roof, flooring, walls & partitions, etc.) with **per-floor columns (1st–4th Flr.)**; materials list p.150–152 (e.g. Roofing: G.I. Sheet, Asbestos, Ceramic Tiles, Concrete Deck, Aluminum, Long Span, Nipa/Anahaw/Cogon); "additional standard materials … shall [be] properly enumerate[d]" |
| Additional Items | fence, gates, garage, balcony, terrace, basement, mezzanine, others |
| Property Appraisal | Unit Construction Cost (/sq.m.); Building Core; Sub-Total; Cost of Additional Items; Total Construction Cost; Depreciation Rate; Total % Depreciation; Depreciation Cost; Market Value — "with the proper application of depreciation rates as prescribed in the approved SFMV" |
| Property Assessment | Actual Use; Market Value; Assessment Level; Assessed Value; Taxable/Exempt; Effectivity Qtr./Yr. |
| Signatories, Memoranda, Record entry, Record of Superseded Assessment | as for land (superseded block without AR page no. on the form) |

Filing: the building FAAS "shall be filed next to the FAAS of the land, on
which they are located regardless of whoever owns the land" and bears the
land PIN with suffix 1001, 1002, … p.145 (PDF 156).

---

## 5. FAAS for Machinery [REF]

Instructions p.153–155 (PDF 164–166); form Attachment 3, p.234–235
(PDF 245–246).

| Block | Fields |
|---|---|
| Header | Transaction Code; ARP No.; PIN |
| Owner / Administrator | name, address, TIN, telephone |
| Property Location | address; **Land Owner + PIN; Building Owner + PIN** |
| Property Appraisal (**multi-row — one row per machine**) | Kind of Machinery; Brand & Model; Capacity/HP; Date Acquired; Condition When Acquired (New or Second Hand); Economic Life — Estimated / Remaining; Year Installed; Year of Initial Operation |
| Cost & depreciation (multi-row) | Original Cost; Conversion Factor; RCN; No. of Years Used; Rate of Depreciation; Total Depreciation % and Value; Depreciated Value; Totals |
| Property Assessment (multi-row) | Actual Use (RACIMST); Market Value; Assessment Level; Assessed Value; Taxable/Exempt; Effectivity Qtr./Yr. |
| Signatories, Memoranda, Record entry | as for land |
| Record of Superseded Assessment | PIN; ARPN; **TDN**; Total Assessed Value; Previous Owner; Effectivity — used "where the machinery has been transferred to another locality or whose ownership has been transferred or when machinery is reappraised for any other reason" (p.155, PDF 166) |

Market value (p.154, PDF 165): newly acquired → "original acquisition cost
plus additional costs of installation"; old machinery → "replacement cost
at the time of appraisal less accumulated depreciation". The machinery FAAS
is filed next to the FAAS of the building where it is installed (p.145,
PDF 156).

---

## 6. Tax Declaration of Real Property [REF]

Instructions p.155–157 (PDF 166–168); form Attachment 4, p.236 (PDF 247).

- **When a TD is prepared** for an RPU (p.156, PDF 167): general revision;
  newly discovered / declared for the first time; change in area; change in
  classification; change in ownership; physical change in the property;
  change in location (machinery).
- **Fields**: TD No. (= FAAS/ARP number); PIN; Owner (name, address, TIN,
  tel.); Administrator/Beneficial User; Location; OCT/TCT/CCT/CLOA No., date,
  Survey No., Lot No., Blk. No.; Boundaries; **Kind of Property Assessed** —
  Land / Building (No. of Storeys, Brief Description) / Machinery (Brief
  Description) / **Others (Specify)**; multi-row **Classification, Area,
  Market Value, Actual Use, Assessment Level (%), Assessed Value**; Totals;
  **Total Assessed Value in words**; Taxable/Exempt; Effectivity Qtr./Yr.;
  Approved By (+date); **"This declaration cancels TD No. ___ Owner ___
  Previous A.V. ___"**; Memoranda.
- **Printed note** (form): the declaration "is for real property taxation
  purposes only and the valuation indicated herein are based on the schedule
  of unit market values prepared for the purpose and duly enacted into an
  Ordinance by the Sangguniang ___ under Ordinance No. ___ dated ___. It
  does not and cannot by itself alone confer any ownership or legal title to
  the property." (p.236, PDF 247; instruction p.157, PDF 168).
- Copies: original + owner's duplicate (+ triplicate for municipalities
  outside MMA); filed by barangay in TDN sequence (p.155–156).
- Approval: City Assessor / MMA Municipal Assessor / Provincial Assessor,
  who may delegate to the Municipal Assessor (p.155, 157).
- Transfer prerequisites (Annex A, BLGF MC 18-2004, p.201–204,
  PDF 212–215): issuance/transfer of TDs of conveyed property requires,
  "aside from the payment of the corresponding realty tax and transfer tax,
  the payment of the capital gains tax" (BIR CAR); the MC relays that R.R.
  No. 24-02 requires assessors to record CAR details (CAR no., date, TIN and
  name of transferor, taxes paid, amounts, OR numbers, dates) at the back of
  the new TD and the transferee's TIN on its face.

---

## 7. Assessment and reassessment transactions; transaction codes [REF]

### 7.1 Transaction codes and ranking — p.145 (PDF 156); p.170 (PDF 181)

| Rank | Transaction | Code |
|---|---|---|
| 1 | Subdivision | SD |
| 2 | Consolidation | CS |
| 3 | Discovery/New Declaration | DC |
| 4 | Reassessment due to Physical Change caused by erosion or when the property is traversed by road etc. | PC |
| 5 | Reassessment due to a dispute in Assessed Value or reassessment to correct an error in the assessment of the property due to wrong information, erroneous documents, etc. | DP |
| 6 | Reassessment due to partial destruction of the property | DT |
| 7 | Transfer/Segregation | TR |
| 8 | Reclassification | RC |
| 9 | General Revision | GR |

"For assessment or reassessment involving two (2) or more transactions, the
highest rank among the transactions shall be indicated" (ROA instructions,
p.167, PDF 178). The code is also used in TMCR, AR and ORC remarks
(e.g. "SD").

### 7.2 Other transaction-related rules

- Reassessment due to "partial or total destruction, … major change in its
  actual use, … great or sudden inflation or deflation of real property
  values, … gross illegality of the assessment when made or any other
  abnormal cause, shall be made within ninety (90) days from the date any
  such cause or causes occurred" (p.109, PDF 120; p.196, PDF 207).
- Reclassifying land to other uses: written notice by owner → inspection →
  report and recommendation within 15 working days; LFC 3-92 controls
  agricultural-to-non-agricultural reclassification; an approved zoning
  ordinance also serves as basis (p.139, PDF 150).
- Several assessments on one property: the assessor cancels all except the
  one properly made; but if an assessee objects, the assessment is **not**
  cancelled and the fact is **noted** on the FAAS, TD, assessment rolls and
  other records; preference to "the person who has the best title … or …
  possession" (p.140, PDF 151). Worked cases with prescribed notations ("Property
  is also declared in the name of Mr. B under Tax Declaration No ___";
  "Portion of ___ is also declared …") p.140–141 (PDF 151–152).
- Untitled property claimed by two or more persons: "a tax declaration
  shall be issued for each claimants" (p.116, PDF 127).

**NOT ESTABLISHED BY REFERENCE:** a transaction code for cancellation
without replacement, for correction of clerical data that does not change
the assessed value, for addition or removal of an improvement (e.g. a new
building on existing land), or for change of administrator/address. The
list above is the complete list in the manual.

---

## 8. PIN and ARP relationships [REF]

### 8.1 PIN structure — Ch. II §1, p.35–42 (PDF 46–53)

`PPP-DD-BBBB-SSS-LL` (14 digits):

| Digits | Meaning |
|---|---|
| 1–3 | Province, City, or Metro Manila municipality index number (a nationwide table is printed, p.35–39) |
| 4–5 | Municipality index (in a province) or City District index |
| 6–9 | Barangay index ("0001" for the Poblacion, then alphabetical) |
| 10–12 | Section index (inverted-"S" numbering within the barangay) |
| 13–14 | Permanent parcel number (inverted-"S" within the section) |

Examples: `020-15-0005-002-05`, `132-06-0012-001-35` (p.41, PDF 52).

- **Building**: land PIN + 4-digit postscript `1001`, `1002`, …; condominium
  units: building PIN (e.g. 1001) + unit postscript `(1)`, `(2)`, … (p.42,
  PDF 53).
- **Machinery**: land PIN + `2001`, `2002`, … (p.42).
- **Building/machinery owned by someone other than the landowner**: parcel
  number in parentheses, e.g. `020-15-0005-002-(05)-1001` (p.42).
- **Mineral rights** held by someone other than the surface-right holder:
  surface PIN + `3001`… (p.75 item 13, PDF 86).
- These postscripts replace temporary postfixes B-1…, M-1… used in the
  Pre-TMCR and FAAS (p.42).
- **Temporary PIN** before tax mapping: municipality/district + barangay +
  4-digit FAAS sequence; buildings/machinery attached to the land FAAS's
  temporary PIN (p.50–51, PDF 61–62).

### 8.2 Retirement — p.42–43 (PDF 53–54)

- Subdivision: "the PIN of the original parcel shall be retired while the
  newly created lots shall be assigned consecutive numbers beginning with
  the number following the highest parcel number in the section"; a new FAAS
  for each resulting parcel; retirement noted on the Post-TMCR.
- Consolidation: original PINs retired; the resulting parcel gets the next
  highest parcel number; noted on the Post-TMCR.
- "The practice of retiring Index Numbers … provides history of physical and
  ownership changes of the parcels and maintains the uniqueness of the PIN."
- New barangays from a mother barangay: the mother's index number is retired;
  new barangays numbered after the highest existing index (p.43).

### 8.3 ARP/TD numbering — Ch. VI §3, p.170–172 (PDF 181–183); p.75 (PDF 86)

| LGU | Format |
|---|---|
| Province | `MM-BBBB-NNNNN` — municipality index, barangay index, assessment count |
| City outside MMA | `DD-BBBB-NNNNN` — district, barangay, assessment count |
| City/municipality in MMA | `G-MM-BBBB-NNNNN` — **General Revision indicator** (A/AA = first GR after 1 Jan 1992, B/BB = second, …), municipality/district, barangay, assessment count |

- Initial numbering is done "every time a general revision of real property
  assessments is conducted" (or during an RPTA project) (p.171). Where no
  tax map exists, the assessment count runs "starting with '00001' … at the
  start of the General Revision … and shall again start with 00001 when a
  General Revision … shall be undertaken" (p.75, PDF 86).
- Installation: tax-mapped LGUs number FAAS/TDs in PIN order within the
  barangay; non-mapped LGUs by owner surname within the barangay; subsequent
  FAAS are numbered chronologically (p.144–145, 172).
- NA numbering: province+municipality prefix (or MMA city/municipality
  index) + NA count (p.172, PDF 183).

### 8.4 Relationship summary [REF]

- **PIN** identifies the physical parcel (and, via postscript, the
  building/machinery on it); changes when a parcel is subdivided or
  consolidated.
- **ARPN = TDN = FAAS number** identifies one assessment of one RPU; a new
  one is issued for each assessment transaction; the previous one is
  recorded as superseded/cancelled.
- The AR and ROA record "only the Assessor's Lot number" (2 digits, plus
  the 4-digit suffix) in their PIN column (p.162, 167).

---

## 9. Property appraisal requirements [REF]

### 9.1 Fundamental principles — p.110 (PDF 121)

Appraised at current and fair market value based on the approved SFMV;
classified on actual use; uniform classification within each LGU;
appraisal, assessment, levy and collection "shall not be let to any
private person"; appraisal and assessment shall be equitable. All real
property, taxable or exempt, is appraised at current and fair market value
(p.137, PDF 148).

### 9.2 Land

- Urban: rectangular lot = base unit value × area; **stripping method**
  beyond a locally fixed **standard depth** (e.g. strips at 100%/80%/60% in
  the illustration) for residential land — not for commercial/industrial,
  corner lots or subdivision lots; **corner influence** as a % increment
  fixed in the SFMV, applied only on the highest-value street;
  **frontage adjustment** for commercial lots = frontage length × 50% of the
  unit base value; adjustments for shape, topography, blight; rules for
  interior, triangular (2/3 or 1/3), trapezoidal and irregular lots
  (p.110–112, PDF 121–123). "The established unit value (in the SFMV) along a
  particular street … shall be the controlling value irrespective of the
  actual use" (p.112).
- Agricultural: area per sub-class × unit base value, summed; adjustment
  percentages for type of road and distance to all-weather road / trading
  centre (the table on p.113 is marked with a footnote; the illustration
  sums −9%, −4%, −2% = −15% → 85% of BMV) (p.112–113, PDF 123–124).
- Plants and trees are part of the land's value and not appraised apart,
  "However if it shall be proven that the ownership of the land is different
  from that of the improvement, a separate valuation and assessment shall be
  made in the names of their respective owners" (p.114, PDF 125).
- Timber/forest land (beneficial use granted) and mineral land are
  appraised **yearly** against the beneficial user/concessionaire on timber
  actually cut / minerals extracted (p.114).
- Land declared for the first time — required documents (p.116, PDF 127):
  *untitled*: DENR-LMB-approved survey plan; CENRO certification
  (alienable and disposable); affidavit of ownership and/or Sworn Statement
  of market value; affidavit of long, continuous and notorious possession;
  barangay captain certification (and of adjoining owners); ocular
  inspection report; NCIP certification for lands of National Cultural
  Communities; *titled*: certified true copy of patent/application, certified
  true copy of title from the Registry of Deeds, approved survey plan.

### 9.3 Buildings — p.117 (PDF 128); p.131 (PDF 142)

- Appraised per the SBUCC and supported by: approved building permit,
  building plan and/or certificate of completion/occupancy; notice of
  inspection date (if owner absent); inspection report; affidavit of
  ownership or Sworn Statement where permits/certificates are absent.
- Mixed residential/commercial buildings appraised on predominant use;
  historical/vintage buildings appraised independently; bowling alleys
  initially valued separately but assessed as part of the building (except
  machinery).
- Previously assessed buildings: revalued on the current SBUCC; RCNLD
  "must be consistently applied every time a reappraisal is made".
- Building classification by structural design regardless of actual use;
  construction Types I–V; extra items as % of BUCC (p.103–106, PDF 114–117).

### 9.4 Machinery — p.118–122 (PDF 129–133)

- Market or cost approach; reproduction cost new includes direct and
  indirect costs (freight, insurance, bank charges, brokerage, arrastre,
  customs duties, installation, …).
- For tax purposes based on actual cost to the owner, supported by the
  owner's declared value/sworn statement; foreign cost converted at the BSP
  rate at acquisition; RCN formula `RCN = OC × FC2/FC1 × PI × REL/EL`
  (PI optional).
- Depreciation (LGC Sec. 225 as quoted): "not exceeding five percent (5%) of
  its original cost or its replacement or reproduction cost … for each year
  of use: Provided, however, that the remaining value for all kinds of
  machinery shall be fixed at not less than twenty percent (20%) … for so
  long as the machinery is useful and in operation" (p.121, PDF 132).
- Observed-condition depreciation bands: Very Good 80–100%, Good 55–75%,
  Fair 35–50%, Poor 15–30%, Scrap 0–10% (p.121–122).
- Rail tracks, transmission lines and submerged pipelines: total value
  **apportioned between the LGUs they traverse** (p.121).

### 9.5 Condominiums and special-purpose properties — p.122–131 (PDF 133–142)

- Each separate unit appraised independently; common areas and indivisible
  portions assessed in the name of the Condominium Corporation/Association;
  capital equipment (elevators, pumps, tanks) listed and assessed
  aggregately in the corporation's name; unsold parking spaces are common
  area — "No separate tax declaration shall be issued … for individual
  parking areas situated within a condominium building" (p.130–131).
- Golf courses, cemeteries, hospitals, schools, gasoline stations and malls:
  method guidance per type (mostly cost or income approach).

---

## 10. Property assessment requirements [REF]

- Classes: residential, agricultural, commercial, industrial, mineral,
  timberland, special. "Classified, valued and assessed on the basis of its
  actual use regardless of where located, whoever owns it, and whoever uses
  it"; actual use determines the assessment level and "should not be
  construed as a criterion for the classification and valuation" (p.133,
  PDF 144; also p.196, PDF 207).
- Mixed land-use areas: predominant use of lands in the area governs; a
  commercial/industrial lot occupied by a mixed-use building is assessed on
  the building's predominant use; vacant land assessed like similar land;
  special rules for water-district/GOCC power land and religious/charitable/
  educational land (p.138, PDF 149).
- Listing: in the name of the owner, administrator or anyone with legal
  interest; undivided estates in the name of the estate or heirs; co-owned
  property in the name of one or more co-owners (severally liable);
  government property whose beneficial use is granted — in the name of the
  grantee (p.139–140, PDF 150–151).
- Exemptions (Ch. V §4, p.141–142): constitutional and statutory
  categories; taxable/exempt status is fixed as of **January 1** for the
  whole year; exemption claims with supporting documents within 30 days of
  declaration or the property is listed taxable; "Exemptions are never
  presumed" (p.142, PDF 153; p.196, PDF 207).
- Notice of new or revised assessment within 30 days, personally, by
  registered mail with return card, or through the punong barangay;
  signatures/return cards kept as proof of receipt; "The sending of the
  notice of assessment to the taxpayers is mandatory to include the validity
  of the assessment made" (p.136, PDF 147).
- Back taxes for property declared for the first time: up to 10 years plus
  the current year, **each period valued on the SFMV in force for that
  period**; interest of 2% per month "or a fraction thereof" if not paid by
  the end of the quarter following receipt of the notice (p.135, 137, 109,
  197 — PDF 146, 148, 120, 208). Illustration p.137 (PDF 148): one parcel
  declared 2003 carries separate market/assessed values with effectivities
  1993, 1994–1999, 2000–2003.
- Appeals do not suspend collection (p.137, 175).

---

## 11. Market value and assessed value [REF]

- Land: Base Market Value = area × unit value (per sub-class line) →
  adjustment percentages from the SFMV → Market Value (p.147–148, PDF
  158–159; worked example: BMV ₱400,000 + 10% corner = ₱440,000).
- Building: unit construction cost × area (+ additional items) → less
  depreciation per the SFMV's depreciation schedule → Market Value
  (p.152–153; FAAS form).
- Machinery: acquisition/replacement cost → less depreciation →
  depreciated value = Market Value (p.154, 119–121).
- Assessed Value = Market Value × Assessment Level, **per actual-use line**,
  totalled (FAAS and TD forms).
- Rounding: land market value "rounded to the nearest tens" (p.148, PDF
  159); illustrations round with "Say" (e.g. p.120–121). **NOT ESTABLISHED
  BY REFERENCE:** a general rounding rule for assessed values or for
  building/machinery values.

---

## 12. Assessment levels [REF]

- Fixed by ordinance of the Sanggunian "at the rates not exceeding" the
  statutory maximums printed on p.133–135 (PDF 144–146): lands by class;
  buildings by class **and by fair-market-value bracket ("Over / Not Over")**;
  machinery by class; special classes by actual use (cultural, scientific,
  hospital 15%; local water districts and power/water GOCCs 10%).
- "In no case shall such increase or decrease of the assessment levels be
  made effective in between general revision of assessment periods" (p.135,
  PDF 146).
- "The ordinance of the SFMV for the general revision … shall include the
  legally approved assessment levels and the percentages of adjustments"
  (p.107, PDF 118).
- **NOT ESTABLISHED BY REFERENCE:** whether a building's bracketed level
  applies to the whole market value (one rate for the bracket the value
  falls in) or marginally per bracket. The tables state brackets and
  levels only; no worked building-assessment example resolves it.

(The percentages above are the manual's statement of statutory ceilings as
of its date — PRIME must not use them as configured values; see §0.4.)

---

## 13. Effectivity of assessment / reassessment [REF]

| Situation | Effectivity | Source |
|---|---|---|
| Assessments or reassessments made after 1 January of any year | 1 January of the succeeding year | p.109 (PDF 120); p.196 (PDF 207) |
| Reassessment due to partial/total destruction, major change in actual use, great or sudden inflation/deflation, gross illegality, any other abnormal cause (made within 90 days of the cause) | Beginning of the quarter next following the reassessment | same |
| Change of ownership, subdivision or consolidation | 1 January of the succeeding year "and the current assessment shall have been fully paid" | p.196 (PDF 207) |
| General revision | "Not later than January 1st of the third year" of the GR calendar | p.79 (PDF 90) |
| Property declared for the first time | Assessed back up to 10 years + current year, on the SFMV in force for each period | p.109, 135, 137 |
| Taxable/exempt status | As of 1 January (tax day), for the whole year | p.142, 196 |

The FAAS and TD record effectivity as **Qtr. and Yr.**; the AR records
"the year or quarter when the assessment took effect"; the ROA records
"Year Taxes Begin".

---

## 14. Superseded assessments and historical records [REF]

- Every FAAS carries a **Record of Superseded Assessment**: PIN, ARP No., TD
  No., Total Assessed Value, **Previous Owner**, Effectivity of Assessment
  "as indicated in the cancelled record", AR page number, recording person
  and date (p.149, 153, 155).
- Every TD carries "This declaration cancels TD No. ___, Name of Owner,
  assessed value of the cancelled property or superseded assessment"
  (p.157).
- AR lists "Previous ARPN and TDN as shown in the Record of Superseded
  Assessment" (p.162); ORC lists the previous owner (p.166).
- PIN retirement is recorded on the Post-TMCR to keep "history of physical
  and ownership changes" (p.42–43).
- Duplicate declarations are annotated rather than silently removed
  (p.140–141).
- **NOT ESTABLISHED BY REFERENCE:** retention periods for cancelled FAAS/TDs,
  or rules for correcting an already superseded record.

---

## 15. Appraisal / assessment approval workflow [REF]

- Three roles recorded on every FAAS, each with name and date:
  **Appraised/Assessed By** → **Recommending Approval** ("usually the
  municipal or assistant municipal assessor") → **Approved By** (City
  Assessor, Municipal Assessor of an MMA municipality, or Provincial
  Assessor) (p.149, 152–153, 154).
- In provinces the Municipal Assessor prepares the FAAS "subject to the
  approval of the Provincial Assessor", who "may delegate the authority to
  approve appraisals and assessments to the Municipal Assessor" (p.144;
  repeated for TD p.155, 157).
- After approval: entry in the Record of Assessment (date, by whom), TD
  issuance, NA within 30 days, AR update (quarterly supplemental to the
  Treasurer unless records are computerized and connected, p.160).
- A general revision is complete only when all FAAS are prepared and duly
  approved, data recorded in the records of assessments, and notices sent
  (p.78, PDF 89).
- Appeals: to the Local Board of Assessment Appeals within 60 days of
  receipt of the NA; the Board decides within 120 days; if the assessor
  concurs in a revision, the assessor notifies the petitioner; further
  appeal to the Central Board (Ch. VII, p.173–177, PDF 184–188; Sec. 226 vs
  252 discussion p.195).
- **NOT ESTABLISHED BY REFERENCE:** a rejection/return path between the
  three roles, whether the same person may hold two roles on one FAAS, or a
  maker-checker rule in PRIME's sense.

---

## 16. Sketches, plans, photographs and supporting records [REF]

| Record | Where required | Source |
|---|---|---|
| Land sketch/plan (not necessarily to scale) | FAAS Land | p.143, 147 |
| Building floor plan or sketch; photograph optional ("may also be attached") | FAAS Building | p.150; form p.232 |
| Survey plan (approved), title copies, CENRO/NCIP/barangay certifications, affidavits, ocular inspection report | First-time land declaration | p.116 |
| Building permit, plans, certificates of completion/occupancy, inspection notice and report, affidavit/Sworn Statement | Building appraisal | p.117 |
| Owner's declared value / sworn statement; BOC/BIR/SEC documents in case of doubt | Machinery | p.119–120 |
| Sworn Statement of the True Current and Fair Market Value (Attachment 11, "Required under Section 202/203 of R.A. 7160") | Filed by owners (GR calendar: Jan 1–Jun 30 of the first year) | p.78, 243–244 |
| Exemption evidence (charters, titles, articles, by-laws, contracts, affidavits, certifications…) within 30 days | Exemption claims | p.142 |
| Signed duplicate NAs / registry return cards | Proof of NA receipt | p.136 |
| Parcellary sketches during tax mapping; aerial photographs as base-map sources | Tax mapping | p.46–57 |
| CAR details on the back of a transfer TD | TD transfer (Annex A) | p.203 |

**NOT ESTABLISHED BY REFERENCE:** digital format, resolution or retention
requirements for sketches, plans or photographs; whether a sketch must be
georeferenced.

---

## 17. Other prescribed records [REF] (brief)

- **TMCR** (Attachment 5): per section map — assessor's lot no., survey/lot
  no., title no., area (ha for agricultural/mineral/timber, sq.m. for
  others), class code, owner, ARP No., TD No., number of buildings,
  machinery ✗, others, remarks (transaction code) (p.158–159).
- **AR** (Attachments 6/7): per barangay; page no. = barangay index + sheet;
  "General Revision CY"; ARP, TD, PIN (lot no. + suffix), lot/block, owner,
  kind (L/B/M), class, assessed value, previous ARPN/TDN (taxable) or legal
  basis of exemption (exempt), effectivity, remarks (p.161–163).
- **ORC** (Attachment 8), **ROA** (Attachment 9: date, ARPN, TD no., owner,
  PIN, location, taxable/exempt land area and L/B/M market and assessed
  values, Year Taxes Begin, Transaction Code), **NA** (Attachment 10)
  (p.164–169).
- Codes (p.169–170): Kind L/B/M; Classification R, A, C, I, M, T, and
  Special SH, SC, SS, SW, SG; Use codes AR, AC, AI, AA, AM, ATF, ASH, ASC,
  ASS, ASLWD, ACH (charitable), ARE (religious), ARC (recreational), AED
  (educational), ACT (cemetery), ARK (park), government ANG/APG/ACG/AMG/ABG,
  AGOCC.
- General revision (Ch. III §1): "once every three (3) years" commencing on
  enactment of the SFMV; partial revision by kind or class allowed;
  a 9-step calendar across three years; base valuation date 1 January of
  the first year (p.77–79, 101). *(Periodicity is a prime candidate for
  change under R.A. 12001 — DOMAIN VERIFICATION REQUIRED.)*

---

## 18. What the reference does NOT establish (summary)

- A definition of "property" as an entity above the parcel/RPU — the manual
  identifies parcels by PIN and assessments by ARPN; it has no concept of a
  permanent "property" identity that survives subdivision/consolidation.
- How a building's bracketed assessment level is applied (whole vs marginal).
- General rounding rules beyond "nearest tens" for land market value.
- Transaction codes for improvement addition/removal, pure cancellation,
  non-value corrections, or address/administrator changes.
- Rejection/return steps in the approval chain; segregation-of-duties rules.
- Digital record, image, signature or retention requirements.
- Billing-side rules except where quoted incidentally (2% monthly interest
  on back taxes; Sec. 252 payment under protest).
- Anything from R.A. 12001 or later issuances.

---

## 19. Architectural concerns for PRIME [INTERP]

Everything in this section is interpretation. It identifies where PRIME's
current model (Phases 0–7, plus the uncommitted Phase 8 step-1 work) may not
be able to represent what the reference describes. No changes are proposed
here beyond naming the concern.

### 19.1 Concept mapping

| Reference concept | Closest PRIME today | Gap |
|---|---|---|
| FAAS (approved per RPU per transaction; source of all records) | `Valuation` + `Assessment` (+ `Land`/`Building`/`Machinery` current-state rows) | No FAAS aggregate; no transaction code; appraisal lines, adjustments, signatories, memoranda, ROA entry, superseded block not modelled |
| ARPN = TDN = FAAS No. | `TaxDeclaration.TaxDeclarationNumber` (unique), `RealPropertyUnit.RpuNumber` (unique) | Separate numbers; no structured ARPN format, GR indicator or per-GR count reset |
| PIN (parcel; retired on SD/CS; building/machinery postscripts) | `Property.PropertyIdentificationNumber` (unique, free text, one per Property) | PIN is per parcel and per RPU (postscripts), and changes; PRIME ties it to the long-lived `Property` |
| Multi-line land appraisal (classification/sub-class lines) and multi-line assessment (actual-use lines) | `Land` has one Classification/ActualUse/SubClassification; `TaxDeclaration` has one Classification/ActualUse; `Assessment` one AssessmentLevel | Cannot hold several appraisal or actual-use lines per RPU |
| Machinery FAAS listing several machines | `Machinery` unique per `RpuId` (Phase 6 index) | One machine per RPU |
| Other Improvements (kind, number, unit value) on the land FAAS | `RpuType.OtherImprovement` exists; no entity | Not representable |
| Building on another's land; mineral rights | Ownership (`PropertyTaxpayer`) is per `Property` | Owner of an RPU can differ from the landowner — not representable |
| Administrator / Beneficial User | `OwnershipType` lookup (values unknown) | No explicit role per RPU/declaration |
| Record of Superseded Assessment / "This declaration cancels" | `PreviousTaxDeclarationId`, `PreviousAssessmentId`, `PreviousRpuId` chains | Linkage exists; snapshot of previous owner, AR page and cancelled AV at time of cancellation not stored |
| Effectivity Qtr./Yr., cause-dependent rules | `Assessment.EffectiveDate` (date), `AssessmentYear`; `TaxDeclaration.EffectivityDate` | Representable as dates, but no rule deriving it from the transaction cause; no quarter concept |
| Three-role approval (appraised → recommending → approved) with provincial delegation | Draft → Submitted/PendingReview → Approved → Posted; maker-checker = creator ≠ approver | No "recommending approval" role/step; no jurisdiction-level approving authority |
| Notice of Assessment (mandatory; 30 days; delivery proof; appeal clock) | None | Not modelled; affects validity of assessments and the appeal window |
| Duplicate/conflicting declarations kept with notations; TD per claimant | CLAUDE.md §61 "duplicate property" validation; unique indexes | Conflicting-claim declarations may need to coexist |
| Back taxes: one declaration → several historical assessment periods on period SFMVs | Versioned SMV/AssessmentLevel exist; one Assessment per request | No "generate prior-period assessments" concept |
| Land adjustments (%-factors summed; stripping; corner; frontage) | `ValuationCalculator.CalculateLand`: Area × rate × `LocationFactor`, clamped to schedule min/max | Adjustment factors not itemised; stripping/frontage/corner not supported; min/max clamp NOT ESTABLISHED BY REFERENCE |
| Building RCNLD with SFMV depreciation schedule, additional items, per-floor materials | `CalculateBuilding`: TotalFloorArea × rate × completion; depreciation explicitly not implemented | Depreciation, extra items, materials checklist absent |
| Machinery: ≤5%/yr depreciation, **≥20% residual while useful and in operation**, RCN via exchange rates | `CalculateMachinery`: cost × remaining/economic life, **floors at 0** | Conflicts with the 20% floor quoted from LGC §225 (p.121) — DOMAIN VERIFICATION REQUIRED |
| Assessment levels for buildings by FMV bracket | `AssessmentLevel` LowerValue/UpperValue; resolution picks the single bracket containing the whole value | Whole-vs-marginal NOT ESTABLISHED BY REFERENCE |
| Levels change only at general revision; SFMV ordinance includes levels and adjustment %s | `AssessmentLevel` independently effective-dated | No constraint tying level changes to a GR |
| Taxable/Exempt per FAAS/TD, fixed as of 1 January | `TaxDeclaration.Taxability` | Jan-1 rule not enforced |
| AR / ROA / ORC / TMCR / NA outputs | Reports (Phase 11), GIS (Phase 7) | To be derived; AR quarterly supplement unnecessary if integrated (p.160) |
| Condominium units, common areas, aggregated capital equipment | Building per RPU | Unit/common-area structure not modelled |
| Linear infrastructure apportioned across LGUs | Single-LGU assumption | Apportionment not modelled |

### 19.2 Principal architectural concerns

1. **Missing aggregate: the FAAS.** The reference treats the approved FAAS
   as the single source record per assessment transaction. PRIME's
   `Valuation`/`Assessment`/`TaxDeclaration` spread its content and lack
   several of its fields. This is the largest structural gap.
2. **Identity: PIN is not PRIME's `Property` identity.** The PIN is a parcel
   (and RPU) identifier that is retired and reissued on subdivision and
   consolidation. PRIME's rule that `Property` is the long-lived identity
   still holds, but `Property.PropertyIdentificationNumber` (unique, one per
   property) conflates the two. PINs likely belong to parcels/RPUs with
   history.
3. **Cardinality.** Several PRIME 1:1 constraints contradict the forms:
   machinery per RPU; single classification/actual use per land record and
   per TD; single assessment level per assessment.
4. **Ownership at RPU level.** A building or machinery may belong to someone
   other than the landowner, and mineral rights can be separate. PRIME's
   ownership is per property.
5. **Effectivity is rule-driven.** The effective date follows from the
   transaction cause (Jan 1 next year vs. next quarter vs. GR date vs.
   back-dated periods); PRIME stores dates but does not derive or validate
   them. The ownership/subdivision rule also links to billing ("current
   assessment … fully paid").
6. **Workflow roles.** The reference's workflow is appraiser → recommending
   officer → approving assessor (with provincial delegation), then ROA
   entry, TD, NA. PRIME's status machine has no recommending step and
   treats approval as a generic permission.
7. **Legal validity depends on the NA.** Without Notice of Assessment
   issuance and delivery tracking, PRIME cannot show an assessment is valid
   or when the 60-day appeal window started.
8. **Conflicting declarations.** The reference expects multiple TDs over the
   same property to coexist with cross-notations; PRIME's data-quality
   rules treat duplicates as errors to prevent.
9. **Valuation engine gaps.** Itemised SFMV adjustment factors, stripping,
   corner/frontage, building depreciation schedules and extra items,
   machinery RCN and the 20% residual floor.
10. **Numbering formats** (PIN, ARPN with GR indicator, NA numbers) are
    structured and jurisdiction-derived; they should be configurable, not
    hard-coded, and LGUs may keep legacy numbering until their next GR.

### 19.3 Observations relevant to in-progress Phase 8 (Billing)

- The reference quotes 2% per month "or a fraction thereof" for interest on
  back taxes (p.109, 197) — consistent with `docs/BILLING.md`'s choice to
  make partial-month counting configurable; it is a data point, not a
  decision.
- Effectivity by quarter (reassessment for abnormal causes) means a tax year
  can have **two assessed values** (before/after the quarter), which the
  billing design (one assessment per RPU per tax year) does not yet cover.
- Transfer TDs require realty tax fully paid (Annex A) — a billing→assessment
  dependency.

---

## 20. Questions requiring domain / legal verification

1. Which provisions of this 2004/2006 manual were changed by R.A. 12001
   and its IRR (including GR periodicity, effectivity, assessment-level
   ceilings, SFMV/"SMV" terminology and approval)?
2. Has BLGF issued a newer manual or revised FAAS/TD formats since 2006
   that supersedes Attachments 1–4?
3. Does the target LGU use the PIN system and ARP numbering exactly as
   prescribed, or a legacy scheme it may retain until its next GR (p.170)?
4. Is the LGU a province (provincial approval with possible delegation), a
   city, or an MMA municipality — i.e. who is the approving officer?
5. For buildings, is the bracketed assessment level applied to the whole
   market value or marginally?
6. Is the machinery 20% residual-value floor (LGC §225 as quoted) still
   the rule, and how is "useful and in operation" evidenced?
7. What rounding rules apply to market value, assessed value and tax?
8. Which transaction code applies to adding or removing an improvement,
   correcting non-value data, or cancelling a declaration outright?
9. Must PRIME issue TDs to multiple claimants of untitled land and keep
   annotated duplicate declarations, and how are they resolved?
10. Is Notice of Assessment delivery evidence required to be kept
    electronically, and does electronic service satisfy Sec. 226 today?
11. Are CAR/capital-gains-tax and transfer-tax checks still prerequisites to
    a transfer TD (Annex A, 2004), and must CAR details be recorded?
12. Must the Record of Superseded Assessment keep a snapshot of the previous
    owner's name even when the owner record is later updated?
13. How are condominium common areas, parking and aggregated capital
    equipment to be declared under the LGU's current practice?
14. Does the LGU have linear infrastructure (transmission lines, pipelines,
    rail) whose value is apportioned with neighbouring LGUs?
15. Retention periods and format requirements for cancelled FAAS/TDs,
    sketches, plans and photographs.
