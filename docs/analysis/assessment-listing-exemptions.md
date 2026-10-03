# L3 — Assessment, listing and exemptions (design)

| | |
|---|---|
| Step | Phase 10, step L3 (CLAUDE.md §97); replaces the earlier step 10c |
| Status | Done (L3-1a to L3-4, 2026-10-02); decisions in §8.1 |
| Sources | LAM 2025 Book III Ch. III (assessment, pp.80–90) and Ch. V (exemptions, pp.96–100); Book I p.35 §1 g (notice to the previous owner); Annex I-D p.141 (transaction codes); RA 7160 §§204–206, 218, 220, 234; RA 12001 and its IRR |
| Depends on | L1 (valuation and assessment lines), L2 (territorial changes), L5 (forms, NOA email, annotations); province answers C7, D1, D3 (L0-4) |
| Feeds | L7 (appeals act on assessments and notices); Phase 13 (import of existing exemptions) |

Committed text cites the LAM by book and page and paraphrases it (CLAUDE.md §118). The LAM's transaction-code
list and the statutory maximum assessment levels are loaded as content, not written here.

## 1. Scope

The plan's four L3 items, as findings G2, G4, G5, G9, H1, H3, H4, D6, D8 and K1 of the gap analysis (untracked,
`docs/lam/`):

1. **Taxability per assessment line and exemptions** (L3-1): who is exempt, on what legal basis, from when, proven
   by what, and how the property moves between the taxable and exempt rolls.
2. **Statutory maximum assessment levels** (L3-2): the LGC caps the levels a Sanggunian may enact.
3. **Transaction catalogue** (L3-3): the LAM's transaction codes and the cases that behave differently.
4. **Due process records** (L3-4): discovery summons, the Notice of Cancellation, and the bar on cancelling while an
   adverse claim is pending in court.

Already done elsewhere and not repeated: NOA email service (L5-1), territorial changes (L2), building depreciation
between general revisions (L1-5, `TransactionType.AllowsNewDepreciation`), annotation carry-over (L5-4), the exempt
roll's legal-basis column in the data (L5-2, empty until this step).

## 2. What the LAM requires

**Exemptions (Book III pp.96–100).** The LGC §234 exemptions (government property unless its beneficial use is given
to a taxable person; charitable, religious and educational use; water and power machinery of water districts and
GOCCs; registered cooperatives; pollution-control equipment), and exemptions under other laws: idle-land tax relief,
farm-produce storage structures up to an assessed-value ceiling (RA 11321), mining pollution-control devices (RA
7942), GSIS and SSS assets, registered export enterprises, diplomatic premises, ancestral domains with a CADT except
certain uses (RA 8371), and protected areas (RA 11038). Exemption follows ownership, character or use, and can cover
only **part** of a property: government land is taxable only for the portions leased to taxable persons (p.96).

**Proof (p.100; LGC §206).** Whoever claims an exemption files the documentary evidence with the assessor within 30
days of the declaration. Without it, the property is **listed as taxable** in the Assessment Roll; once proven, it
is **dropped** from that roll.

**Exempt properties are still appraised and assessed** (LAM Annex I-I; CLAUDE.md §43): they are listed on the exempt
roll with their values.

**Assessment levels (pp.80–84).** The Sanggunian fixes the levels by ordinance and may not exceed the LGC maximums
(for land by class; for buildings and machinery by class and value bracket; special classes).

**Assessment and reassessment cases (pp.84–85)** include destruction, sudden inflation or deflation, change of use,
area or ownership, physical change, gross illegality, and LGU creation, merger or territorial transfer.

**Issuance (pp.85–88).** A voluntary declaration follows a sworn statement and inspection. A **discovery** starts with
the assessor's declaration and a **summons** to the owner to declare within 15 days, a second 15-day notice if
ignored, then coordination with other agencies, inspection, appraisal, records, and the NOA and TD.

**Cancellation (pp.88–89).** By the assessor (general revision, change of use, value inflation or deflation,
illegality, court order) or on the declarant's request (LGC §220 request, transfer, subdivision or consolidation, new
improvements, destruction). **No cancellation while an adverse claim is pending before a competent court.**
Effectivity follows the assessment it serves. When the assessor cancels on its own motion, a **Notice of
Cancellation** goes to the previous declarant and others with a legal interest; the same notice tells a previous
owner that a reassessment cancelled their assessment (Book I p.35 §1 g).

**Transaction codes (Annex I-D p.141)** are listed without an order of precedence, unlike the MRPAAO's ranked codes.

## 3. What PRIME has

| Area | Today | Gap |
|---|---|---|
| Taxability | `TaxDeclaration.Taxability` (Taxable/Exempt) for the whole TD; rolls choose by it | No exempt **part** of a unit; no reason or legal basis; no exemption record |
| Exemptions | None (CLAUDE.md §43 planned `ExemptionType`, `PropertyExemption`, `ExemptionDocument`) | Types, claims, evidence, proof deadline, approval, effectivity, expiry, ceiling |
| Exempt roll | Lists TDs marked Exempt; `legalBasis` null | Basis from the exemption |
| Assessment levels | Effective-dated, approved under maker-checker; no ceiling | Optional statutory-maximum check |
| Use and kind codes | Lookup codes (MRPAAO-style); kind L/B/M on the rolls | Recode the use catalogue as content (no code change) |
| Transactions | Configurable `TransactionType` (code, kind, optional rank, effectivity rule, cause window, depreciation flag, requirements checklist); the TD keeps the highest-ranked code, the first one when none is ranked | LAM codes as content; kinds for court orders and machinery relocation; no-rank rule to confirm (D1) |
| Discovery | `NewDiscovery` kind; unknown-owner declaration (LGC §204) | No summons, deadlines or outcome |
| Cancellation | Through a transaction with a reason and checklist; cancels listed TDs | No pending-court-claim guard; no Notice of Cancellation |
| Documents | `Document` with a related entity (type, id), private storage | Usable for exemption evidence as is |

## 4. Proposal

### 4.1 L3-1 — exemptions and taxability per assessment line

**Exemption types (configuration).** `ExemptionType`, effective-dated configuration approved under maker-checker and
loaded from the content pack like the other catalogues: code, name, **legal basis** (text, e.g. "RA 7160 §234(d)"),
the kinds it can apply to (land, building, machinery, other), whether proof is required, an optional **assessed-value
ceiling** (for exemptions limited by value, e.g. RA 11321), and remarks. PRIME ships DEMO types only; the province's
list is content.

**Exemption claims (records).** `PropertyExemption` on one unit (RPU), optionally limited to **one assessment line**
(an actual use or portion, G9), with: the type, the claimant, the date claimed (the declaration date by default),
the **proof due date** (claim date + `Exemptions:ProofPeriodDays`, default 30 per LGC §206, a setting), the
reference (certificate, charter, CADT, lease …), evidence as `Document`s, status, the effective and expiry dates, and
who decided. Statuses: `Claimed` → `ProofFiled` → `Approved` or `Rejected`; an approved one may later `End` (expiry,
change of use, sale to a taxable person) with a reason. Approval is maker-checker (CLAUDE.md §46). Nothing is
deleted.

**Taxability per line.** `AssessmentLine.Taxability` and `AssessmentLine.ExemptionId`, set when the assessment is
made from the exemptions **approved and in force** on its effective date. An unproven claim leaves the line taxable
(LGC §206: listed as taxable until proven). The TD's `Taxability` becomes a summary of its lines: `Taxable`,
`Exempt`, or the new value `PartlyExempt` (Q2). The ceiling of a type is checked on the line's assessed value; above
it the line stays taxable and the reason is recorded.

**When proof comes after the assessment.** Posted assessments are never changed (CLAUDE.md §76). An exemption
approved after the declaration takes effect through the normal workflow: approving it opens a reassessment of the
unit (same valuation, lines re-marked) and a replacing TD, effective per the transaction type's effectivity rule
(Q3). The old TD stays in the record, and the rolls move the unit from the taxable to the exempt roll from the new
TD's effectivity: this is the LAM's "dropped from the Assessment Roll".

**Rolls.** The taxable roll lists the taxable lines' assessed value of each TD; the exempt roll lists the exempt
lines' with the exemption's legal basis (the L5-2 `legalBasis` field). A partly exempt TD appears on both, each with
its part (Q4).

**Proof deadline.** Claims whose proof is overdue are listed (a worklist, and on the dashboard's pending items); PRIME
does not reject them automatically (Q5).

**Screens.** Exemption types under the reference catalogues (read-only, loaded from content; approval by a second
user); on the property profile a per-unit *Exemptions* panel to record a claim, attach evidence, approve or reject,
and end; the FAAS assessment rows and the TD show each line's taxability.

### 4.2 L3-2 — statutory maximum assessment levels

An optional, effective-dated `AssessmentLevelCeiling` table, loaded from the content pack: by property type
(land/building/machinery), classification or actual use, and value bracket, the maximum percentage. When a ceiling is
in force for a level's keys, creating or approving an assessment level above it is refused with the ceiling's legal
basis (Q6). With no ceiling configured, nothing changes. The values come from the LGC and are content, not code.

The use and kind codes the LAM rolls print are data: the province's actual-use codes are loaded through the content
pack (the lookup's `code`), so no code change is needed.

### 4.3 L3-3 — transaction catalogue

The LAM's transaction codes are loaded as `TransactionType` content (code, kind, effectivity rule, requirements),
replacing the DEMO catalogue in the province's database. Behaviour PRIME needs beyond today's kinds:

- **Court order** (`CourtOrder` kind): a cancellation, restoration or revival ordered by a court. Restoring a
  cancelled declaration issues a **new** TD that names the cancelled one, never reopens it. The order is a required
  document.
- **Machinery relocation** (`MachineryRelocation` kind): a machine moved to another property. The machine's unit on
  the old property is retired and a new unit is created on the receiving property, linked to the old one
  (`PreviousRpuId`); the old TD is cancelled by the new one, which may sit on a different property (Q7).
- **Reassessment due to depreciation** and **special projects** need no new kind: the first is a `Reassessment` type
  with `AllowsNewDepreciation`; the second a `Reassessment` (or `NewAssessment`) type with its own code (Q8).

**The code a TD shows (R-4, D1).** The LAM ranks no codes. Proposed rule: when ranks are configured, keep today's
highest-rank rule; when none is, the TD shows the code of the transaction that produced it, and for a TD produced by
an assessment, the assessment's own code (Q9).

### 4.4 L3-4 — discovery summons, Notice of Cancellation, pending-claim guard

**Discovery summons.** On a `NewDiscovery` transaction: `DiscoverySummons` records — addressee, date issued, how
served, date received, the **due date** (received + `Discovery:SummonsPeriodDays`, default 15), and the outcome
(`Complied`, `NotComplied`). A second summons is allowed only after the first is not complied with. When the second is
not complied with, the transaction records the inter-agency verification (a note and documents) and the assessor
declares the property (LGC §204, existing unknown-owner and owner-declaration paths). Summonses print from a form
(`DISCOVERY_SUMMONS`; the LAM gives no annex, so a PRIME provisional layout, watermarked, until the province supplies
one) (Q10).

**Notice of Cancellation.** A record and form (`NOTICE_OF_CANCELLATION`) generated when an approved transaction
cancels a TD **on the assessor's own motion** (a flag on the transaction type: `CancelsMotuProprio`), and when a
reassessment cancels the assessment of a previous owner (Book I p.35 §1 g). Addressees: the cancelled TD's declared
owners and the parties with a legal interest (administrators, legal-interest holders) as of the cancellation; one
notice per addressee address, like the combined NOA. Service is recorded as for the NOA (personal, registered mail,
Punong Barangay, email) (Q11).

**Pending court claim.** `AnnotationType.BlocksCancellation` (default no). While an annotation of such a type (for
example an adverse claim pending in court) is in force on a TD, no transaction may cancel or replace it; the refusal
names the annotation. The assessor lifts the annotation, with the court's resolution as reference, to proceed (Q12).

## 5. Data and migrations

Additive only:
- `ExemptionTypes` (effective-dated configuration), `PropertyExemptions`; evidence through `Documents`.
- `AssessmentLines.Taxability` (default Taxable), `AssessmentLines.PropertyExemptionId`; `Taxability.PartlyExempt`.
- `AssessmentLevelCeilings`.
- `PropertyTransactionKind.CourtOrder`, `MachineryRelocation`; `TransactionTypes.CancelsMotuProprio`.
- `DiscoverySummonses`; `NoticesOfCancellation` (+ addressees).
- `AnnotationTypes.BlocksCancellation`.

Existing posted assessments keep their lines; their new `Taxability` is set from their TD's taxability by the
migration (a fully exempt TD's lines become exempt), with no exemption record (pre-L3 history, R-11).

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| L3-1a | Exemption types (content, approval), claims with evidence and proof deadline, approve/reject/end, overdue worklist | M |
| L3-1b | Taxability per line at assessment; TD summary; rolls by line with legal basis; reassessment on later proof | M |
| L3-2 | Assessment-level ceilings (content) and the check on level approval | S |
| L3-3 | Court-order and machinery-relocation kinds; the no-rank code rule; the LAM catalogue loaded as content | M |
| L3-4 | Discovery summons; Notice of Cancellation; pending-claim guard | M |

Each step: build, tests, browser check of its screens, design-doc log, roadmap and memory (CLAUDE.md §108).

## 7. Exit criteria

1. A DEMO cooperative's land claims an exemption: listed taxable until proof is filed and approved; then a replacing
   TD lists it on the exempt roll with the exemption's legal basis; the earlier TD and roll entries are unchanged.
2. A DEMO government lot with a leased portion carries one taxable and one exempt line; it appears on both rolls with
   the right assessed values, and the FAAS and TD show each line's taxability.
3. An assessment level above a configured ceiling cannot be approved.
4. A discovery transaction records two summonses with their due dates before the assessor's declaration.
5. A motu proprio cancellation produces a Notice of Cancellation to the previous declarant; a TD under an adverse claim
   pending in court cannot be cancelled until the annotation is lifted.
6. A machine relocated to another property keeps its history across both properties.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Exempt part of a unit [C7]: separate unit or a line on the same FAAS? | A line on the same FAAS (R-9): `AssessmentLine.Taxability`, so one unit, one TD |
| Q2 | How does a TD with taxable and exempt lines show its taxability? | New value `PartlyExempt`; the TD form ticks both boxes and the rows say which line is which |
| Q3 | Exemption proven after the declaration | Approval opens a reassessment and a replacing TD through the normal workflow, effective per the transaction type; posted records never change |
| Q4 | A partly exempt TD on the rolls | On both rolls, each with its own lines' assessed value |
| Q5 | Overdue proof | A worklist and a dashboard count; no automatic rejection (the property simply stays listed as taxable, as LGC §206 says) |
| Q6 | Statutory maximum levels | Optional ceiling table as content; levels above it are refused at creation and approval |
| Q7 | Machinery relocation | New kind: retire the old unit, create a linked unit on the receiving property, the new TD cancels the old one across properties |
| Q8 | RA, SP and DEP codes [D1] | No new kinds: RA and SP are reassessment-type codes, DEP is a reassessment type that allows new depreciation (L1-5) |
| Q9 | Which code a TD shows when no codes are ranked [D1] | The code of the transaction (or assessment) that produced it; ranks still win where configured |
| Q10 | Discovery summons form | PRIME provisional layout, watermarked, until the province supplies its own; periods are settings (default 15 + 15 days) |
| Q11 | Notice of Cancellation | Generated for motu proprio cancellations (flag on the transaction type) and for a previous owner's cancelled assessment; served and recorded like the NOA |
| Q12 | Pending court claim | Annotation types flagged `BlocksCancellation`; such an annotation in force blocks any cancellation or replacement of its TD until lifted |
| Q13 | Exemption types shipped | DEMO types only in the repository; the province's list (with legal bases) as content, approved by a second user |
| Q14 | Assessed-value ceiling of an exemption (e.g. RA 11321) | A field of the exemption type; above it the line stays taxable with the reason recorded |
| Q15 | Existing posted assessments | Lines take their TD's taxability by migration; no exemption records are invented for them |

### 8.1 Decisions (user, 2026-10-02: "confirm all")

Q1–Q15 accepted as recommended. Where a question waits on the Provincial Assessor (C7 for Q1; D1 for Q8 and Q9), the
recommendation is the provisional default until the answer comes.

## 9. Implementation log

### L3-1a — exemption types and claims (2026-10-02)

Done; migration `Exemptions` (three new tables) applied to the local database only.

- `ExemptionType`: effective-dated configuration (code, name, description, kinds of unit it covers, proof required,
  optional assessed-value ceiling, legal basis), approved by a second user. Content-pack kind `exemption-types` (JSON
  catalogue); the sample pack has two DEMO types. Admin: *Forms & Numbering → Exemption types*.
- `PropertyExemption`: a claim on a unit, or on its part in one actual use, with claimant, instrument reference, date
  of declaration and proof due date (`Exemptions:ProofPeriodDays`, default 30, LGC §206). Statuses Claimed → ProofFiled
  → Approved/Rejected → Ended. A type that needs no proof starts at ProofFiled. One open or approved claim per unit,
  type and part. Approval and rejection by someone other than whoever recorded the claim (CLAUDE.md §46); approval
  records the effective date and an optional expiry; an approved exemption ends with a date and reason. Nothing is
  deleted. Jurisdiction-filtered through the property.
- **Evidence by reference** (`ExemptionEvidence`: document, reference number, date, received on and by). PRIME has
  no file storage yet (the `Documents` table has no upload pipeline, CLAUDE.md §59), so attaching scans waits for the
  documents module. Late proof is accepted and flagged; an overdue claim is flagged, never rejected automatically (Q5).
- Screens: the property's *Exemptions* tab (record a claim, file proof, approve, reject, end, decision trail and
  evidence); an *Exemptions* worklist page (open claims, overdue first, a warning with the overdue count). The
  dashboard count waits for the dashboard (it is still a placeholder).
- Nothing yet changes an assessment or a roll: that is L3-1b (below).
- Tests: `ExemptionTests` (2); content-pack import counts updated for the DEMO types; 628 tests pass. Browser: type
  created as the dev admin and approved as the dev checker; a claim on DEMO-BILL-AE94B8's land recorded, proof filed,
  approval by the recorder refused, approval by the checker, worklist. Dev DB keeps that DEMO type and claim.

### L3-1b — taxability per line, rolls, reassessment on later proof (2026-10-02)

Done; migration `LineTaxability` applied to the local database only.

- `AssessmentLine.Taxability` (Taxable/Exempt, check constraint), `PropertyExemptionId` and `TaxabilityNote`.
  `ExemptionTaxability.MarkAsync` is the single rule: a line is exempt under the approved exemption in force on the
  assessment's effective date for its actual use (a part's own exemption before one of the whole unit); over the type's
  assessed-value ceiling (the version in force) it stays taxable with the reason (Q14); an unproven claim leaves it
  taxable (LGC §206). Lines are marked when the assessment is calculated and again when it is made (final approval),
  so an exemption decided after the draft counts. An exemption is in force from its effective date to its expiry
  (inclusive) and until the day it ended (exclusive).
- `Taxability.PartlyExempt`: a TD declaring an assessment takes its taxability from the lines (on creation, on
  preparation at posting, and on approval); a TD request cannot ask for PartlyExempt. A TD without an assessment keeps
  the taxability it was declared with.
- Q15: the migration marks the lines of assessments declared by an exempt TD as exempt, with no exemption record.
  **Decided (user, 2026-10-02):** a unit with no exemption records at all whose current TD is declared exempt
  keeps its new lines exempt, with the note "Exempt as declared on TD …, before exemption records were kept; record
  the exemption", so declarations made before PRIME kept exemption records are not silently made taxable by the next
  assessment. Once any claim is recorded for the unit, only approved exemptions count.
- Q3: approving an exemption, **and ending one** (decided by the user, 2026-10-02: the same rule moves the unit back
  to the taxable roll), opens a Draft reassessment when the unit's assessment in force would now be marked differently: same
  valuation and values, lines re-marked, the posted assessment as its previous one, remarks naming the exemption. Its
  effectivity follows the transaction type named by the setting `Exemptions:ReassessmentTransactionCode` when its
  rule derives a date (empty by default, decided by the user 2026-10-02 until the Provincial Assessor names the code:
  DOMAIN VERIFICATION REQUIRED, which Annex I-D code applies); otherwise the exemption's own date, never before the assessment in force. None is opened when the latest assessment is approved
  but not posted, or one is in progress (it is marked when made); the decision returns the reason. Posting the
  reassessment prepares the replacing TD through the normal workflow; posted records never change.
- Rolls (Q4): each FAAS in force is split by its lines; the taxable roll lists the taxable part's assessed value, the
  exempt roll the exempt part's with the legal bases of its exemptions (`legalBasis`), and a partly exempt FAAS is on
  both (`lam.partlyExempt`, `lam.wholeAssessedValue`). A TD without an assessment is listed wholly as declared. The
  Record of Assessment's LAM columns split market and assessed values the same way.
- Forms: TD and FAAS rows carry `taxability`, `legalBasis` and `taxabilityNote`. Built-in reference layouts
  TAX_DECLARATION v5 and FAAS_LAND/BUILDING/MACHINERY v3 mark exempt rows with their basis and tick both boxes for a
  partly exempt TD (installed only where no LAM version exists). The untracked LAM versions of the same four forms and
  AR_EXEMPT were revised the same way and imported into the dev database as new versions effective 2026-10-03.
- Screens: a taxability tag (with the basis or note) on the assessment preview rows, the appraisal record's rows, each
  assessment (summary of its lines) and each TD; approving or ending an exemption says whether a reassessment was
  opened and why not; the claim shows its reassessment.
- Frozen treasury code: `BillService` still refuses any TD that is not wholly taxable, so a partly exempt TD cannot be
  billed there; billing is out of PRIME's scope (CLAUDE.md §0) and was not changed.
- Tests: `ExemptionTaxabilityTests` (4: a partly exempt TD on both rolls with the basis and the TD form's rows; late
  proof opening a reassessment that moves the unit to the exempt roll and ending it moving it back; the ceiling; a unit
  declared exempt before exemption records); renderer test of the new built-in versions; 640 tests pass. Browser: on
  DEMO property 990-01-0001-001-01, a claim recorded and proven as the dev admin and approved as the dev checker opened
  a reassessment effective 2026-10-02; posted and declared (the dev database has no TD numbering scheme for that
  municipality, so the TD was typed, and its request's "Taxable" was overridden by the lines to Exempt); the TD list,
  assessment list and appraisal record show Exempt with the basis; the LAM exempt roll lists the TD with the legal
  basis and the taxable roll does not. The revised LAM TD and FAAS templates were rendered against that TD.

### L3-2 — statutory maximum assessment levels (2026-10-02)

Done; migration `AssessmentLevelCeilings` (one new table) applied to the local database only.

- `AssessmentLevelCeiling`: effective-dated configuration keyed by code (property type; classification and actual use
  optional, blank meaning any; a value bracket read like the levels'; the maximum percentage; legal basis), approved by
  a second user. Content-pack kind `assessment-level-ceilings` (JSON, lookups by code, the pack may add them); the
  sample pack has one DEMO ceiling (effective 2099). Admin: *Valuation Rules → Level ceilings*. None is built in.
- The check: creating or approving an assessment level is refused (`ASSESSMENT_LEVEL_ABOVE_CEILING`) when a ceiling in
  force on the level's effective date matches its property type (and its classification or actual use, where the
  ceiling names one), its bracket overlaps the level's, and the level is above it. The message names the lowest such
  maximum, its keys, bracket, legal basis and code. With no ceiling configured nothing changes. Content-pack levels go
  through the same service, so the check applies to them at import.
- Approving a ceiling never changes levels already approved (they are the LGU's ordinance): it warns, naming the
  ordinances of approved levels in force above it, that a new level must replace them.
- Assessments are not re-checked: they use approved levels, which were checked when approved.
- Tests: `AssessmentLevelCeilingTests` (3); content-pack import counts updated for the DEMO ceiling; 643 tests pass.
  Browser: a DEMO ceiling (Land / DEMO_Residential, 15%, effective 2099-01-01) created as the dev admin and approved as
  the dev checker, with the warning about an approved 20% level of that key; a new 20% level from 2099-01-01 refused
  with the ceiling's basis. The dev database keeps that DEMO ceiling.

### L3-3 — court orders, machinery relocation, codes (2026-10-02)

Done; migration `CourtOrderAndRelocation` (two nullable columns) applied to the local database only.

- **Court order** (`PropertyTransactionKind.CourtOrder`): a type of this kind must list at least one mandatory
  requirement (the order). It cancels TDs like any transaction (the TDs it lists). To restore or revive a cancelled
  declaration, a TD added to a court-order transaction names it in `RestoresTaxDeclarationId` (a cancelled TD of the
  same unit); the new TD takes the next revision number, brings back the restored TD's unlifted annotations that carry
  over, and the cancelled TD stays cancelled. Restoring outside a court order is refused, and one declaration is
  restored by at most one approved TD.
- **Machinery relocation** (`MachineryRelocation`, Q7): filed on the receiving property, naming the moving machinery
  unit (`RelocatedRpuId`, an active machinery unit of another property, whose property is added as the Source). The
  unit on the receiving property continues the moved one (`PreviousRpuId`); its TD, in the transaction, names the moved
  unit's current TD as the one it replaces, across properties. Submission needs that TD. Approval cancels the old TD
  ("Cancelled by TD No. …") and retires the moved unit (`Superseded`, ended the day before the effective date). The
  drawer creates the receiving unit with the link; its machinery details, valuation and assessment follow the usual
  screens.
- **Code a TD shows** (Q9): `TransactionCodes.Highest` keeps the highest rank where ranks are configured; with none
  ranked, the first candidate wins, and candidates are listed transaction, named code, assessment — so the producing
  transaction's code, else the assessment's. Behaviour unchanged; documented and tested.
- **LAM catalogue as content** (untracked `lgu-content/`, pack version `…+lam-codes-1`): the eleven Annex I-D codes,
  unranked, with kinds per Q8 (RA, PC, DT, SP and DEP are reassessments; DEP allows a new depreciation). Effectivity
  rules only where Book III §6 is explicit: NextJanuary for RA, PC, SP and DEP; NextQuarter with the 90-day window for
  DT (partial destruction) and RC (change in actual use); Fixed for GR; none for SD, CS, DC and TR (dates given per
  transaction). Requirements are left to the province's Citizen's Charter. Imported and approved in the dev database,
  effective 2026-10-03 (provisional). The LAM gives no codes for court orders or machinery relocation: their codes are
  for the Provincial Assessor (added to question D1); the dev database has DEMO-CO and DEMO-MR types for checks.
- Screens: new kinds in the type admin; a relocation picker (property, then its machinery unit) when opening a
  relocation; the drawer explains a court order, and for a relocation creates the receiving unit; the TD modal shows
  "Restores TD" under a court order and offers the moved unit's TD as the one replaced under a relocation.
- Tests: `CourtOrderAndRelocationTests` (4); 647 tests pass. Browser, on DEMO property 990-01-0001-001-01: a machine
  (DEMO-MACH-L16, TD DEMO-TD-MACH-L33) relocated from DEMO-BILL-AE94B8 — unit created from the drawer, its TD added
  replacing the old one, submitted, approved: old TD cancelled by the new one, old unit Superseded; a court order
  cancelling DEMO-TD-L31B-77222 with its order recorded, then a second restoring it by a new TD (revision 3), the
  original staying cancelled. Approvals beyond the first signature were completed through the API.


### L3-4 — discovery summons, Notice of Cancellation, pending-claim guard (2026-10-02)

Done; migration `SummonsAndNoticesOfCancellation` applied to the local database only.

- **Pending court claim** (Q12): `AnnotationType.BlocksCancellation` (default off; set through the content pack's
  `blocksCancellation`). `CancellationGuard` refuses (`TD_CANCELLATION_BLOCKED`, naming the annotation and its
  reference) while such an annotation is unlifted, wherever a TD is cancelled or replaced: a direct cancellation, a
  transaction listing it (when opened and again at approval) and the approval of a TD naming it as the one replaced.
  Lifting the annotation, with the court's resolution as reference, clears the way.
- **Notice of Cancellation** (Q11): `TransactionType.CancelsMotuProprio` (content pack `cancelsMotuProprio`; switch
  in the type admin). Approving such a transaction drafts a `NoticeOfCancellation` for each TD it cancels, one per
  addressee address, to the declared owners and the parties with a legal interest; the previous-owner case drafts one
  when a TD declaring a new assessment replaces a TD declared in the name of owners who no longer own the unit (a
  transfer re-declaring the same assessment does not). The notice freezes the TD numbers, ground, reason, date and
  addressees; it is issued (numbered by a `NoticeOfCancellation` scheme, if any), served as for the NOA, or
  cancelled with a reason. Form `NOTICE_OF_CANCELLATION` v1, PRIME provisional and watermarked.
- **Discovery summons** (Q10): `DiscoverySummons` on an open new-discovery transaction: addressee, issue date,
  service (mode, receipt, receiver, proof), due date = receipt + `Discovery:SummonsPeriodDays` (15, frozen on the
  summons), outcome Complied or NotComplied (non-compliance only after the due date). A second summons only after a
  first not complied with; after a second not complied with, the transaction records the verification with other
  agencies (`InterAgencyVerification`). Summonses never block the transaction (LGC §204 declaration). Numbering kind
  `DiscoverySummons`; form `DISCOVERY_SUMMONS` v1, PRIME provisional and watermarked, legal basis from
  `Discovery:SummonsLegalBasis`.
- Screens: Notices tab, "Notices of Cancellation" table (issue, record service, print, cancel; the NOA's service
  dialog is shared); transaction drawer, "Discovery summonses" panel for a new-discovery transaction (issue, record
  service, record outcome, print, verification) with the next step shown; type admin switch and column "Notice of
  Cancellation"; the two new numbering kinds in the numbering admin.
- Tests: `CancellationAndDiscoveryTests` (4); 653 tests pass. Browser, dev database (DEMO): type DEMO-MP created
  with the switch and approved by the checker; annotation type DEMO-ADVERSE (blocking) added to the dev database
  directly; TD DEMO-TD-VA-2027B on DEMO-BILL-AE94B8 annotated, opening a DEMO-MP cancellation refused with the
  annotation named, lifted, opened and approved (through the API as the checker): one notice drafted to the declared
  owner, then issued, served and printed from the Notices tab. Discovery on 990-01-0001-001-01 (type DEMO-DC, since
  the LAM's DC is in force only from 2026-10-03): two back-dated summonses served and not complied with, due dates
  2026-08-28 and 2026-09-19, then the verification recorded; summons 1 printed.

**L3 complete.** Exit criteria 1–3 and 6 were met in L3-1 to L3-3; 4 and 5 by L3-4.
