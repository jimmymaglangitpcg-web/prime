# PRIME — Forms Model from the MRPAAO (2004/2006)

Status: **steps 1–3 implemented 2026-09-25** (§6–§11; step 1 is commit
`c5b3f8d`). Steps 4–7 are outlines.

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

