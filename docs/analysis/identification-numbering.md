# Identification and Numbering — Design (Step L2)

| | |
|---|---|
| Date | 2026-10-02 |
| Status | **Approved 2026-10-02: all recommendations Q1–Q12 accepted (§8).** **L2 complete** — L2-1 to L2-3 done (§9) |
| Rules | CLAUDE.md §7, §19, §22, §36–§38, §66, §73, §97 (L2), §114–§115, §118 |
| Sources | LAM 2025 Book II Ch. I §2–§4 (index numbers, PIN components and examples, cancellation and retirement, pp.36–40), tax mapping (pp.50–59); Book I Ch. II §4 (numbering systems: TD and NOA numbers, pp.23–24); Book III Ch. IV (condominium PINs, pp.91–94) |
| Builds on | Step 10a (`docs/analysis/property-identification.md`): index numbers, sections, the PIN built from its parts, PIN history, temporary PINs, control rolls |
| Commit status | Cites and paraphrases the LAM; reproduces none of its tables or figures. May be committed (§118) |
| Depends on | The Provincial Assessor's answers to Part B of the L0-4 question list (B1–B7). Choices that depend on one are marked **[B*n*]** with the provisional default |

## 1. Purpose

Step 10a built PRIME's Property Identification Number to the MRPAAO. The LAM changes the
details: a three-digit parcel number, unit PINs with several parts (building, condominium
floor and unit, machinery inside a unit), a parcel number `000` for structures over water, a TD
number that starts with the general-revision year, a Notice of Assessment number taken from its
TD, and the retirement of every PIN of an LGU that is created or whose territory moves. L2 makes
these possible. All formats stay configuration: the LGU decides digits and patterns, and the
LAM lets existing numbering continue until the next general revision (Book I p.23).

## 2. What the LAM says (paraphrased)

- **Index numbers** (Book II p.36): the province or city number (3 digits) comes from the BLGF;
  the Provincial Assessor numbers the municipalities (2 digits); the assessor numbers the
  barangays. The text gives the barangay number as four digits in §2 and three in §3 — an
  inconsistency the province must settle **[B1]**.
- **The land PIN** (pp.36–38) has five parts: province/city, municipality or city district,
  barangay, section (3 digits, numbered from the north) and parcel (**3 digits**, numbered in an
  inverted "S" within the section).
- **Unit PINs** (pp.38–39): a building adds a 4-digit building number starting at 1001; machinery
  on a lot adds one starting at 2001. A leasing property (condominium) adds the condominium
  number in parentheses (starting at 3001), then a floor number (`F01`; basement, parking and
  mezzanine floors use `B`, `P`, `M`) and a 3-digit unit number; machinery in such a unit takes the
  condominium and floor parts followed by its machinery number. Mineral rights held apart from the
  surface take the surface PIN with a 4001 series (Book III pp.59–60).
- **Structures over water** not attached to land use parcel number `000` (pp.38–39).
- **Cancellation and retirement** (p.40): subdivided or consolidated parcels' PINs are cancelled
  and the new parcels take the next numbers in the section (as step 10a does); a demolished
  building's or inoperable machine's PIN is retired; when an LGU is created **all** its PINs are
  retired and new ones assigned under the new index numbers, followed by new tax mapping; when
  territory moves to another LGU, the transferred properties' PINs are retired and the receiving
  LGU is re-tax-mapped.
- **A parcel crossed by a barangay line** takes the barangay of its larger part, with the area and
  assessed value of each part annotated (p.59); a municipal or city line makes separate parcels.
- **Tax maps** (pp.50–52) show boundary conflicts hatched, with the PIN.
- **TD number** (Book I p.23): 15 digits — the year of the general revision (4), the municipal
  index (2; cities 3), the barangay index (4) and an assessment count (5) that starts at 1 for
  each barangay. Elsewhere (p.19) the first part is described as the year the transaction was
  approved **[B5]**.
- **NOA number** (Book I p.24): 11 digits — municipal index, barangay index and **the assessment
  count shown on the corresponding TD**. Which number a notice covering several TDs carries is not
  said **[B6]**.
- Existing numbering may continue **until the next general revision** (Book I p.23).

## 3. What PRIME has, and the gaps

| Area | Exists (step 10a and later) | Gap |
|---|---|---|
| PIN pattern | Effective-dated numbering schemes; tokens `{LGUIDX}{MUNIDX}{BRGYIDX}{SECT}{SEQ:n}`; per-section sequence, never reused | Configuration only: a LAM scheme version with `{SEQ:3}` and its digits **[B1, B2]** |
| Barangay index | 4 digits, unique, never reused | Width becomes a setting if the province uses 3 **[B1]** |
| Unit PIN | `RealPropertyUnit.PinSuffix` (one integer, from `UnitPin:SuffixStart`: buildings 1001, machinery 2001); the **parcel number** is parenthesised when a unit is owned apart from the land (MRPAAO p.42) | No condominium, floor or unit parts; no machinery inside a unit; no mineral-right series; the parenthesis convention differs from the LAM's **[B7]** |
| Parcel `000` | Parcel numbers come from the section sequence starting at 1 | `000` cannot be issued for a structure over water |
| Retirement | Subdivision/consolidation retire and re-number; barangay split/retire; single-property retirement | No bulk retirement for a created LGU or a territory transfer; no transaction kind for it |
| Barangay line | Free-text TD annotations | No structured parts (barangay, area, assessed value) |
| Tax map | Section and barangay layers; printed sheets | No disputed-area (boundary conflict) layer or hatching |
| TD number | Scheme tokens; `{REV}` (the revision year) scopes the sequence but is **not printed**; `{YEAR}` is the calendar year | The general-revision year cannot be printed **[B5]** |
| NOA number | Its own scheme and sequence | Cannot take the TD's assessment count; PRIME does not keep a TD's count apart from its printed number **[B6]** |

## 4. Proposal

### 4.1 L2-1 — numbering

- **Printed revision year.** A new token `{GRYEAR}`: the revision year of the SMV in force on the
  document's date (the value `{REV}` already computes), printed. `{REV}` stays as it is (scope
  only) for existing schemes. A LAM TD scheme is then, for example, general-revision year +
  municipal index + barangay index + `{SEQ:5}`; because a sequence runs per scope (the pattern
  without its sequence), the count restarts for each barangay and each general revision, as the
  LAM says. If the province reads p.19 instead **[B5]**, the scheme uses `{YEAR}`: configuration.
- **The assessment count is kept.** When a TD number is assigned, PRIME stores the sequence value
  it used (`TaxDeclaration.AssessmentCount`), so other numbers can take it.
- **NOA from its TD.** A new token `{TDCOUNT}`: the assessment count of the notice's TD (for a
  notice covering several TDs, its first TD **[B6]**). A LAM NOA scheme is municipal index +
  barangay index + `{TDCOUNT:5}`, with no sequence of its own; a notice for a TD without a count
  (numbered before L2) is refused with the reason. The numbering service accepts a pattern
  without `{SEQ}` only when it contains `{TDCOUNT}`; uniqueness is still enforced.
- **Digits.** Index widths are validated against configuration (`Pin:BarangayIndexDigits`,
  default 4 **[B1]**) instead of fixed values.
- **Parcel width.** No code: the province adds a PIN scheme version with `{SEQ:3}` effective on the
  next general revision's date **[B2]**. Existing PINs are **not** renumbered; parcels numbered
  after that date take three digits. A section that reaches 999 is refused with a message to open
  a new section (as today at 99).
- **ARP number.** The LAM's FAAS and TD carry the TD number and PIN, not an ARP number. Leaving it
  off the LAM form versions is step L5; the stored data stays.

### 4.2 L2-2 — structured unit PINs

- The unit PIN becomes a list of **parts** after the property PIN, replacing the single integer
  additively (`PinSuffix` stays and is read as a one-part unit PIN):
  - **structure** — a building or other structure number (series start configured, 1001);
  - **leasing property** — a condominium or leasing-building number (3001), printed in
    parentheses **[B7]**;
  - **floor** — a prefix (`F`, `B`, `P`, `M`, configured) and a 2-digit number;
  - **unit** — a 3-digit unit number within the floor;
  - **machine** — a machinery number (2001), on a lot or inside a unit;
  - **mineral right** — a 4001 series on the surface PIN.
- Which parts a unit has follows from what it is: a building on a lot (structure); a condominium
  unit (leasing property, floor, unit); machinery on a lot (machine); machinery in a unit
  (leasing property, floor, machine); a mineral right (mineral right). Series numbers are
  allocated per property and kind, never reused, as today.
- **Parentheses** follow a setting: the LAM's (the leasing-property number) by default, or the
  MRPAAO's (the parcel number when owned apart from the land) for LGUs that keep their numbering
  until the next general revision **[B7]**.
- **Mineral rights:** a new `RpuType.MineralRight` so the unit can be identified and declared
  apart from the surface. Its valuation and the yearly reassessment on remaining deposits
  (Book III pp.70–71) are not built until an LGU needs them (the province has none known).
- **Parcel `000`:** a property in a section may be marked **over water**; its PIN takes parcel
  `000` (not from the sequence) and it carries no land unit. Its buildings are numbered as usual.
- Condominium **valuation** (pro-rata shares of common areas) remains step L4; L2-2 only gives the
  units their PINs.

### 4.3 L2-3 — territorial changes, barangay lines, disputed areas

- **Territorial change** (B6 of the gap analysis): a new transaction kind
  `PropertyTransactionKind.TerritorialChange` and a **background job** (§73) that, for the
  properties of a barangay, several barangays or a municipality, retires their PINs (reason and
  legal basis — the creating law or court order — recorded) and assigns new ones under the new
  index numbers. Two ways **(Q9)**: keep each property's section and parcel number under the new
  index numbers (default), or give temporary PINs until the area is re-tax-mapped. It records
  every old and new PIN, is resumable and shows its progress; reassessment (Book III p.85) is then
  a general-revision or reassessment run over the same properties.
- **Barangay line:** a property crossed by a barangay line records its **parts** — barangay, area
  and assessed value share — printed as the TD/FAAS annotation; its PIN uses the barangay of the
  larger part (checked).
- **Disputed areas:** an effective-dated GIS layer of boundary conflicts (imported as GeoJSON like
  the other layers), drawn hatched on tax-map sheets with the PINs of the parcels it touches.

## 5. Data and migrations

All additive (CLAUDE.md §105): `TaxDeclaration.AssessmentCount`; unit PIN parts on
`RealPropertyUnit` (or a child table) with `PinSuffix` kept; `RpuType.MineralRight`;
`Property.IsOverWater`; `PropertyTransactionKind.TerritorialChange`; a territorial-change job
table with its items (old PIN, new PIN, status); property barangay parts; a disputed-area layer
table. No existing PIN, TD or NOA number changes.

## 6. Delivery steps

| Step | Scope | Verification |
|---|---|---|
| L2-1 | `{GRYEAR}`, `{TDCOUNT}`, stored assessment count, barangay index width, DEMO LAM schemes (TD, NOA, a 3-digit parcel PIN version) | A TD numbered as GR year + indices + count restarting per barangay; its NOA taking the same count; an older PIN unchanged after the new scheme takes effect |
| L2-2 | Unit PIN parts, parenthesis setting, mineral-right unit, parcel `000` | Each example kind of the LAM (building, leasing unit, machinery on a lot and in a unit, over water) composes with DEMO index numbers |
| L2-3 | Territorial-change job and transaction kind; barangay parts; disputed-area layer and hatching | A DEMO barangay moved to another municipality: every PIN retired and re-assigned, history kept, job resumable; hatched area on a printed sheet |

Each step: build, full tests, `npm run build` and lint, browser check, documentation (§107).

## 7. Exit criteria

1. A LAM-format TD and its NOA number are produced from DEMO index numbers, and the count
   restarts for each barangay and each general revision.
2. Every unit-PIN example in Book II pp.38–39 has a DEMO counterpart that PRIME composes.
3. A territorial change retires and re-assigns every affected PIN with full history, without
   touching other properties.
4. Every existing PIN, TD and NOA number stays as it was.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Barangay index width **[B1]** | A setting, default 4 digits (PRIME's current value) |
| Q2 | Parcel number of 3 digits and existing PINs **[B2]** | A PIN scheme version with `{SEQ:3}` effective on the next general revision; existing PINs are not renumbered |
| Q3 | The first part of the TD number **[B5]** | The general-revision year (`{GRYEAR}`, p.23); `{YEAR}` if the province reads p.19 — configuration either way |
| Q4 | How the NOA number takes its TD's count | Store each TD's assessment count when it is numbered; the NOA pattern prints it (`{TDCOUNT}`); TDs numbered before L2 have none, so their notices keep the existing NOA scheme |
| Q5 | A notice covering several TDs **[B6]** | It takes its first TD's count (and still lists every TD) |
| Q6 | Parentheses in unit PINs **[B7]** | A setting: the LAM's (leasing-property number) by default, the MRPAAO's for LGUs keeping their numbering until the next general revision |
| Q7 | Structured unit PIN | Parts (structure, leasing property, floor with prefix, unit, machine, mineral right) chosen by the kind of unit; series starts and floor prefixes configured |
| Q8 | Mineral rights | Add the unit kind and its PIN series now; valuation and the yearly reassessment only when an LGU needs them |
| Q9 | New PINs after a territorial change | Default: keep section and parcel numbers under the new index numbers; option: temporary PINs until re-tax-mapping — chosen per job |
| Q10 | A parcel crossed by a barangay line | Structured parts (barangay, area, assessed-value share) printed as the annotation; the PIN uses the larger part's barangay |
| Q11 | Boundary conflicts on tax maps | An effective-dated disputed-area layer, hatched on printed sheets with the parcel PINs |
| Q12 | Order of work | L2-1, then L2-2, then L2-3; L2-3's job reuses the general-revision job pattern |

Not asked here, decided by earlier rules: formats are configuration (§7, §115); numbers are never
reused and history is never destroyed (§49, §76); the LAM form versions (and the ARP number) are
step L5; condominium valuation is step L4.

### 8.1 Decisions (user, 2026-10-02: "proceed all recommendations")

Q1–Q12 accepted as recommended.

## 9. Implementation log

### L2-1 — numbering (2026-10-02)

**Built**
- `{GRYEAR}`: the revision year, printed (same value as `{REV}`, which stays scope-only).
  `{TDCOUNT}` / `{TDCOUNT:n}`: the TD's assessment count. A pattern with `{TDCOUNT}` has no
  `{SEQ}` and allocates nothing (`NumberPattern.IsDerived`, `FormatDerived`).
- The numbering service returns the sequence it used (`AssignNumberAsync`,
  `GenerateNumberIfConfiguredAsync` → `NumberAssignment`); the TD stores it as
  `TaxDeclaration.AssessmentCount` (typed numbers: none), on the TD screen's create and on the TD
  prepared when an assessment is posted; the TD DTO shows it.
- The NOA takes the count of its TD (a notice of several TDs: its first; Q5). A TD without a count
  (numbered before L2) is numbered by the latest earlier approved NOA scheme without `{TDCOUNT}`
  (Q4); with none, `NUMBER_TD_COUNT_MISSING`.
- The revision year behind `{REV}`/`{GRYEAR}` is now the SMV in force **covering the property's
  municipality** (province-wide deployment), as of the TD's **effectivity** date (before: any SMV,
  as of today).
- `Pin:BarangayIndexDigits` (3 or 4, default 4; checked at start-up) governs the barangay index on
  the admin screen, in barangay splits and in content packs; the database check accepts 3 or 4.
- The NOA number's unique index became non-unique: a LAM number repeats its TD's count, which
  restarts at each general revision; the TD number keeps them apart.
- Admin help text lists the new tokens and the LAM examples.
- Migration `LamNumbering` (TD column, barangay check, NOA index) on the **local database only**.
- No PIN scheme change is shipped: the 3-digit parcel is a scheme version the province adds for its
  next general revision (Q2); no code renumbers existing PINs.

**Verified**
- 1 unit test (tokens, scope key with the printed year, derived formatting and padding, missing count).
- 3 integration tests (`LamNumberingTests`, DEMO index numbers 07 / 0101, 0102, DEMO SMV of revision
  2025 covering the town): TD numbers 2025-07-0101-00001, -00002 and 2025-07-0102-00001 (the count
  restarts per barangay) with their counts stored; the NOA 07-0101-00002 from the second TD's count;
  a TD without a count numbered by the earlier NOA scheme; the revision year scoped to the covering
  SMV; a derived scheme with no fallback refused; pattern validation.
- Full suite: 586 tests pass (218 domain, 56 application, 312 integration). `npm run build` and
  `npm run lint` clean.
- Browser: on Admin → Forms → Numbering schemes, `{TDCOUNT}{SEQ}` refused with the reason; a LAM NOA
  pattern (example `15000500001`) and a LAM TD pattern (example `202615000500001`) created as
  Drafts. They are left unapproved in the dev database so its TD numbering is unchanged.

### L2-2 — structured unit PINs (2026-10-02)

**Built**
- A unit PIN is composed from **parts** after the property PIN (`UnitPin.Parts` / `Compose`): its series
  number (building 1001, machinery 2001, mineral right 4001), and for a unit of a leasing property the
  leasing number (3001 …), the floor (prefix F/B/P/M + 2 digits) and the unit number (3 digits); machinery
  installed in such a unit takes the unit's leasing number and floor, then its machine number.
  `PinSuffix` stays the series number.
- `RealPropertyUnit` gains `IsLeasingProperty` (a building whose number comes from the leasing series),
  `FloorPrefix`, `FloorNumber`, `UnitNumber`. A unit of a leasing property is a Building unit whose host
  (`HostRpuId`) is the leasing property; its unit number is given or the next on its floor, unique per
  leasing property and floor. Machinery may name a condominium unit as its host. Refusals:
  `UNIT_NUMBER_DUPLICATE`, an unknown floor prefix, a hosted building whose host is not a leasing property.
- `RpuType.MineralRight` with the 4001 series (`UnitPin:SuffixStart:MineralRight`); no valuation (Q8).
- **Parentheses** follow `UnitPin:Parentheses`: `LeasingProperty` (the LAM's, default) parenthesises the
  leasing number; `Parcel` keeps the MRPAAO's parenthesised parcel number for a unit owned apart from the
  land. With the LAM default a separately owned building's PIN is no longer shown with a parenthesised
  parcel (two existing tests updated to the decided convention).
- Every place that composes a unit PIN (unit list, FAAS and its land/building references, TD form, NOA
  form, registers) now uses one helper (`UnitPin.ForUnitAsync`) with the setting.
- **Parcel 000:** `PlaceInSectionRequest.OverWater` gives a property with no land unit the PIN of its
  section with parcel 000 (`NumberPattern.FormatZero`), marks it `Property.IsOverWater`, and names no
  parcel (the permanent-PIN check allows that only for parcel 0). One such property per section: a
  second is refused (`PIN_OVER_WATER_EXISTS`) and is recorded as a building unit of the first, so the
  structures are numbered 1001, 1002 … Owners are recorded per unit.
- Screens: the Add RPU dialog offers Mineral right, "Leasing property (condominium)" for a building,
  and "Unit of a leasing property" with floor prefix, floor number and optional unit number; machinery
  may be installed in a condominium unit. The PIN tab's placement dialog has "Structures over water"
  (no parcel; parcel 000).
- Migration `StructuredUnitPins`, additive, **local database only**.

**Verified**
- 3 integration tests (`StructuredUnitPinTests`, DEMO 020-15-0005, section 002, a DEMO LAM PIN scheme with
  `{SEQ:3}`): the land PIN 020-15-0005-002-001; building -1001; leasing property -(3001); units
  -(3001)F01-001, -(3001)F01-002, -(3001)B01-001; machinery in the unit -(3001)F01-2001; machinery on the
  lot -2002; mineral right -4001; the refusals; over water 020-15-0005-002-000 with units -1001, -1002, a
  second over-water property refused, a property with land refused; both parenthesis conventions.
- Full suite: 589 tests pass (218 domain, 56 application, 315 integration). `npm run build` and lint clean.
- Browser: on DEMO-BILL, through the Add RPU dialog, a leasing property (DEMO-BILL-AE94B8-(3001)), a unit on
  floor 3 (…-(3001)F03-001) and a machine in it (…-(3001)F03-2002). These stay in the dev database.

### L2-3 — territorial changes, barangay lines, disputed areas (2026-10-02)

**Built**
- **Territorial change:** `TerritorialChangeJob` (kind: new LGU or transferred territory; legal basis —
  the law or court order; effective date; PIN mode) with its barangay **mappings** (source → receiving
  barangay, which must be active and have its index numbers) and one **item** per property (old PIN, new
  PIN, status, error). `PropertyTransactionKind.TerritorialChange` added to the catalogue's kinds.
  Created as a Draft; approval by a second user lists the active properties of the source barangays and
  enqueues `TerritorialChangeJobRunner` (background job). Each property is moved in its own database
  transaction: its PIN is retired with the legal basis as the reason, the property and its parcels move to
  the receiving barangay (and municipality and province), and the new PIN is given —
  `KeepParcelNumbers`: the same section number in the receiving barangay (created when missing) and the
  same parcel number (reserved in its sequence; parcel 000 kept); `TemporaryPins` (or a property without
  a permanent PIN): a temporary PIN from the receiving barangay. A failed property is recorded with its
  reason and the rest go on; **Resume** reruns the pending and failed ones. Unit PINs follow the new
  property PIN. `/api/territorial-changes` (create, approve, resume, get, list).
- **Barangay line:** `PropertyBarangayPart` (barangay, area, assessed-value share); `PUT
  /api/properties/{id}/barangay-parts` with a reason (audited) replaces them: none, or at least two
  distinct barangays of the property's municipality, shares totalling 100, and the property's own
  barangay holding the larger area (`BARANGAY_PART_NOT_LARGEST` otherwise). The TD form data carries
  them (`barangayParts`) for the annotation; the LAM form layout itself is step L5. Filtered by
  jurisdiction like the other property records.
- **Disputed areas:** a `DisputedAreas` reference layer (keyed by `code`, polygons, effective-dated,
  GeoJSON import like the others). The section tax map adds each dispute touching the sheet as a
  `disputed` feature with the PINs of the section's parcels it touches; the print page hatches it and
  lists it in the legend.
- Screens: Admin → Property Identification → **Territorial changes** (list with progress, new draft with
  barangay pickers, approve and run, details with every old and new PIN, resume); the property's PIN tab
  has **Crossed by a barangay line** with an edit dialog; the tax map sheet draws disputed areas hatched.
- `IApplicationDbContext.ClearChangeTracker()` so a failed property's unsaved changes are dropped.
- Migration `TerritorialChanges`, additive, **local database only**.

**Verified**
- 4 integration tests (`TerritorialChangeTests`; DEMO towns 15 and 16): a barangay of town 15 transferred
  to town 16 — 020-15-0005-002-001 and -002 become 020-16-0001-002-001 and -002 (section 002 created in
  the receiving barangay), a temporary-PIN property gets T-16-0001-0001, the old PINs retired with the law
  as the reason, a building's unit PIN follows, a property elsewhere untouched, nothing left to resume;
  temporary-PIN mode; refusals (receiving barangay without index numbers, no legal basis); barangay parts
  (valid, not the larger part, another municipality, shares not 100, cleared); a disputed area drawn on the
  section's tax map naming only the PIN of the parcel it touches. The jurisdiction test now covers the
  barangay parts (filtered) and lists the territorial-change items as unfiltered with the reason.
- Full suite: 593 tests pass (218 domain, 56 application, 319 integration). `npm run build` and lint clean.
- Browser: on DEMO-BILL's PIN tab, parts recorded (DEMO_Barangay_1 400 sqm 75 %, DEMO_Barangay_2 200 sqm
  25 %) with a reason; on the admin page a Draft territorial change created (DEMO_Barangay_2 → DEMO_Barangay_1,
  "DEMO RA 0000 — not a real law"). It is **left unapproved** in the dev database; approving it would move
  DEMO_Barangay_2's properties.

### L2 exit criteria (§7) — status 2026-10-02
1. LAM-format TD and NOA numbers from DEMO index numbers, the count restarting per barangay and per
   revision: **met** (`LamNumberingTests`).
2. Every unit-PIN example of Book II pp.38–39 composed: **met** (`StructuredUnitPinTests`).
3. A territorial change retires and re-assigns every affected PIN with history, nothing else touched: **met**
   (`TerritorialChangeTests`).
4. Existing PIN, TD and NOA numbers unchanged: **met** — nothing renumbers stored numbers; the only visible
   change is the decided parenthesis convention (Q6) on separately owned units' composed PINs.

