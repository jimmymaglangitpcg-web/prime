# PRIME — Phase 10a: Real Property Identification System (design for review, 2026-09-26)

Phase 10a (CLAUDE.md §97, §115) brings PRIME in line with MRPAAO Chapter II,
*Real Property Identification System*: manual pages 35–76 (PDF pages 46–87)
of `docs/References/ManualRPAandAO.pdf`. As with every MRPAAO step (CLAUDE.md
§0), the manual supplies the **structure**: the parts of a PIN, how they are
numbered, sections, retirement and the control rolls. The actual numbers and
formats stay **configuration**, because the Local Assessment Manual
(DOF DC 004-2025) supersedes the MRPAAO and no retrieved source prescribes a
current PIN format (regulatory baseline, Q5: DOMAIN VERIFICATION REQUIRED).

## 1. What the manual requires

### 1.1 The PIN (Ch. II §1; p.35–43)

| Part | Digits | Numbering (manual) |
|---|---|---|
| Province, city, or Metro Manila municipality index | 1–3 | Assigned per LGU by a national list (p.35–39; e.g. Antipolo 177, Baguio City 102) |
| Municipality index (in a province) or city district index | 4–5 | Municipalities: "01" upward, alphabetical. City districts: "01" from the upper-left district in an inverted "S" |
| Barangay index | 6–9 | "0001" for the Poblacion, then alphabetical by municipality. Poblacion I, II…: 0001, 0002…. Named poblaciones: 0001 for the barangay with the municipal building |
| Section index | 10–12 | "001" from the upper-left section of the barangay, inverted "S" |
| Parcel number | 13–14 | "01" from the upper-left parcel of the section, inverted "S" |

Example: `020-15-0005-002-05` (province 020, municipality 15, barangay 0005,
section 002, parcel 05); `132-06-0012-001-35` in a city district.

**Units** (p.42), all with a four-digit postscript on the land's PIN:
- a building is `…-1001`, `…-1002`;
- machinery is `…-2001`, `…-2002`;
- a mineral right held apart from the surface right is `…-3001` (p.75, item 13);
- condominium units carry the building's PIN plus `(1)`, `(2)`…;
- when the unit's owner is not the landowner, the land's parcel number is put
  in parentheses: `020-15-0005-002-(05)-1001`.

The four-digit postscripts replace the temporary `B-1`/`M-1` postfixes.

**Retirement** (p.42–43): subdivision retires the parent parcel's PIN, and the
new lots take the next numbers after the highest parcel number in the section.
Consolidation retires the sources' PINs, and the result takes the next highest
number. Retirements are noted on the post-TMCR. A barangay that splits retires
its index number, and the new barangays take the next numbers after the
highest in the municipality or district (e.g. 0022–0025 after 0021). The
purpose is that PINs stay unique and their history is kept.

### 1.2 Tax mapping operations (Ch. II §2; p.44–76)

- **Objectives:** a complete inventory of real property, a permanent link
  between property and records, ownership of every parcel, and the total land
  area of the LGU.
- **Pre-field:**
  - base maps;
  - a FAAS copied from each active TD (without classification, use or values),
    grouped by barangay and alphabetical by owner;
  - a **temporary PIN** per land FAAS, `MM-BBBB-NNNN` (district or
    municipality, barangay, a 4-digit FAAS number, so it cannot be mistaken
    for a 2-digit parcel number), with `B1`, `B2`… and `M1`, `M2`… for units;
  - the **pre-TMCR** in temporary-PIN order (Figure 3: lot no. temp/final,
    declared owner, address, TD no., survey/cad no., title no., area declared
    and tax-mapped, kind of land, improvement, remarks);
  - a preliminary office tie-up of FAAS to parcel (a left check mark), then
    a field confirmation (a right check mark);
  - a work plan.
- **Field:**
  - owner interviews and ownership identification;
  - parcel sketching for subdivisions and consolidations;
  - conflicting boundaries and adjustment factors (road, distances);
  - mixed classification (the parts must add up to the mapped area);
  - a new FAAS for undeclared parcels, with the next temporary PIN;
  - identifying buildings and machinery;
  - a daily review checklist.
- **Office:**
  - tax maps in a standard format;
  - index maps, bound in this order: province or city, municipality,
    district, barangay, section, then the property identification maps;
  - the **post-TMCR**, one per tax map (section), in PIN order (Figure 10:
    assessor's lot no., survey lot no., title no., area, class code, owner,
    ARP no., TD no., building/structure, machinery, other, remarks).
- **Miscellaneous rules** (p.73–76):
  - a crowded subdivision gets its own new section, numbered after the last
    one in the barangay;
  - tax maps are revised after the year's end or at a general revision, with
    new numbers when about 50% of a section's parcels changed;
  - an unknown owner is recorded as "Occupant";
  - roads, canals and restricted easements are separate parcels, unless the
    owner asks in writing to keep them;
  - a parcel crossing barangays takes the PIN of the larger part, with an
    annotation of each part's area and assessed value;
  - contiguous parcels may be declared as one at the owner's written
    request; a railroad is one parcel per tax map;
  - a building on two lots takes the PIN of the lot it mostly covers;
  - a parcel crossing an LGU boundary is split per LGU;
  - **without a tax map**, units are identified by the ARPN,
    `MM-BBBB-NNNNN`: the daily transaction series, restarting at each
    general revision (item 14).

## 2. What PRIME has, and the gaps

| Area | Exists | Gap |
|---|---|---|
| Location | Province, Municipality (`IsCity`), Barangay, each with a PSGC code | No assessor **index numbers** (the PIN's index numbers are not PSGC codes); no **city districts**; no retired barangay index |
| PIN | `Property.PropertyIdentificationNumber`, from the `PropertyIdentificationNumber` numbering scheme (tokens `{PROV}{MUN}{BRGY}` = PSGC codes, `{SEQ}`) or typed | Not composed of index numbers, section and parcel number; one flat sequence instead of per-section parcel numbers |
| Sections | `Property.TaxMapNumber` (free text) | No tax map **section** (entity, index number, boundary) |
| Parcel | `Parcel` with geometry, survey/lot/block, barangay | No section, no parcel number |
| Unit PINs | `RpuType` land/building/machinery/other; `PinSuffix` from `UnitPin:SuffixStart`; parenthesised parcel number when the unit has its own owners; `LandRpuId`, `HostRpuId` | Mineral rights (3001) and condominium unit postscripts `(1)`, `(2)` are not modelled |
| Retirement | Property transactions (A5) with subdivision/consolidation types | No PIN retirement or PIN history; no "next highest number in the section" rule |
| Control rolls | TMCR register per **barangay**, frozen on issue | The manual's TMCR is per **section**, in PIN order, with index numbers; no pre-TMCR; no temporary PINs |
| GIS | Barangay, zone and road layers (effective-dated, GeoJSON import); tax map print | No section layer; no index maps |
| Interim identification | — | No ARPN for LGUs without tax maps |

## 3. Proposal

### 3.1 Index numbers and districts (configuration)

- `Province.PinIndexNumber`: 3 digits (province or Metro Manila municipality
  as the manual lists it).
- `Municipality.PinIndexNumber`: 3 digits for a city with its own number in
  the national list, otherwise 2 digits (the municipality's number in its
  province).
- New **`CityDistrict`** (municipality, 2-digit index number, name); a
  barangay of a city or Metro Manila municipality belongs to one.
- `Barangay.PinIndexNumber`: 4 digits, unique within its municipality or
  district, **never reused**. A split barangay is marked retired, with its
  successors, and the new barangays take the next numbers.
- Index numbers are entered by the LGU and changed only through an audited
  admin action. PRIME ships none (question 1).

### 3.2 Tax map sections

New **`TaxMapSection`**: barangay, 3-digit section index number, an optional
boundary polygon (imported like the barangay layer), a status
(active/retired), and a link to the section it was split from (misc rule 2).
Each barangay numbers its sections, never reusing a number. The free-text
`Property.TaxMapNumber` stays for migrated data.

### 3.3 The PIN, built from its parts

- `Parcel` gains `SectionId` and `ParcelNumber`.
- The PIN comes from the existing numbering foundation, with new pattern
  tokens: `{LGUIDX}` (province, city or Metro Manila municipality),
  `{MUNIDX}` (municipality or district), `{BRGYIDX}`, `{SECT}`, and the
  sequence as the parcel number. For example, the manual's format is
  `{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}`.
- Because a sequence runs per **scope key** (the pattern without its
  sequence), each section numbers its parcels separately. The allocator never
  reuses a number, which gives the manual's "next highest parcel number in
  the section" and keeps retired numbers unused.
- The pattern is configuration, so a LAM format replaces it without code.
- **When a PIN is assigned:** when a property's parcel is placed in a
  section. Typing a PIN stays possible for migration when the scheme allows
  manual entry (the existing `AllowManualEntry`).
- A section that reaches the width of its parcel number (99 at two digits) is
  refused with a message to open a new section (misc rule 2).

### 3.4 PIN history and retirement

New **`PinAssignment`** records each PIN a property carried: temporary,
permanent or interim (ARPN), with when it was assigned, when and why it was
retired, and the transaction.
- An approved **subdivision** or **consolidation** property transaction
  retires the source properties' PINs, marks those properties retired, and
  assigns the new parcels their numbers.
- The post-TMCR shows retired PINs with their remarks.
- Nothing is deleted; a PIN is never assigned twice.

### 3.5 Control rolls

- **Post-TMCR:** the TMCR register is scoped to a **section**, in PIN order,
  with the province/city, municipality/district, barangay and section index
  numbers in its heading, and retired PINs noted.
- **Pre-TMCR:** a register of the land FAAS/TDs of a barangay (or of a
  municipality or district), with temporary PINs, both tie-up marks, declared
  and tax-mapped areas and remarks, in temporary-PIN order.

### 3.6 Tax mapping campaign (temporary PINs and tie-up)

A light record of the manual's campaign:
- temporary PINs `MM-BBBB-NNNN` on land units, with `B1`/`M1` postfixes
  derived for their units;
- the office tie-up and field confirmation per unit;
- "undeclared" parcels found in the field.

The field work itself (interviews, sketching) stays outside PRIME. When a
parcel gets its permanent PIN, the temporary PIN is kept in its history.

### 3.7 GIS

- A **section boundary layer**, effective-dated and imported like the
  barangay layer.
- Parcels labelled with their parcel number.
- A section index map and a barangay index map in the tax map print (the
  standard sheet size and symbols are DOMAIN VERIFICATION REQUIRED).

### 3.8 Screens

- **Admin → Property identification:**
  - index numbers for the LGU, municipalities, districts and barangays;
  - sections per barangay;
  - retiring a barangay index.
- **Property Profile:** the PIN with its parts, and the PIN history. Placing
  the parcel in a section assigns the PIN.
- **Registers:** the TMCR per section; the pre-TMCR.

## 4. Delivery (checkpointed)

| Step | Content | Verified by |
|---|---|---|
| 10a-1 | Index numbers, city districts, tax map sections (data, API, admin screen) | Integration tests (uniqueness, never-reused, retirement of a barangay index), browser |
| 10a-2 | PIN tokens; parcel section and number; PIN assignment and history | Unit tests (tokens, per-section sequences); integration tests (next highest number, full section refused, manual entry for migration) |
| 10a-3 | Retirement on subdivision and consolidation | Integration tests with the A5 property transactions |
| 10a-4 | TMCR per section, pre-TMCR, section layer and index maps | Integration tests; the printed registers and tax map in a browser |
| 10a-5 | Tax mapping campaign: temporary PINs and tie-up; ARPN tokens for LGUs without tax maps | Integration tests; browser |

Migrations are additive; existing PINs are kept as they are.

## 5. Open for review

1. **Index numbers:** entered by the LGU (recommended), or seeded from the
   manual's 2004 national list (p.35–39)? That list is official but old, and
   the LAM may change it; entering only the LGU's own numbers avoids
   shipping stale data.
2. **City districts** as a new entity, with barangays belonging to one
   (recommended), or the district as a plain number on the barangay?
3. **PIN through the numbering foundation**, with new tokens and a
   per-section sequence (recommended), or a dedicated PIN service with the
   manual's format built in? The first keeps the format configurable for
   the LAM.
4. **When a PIN is assigned:** when the property's parcel is placed in a
   section (recommended), or at property registration as today? Registration
   before tax mapping would then carry a temporary PIN or an ARPN (step 10a-5)
   or a typed one.
5. **One PIN per property**, taken from its current parcel (recommended)?
   The model allows several parcels per property. With this option, a
   property's parcels are its history, and a second current parcel is a
   separate property.
6. **Full section:** refuse and ask for a new section when the parcel number
   width is reached (recommended, misc rule 2), or allow a wider number?
7. **Tax mapping campaign (10a-5):** include the light version above
   (recommended), or leave temporary PINs and the pre-TMCR out until an LGU
   runs a campaign in PRIME?
8. **Mineral rights (3001) and condominium unit postscripts:** defer to 10b
   and 10c, where their appraisal and assessment are designed (recommended),
   or add the unit types now?

### 5.1 Decisions (user, 2026-09-26: "yes to all")

All eight recommendations are accepted as written above.

## 6. Implementation status

**10a-1 done (2026-09-26, uncommitted):**
- **Index numbers:**
  - `Province.PinIndexNumber` (3 digits); `Municipality.PinIndexNumber`
    (3 digits for a city or Metro Manila municipality with its own number,
    else 2). The 3-digit numbers of provinces, cities and Metro Manila
    municipalities share one number space (checked in the service), and
    2-digit numbers are unique within the province.
  - New `CityDistrict` (2 digits, unique per city or municipality).
  - `Barangay.PinIndexNumber` (4 digits), `CityDistrictId`, `RetiredOn` and
    `RetirementReason`, and `SplitFromBarangayId`. Barangay numbers are
    unique per municipality and district with **NULLS NOT DISTINCT** (needs
    PostgreSQL 15 or later), retired numbers included.
- **Sections:** new `TaxMapSection` (3 digits, unique per barangay and never
  reused; `SplitFromSectionId`; retirement).
- Migration `PropertyIdentificationIndexes` is additive and applied to the
  local dev database only.
- **`PropertyIdentificationService`:**
  - lists and sets the index numbers; changing a number already set needs a
    reason (audited);
  - adds districts and sections with the next number by default;
  - retires a section;
  - **divides a barangay**: it retires the mother's number and gives the new
    barangays the numbers after the highest ever used (the manual's
    0021 → 0022–0025 example is a test).
- API: `/api/property-identification/…` (provinces, municipalities, districts,
  barangays, split, sections, retire).
- UI: **Admin → Property Identification**: the province and city or
  municipality, their numbers, barangays (number, district, divide), their
  sections (add with the next number, retire), and districts.
- Tests: `PropertyIdentificationTests` (4). Full suite: 136 domain,
  37 application and 211 integration tests pass; oxlint is clean and the
  production build passes.
- **Verified in a browser** against the dev database. On DEMO_Province and
  DEMO_Municipality, set to DEMO numbers 990 and 01: barangay
  DEMO_Barangay_1 was set to 0001, sections 001 and 002 were added, and 002
  was retired. No console errors.
- Changing an index number that PINs already use is now blocked (10a-2).
- The dev database holds about ten "Demo Province" rows left by earlier
  committed test runs (the concurrency test), which clutter the pick lists.

**10a-2 done (2026-09-26, uncommitted):**
- **Pattern tokens:** `NumberPattern` gains `{LGUIDX}`, `{MUNIDX}`,
  `{BRGYIDX}` and `{SECT}`, plus `Fits` (does a sequence fit its `{SEQ:n}`
  width?) and `Tokens`. `PinContexts.ForBarangayAsync` fills them:
  - LGU index: the city's or Metro Manila municipality's own 3-digit
    number, else the province's;
  - municipality index: the district's, else the municipality's 2-digit
    number.
- **Parcel:** `Parcel.SectionId` and `ParcelNumber`, unique per section.
- **PIN history:** new `PinAssignment`, with `PinKind` Registered, Temporary
  or Permanent. A PIN is unique across all history, and a property has one
  current PIN. The migration `PinAssignments` backfills each existing
  property's PIN as its current "Registered" assignment.
- **`NumberedDocumentKind.TemporaryPin` (10)**, and
  `INumberSequenceAllocator.ReserveAsync` (raises a scope's last value, for
  typed parcel numbers).
- **Registration:** when the PIN scheme in force uses `{SECT}`, a property
  gets a typed PIN or a temporary one from the TemporaryPin scheme;
  otherwise `NUMBER_REQUIRED`. With any other scheme it works as before.
  Every PIN is recorded in `PinAssignments`.
- **`PinService.PlaceInSectionAsync`** gives the permanent PIN from the PIN
  scheme:
  - the section's sequence is the parcel number, so each section numbers its
    own parcels and a number is never reused;
  - a typed parcel number is allowed for migration when the scheme permits
    manual entry, and it is reserved in the sequence;
  - the previous PIN is retired as "Superseded".
  - Refusals: `PIN_ALREADY_PERMANENT`, `PIN_SCHEME_NOT_SECTIONED`,
    `NUMBER_CONTEXT_MISSING`, `TAX_MAP_SECTION_FULL` (the width is used up;
    open a new section), `TAX_MAP_SECTION_RETIRED`, `PIN_DUPLICATE`, the
    wrong barangay, and a parcel that is not the property's.
- **Index locks:** changing a province, municipality or barangay index
  number, or moving a barangay to another district, is refused
  (`PIN_INDEX_LOCKED`) once permanent PINs use it.
- API: `GET /api/properties/{id}/pin` and
  `POST /api/properties/{id}/pin/place-in-section`. The parcel summary gains
  the barangay id, section and parcel number.
- UI: a **PIN** tab on the Property Profile (the PIN and its kind, its parts,
  the history, and "Place in tax map section" with an optional migration
  parcel number). The Forms admin lists the new tokens and the TemporaryPin
  kind. The profile's stale delinquency note is removed.
- Tests: 3 unit tests (`NumberPatternTests`) and 7 integration tests
  (`PinAssignmentTests`):
  - temporary to permanent, with the next number in the section;
  - a city district PIN (`132-06-0012-001-01`);
  - migrated number 35, then 36, then 99, then the section is full;
  - the refusals;
  - registration without a PIN scheme or a temporary scheme;
  - index locks.

  `PropertyRegistrationFlowTests` now also removes its own PIN history in
  its cleanup. Full suite: 139 domain, 37 application and 218 integration
  tests pass; oxlint is clean and the production build passes.
- **Verified in a browser** against the dev database. The dev database now
  has DEMO schemes, approved as the dev checker: the PIN
  `{LGUIDX}-{MUNIDX}-{BRGYIDX}-{SECT}-{SEQ:2}` (manual entry allowed) and
  the TemporaryPin `T-{MUNIDX}-{BRGYIDX}-{SEQ:4}`.
  - `DEMO-E2E-EDE9F5` got a parcel and was placed in section 001 of
    DEMO_Barangay_1 from its PIN tab.
  - Its PIN became `990-01-0001-001-01`; the backfilled "Registered" PIN is
    kept in the history as superseded. No console errors.
  - With the sectioned PIN scheme in force, registering a property in the
    dev app now gives a temporary PIN unless a PIN is typed.

**10a-3 done (2026-09-26, uncommitted):**
- **Which properties** (A5 property transactions, `RelatedProperties`):
  - a **subdivision** is filed on the mother property and names at least
    two resulting lots (role Result), each registered as its own property
    with its parcel;
  - a **consolidation** is filed on the consolidated property, registered
    with its parcel, and names at least two sources (role Source).

  Opening either is refused (`TRANSACTION_RELATED_PROPERTIES_INVALID`)
  without two properties in the right role, and (`PROPERTY_NOT_ACTIVE`) when
  any of them is already retired. Such a transaction may be submitted
  without TDs, because retiring and numbering is itself its effect.
- **On approval**, in the same database transaction as the TD changes:
  - the ended properties (the mother, or the sources) and their active
    parcels become **Subdivided** or **Consolidated**;
  - their current PINs are retired ("Retired by the subdivision …"), with
    `PinAssignment.PropertyTransactionId` pointing at the transaction. A
    retired PIN stays on the property as its last PIN and is never given
    again;
  - when those PINs are permanent and in **one** section, each resulting
    property without a permanent PIN takes the **next parcel number after
    the highest in that section**, in registration order. The new
    assignment records the transaction (`AssignedByTransactionId`, new
    column), and the lot's temporary PIN is retired as superseded;
  - a resulting property that already has its permanent PIN keeps it. This
    covers a crowded subdivision drawn on a new section (misc rule 2), or
    lots whose numbers the office wants in inverted-"S" order: place them
    from the PIN tab before approval;
  - sources in **several** sections: approval is refused
    (`TRANSACTION_PIN_SECTION_AMBIGUOUS`) until the consolidated property is
    placed from its PIN tab;
  - a resulting property needs exactly one active parcel
    (`TRANSACTION_PARCEL_REQUIRED`);
  - sources that were never tax-mapped: they are retired, and the resulting
    properties keep the PINs they were registered with.

  Every refusal is checked before anything changes.
- `PermanentPins` (Application) holds the PIN assignment and retirement
  that `PinService` and the transaction approval share.
- UI:
  - The new-transaction form asks for the resulting lots or the source
    properties (property search) for these two kinds.
  - The transaction drawer lists them, linked to their profiles, and the
    approval prompt says what happens to the PINs.
  - The PIN tab says "by property transaction" for a PIN a transaction
    gave, and shows a retired property's PIN as retired.
- Tests: `PinRetirementTests` (5 integration tests):
  - a subdivision gives lots 03 and 04 after 01 and 02, and the retired
    mother is refused in a further subdivision;
  - a consolidation;
  - the opening checks;
  - sources in two sections;
  - an untax-mapped subdivision.

  Full suite: 139 domain, 37 application and 223 integration tests pass;
  oxlint is clean and the production build passes.
- **Verified in a browser** against the dev database:
  - DEMO data made through the API: an approved DEMO-SD subdivision type
    (approved as the dev checker), mother `990-01-0001-001-02` (lot
    DEMO-10A3-MOTHER), and lots DEMO-10A3-LOT-A and -B with temporary PINs.
  - The subdivision was opened from the mother's Transactions tab (both
    lots picked), submitted, and approved with "Act as checker".
  - The mother became Subdivided and its PIN retired. The lots became
    `990-01-0001-001-03` and `-04`, with their temporary PINs kept in the
    history. No console errors.
- Still open:
  - The API already lets a transaction issue TDs on its related properties'
    RPUs (the lots' new TDs). The drawer's "Add TD" offers only the filing
    property's RPUs, so that is a UI gap.
  - The mother's RPUs are not ended automatically. Cancel its TDs through
    the transaction's "TDs this transaction cancels".

Next: 10a-4 (TMCR per section, pre-TMCR, section layer and index maps).
