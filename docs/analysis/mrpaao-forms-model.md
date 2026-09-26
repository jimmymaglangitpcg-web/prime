# PRIME — Forms Model from the MRPAAO (2004/2006)

Status: **all seven steps implemented** (§6–§17): step 1 `c5b3f8d`, steps
2–3 `81ce318`, steps 4–5a `f9376c6`, step 5b `29e0d34`, step 6 `e7f3ad8`
(2026-09-25); step 7, the sworn statement, 2026-09-26 (§16–§17).

Decision (user, 2026-09-25): the forms follow the **Manual on Real
Property Appraisal and Assessment Operations** (`docs/References/ManualRPAandAO.pdf`,
DOF-BLGF, Local Assessment Regulations No. 1-04, 1 Oct 2004; cover
January 2006). This answers docs/FORMS-REVISION-PLAN.md §9 decision 2.

Citations: "p.N" is the manual's printed page; the PDF page is N + 11.
Field lists for the FAAS and TD are also in
docs/analysis/blgf-appraisal-assessment-analysis.md §2–§8.

---

## 1. Scope rule

The manual is **superseded by the Local Assessment Manual** (DOF DC
004-2025, not yet obtained; see
docs/analysis/current-real-property-regulatory-baseline.md). So:

| Taken from the manual | Not taken from the manual |
|---|---|
| Form layouts, field lists, row structure | Rates, percentages, depreciation tables |
| How records relate (FAAS → TD → rolls) | "Market value rounded to the nearest tens" (p.147) |
| Numbering conventions (PIN, suffixes) as configurable formats | The three-signatory chain as a hard rule |
| Transaction codes and ranks, as catalogue data | Any value presented as current law |

Rules the manual states are **configurable settings**, off or DEMO by
default, marked DOMAIN VERIFICATION REQUIRED. Forms built from it carry the
authority **"MRPAAO 2004 (superseded) — reference layout"** until the LAM
arrives. The manual is a public issuance, so its layouts may be committed
(FORMS-REVISION-PLAN §7).

---

## 2. How the manual models records

- "The FAAS when approved is the source of information for all other
  assessment records" (p.143). TD, AR, ORC, ROA and NA are "extracted from
  the FAAS" (p.156, 161, 165, 169).
- "A FAAS is prepared each time there is an assessment transaction"
  (p.144); "Only one FAAS shall be prepared for each real property unit
  declared under an existing and active Tax Declaration" (p.50).
- FAAS No. = ARP No. = TD No.: "Indicate the TD number which is also the
  FAAS number" (p.156); "the Tax Declaration Number which is the ARPN"
  (p.159).
- A TD is prepared on general revision, new discovery, change in area,
  classification or ownership, physical change, or change in location
  (p.156). **A transfer produces a new TD and a new FAAS even when the value
  does not change.**

Three families of forms:

| Family | Forms | PRIME treatment |
|---|---|---|
| Records of one assessment transaction | FAAS Land (Att. 1), FAAS Building (Att. 2), FAAS Machinery (Att. 3), TD (Att. 4), NA (Att. 10) | Issued forms rendered from a read model (the A7/A8 design) |
| Owner's declaration (input) | Sworn Statement (Att. 11; LGC §202) | New record + form |
| Registers | TMCR (Att. 5), AR Taxable (Att. 6), AR Exempt (Att. 7), ORC (Att. 8), ROA (Att. 9) | Dated reports from posted records, frozen when issued; never hand-kept tables |

Attachment 12 (GR Forms 1–2) is SMV preparation, not assessment records;
it belongs to the SMV admin work, outside this model.

---

## 3. Field mapping

Legend: ✅ stored · ➗ derivable from stored data · ⚠ stored in a
narrower shape · ❌ missing. "Step" is the §5 step that closes the gap.

### 3.1 Blocks common to the three FAAS (Att. 1–3; p.145–155)

| Field | Status | Step |
|---|---|---|
| Transaction Code (highest rank when several) | ➗ catalogue has code + rank; ❌ not tied to the FAAS | 4 |
| ARP No. | ⚠ separate `FaasNumber` and TD number | 1 |
| PIN, incl. building `1001…`, machinery `2001…`, `(LL)` for another owner | ✅ land PIN; ❌ unit suffixes | 1 |
| OCT/TCT/CLOA No. + date; Survey No.; Lot; Blk | ✅ number, survey, lot, blk; ❌ title kind, title date | 3 |
| Owner / Administrator-Beneficial User: name, address, TIN, tel | ✅ (party roles, A4) — per property only | 1 |
| Location | ✅ | — |
| Boundaries N/E/S/W | ❌ | 3 |
| Land sketch / floor plan | ➗ sketch from parcel geometry; ❌ floor-plan attachment | 3 |
| Taxable / Exempt | ✅ TD taxability | — |
| Effectivity Qtr./Yr. | ➗ from effective date | 5 |
| Appraised/Assessed By; Recommending Approval; Approved By | ✅ approval chain (configure 3 steps) | — |
| Memoranda | ✅ remarks | — |
| Date of Entry in the Record of Assessment; By | ❌ posted-at/by | 4 |
| Record of Superseded Assessment: PIN, ARP, TD, total AV, previous owner, effectivity, recorder | ➗ previous FAAS | 1 |
| … AR Page No. | ❌ exists once AR is issued | 6 |

### 3.2 FAAS Land / Other Improvements (Att. 1)

| Block | Status | Step |
|---|---|---|
| Land appraisal rows: classification, sub-classification, area, unit value, base MV | ⚠ one Land per RPU, one classification | 2 |
| Other improvements rows (trees/plants): kind, number, unit value, base MV | ❌ | 2 |
| Market value adjustment rows: factor, %, value adjustment | ⚠ one location factor + corner/road fields | 2 |
| Property assessment rows by actual use: MV, level, AV | ⚠ one actual use per assessment | 2 |

### 3.3 FAAS Building (Att. 2)

| Block | Status | Step |
|---|---|---|
| Land reference: land owner, title, survey, lot, blk, land TD/ARP, land area | ➗ once the building names its land unit | 1 |
| Building owner ≠ land owner | ❌ owners are per property | 1 |
| Kind of bldg, structural type, storeys, total floor area | ✅ | — |
| Bldg. permit no. + date; CCT; certificates of completion / occupancy; dates constructed/completed, occupied; age | ❌ (years only) | 3 |
| Area of 1st–4th floor | ❌ | 3 |
| Structural materials checklist (part × material × floor) | ❌ | 3 |
| Additional items (fence, gate, garage, balcony, mezzanine…) | ⚠ components without an "additional" flag | 3 |
| Appraisal: unit construction cost, building core, additional items, total construction cost, depreciation rate / %, depreciation cost, MV | ⚠ no depreciation (plan A9), additional items not valued | 2, A9 |

### 3.4 FAAS Machinery (Att. 3)

| Block | Status | Step |
|---|---|---|
| Land owner + PIN; building owner + PIN | ❌ machinery not tied to a building | 1 |
| One row per machine | ⚠ one Machinery per RPU | 2 |
| Kind, brand & model, capacity, date acquired, new/second hand, economic life estimated/remaining | ✅ | — |
| Year installed; year of initial operation | ❌ | 3 |
| Original cost, conversion factor, RCN, years used, depreciation rate, total depreciation, depreciated value | ✅/➗ breakdown; ❌ conversion factor | 3 |

### 3.5 Tax Declaration (Att. 4; p.155–157)

| Field | Status | Step |
|---|---|---|
| TD No. (= ARP), PIN, owner, administrator, location | ✅ | — |
| OCT/TCT/CLOA/CCT No., date; boundaries | ❌ kind/date, boundaries | 3 |
| Kind of property assessed + brief description; "Others (specify)" | ➗ | 5 |
| Rows: classification, area, MV, actual use, level, AV; totals | ⚠ one row | 2 |
| Total AV in words | ➗ template filter | 5 |
| Taxable/Exempt; effectivity Qtr./Yr.; Approved By | ✅/➗ | — |
| "This declaration cancels TD No. / Owner / Previous A.V." | ➗ | 1 |
| Memoranda | ✅ | — |
| Note: Sangguniang ___, Ordinance No. ___ dated ___ | ✅ SMV ordinance; ❌ Sanggunian type | 3 |
| Back: BIR CAR details on transfer (Annex A, MC 18-2004) | ⚠ evidence reference only | 3 |

### 3.6 Registers

| Register | Contents | Gaps | Step |
|---|---|---|---|
| **TMCR** (Att. 5; p.158–159) — per tax-map section | Prov/Mun/Brgy with index nos.; section index; rows: assessor's lot no., survey/lot no., title no., area, class code, owner, ARP, TD, no. of bldgs, machinery ✕, others, remarks (transaction code) | ❌ index numbers, section; derivable from a manual-format PIN | 6 |
| **AR Taxable** (Att. 6; p.160–162) | Page no. = brgy index + sheet; GR year; date prepared; rows: ARPN, TD, PIN (assessor's lot + suffix), lot/blk, owner, owner address, kind L/B/M, class code, AV, previous ARPN/TDN, effectivity, remarks | ❌ index numbers; quarterly supplements = dated issues | 6 |
| **AR Exempt** (Att. 7) | as above, with **legal basis of exemption** instead of previous ARPN/TDN | ❌ exemptions not modelled | 6 (+ exemptions) |
| **ORC** (Att. 8; p.164–166) — per owner | Name, address, tel, TIN, date prepared, LGU + index; rows: date of entry, kind, class code, PIN, title, lot/blk, ARP, TD, previous owner, location, area, MV, AV, remarks; totals | ❌ index number | 6 |
| **ROA** (Att. 9; p.166–167) — by barangay, classification, page | Rows: date, ARPN, TD, owner, PIN, location; taxable and exempt × L/B/M: land area, MV, AV, year taxes begin; transaction code; balance forwarded, subtotals | ❌ transaction code, posting date | 4, 6 |

### 3.7 Notice of Assessment (Att. 10; p.167–169)

NA No.; owner name and address; date; tax year; **rows**: ARPN, TDN, PIN,
location, classification, MV, AV; total; assessor's signature; printed
notes citing LGC §223/§226. Prepared on first assessment, general
revision, increase or decrease, **and on change of declared owner, owner's
address, or property location**.

Gaps: a PRIME notice covers one assessment (⚠) and is prepared only for
first assessment or a value change (⚠). Step 5.

### 3.8 Sworn Statement (Att. 11; LGC §202–203)

Declarant, citizenship, civil status, postal address, capacity (owner /
administrator / authorized representative), LGU; row sets for **land**
(existing TD or "NEW", lot, block, cadastral/PLS no., title, area,
classification, location, declared FMV), **buildings** (TD, floor area,
storeys, description, year completed/occupied, actual use, lot owner,
location, declared FMV), **machinery** (TD, description, date acquired,
date operation commenced, original cost, installation cost, depreciation,
location, declared FMV), **other improvements** (TD, kind, productive /
non-productive counts, annual product, ages, declared FMV); witnesses;
jurat (date, place, CTC no., administering officer). ❌ entirely. Step 7.

---

## 4. Structural decisions the manual settles

| # | Decision | Manual's answer | Where |
|---|---|---|---|
| 1 | Numbers | FAAS = ARP = TD number | p.156, 159 |
| 2 | Rows | Multi-row land, improvements, adjustments, actual-use assessment, machines | Att. 1–4 |
| 3 | Ownership | Per unit — a building/machine may belong to someone other than the landowner | p.42, Att. 2–3 |
| 4 | Unit links | Building → land; machinery → building | p.42, 145, Att. 2–3 |
| 5 | Transaction code | On every FAAS; highest rank | p.145, 167, 170 |
| 6 | Index numbers, sections | Required by TMCR, AR, ORC, ROA, PIN | Ch. II, p.158–167 |
| 7 | Registers | Derived from the FAAS | p.143, 161, 165 |
| 8 | Descriptive fields | All of §3 | Att. 1–4 |

This also reverses the A8 choice of one FAAS form: the manual has three,
so FAAS becomes `FAAS_LAND`, `FAAS_BUILDING`, `FAAS_MACHINERY`.

---

## 5. Implementation steps (outline)

Each step is designed and reviewed before code, per the phase rules.

1. **Identity and structure** — the FAAS record, shared numbering, unit
   PIN suffixes, unit links, per-unit parties. Designed in §6.
2. **Multi-row appraisal and assessment** — land strips, other
   improvements, adjustment-factor rows, assessment lines by actual use,
   several machines per machinery FAAS. Ripples into valuation, billing
   (tax by line classification) and general revision. Largest step.
3. **Descriptive fields** — boundaries; title kind and date; building
   permit and certificate dates, per-floor areas, structural materials
   checklist (configurable parts and materials), additional items;
   machinery year installed, year of initial operation, conversion factor;
   LGU Sanggunian type; structured CAR fields on transfers; attachments
   (floor plan, photographs) via document storage.
4. **Transaction code and posting stamp** — derived code per FAAS,
   posted-at/by as the ROA entry.
5. **Form templates** — FAAS ×3, TD, NA (multi-property, and issued for
   owner/address/location changes), under the MRPAAO reference authority.
6. **Registers** — TMCR, AR taxable/exempt, ORC, ROA as issued, dated
   reports; LGU and barangay index numbers, tax-map sections. AR Exempt
   waits for exemptions.
7. **Sworn Statement** — record, intake screen, form.

---

## 6. Step 1 design — identity and structure (approved 2026-09-25)

### 6.1 What a FAAS is in PRIME

The manual's FAAS is one record per RPU per assessment transaction, with
the TD's number. PRIME today has the pieces but not the record:
`Assessment` (value, created on valuation events), `TaxDeclaration`
(created by transactions), and no link between them. A transfer creates a
new TD but no new assessment, so under today's model there is no new FAAS
for a transfer — the manual requires one.

**Proposal: the FAAS is the Tax Declaration together with the assessment
it declares.**

- `TaxDeclaration.AssessmentId` (new, nullable while Draft): the
  assessment this TD declares. Required to approve a TD once a setting
  `Faas:RequireAssessmentOnTd` is on (off for legacy data).
- New assessment (discovery, reassessment, general revision) → new TD
  pointing to it. Transfer, correction of owner/address → new TD pointing
  to the **same** assessment. Both are new FAAS, as in the manual.
- The **FAAS number is the TD number** when the numbering setting
  `Faas:NumberSource = TaxDeclaration` (the manual's convention). Setting
  `Own` keeps today's separate `Faas` scheme for LGUs that differ.
  `Assessment.FaasNumber` stays for `Own` and for the A7 history.
- The FAAS form subject becomes the TD (form data: the TD, its assessment
  and valuation, parties as of the TD's effectivity). "Record of
  Superseded Assessment" = the previous TD, its assessment and its owners.
  The A7 appraisal record stays as the assessment's calculation view.

Consequence to confirm: posting a reassessment or general revision should
**prepare a draft TD** for each affected RPU (manual p.156: a TD is
prepared on general revision). Today they create assessments only.

### 6.2 Unit PIN suffixes (p.42)

- `RealPropertyUnit.PinSuffix` (new, nullable): Land none; Building
  `1001`, `1002`…; Machinery `2001`…; assigned on RPU creation as the next
  in the property for its type; never reused (retired with the RPU).
- Unit PIN = property PIN + `-` + suffix; when the unit's owner differs
  from the land's owner, the parcel number is parenthesised
  (`020-15-0005-002-(05)-1001`). Computed, not stored, so it follows
  ownership changes. Formats are configurable (a PIN scheme may differ).
- Out of scope: condominium unit postscripts `(1)`, `(2)`; mineral rights
  `3001` (no RPU type yet).

### 6.3 Unit links

- `RealPropertyUnit.LandRpuId` (new, nullable): for Building, Machinery
  and OtherImprovement units, the land unit they stand on — same property.
  Gives the building FAAS its Land Reference block.
- `RealPropertyUnit.HostRpuId` (new, nullable): for Machinery, the
  building unit it is installed in (Att. 3 "Building Owner + PIN").
- Both effective-dated through the RPU's own history: moving machinery
  creates a new RPU with `PreviousRpuId` (the manual reassesses on change
  of location, p.156).

### 6.4 Per-unit parties

- `PropertyTaxpayer.RpuId` (new, nullable). Null = party of the whole
  property (today's rows, unchanged). Set = party of that unit only.
- A unit's parties = its own rows if any are current, else the property's.
  Ownership shares must total 100% **per scope** (property, or unit).
- A5 transfers gain an optional unit target, so a building can change
  hands without the land.
- The parenthesised PIN in §6.2 is computed from this.

### 6.5 Data changes (one additive migration)

| Table | Change |
|---|---|
| `TaxDeclarations` | + `AssessmentId` (FK, nullable, indexed) |
| `RealPropertyUnits` | + `PinSuffix` (unique per property when set), + `LandRpuId`, + `HostRpuId` (FKs, nullable; check: not self) |
| `PropertyTaxpayers` | + `RpuId` (FK, nullable, indexed) |
| `PropertyTransactionParties` (A5) | + `RpuId` (nullable) |

No data is rewritten. Existing rows keep their meaning (null = today's
behaviour).

### 6.6 Decisions (user, 2026-09-25: "yes to all")

1. FAAS = TD + the assessment it declares.
2. Posting an assessment prepares a Draft TD declaring it.
3. `Faas:NumberSource = TaxDeclaration` by default.
4. Per-unit parties, not a separate property.

**DOMAIN VERIFICATION REQUIRED** (manual superseded): whether the LAM
keeps FAAS = ARP = TD numbering, the PIN postscript convention, and a new
FAAS on every transfer.

---

## 7. Step 1 — implementation status (2026-09-25)

**Settings** (`appsettings.json`; each a policy from the superseded manual,
DOMAIN VERIFICATION REQUIRED):

| Key | Default | Effect |
|---|---|---|
| `Faas:NumberSource` | `TaxDeclaration` | FAAS number = TD number. `Own`: the `Faas` numbering scheme, assigned on assessment approval (the A7 behaviour) |
| `Faas:RequireAssessmentOnTd` | `false` | When true, a TD cannot be approved without an assessment |
| `Faas:PrepareTdOnPosting` | `true` | Posting an assessment prepares a Draft TD declaring it |
| `UnitPin:SuffixStart` | `Building: 1001`, `Machinery: 2001` | Postscript series per unit type; a type with no entry gets none |

**FAAS = TD + assessment.**
- `TaxDeclarations.AssessmentId`. On creation, a TD declares the named
  assessment (it must be the same RPU's, Approved or Posted:
  `TAX_DECLARATION_ASSESSMENT_INVALID`). Otherwise it declares the
  assessment in force on its effectivity date. So a transfer's TD
  declares the unchanged assessment: a new FAAS without a new valuation.
- A TD drafted before its assessment existed is bound on approval, by the
  same rule (`TaxDeclarationApproval.CheckAsync`, shared with transaction
  approval). With `RequireAssessmentOnTd`, a TD that still declares none
  is refused (`TAX_DECLARATION_ASSESSMENT_REQUIRED`).
- The TD DTO carries `assessmentId` and `faasNumber`: the TD number, or
  under `Own` the assessment's number. It is null while no assessment is
  declared.
- Posting an assessment prepares a Draft TD in the same database
  transaction. The TD replaces the unit's current TD and declares the
  assessment. It takes the classification and actual use of the
  assessment level applied, and copies taxability, and the sub-class when
  the class is unchanged. None is prepared (logged) when:
  - the unit has no current TD (its first TD is declared by hand);
  - a TD already declares the assessment;
  - no TD numbering scheme is in force (a TD cannot exist unnumbered).
- General revision creates Draft assessments that go through the same
  Post, so it gets the same behaviour.

**Unit PIN postscripts.**
- `RealPropertyUnit.PinSuffix` holds the next number in the series for
  the property and type. It is unique per property
  (`UX_RealPropertyUnit_Property_PinSuffix`) and never reused. A
  concurrent creation retries with the next number.
- The DTO's `unitPin` = property PIN + `-` + suffix. When the unit has
  its own current owners, the PIN's last segment is parenthesised:
  `DEMO-BILL-(AE94B8)-1001`.
- **Existing units are not renumbered.** Units created before this change
  have no postscript, as numbering schemes never renumber existing records.

**Unit links.** `LandRpuId` (building, machinery or other improvement →
a Land RPU of the same property) and `HostRpuId` (machinery → a Building
RPU of the same property). Errors: `RPU_LAND_LINK_INVALID`,
`RPU_HOST_LINK_INVALID`. Check constraints forbid self-links.

**Per-unit parties.**
- `PropertyTaxpayers.RpuId`, for a building, machinery or
  other-improvement unit only. The land's parties are the property's
  (`PROPERTY_PARTY_UNIT_INVALID`).
- Share totals, duplicates and the unknown-owner rule apply per scope. The
  unknown-owner unique index is now on `(PropertyId, RpuId)` with
  `NULLS NOT DISTINCT`.
- `PropertyParties.ScopeAsync` resolves a unit's parties: its own rows if
  it has an owner or unknown-owner row, otherwise the property's. The
  appraisal record, the Notice of Assessment addressees, and the TD and
  tax bill forms use it.

**Deviation from §6.5.** The unit targeted by a transfer is on the
transaction (`PropertyTransactions.TransferRpuId`), not on each new party,
since all parties of one transfer share its scope. A unit transfer ends
only that unit's owners. When the unit had none of its own, it ends
nothing, and the unit passes to the new owners while the land stays with
the property's owners (`TRANSACTION_UNIT_INVALID` for land or another
property's unit).

**UI.**
- RPUs table: a Unit PIN column, with a tooltip when the unit is owned
  apart from the land, and a "Stands on / installed in" column.
- Add RPU: land and host selectors.
- TD table: a FAAS No. column.
- Owners: a Holds column (whole property or one unit only), and a unit
  selector in Add Party.
- New transfer: a unit selector.

**Verified:**
- 7 new integration tests (`FaasIdentityTests`):
  - the TD declares the assessment in force, and its FAAS number is the
    TD number;
  - another unit's assessment is refused;
  - posting prepares a Draft TD;
  - posting with no TD scheme prepares nothing;
  - postscripts 1001/1002/2001 and link validation;
  - unit owners: scoped shares, the parenthesised PIN, and a unit transfer
    that leaves the land owner.
- The A5 transfer test now asserts that the transfer TD declares the
  existing assessment.
- The A7 numbering test runs with `Faas:NumberSource=Own`.
- The HTTP registration flow creates a linked building and a unit owner.
  This caught a real bug: the owners endpoint dropped `rpuId`.
- Full suite passes (90 domain, 37 application, 113 integration).
- Production frontend build and lint pass.
- In the browser on `DEMO-BILL-AE94B8`:
  - a building RPU was created on the land (Unit PIN
    `DEMO-BILL-AE94B8-1001`);
  - after adding a building-only owner, it showed
    `DEMO-BILL-(AE94B8)-1001`;
  - the owners table shows Whole property vs. RPU only;
  - the TD table shows the FAAS No. column;
  - no console errors.

Migration `FaasIdentityAndUnits` is additive. It adds 6 nullable columns,
FKs, indexes and 3 check constraints, and recreates the unknown-owner
index with the unit in its key. It is applied to the local dev DB only.
The dev DB now also holds a DEMO building RPU (`DEMO-BLDG-562763`) with a
building-only owner.

---

## 8. Step 2 design — multi-row appraisal and assessment (approved 2026-09-25)

### 8.1 What the manual needs, and what PRIME has

| Manual (Att. 1–3) | PRIME today |
|---|---|
| Land appraisal: several strips (classification, sub-class, area, unit value, base MV) | One `Land` per RPU with one classification (unique index on `RpuId`) |
| Other improvements: trees/plants (kind, number, unit value, base MV) | Nothing |
| Market value adjustments: several factors (factor, %, value adjustment) | One `LocationFactor` multiplier |
| Building: core by unit construction cost, plus additional items; assessment rows by actual use (mixed use) | One area × one SMV rate; components carry cost but are not valued |
| Machinery: one row per machine on one FAAS | One `Machinery` per RPU (unique index) |
| Property assessment: one row per actual use (MV, level, AV), then totals | One market value, one level, one assessed value |
| TD: rows of classification, area, MV, actual use, level, AV | One classification and actual use on the TD |

Everything downstream reads the single row: billing (`AssessedValue` plus
the TD's classification, which picks classification-specific rates), the
appraisal record, the Notice of Assessment, the forms, and general
revision.

### 8.2 The model: lines at each stage

```text
RPU
 └─ Valuation (one per valuation run; totals)
      └─ ValuationLine*   one per strip / improvement / building portion / machine
           classification, sub-class, actual use, quantity + unit, unit value,
           base value, adjustments, market value, own breakdown, source row
 └─ Assessment (totals; FAAS header)
      └─ AssessmentLine*  one per (classification, actual use)
           market value = Σ its valuation lines, assessment level (resolved per line),
           assessed value
```

- **`ValuationLine`**: the unit of calculation. `Valuation.ComputedMarketValue`
  = Σ lines. Each line keeps its own breakdown (jsonb) and the SMV
  schedule it used, so each FAAS appraisal row is reproducible (CLAUDE.md
  §31). `Valuation.SourceType` stays (land/building/machinery).
  `SourceId` becomes nullable, because a machinery FAAS has several sources.
- **`AssessmentLine`**: valuation lines grouped by (classification, actual
  use) — the manual's "Property Assessment" rows. Each group gets its own
  assessment level: classification, actual use, property type and bracket.
  `Assessment.MarketValue` and `Assessment.AssessedValue` = Σ lines.
  `AssessmentLevelId` and `AssessmentPercentage` become nullable: set when
  there is one line, null for a mixed-use FAAS, where the lines carry them.
- **Rounding**: assessed value per line to centavos (today's rule), and the
  total is the sum of rounded lines. DOMAIN VERIFICATION REQUIRED (the
  manual's "nearest tens" stays a switch, off).
- **Bracket basis** (DOMAIN VERIFICATION REQUIRED): the market value the
  level bracket is looked up with. Setting `Assessment:LevelBracketBasis` =
  `Line` (default: each line's own MV) or `Unit` (the RPU's total MV,
  applied to every line). Whole-value (not marginal) application stays as
  today.

### 8.3 Inputs per kind

**Land** (Att. 1):
- `Land` stays the land record: location, road, corner lot, zoning, and
  one row per land RPU.
- New `LandStrip*`: classification, sub-class, actual use, zone, area,
  area unit. Valued by the SMV schedule for its classification, actual
  use and zone.
- New `LandImprovement*` (trees, plants and other non-building
  improvements):
  - improvement kind (new lookup `ImprovementKind`), number,
    productive/non-productive, actual use;
  - valued by an SMV schedule row for that kind. `SmvSchedule` gains a
    nullable `ImprovementKindId` and a unit such as "per tree".
- New `LandAdjustment*`, one row per factor:
  - it references an adjustment factor from a new configurable
    `AdjustmentFactor` catalogue, belonging to an SMV: code, name,
    percent, and which property type or classification it applies to;
  - the percent is frozen on the row. Value adjustment = base value ×
    Σ% / 100.
  - The manual's examples (corner influence, road, distance to market,
    blighted area) are data, never code.
  - Adjustments apply per strip, or to all strips (a nullable `LandStripId`).
- `Land.Area`, `ClassificationId`, `ActualUseId` and `SubClassificationId`
  become the **principal strip's** mirror (kept in step by the service),
  so existing readers keep working.
- `LocationFactor` stays for rows that already carry it, applied as today.
  New entries use adjustment rows.

**Building** (Att. 2):
- New `BuildingUsePortion*`: actual use, classification, floor area (Σ =
  total floor area). A mixed-use building gives one assessment line per use.
- Valuation: unit construction cost (the SMV rate for the building) ×
  portion area = building core, then + additional items and × completion.
  - Additional items are `BuildingComponent` rows flagged
    `IsAdditionalItem` with a cost, added to the portion they belong to,
    or spread by area.
  - Depreciation stays absent until plan A9 supplies a configurable table.
- A building with no portions values as one portion with the TD's
  classification and actual use, which is today's behaviour.

**Machinery** (Att. 3):
- Drop the one-machine-per-RPU rule, so a machinery RPU holds several
  `Machinery` rows.
- Each gets `ActualUseId` and `ClassificationId` (default: the TD's) and
  becomes one valuation line (LGC §224–225, unchanged).

### 8.4 Downstream

| Consumer | Change |
|---|---|
| `ValuationService` | New `ComputeForRpuAsync(rpuId)`, used by general revision and the UI. It builds all lines for the RPU in one valuation. The per-entity methods remain and value the whole RPU. |
| `AssessmentService.CreateAsync` | Unchanged request. It groups the valuation lines into assessment lines and resolves a level per line. Refused, with the line named, if any line has no level. |
| Billing | `BillingCalculationInput` takes assessment lines (classification, assessed value). Per tax type: tax = Σ line AV × rate for that line's classification. The increase cap applies to the tax type's total. New `TaxBillTaxTypeLine` rows keep each line's base, rate and tax for the breakdown. `TaxBill.AssessedValue` = total. `TaxBill.ClassificationId` = the principal line's. |
| TD | The TD keeps its principal classification and actual use (the largest line by market value). Its rows come from its assessment's lines. The prepared TD (step 1) takes the principal from the lines. |
| Appraisal record (A7), NA, forms | They carry the lines. Templates render rows (step 5). |
| General revision | Uses `ComputeForRpuAsync`, so every kind and every line is revalued together. |

### 8.5 Existing data (migration)

The migration is additive, plus one data step that derives lines from
what is already stored:
- Each existing `Valuation` gets one `ValuationLine` with the same
  market value and breakdown.
- Each existing `Assessment` gets one `AssessmentLine` with the same market
  value, level, percent and assessed value.
- Each existing `Land` gets one `LandStrip` from its area, classification
  and actual use.

No value changes. Every assessment then has at least one line, so readers
can rely on lines. The migration drops the unique index on
`Machinery.RpuId`; that is the only constraint removed.

### 8.6 Delivery in four parts (each built, tested and shown to you)

1. **2a — lines core**:
   - `ValuationLine` and `AssessmentLine`, the backfill,
     `ComputeForRpuAsync`, and assessments built from lines;
   - billing per line, and the appraisal record, notice and bill carrying
     lines;
   - general revision switched over;
   - UI: the assessment lines table under each RPU.

   No new inputs yet, so every existing unit gets exactly one line.
2. **2b — land strips, improvements, adjustments**: the three land tables,
   the `ImprovementKind` and `AdjustmentFactor` catalogues (with maker-checker
   approval like other configuration), `SmvSchedule.ImprovementKindId`, and
   the land UI.
3. **2c — building use portions and additional items**.
4. **2d — several machines per machinery RPU**.

### 8.7 Decisions (user, 2026-09-25: "agree to all")

1. Assessment lines are grouped by (classification, actual use), with a
   level per line.
2. The default bracket basis is `Line`.
3. Tax is computed per line and summed per tax type, with the increase
   cap on the total.
4. `LocationFactor` is kept for legacy rows only.
5. Delivered in the order 2a → 2b → 2c → 2d.

**DOMAIN VERIFICATION REQUIRED**: the bracket basis; rounding per line
vs. per total; whether adjustment factors add (Σ%) or compound; how
additional items and depreciation combine for buildings (A9); and the
productive / non-productive treatment of trees.

---

## 9. Step 2 — implementation status (2026-09-25)

**2a — lines core.**
- **New tables:** `ValuationLines`, `AssessmentLines` and
  `TaxBillTaxTypeLines`.
- **Assessment:** `AssessmentLevelId` and `AssessmentPercentage` are now
  nullable, and set only for a single-line assessment (check constraint
  `CK_Assessments_Level`).
- **Assessment creation:** `AssessmentService.CreateAsync` groups a
  valuation's lines by (classification, actual use). A line without its
  own classification or use takes the unit's TD's. Each group gets its own
  level. The bracket basis is the `Assessment:LevelBracketBasis` setting,
  `Line` by default. A group without a level is refused and named
  (`ASSESSMENT_LEVEL_NOT_FOUND`).
- **Billing calculator:** takes `Lines`. Each line is taxed at the rate
  for its classification (class-specific rate first, else the general
  one) and rounded, and the lines are summed per tax type. The increase
  cap applies to the tax type's total. The bill keeps the per-line tax
  (`TaxBillTaxTypeLine`), and the tax type names the principal line's rate.
- **Other readers:** `ValuationService.ComputeForRpuAsync` values a whole
  unit, and general revision uses it. The appraisal record, the TD form
  data, the bill DTO and the prepared TD (step 1) read the lines. The
  prepared TD takes the principal line's classification and use.
- **Existing records:** the migration gave every existing valuation,
  assessment and bill tax row one line with the same values. This was
  verified on the dev DB: 0 mismatches.

**2b — land.**
- **New tables:** `LandStrips`, `LandImprovements` and `LandAdjustments`,
  plus the `ImprovementKinds` lookup and the `AdjustmentFactors`
  configuration.
  - Adjustment factors are versioned per (SMV, code) with maker-checker
    approval, through `/api/adjustment-factors`.
  - `SmvSchedule.ImprovementKindId` holds rates for trees and plants.
    The schedule versioning key now includes it.
- **Strips and improvements:**
  - A new land starts with strip 1 from its registration.
  - `POST /api/land/{id}/strips|improvements|adjustments` add rows.
  - Land keeps the total area and mirrors the principal strip's
    classification.
  - Each strip is priced by its own SMV rate.
  - Each improvement is priced by the rate for its kind, under its own
    classification and use or the principal strip's.
- **Adjustments:** they add (Σ%), then the legacy `LocationFactor`
  applies, then the schedule limits.
  - **Deviation from §8.3:** an adjustment names the factor by **code**.
    Valuation takes the version in force under the SMV that prices the
    strip (`ADJUSTMENT_FACTOR_NOT_FOUND` otherwise). So a general revision
    under a new SMV applies the new ordinance's percentages without
    re-entering adjustments.
  - A land with no strips is valued as one strip of its own fields,
    adjustments included. Adding a strip to such a land first turns its
    registered area into strip 1.
- **Existing land:** the migration gave every existing land one strip.
- **Breakdown change:** the location factor now appears only when the
  land carries one. Before, a factor of 1 was always shown.

**2c — buildings.**
- **New table:** `BuildingUsePortions`. `BuildingComponents` gains
  `IsAdditionalItem` (which requires a cost, by check constraint) and
  `BuildingUsePortionId`.
- **Endpoints:** `POST /api/buildings/{id}/use-portions|components`. This
  is the first component endpoint; components were model-only before.
- **Valuation:** each portion's floor area × its SMV rate, plus its
  additional items (items not tied to a portion are spread by floor area
  to the centavo), × completion.
  - The portions must total the building's total floor area
    (`BUILDING_USE_PORTIONS_INCOMPLETE`), and adding one cannot exceed it.
  - A building with no portions values as before, under its TD's
    classification and use.

**2d — machinery.**
- The unique index on `MachineryUnits.RpuId` is dropped, so several
  machines per unit are allowed. This is the only constraint removed in
  step 2.
- Machines gain an optional `ClassificationId` and `ActualUseId`
  (default: the TD's).
- Valuing any machine values the whole unit, one line per machine.
  `Valuation.SourceId` names the first machine; the lines name each one.
- `GET /api/rpus/{id}/machinery-units` lists the machines. The old
  single-machine endpoint returns the first one.
- The appraisal record carries `MachineryUnits`.

**UI.**
- Assessments table: shows "N rows" when a unit has several levels.
- Appraisal drawer: appraisal rows and assessment rows, with bracket and
  ordinance.
- Land: strips, improvements and adjustments tables with add dialogs.
- Building: use portions (with a coverage warning), and components with
  an additional-item flag.
- Machinery: a machines table with "Add machine". A machine can name its
  own classification and use.

**Known gaps (not fixed here):**
- **Assessment level brackets cannot be entered through the API.**
  `AssessmentLevelService` keeps one open level per (classification, use,
  property type) and closes it when a new one is created. So a bracket set
  (e.g. 0–250,000 at x%, above at y%) exists only as data. The tests
  insert brackets directly. This needs a small change to how the level
  service versions brackets.
- There is no correction path for strips, improvements, adjustments,
  portions or components: rows can be added, not edited or ended. Land,
  Building and Machinery have no update endpoints either.
- The provisional FAAS template (A8) still shows one assessment level.
  For a mixed-use assessment it shows the principal line's fields and a
  blank level. The row tables are step 5 (MRPAAO templates).
- The improvement kinds and component types lookups have no admin API
  (the same gap as the other lookups).

**DOMAIN VERIFICATION REQUIRED** (unchanged from §8.7):
- the bracket basis;
- rounding per line;
- whether adjustment percentages add or compound;
- whether adjustments apply to improvements (today they do not);
- how additional items are spread;
- how productive and non-productive trees are treated.

**Verified:**
- Unit tests: multi-line billing (per-class rates, principal rate, per-line
  rounding, cap on the total, sum check); land strip, improvement and
  building-portion calculations; spreading by area.
- Integration tests (`AssessmentLinesTests`, `LandAppraisalTests`,
  `BuildingAppraisalTests`, `MachineryAppraisalTests`):
  - a level per line, and the `Unit` bracket basis;
  - a missing level named;
  - a bill taxing each line;
  - strips, improvements and adjustments valued and assessed by use;
  - an unknown factor code, and a factor not in force;
  - use portions with spread and assigned additional items;
  - incomplete or excess portions;
  - two machines on one FAAS;
  - a machine with missing inputs named.
- Full suite passes (101 domain, 37 application, 127 integration).
- Production frontend build and lint pass.
- In the browser on `DEMO-BILL-AE94B8`:
  - the appraisal drawer shows the assessment row;
  - the land shows the backfilled strip 1;
  - adding a 100 sqm strip raised the land area to 600 sqm;
  - the building and machinery sections render.

**Migrations:** `AppraisalAndAssessmentLines` (with backfill),
`LandStripsImprovementsAdjustments` (with backfill),
`BuildingUsePortionsAndAdditionalItems` and `SeveralMachinesPerUnit`.
All are applied to the local dev DB only. The DEMO land now has two
strips (500 + 100 sqm).

---

## 10. Step 3 design — descriptive fields (implemented; see §11)

These fields describe the property and are printed on the FAAS and TD.
None of them changes a value.

### 10.1 Fields

| Where | New fields | Source (MRPAAO) |
|---|---|---|
| **Property** | Boundaries: North, East, South, West (text, max 500 each). Title kind (new lookup `TitleType`: OCT, TCT, CLOA, CCT … as LGU data) and title date. | Att. 1, 4; p.146 |
| **Building** | Building permit no. and date issued; CCT no.; certificate of completion date; certificate of occupancy date; date constructed/completed; date occupied. The age is computed. | Att. 2; p.150 |
| **Building floors** (new `BuildingFloor*`) | Floor number and area. The manual's form has 1st–4th floor areas ("use additional sheets"), so PRIME allows any number of floors. Floor areas must total the building's total floor area when floors are given. | Att. 2 |
| **Structural materials** (new `BuildingMaterial*`) | Structure part × material × floor (null = all floors), with "Others (specify)" text. It uses two configurable catalogues, `StructuralPart` (roof, flooring, walls & partitions …) and `StructuralMaterial` (per part). | Att. 2; p.150–152 |
| **Machinery** | Year installed; year of initial operation; conversion factor. | Att. 3 |
| **LGU settings** | `Lgu:SanggunianName` (e.g. "Sangguniang Panlalawigan"), for the TD's printed note. | Att. 4 note |
| **Transfer** (new `TransferTaxClearance`, one per transfer transaction) | BIR CAR no. and date; transferor name and TIN; transferee TIN; capital gains tax, documentary stamp tax and transfer tax paid (amount, OR no. and date each). | Annex A (BLGF MC 18-2004) |

### 10.2 Editing descriptive fields

Today Property, Land, Building and Machinery can only be created, not
corrected. Step 3 adds **update endpoints for descriptive fields only**
(not area, classification or anything that is valued):
- a reason is required;
- the audit log keeps the old and new values (it already does on every save);
- an issued FAAS or TD keeps what it printed (frozen snapshot).

Valued fields keep changing only through new rows, as now.

### 10.3 Not in this step

- **Floor plan and photograph attachments.** They need document upload.
  The `Document` metadata table exists, but no storage service does yet.
  They come with the document storage work.
- **Deriving replacement cost from the conversion factor.** The factor is
  recorded and printed only; RCN stays an entered figure.
  DOMAIN VERIFICATION REQUIRED before any derivation.

### 10.4 Open for review

1. Descriptive fields are corrected in place, with a required reason and
   the audit log, rather than versioned. Agree?
2. Seed the manual's structural parts and materials lists (p.150–152) as
   starting reference data, labelled "MRPAAO 2004" and editable. They are
   descriptors, not values. Agree?
3. Record the conversion factor only (no derivation)?
4. Defer attachments to document storage?

**DOMAIN VERIFICATION REQUIRED:** the title kinds in use locally; the CAR
fields the LAM or BIR currently require on a TD.

---

## 11. Step 3 — implementation status (2026-09-25)

The four §10.4 questions were taken as recommended, since the user said
"next step" without answering:
- descriptive fields are corrected in place, with a reason and the audit log;
- the manual's lists are seeded;
- the conversion factor is recorded only;
- attachments are deferred.

**Data:** migration `DescriptiveFields` (additive).
- New columns on `Property` (boundaries, title kind and date),
  `Buildings` (permit, CCT, certificate and occupancy dates) and
  `MachineryUnits` (year installed, year of initial operation, conversion
  factor).
- New tables: `BuildingFloors`, `BuildingMaterials` and
  `TransferTaxClearances` (one per transaction), plus the lookups
  `TitleTypes`, `StructuralParts` and `StructuralMaterials` (materials
  carry their part).
- Setting `Lgu:SanggunianName`, passed to every form as
  `lgu.sanggunianName`.

**Seed:** 4 title kinds (OCT, TCT, CLOA, CCT) and the manual's checklist
(16 structure parts, 66 materials; p.150–152), inserted by the migration
and skipped when a code exists. The description reads "MRPAAO 2004
(superseded) … starting list, editable".
- DOMAIN VERIFICATION REQUIRED: the manual's two-column print makes
  Foundation vs. Columns ambiguous. It was read as Foundation {reinforced
  concrete, plain concrete}, Columns {steel, reinforced concrete, wood}.

**API** (`DescriptionsController`; each returns the record as it now
stands):
- `PUT /api/properties/{id}/description`
- `PUT /api/buildings/{id}/description`
- `POST /api/buildings/{id}/floors|materials`
- `PUT /api/machinery/{id}/description`
- `PUT /api/transactions/{id}/tax-clearance` (transfers only, before
  approval)
- `GET /api/reference/title-types|structural-parts|structural-materials`

Corrections require a reason, which is stored as the audit log's reason
with the old and new values. The rules:
- valued fields are never edited here;
- floors cannot exceed the total floor area, and a floor number is unique;
- a checklist entry is a catalogue material of that part, or "Others
  (specify)", never both.

**FAAS and TD data:**
- The appraisal record carries the boundaries, title kind and date, the
  building's descriptive dates, floors and materials, and the machinery
  years and conversion factor.
- The property form data carries boundaries and title.
- The TD form data carries `transferClearance` when the TD was issued
  under a transfer.

**UI:**
- Property profile: "Edit description" (title and boundaries shown in
  Basic Information).
- Building: a descriptive summary with "Edit description", plus floor-area
  and structural-materials tables with add dialogs (materials filtered by
  part).
- Machinery: "Edit" per machine.
- Transfer drawer: a "BIR clearance (CAR)" row with Record/Edit.

**Verified:**
- 4 integration tests (`DescriptiveFieldsTests`):
  - a property correction refused without a reason, then audited with the
    reason and on the appraisal record;
  - floor and material rules;
  - a transfer clearance recorded and present in its TD's form data;
  - a clearance refused on a non-transfer.
- Full suite passes (101 domain, 37 application, 131 integration).
- Production frontend build and lint pass.
- In the browser on `DEMO-BILL-AE94B8` (API on http://localhost:5221):
  - "Edit description" saved title kind TCT, number T-DEMO-123 and two
    boundaries, and Basic Information shows them;
  - the transfer drawer shows the clearance row;
  - no console errors.

**Not in this step:** attachments (document storage); a correction path
for floors and materials (add-only, like the step 2 rows).

---

## 12. Step 4 — transaction code and Record of Assessment entry (2026-09-25)

Designed and built in one pass after "commit then proceed". The choices
below follow the manual; each is a setting or catalogue data.

**Transaction code on the FAAS (TD).**
- `TaxDeclarations.TransactionCode` and `TransactionRank` are frozen when
  the TD is drafted. The candidates are:
  - the transaction the TD is drafted under (its code, and its catalogue
    type's rank);
  - a code named on the request (it must be a type in force:
    `TRANSACTION_CODE_NOT_IN_FORCE`);
  - `Faas:GeneralRevisionTransactionCode` (default "GR") when the declared
    assessment came from a general revision.
- **The highest rank wins**: the lowest rank number; unranked codes come
  last (MRPAAO p.167).
- The Draft TD prepared on posting (step 1) takes GR for a general-revision
  assessment. Otherwise it has no code, and the assessor names one on a
  new TD.
- The codes and ranks are catalogue data. The MRPAAO list (SD 1 … GR 9)
  is not seeded as transaction types, because types carry prerequisites
  and legal bases the LGU must configure.

**Record of Assessment entry.**
- `Assessments.PostedAt` and `PostedBy` are stamped by `PostAsync`. This
  is the FAAS "Date of Entry in the Record of Assessment … By".
- `CK_Assessments_Posted` allows a stamp only on a posted assessment.
- Assessments posted before this change keep no stamp; their posting time
  was never recorded, so none is invented.
- The appraisal record carries `RecordEntry` (date and name) and the TD in
  force's transaction code.

**Backfill:** TDs drafted under a transaction were given its code and rank.

**UI:** a Code column on TDs; an optional transaction code (types in
force) on a new TD outside a transaction; an "Entered in ROA" column on
assessments; the entry and code in the appraisal drawer.

**Verified:**
- 2 integration tests (`TransactionCodeTests`):
  - a transfer TD takes TR/7, a named SD/1 outranks it, and an unknown
    code is refused;
  - posting a general-revision assessment stamps the entry (user B) and
    prepares a TD with GR/9, and the appraisal record shows who entered it.
- Full suite passes (101 domain, 37 application, 133 integration).
- Production frontend build and lint pass.
- The browser shows the new columns with no console errors.

Migration `TransactionCodeAndPostingStamp` is applied to the local dev DB
only.

---

## 13. Step 5a — MRPAAO FAAS and TD layouts (2026-09-25)

Step 5 is split. 5a is the FAAS ×3 and the TD, which need templates and
data only. 5b is a Notice of Assessment for several properties, which
changes how notices are stored and gets its own design.

**Authority and seeding.**
- New `FormAuthority.Mrpaao`: a reference layout of the superseded manual.
  It has no PROVISIONAL watermark; each form prints a banner naming its
  attachment and the fact that the manual is superseded by the LAM.
- `ProvisionalFormSeeder` installs PRIME's built-in versions, provisional
  and MRPAAO. It never touches a version with any other authority (e.g.
  the LAM's).
- A newer built-in version replaces a built-in predecessor that started
  the same day. The predecessor is marked Cancelled with a remark, never
  deleted, and forms issued under it keep pointing to it.

**Forms** (templates in `Documents/Templates`):

| Code | Version | Subject | Source |
|---|---|---|---|
| `FAAS_LAND` | 1 | `Faas` (a TD) | Att. 1, p.230–231 |
| `FAAS_BUILDING` | 1 | `Faas` | Att. 2, p.232–233 |
| `FAAS_MACHINERY` | 1 | `Faas` | Att. 3, p.234–235 |
| `TAX_DECLARATION` | 3 | TD | Att. 4, p.236 (replaces provisional v2) |

**FAAS data** (`FaasFormDataProvider`). The subject is the TD, and the
FAAS/ARP number is the TD number (or the assessment's under
`Faas:NumberSource = Own`). It carries:
- the transaction code and the unit PIN with its postscript;
- owners and administrators with TIN and telephone, as of the TD's
  effectivity;
- taxable/exempt, effectivity quarter and year, and memoranda (TD and
  assessment remarks);
- the Record of Superseded Assessment (the previous TD: PIN, ARP, TD,
  total AV, previous owners, effectivity);
- the Land Reference (building and machinery) and the Building Reference
  (machinery);
- prepared rows for each table: land appraisal, other improvements, market
  value adjustments (factors from the breakdown), building appraisal per
  use portion, additional items, and machines with their depreciation
  figures;
- the full appraisal record for the assessment rows, signatures and Record
  of Assessment entry.

It can be issued only when the TD is Approved (or Cancelled, as a copy)
and declares an assessment; otherwise it previews. A FAAS form of the
wrong kind shows a notice naming the right one.

**TD v3 data** (added to the TD provider under `mrpaao`):
- the declared assessment (else the latest posted one, as before), with
  row areas and the SMV ordinance;
- unit PIN, effectivity quarter, transaction code;
- kind of property (storeys and brief description);
- "This declaration cancels TD No. / Owner / Previous A.V.";
- declared parties with TIN and telephone.

The template prints:
- the total assessed value in words (new `amount_words` filter, e.g. ONE
  HUNDRED THOUSAND PESOS AND 50/100);
- all annotations, lifted ones with their lift;
- earlier approval steps by label, and the last in "Approved by";
- the note with `Lgu:SanggunianName` and the SMV ordinance;
- a back page with the BIR clearance when the TD came from a transfer.

A new `num` filter prints areas and counts without trailing zeros.

**Not reproduced:**
- the land sketch (it points to the tax map; a GIS extract is future
  work);
- the floor plan (an attachment, once document storage exists);
- the AR page no. (it comes with the registers, step 6);
- building depreciation (plan A9).

**UI:**
- a **FAAS** button on each TD row that declares an assessment (Land,
  Building or Machinery form by unit type; "Preview" until approved);
- the old provisional FAAS button on assessments is removed (the A8
  definition stays in the database);
- the document page labels the new authority.

**Test infrastructure.** Integration test classes now run one at a time
(`TestAssemblyInfo.cs`). They share the dev database, and parallel
classes retiring the same configuration rows deadlocked (Postgres 40P01)
once more classes used that pattern.

**Verified:**
- `MrpaaoFormsTests`:
  - a land FAAS issued from the TD (MRPAAO authority, every block, code,
    values, effectivity, no watermark);
  - a v3 TD preview (amount in words, kind, "cancels", the note, area);
  - a TD without an assessment that previews only, and a wrong-kind
    notice.
- An amount-in-words theory, and parse tests for the four templates.
- Five older tests updated where they asserted the provisional TD layout.
- Full suite passes three times in a row (101 domain, 37 application,
  145 integration).
- Production frontend build and lint pass.
- In the browser on `DEMO-BILL-AE94B8`:
  - the land FAAS preview of the DEMO draft TD (linked in the dev DB to the
    posted assessment) shows every block;
  - TD `DEMO-TD-AE94B8-R5` issued under v3 shows the land box, amount in
    words, approver, "cancels TD No. …-R2", and the note with the SMV
    ordinance;
  - no console errors.

**Local dev DB notes:**
- The MRPAAO template rows were refreshed in place during development.
  They were unreleased, and no MRPAAO form had been issued before the
  final text.
- The provisional TD v2 was installed today, so it is marked Cancelled
  (superseded by v3 on its first day).
- One TD (R5) is now issued under v3.

---

## 14. Step 5b — Notice of Assessment for several properties (2026-09-25)

**Model.**
- A notice keeps one addressee, one service record and one appeal clock,
  and now lists **items** (`NoticeOfAssessmentItems`): one per property
  assessment, each with its reason and frozen values.
- The header's assessment fields are the first item's, and its values are
  the items' totals.
- `AddresseeTaxpayerId` marks a notice combining several properties of one
  declared owner.
- The migration `NoticeItems` gave every existing notice one item from its
  own values.

**Reasons.** The value reasons (first assessment, increase, decrease;
LGC §223) are still derived, and are given once per assessment. The
MRPAAO's descriptive reasons (p.168) are new:
- `DeclaredOwnerChanged`, `OwnerAddressChanged`, `LocationChanged`;
- the user names one, and it is allowed although the value is unchanged;
- it may recur, but not while a draft notice for the assessment is open;
- naming a value reason is refused (`VALIDATION_FAILED`).

**Combined notice.**
- `POST /api/notices/combined {taxpayerId, assessmentIds}`: every
  assessment must be posted and need a notice for its values, and the
  taxpayer must be a current declared owner of each unit
  (`NOTICE_ADDRESSEE_NOT_OWNER`).
- The addressee is the taxpayer's name and address.
- The §223 issue period runs from the earliest approval among the items.
- `GET /api/notices/candidates?taxpayerId=` lists the owner's posted
  assessments that need a notice and have none.
- A combined notice appears in the Notices tab of every property it lists.

**Form.** `NOTICE_OF_ASSESSMENT` v2 is the MRPAAO Attachment 10 layout:
- NA No., the Republic / city or municipality / province heading, the date
  and the addressee;
- the manual's letter text, and item rows (ARP No., TDN, PIN, location,
  classification, MV, AV) with totals;
- a line per item stating its reason (and the previous AV for an increase
  or decrease);
- the assessor's signature, and the manual's notes 1–3, with the appeal
  period and deadline added to note 1.

**Not in this step:** notices are not prepared automatically when a
transfer or a correction changes the declared owner, address or location;
the assessor generates them with the reason.

**UI:** the Generate dialog has an optional descriptive reason, and a new
"Combined notice for an owner" dialog lists the chosen owner's candidates
with checkboxes.

**Verified:**
- 3 new integration tests:
  - a combined notice for two properties (candidates, two items, total AV
    200,000, the addressee, shown on both properties, and the v2 form with
    both TDNs, total and §223 note);
  - a refusal when the addressee does not own a unit;
  - a descriptive reason on an unchanged value (derived reasons refused,
    duplicates refused while a draft is open).
- Two older notice tests are kept by matching their wording in v2 ("DRAFT
  — NOT ISSUED", "assessed for the first time", "… days from the date of
  your receipt").
- Full suite passes (101 domain, 37 application, 149 integration).
- Production frontend build and lint pass.
- In the browser: the DEMO property's served notice prints in the v2
  layout, the combined dialog loads the owner's candidates, and there are
  no console errors.

## 15. Step 6 — the registers (2026-09-25)

**Model.**
- A **register run** (`RegisterRuns`) records one register's kind, scope and
  date, and nothing else. Its rows are never kept by hand.
- The kinds are the Tax Map Control Roll (Att. 5), the Assessment Roll —
  Taxable (Att. 6) and — Exempt (Att. 7), the Ownership Record Card (Att. 8)
  and the Record of Assessment (Att. 9).
- The scope depends on the kind:
  - TMCR and the Assessment Rolls: a barangay;
  - ORC: a taxpayer;
  - ROA: a barangay and a classification.
  The database enforces these rules (`CK_RegisterRuns_Scope`,
  `CK_RegisterRuns_Period`), and so does the service (`VALIDATION_FAILED`).
- `FromDate`:
  - the ROA's period start;
  - an Assessment Roll *supplement* (e.g. quarterly): only FAAS entered on or
    after it.
- Printing a run issues it as a form (`FormSubjectType.Register`, subject =
  the run). The issued snapshot freezes the rows, and a reprint returns the
  same issue. A later register is a new run.

**Rows: the FAAS in force at `AsOf`.**
- A TD is in force when it is effective by that date and approved by then,
  and it is still approved or was cancelled after that date. Per unit, the
  latest such TD counts.
- Its values come from the assessment it declares, or else from the unit's
  latest posted assessment effective by then.
- The date of entry is the TD's approval (or creation) on the LGU calendar.
- Owners are the unit's own current parties, or else the property's (§7).
- ARP No. follows `Faas:NumberSource`.

| Register | Rows |
|---|---|
| TMCR | One per land TD in the barangay: assessor's lot no. (last PIN segment), survey/lot/block, title, area, class code, declared owner, ARP, TD, the number of buildings, machinery (X), other improvements (land improvement kinds), remarks = transaction code |
| AR Taxable / Exempt | One per TD in the barangay by taxability: ARP, TD, full unit PIN, lot/block, owner and address, kind L/B/M/O, class, AV, previous ARP/TD (taxable) or legal basis (exempt: *not recorded*, since exemptions are not yet modelled), effectivity quarter/year, remarks; total AV; revision year = the SMV of the barangay's latest posted assessment |
| ORC | One per unit the owner holds: date of entry, kind, class, PIN, title, lot/block, ARP, TD, previous owner (owners of the previous TD at its entry), location, area, MV, AV; totals |
| ROA | Each TD of the barangay and classification entered in [FromDate, AsOf], including ones cancelled since: date, ARP, TD, owner at entry, PIN, location, land area (taxable or exempt), MV and AV split land/building/machinery, year taxes begin (effectivity year; exempt: none), transaction code; totals |

**Forms.**
- Five MRPAAO layouts, in landscape: `TMCR`, `AR_TAXABLE`, `AR_EXEMPT`,
  `ORC`, `ROA` (v1).
- Printing a run with another register's form shows a warning, as the FAAS
  forms do.

**API and UI.**
- `POST /api/registers` creates a run, and `GET /api/registers` lists the
  latest 200.
- A new **Registers** page creates runs (the fields change with the kind),
  lists them, and previews or issues each one.

**DOMAIN VERIFICATION REQUIRED / gaps.**
- The manual's location index numbers and register page numbering are not
  recorded, so the forms show PSGC codes.
- The exempt roll's legal basis waits for exemptions (CLAUDE.md §43).
- Rows are built per run in one request, with per-row queries. A large
  barangay needs the background-job mechanism (CLAUDE.md §73) and
  set-based queries before production use.
- Runs cannot be cancelled. An issued run can be cancelled through the
  issued form.

**Verification.**
- 6 integration tests (`RegistersTests`): a TMCR issued and frozen; empty
  before the TD took effect; taxable vs exempt; a supplement window; the
  ORC; the ROA period with totals; scope validation; the wrong-form
  warning.
- The five templates parse (`FluidFormRendererTests`).
- Full suite: 101 domain, 37 application and 160 integration tests pass.
- The frontend production build and lint pass.
- Browser (Playwright): a TMCR run was created and previewed, and an ROA
  run was created with a period and issued, with no console errors.
- Migration `RegisterRuns` (additive: one table) is applied locally only.

## 16. Step 7 design — the Sworn Statement (for review, 2026-09-26)

Source: MRPAAO Attachment 11 (manual p.243–244, PDF p.254–255), "Sworn
Statement of the True Current and Fair Market Value of Real Properties
(Taxable or Exempt), required under Section 202/203 of RA 7160". The manual
also cites it as support for appraisal:
- buildings without a building permit or certificate (p.117, §6.A.1(d));
- machinery, whose appraisal rests on the owner's actual cost (p.119,
  §7.C.1; doubtful values may be checked with BOC, BIR or SEC, p.120).

The manual's body does not state the §202/§203 filing periods, and RA 12001
may have changed them. **DOMAIN VERIFICATION REQUIRED:** PRIME records the
basis of a filing but enforces no deadline.

### 16.1 What the form holds

| Part | Fields (Att. 11) |
|---|---|
| Header | Sworn Statement Index No. |
| Declarant | Name, citizenship, civil status, postal address, TIN; capacity: owner, administrator or authorized representative |
| Scope | City/municipality and province (Note 1: one statement covers one city or municipality); "owned by" (the owners' names, when filed by an administrator or representative) |
| A. Land | Existing TD No. (or "NEW", Note 3), lot, block, cadastral/PLS no., title, area (ha or sqm), classification, location, declared value |
| B. Buildings and other structures | Existing TD No., total floor area, storeys, general description, year completed/occupied, actual use, owner of the lot, location, declared value |
| C. Machinery | Existing TD No., description, date acquired, date operation commenced, original acquisition cost, cost of installation on site, value of depreciation, location, declared value |
| Other improvements (perennial trees/plants) | Existing TD No., kind, number productive and non-productive, annual product per tree/plant, ages, declared value |
| Execution | Signed on (day, place); two witnesses (required only when the affiant thumbmarks) |
| Jurat | Sworn on, CTC No., issued on and at, administering officer and TIN |

### 16.2 Model (one additive migration)

**`SwornStatement`**
- `Number`: the index no. A new `NumberedDocumentKind.SwornStatement` is
  assigned on filing if a scheme is in force; otherwise it is entered by
  hand, or left blank.
- Declarant:
  - `DeclarantName`, `Citizenship`, `CivilStatus`, `PostalAddress`,
    `DeclarantTin` (as written on the statement);
  - an optional `DeclarantTaxpayerId`, since an administrator or
    representative is often not a registered taxpayer.
- `Capacity` (Owner, Administrator, AuthorizedRepresentative) and
  `OwnerNames`, required unless the capacity is Owner.
- `MunicipalityId` (the province follows from it).
- `FilingBasis`: §202 declaration, §203 new property or improvement, or
  other; the user records it and PRIME infers nothing.
- Execution: `SignedOn`, `SignedAt`, `Thumbmarked`, `Witness1`, `Witness2`.
- Jurat:
  - `SwornOn`, `SwornAt`, `AdministeringOfficer`, `OfficerTin`;
  - `IdentityDocument`, `IdentityDocumentIssuedOn` and `IdentityDocumentIssuedAt`
    replace the form's fixed "CTC No." with free text, since which identity
    evidence a jurat requires is a notarial rule (DOMAIN VERIFICATION
    REQUIRED).
- `ReceivedOn`: the date the assessor's office received it (Note 2).
- `Status`, `Remarks`, and audit fields.

**`SwornStatementItem`**: one table for the four kinds.
- `Kind` (Land, Building, Machinery, OtherImprovement) and `Sequence`.
- The existing declaration:
  - `TaxDeclarationId`: a TD in PRIME, which must be approved; the property,
    unit and location are taken from it.
  - `ExistingTdNumber`: text, for a number PRIME does not have, e.g. before
    data migration.
  - Neither set: **NEW** (Note 3).
- `PropertyId` and `RpuId`: filled from the TD, or linked later once a NEW
  property is registered (§16.4).
- `Location` and `DeclaredMarketValue` (numeric(18,2), ≥ 0).
- Kind columns, all nullable, with a check constraint naming the ones each
  kind requires:
  - Land: lot, block, cadastral no., title, area, area unit, classification
    (lookup);
  - Building: floor area, storeys, description, year completed, actual use
    (lookup), lot owner name;
  - Machinery: description, date acquired, date operation commenced,
    acquisition cost, installation cost, depreciation;
  - OtherImprovement: kind (the `ImprovementKind` lookup), productive and
    non-productive counts, annual product, ages.

### 16.3 Lifecycle

- **Draft**: encoded, and editable. Items can be added and removed, since a
  draft is not yet a record.
- **Filed**: locked. Filing requires:
  - at least one item;
  - the declarant and capacity;
  - the signing date and the jurat (sworn on, administering officer);
  - two witnesses when thumbmarked;
  - the received date;
  - every linked TD in the statement's city or municipality (Note 1;
    `SWORN_STATEMENT_OTHER_LGU`).
- **Cancelled**: with a reason, and never deleted.

There is no approval step: it is the owner's declaration, not the
assessor's act.

Corrections are a new statement that names the one it replaces (`SupersedesId`).

### 16.4 How PRIME uses it

- **Declared values are information only.** They never feed valuation,
  which follows the SMV and the valuation rules. The appraisal record and
  the unit show the declared value next to the appraised value, and the
  statement it came from.
- **Property Profile:** a Sworn Statements section lists the statements
  whose items reference the property or its units.
- **NEW items:** once the property or unit is registered, a user links the
  item to its RPU. PRIME does not create properties from a statement; that
  stays with registration and property transactions.
- **Machinery:** the stated acquisition, installation and depreciation
  figures stay on the statement. Copying them into the machinery record is
  a later, explicit action, and not in this step.

### 16.5 Form

`SWORN_STATEMENT` v1 follows the MRPAAO Attachment 11 layout, under the
`Mrpaao` authority, with a new `FormSubjectType.SwornStatement` (subject =
the statement).
- A draft previews it, so it can be printed pre-filled for the affiant to
  sign and swear.
- A filed statement can be issued, which freezes it as received.
- Notes 1–4 are printed as in the manual.

### 16.6 API and UI

**API**
- `POST /api/sworn-statements`, and `PUT` to change a draft's header.
- `POST /{id}/items` and `DELETE /{id}/items/{itemId}` (draft only).
- `POST /{id}/file` and `POST /{id}/cancel` (with a reason).
- `POST /{id}/items/{itemId}/link` (an RPU, for a NEW item).
- `GET` with filters (city/municipality, declarant, TD number, received
  date), paged. `GET /api/properties/{id}/sworn-statements`.

**UI**
- A **Sworn Statements** page:
  - a list with filters;
  - an intake form in the order of the paper form (declarant, then items by
    kind, each item picking an existing TD by number or marked NEW, then
    execution and jurat);
  - Print (preview), File, and Issue.
- The property's Sworn Statements section.

### 16.7 Delivery

1. **7a:** entities, migration, service, API, integration tests.
2. **7b:** the `SWORN_STATEMENT` template.
3. **7c:** the page, the property section, the declared-versus-appraised
   display, and a browser check.

### 16.8 Open for review

1. One items table for the four kinds, with a check constraint per kind,
   rather than four tables.
2. Draft → Filed → Cancelled with no approval step, and corrections by a
   superseding statement.
3. Declared values are information only and never feed valuation.
4. The declarant is free text with an optional taxpayer link.
5. The filing basis is recorded and no deadline is enforced.
6. NEW items are linked to an RPU later; statements create no properties.
7. The jurat's identity document is free text, not a fixed CTC field.
8. Scanned signed copies wait for document storage (not built yet).

### 16.9 Decisions (user, 2026-09-26: "yes")

All eight proposals in §16.8 were accepted as written.

## 17. Step 7 — implementation status (2026-09-26)

Implemented as designed in §16, with these specifics:

**Model and API (7a).**
- The entities are `SwornStatement` and `SwornStatementItem`, added by the
  migration `SwornStatements` (additive: two tables).
- New enum values: `NumberedDocumentKind.SwornStatement` and
  `FormSubjectType.SwornStatement`.
- The status also has **Superseded**:
  - filing a correction (`SupersedesId`) marks the corrected statement
    Superseded;
  - cancelling a filed correction puts the corrected one back to Filed;
  - there is only one live correction per statement
    (`UX_SwornStatements_Supersedes_Live`).
- Database rules:
  - a filed statement has its signing date, jurat and received date;
  - owners are named unless the declarant is the owner;
  - a thumbmarked statement has witnesses;
  - each item kind has its required columns;
  - a TD in PRIME and a typed TD number are never both set;
  - amounts are not negative.
- An item's kind must fit the unit (`SWORN_STATEMENT_KIND_MISMATCH`):
  - trees and plants go on a Land or OtherImprovement unit;
  - a building goes on a Building or OtherImprovement unit.
  A linked TD must be approved (`TAX_DECLARATION_NOT_IN_FORCE`) and in the
  statement's city/municipality (`SWORN_STATEMENT_OTHER_LGU`).
- Filing checks everything at once (`SWORN_STATEMENT_INCOMPLETE` lists what
  is missing). The index number is generated when a `SwornStatement`
  numbering scheme is in force; otherwise it is typed or left blank.
- API: `/api/sworn-statements` (search, get, create, update, items
  add/remove/link, file, cancel) and `/api/properties/{id}/sworn-statements`
  (non-draft statements that declare the property).

**Form (7b).**
- `SWORN_STATEMENT` v1 follows the Att. 11 layout, in landscape, under the
  `Mrpaao` authority.
- A draft previews with "DRAFT — FOR SIGNING; NOT YET FILED"; a filed
  statement issues, with its index number as the document number.
- Notes 1–3 are printed. Note 4 ("Original acquisition cost of
  Machinery …") is cut off in the PDF text, so it is not reproduced
  (verify against the printed manual).

**UI (7c).**
- A **Sworn Statements** page lists statements, with search by declarant,
  owner or number, by exact TD number, and by status.
- The statement page:
  - the header form follows the paper form (declarant, execution, jurat,
    received date);
  - properties are added by part;
  - an existing TD is chosen by finding the property, then its RPU, which
    uses the RPU's approved TD;
  - Preview, File (with an optional index number), Issue, Correct (new
    statement), Cancel (with a reason), and Link to unit for a NEW item.
- The Property Profile has a **Sworn statements** tab. Each declared value
  sits next to the unit's latest posted market value and the difference.

**Verification.**
- 6 integration tests (`SwornStatementTests`):
  - declare an existing TD and a NEW building, then file;
  - filing requirements (items, jurat, received date, witnesses) and
    representative owners;
  - kind, in-force and one-LGU checks;
  - correction and restore on cancel;
  - link a NEW item, and search;
  - preview a draft, then issue once filed.
- The template parses (`FluidFormRendererTests`).
- Full suite: 101 domain, 37 application and 167 integration tests pass.
- The frontend production build and lint pass.
- Browser (Playwright): a statement was created, with a land TD from PRIME
  and a NEW building; it was filed, the building was linked to its RPU, the
  form was issued, and the property tab showed declared against appraised,
  with no console errors.
- The browser check renamed two dialog fields that were both labelled
  "Unit" to "RPU" and "Area unit".
- Dev DB: the check left one filed DEMO statement and two incomplete DEMO
  drafts.
- Migration `SwornStatements` is applied locally only.

**Gaps.**
- Scanned signed copies wait for document storage.
- The filing periods (§202/§203) and the jurat's identity evidence remain
  DOMAIN VERIFICATION REQUIRED.
- Copying machinery costs from a statement into the machinery record is not
  built (§16.4).

With step 7, all seven steps of the MRPAAO forms model are implemented.
