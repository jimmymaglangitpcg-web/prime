# Records and Forms — Design (Step L5)

| | |
|---|---|
| Date | 2026-10-02 |
| Status | **Approved 2026-10-02: all recommendations Q1–Q16 accepted (§8.1).** Implementation in progress (§9) |
| Rules | CLAUDE.md §7, §43, §48–§49, §58, §68, §97 (L5), §114, §117–§118 |
| Sources | LAM 2025 Book I Ch. II (assessment records, pp.17–26); Annexes I-C (TMCR, pp.136–137), I-D to I-F (FAAS land, building, machinery, pp.138–163), I-G (TD, pp.164–167), I-H and I-I (assessment rolls, pp.168–171), I-J (ORF, pp.172–173), I-K (ROA, pp.174–175), I-L (NOA, pp.176–177); Book III p.88–89 (cancellation) |
| Builds on | The forms foundation (`docs/FORMS-REVISION-PLAN.md`, `docs/analysis/mrpaao-forms-model.md`): versioned Liquid form definitions, frozen issued snapshots, register runs, the content pack's `forms` kind (`docs/analysis/lgu-content-pack.md`) |
| Commit status | Cites and paraphrases the LAM; reproduces none of its layouts, tables or code lists. May be committed (§118). The LAM templates themselves are content and stay in the untracked `lgu-content/` |
| Depends on | L0-4 answers D2 (annotation carry-over), D3 (exempt-roll legal basis), D4 (past owners' ORFs), D5 (owner's sex). Choices that depend on one are marked **[D*n*]** with the provisional default |

## 1. Purpose

PRIME prints its assessment records through versioned form definitions. The built-in
versions follow the MRPAAO attachments. The LAM 2025 replaces those forms: the FAAS for land,
buildings and machinery, the Tax Declaration, the Notice of Assessment, the Tax Map Control
Roll, the two Assessment Rolls, the Ownership Record Form (formerly Card) and the Record of
Assessment. The form mechanism does not change: a LAM version is a new version of each form,
loaded as content under maker-checker. L5 supplies what the LAM versions need that PRIME does
not yet record or hand to the templates, and authors those versions in the untracked LGU
content pack.

## 2. What the LAM says (paraphrased)

- **All records are extracted from the FAAS** (Annex instructions, pp.164, 168, 172). The FAAS
  is the source; the TD, rolls, ORF and ROA repeat its values.
- **FAAS, every kind** (pp.138–144): a data-privacy notice; TD number, PIN and transaction code;
  whether the land is titled, the title type (including CLOA, and CCT on the TD), title number
  and registration date; owner and administrator/beneficial user/possessor, each with sex,
  mailing address, TIN, phone and email; boundaries given as the last digits of the adjoining
  PINs; the appraisal and assessment blocks; effectivity by year and quarter; taxability; the
  **period covered by back taxes**; the record of superseded assessment (previous TD, PIN,
  market and assessed value, previous owner, effectivity); signatures for appraised, assessed,
  recommending approval and approved by, plus **entry in the Assessment Roll by** and **encoded
  by**, each with a date.
- **Building FAAS** (pp.145–156) adds property type, structural type, building age, per-floor
  areas, material codes, depreciation rate, depreciated and adjusted market value. **Machinery
  FAAS** (pp.157–163) adds engineering registration number and date, importation permit number
  and date, country of origin, supplier and address, official receipt number and date, the land
  **and** building references, condition when acquired, years used, remaining life and
  depreciation rate.
- **TD** (pp.164–167): kind of property (land, building, machinery, others); per row the
  classification, sub-class or structural type or machine description and brand, area or
  capacity, market value, actual use, level and assessed value; the total assessed value **in
  words**; effectivity quarter and year; back-tax period; taxability; what the declaration
  cancels (TD number, owner, assessed value); a note citing the SMV and the **Sanggunian and
  the tax ordinance number and date**; annotations on the back.
- **TMCR** (pp.136–137) adds the cadastral number, the previous PIN and the market value, with
  counts of buildings, machines and other improvements and the transaction code as remarks.
- **Assessment Rolls** (pp.168–171): general-revision year, index numbers, date prepared, TD
  and previous TD, section and parcel, owner and administrator with their addresses, kind code
  (L, B, M), use code, assessed value, effectivity and remarks; page numbers run from 1. The
  exempt roll's instructions list no legal-basis column (the MRPAAO's had one).
- **ORF** (pp.172–173): owner with address, email, phone and TIN; province and municipality
  with index numbers; per property the kind, classification, TD, location, area or brand/model/
  capacity, market and assessed value, **NOA number** and remarks. Book I p.21 allows ORFs of
  deceased, moved-away or sold-out owners to be set apart and disposed of after five years.
- **ROA** (pp.174–175): per recorded transaction the date, TD, owner, section–parcel number,
  location, classification, land and building area, market and assessed value of taxable and
  exempt property, effectivity and transaction code.
- **NOA** (pp.176–177): adds delivery **by email** with the declarant's email address, and a date
  delivered, date received, date mailed and registered-mail number in the proof of service.
- Several annex layouts (the rolls, ORF, ROA) are images. Their written instructions give the
  columns; the exact arrangement must come from the province's copies.

## 3. What PRIME has, and the gaps

| Record | PRIME today | Gap |
|---|---|---|
| All forms | Versioned definitions per form code, MRPAAO built-ins, pack import of `Lam` versions as Drafts, frozen snapshots | No LAM versions written yet |
| Parties | Name, address, TIN, contact, email on `Taxpayer`; FAAS/TD data hand over TIN and contact but **not email** | Sex (none); email not passed to forms |
| Property | Title type, number and date; survey, lot, block; boundaries as text | Registration type (derivable); cadastral number (none) |
| FAAS | Owners, administrators, superseded record (no market value), land/building references, appraisal rows, approval-chain signatures | Back-tax period; superseded market value; AR entry (page, by, date); encoded by; email |
| Machinery | Acquisition, import, currency, origin, index series, dates, lives (L1-6) | Engineering registration, importation permit, supplier, OR |
| TD | Rows, effectivity, cancels block, SMV ordinance, annotations, transfer clearance | AV in words; back-tax period; tax-ordinance citation and Sanggunian; per-row sub-class/structural type/capacity; annotation carry-over to the successor TD |
| TMCR | MRPAAO columns | Cadastral number, previous PIN, market value |
| Rolls | MRPAAO columns, kind code, class code | GR year present; administrator and address; page numbers persisted |
| ORF | "Ownership Record Card" (MRPAAO Att. 8) | Name; email, TIN; NOA number; past owners hidden, never deleted |
| ROA | Per transaction, L/B/M split | Section–parcel, building area, taxable/exempt split as the LAM lists it |
| NOA | Personal, registered mail, Punong Barangay; received date | Email mode, email address, date emailed |
| Signatories | Name, position, office, delegation from approval records | REA licence number and validity (Book I p.9) |

## 4. Proposal

### 4.1 L5-1 — data the LAM forms need (additive)

- `Taxpayer.Sex` (Male, Female, not stated), optional; captured on the taxpayer screen; printed
  only when `Forms:PrintOwnerSex` is on (default off; Data Privacy Act minimisation) **[D5]**.
- `Property.CadastralNumber` and `Parcel.CadastralNumber` (the parcel overrides, as survey and
  lot numbers already do).
- Registration type **derived**: titled when a title number is recorded.
- Machinery: `EngineeringRegistrationNumber/Date`, `ImportPermitNumber/Date`, `SupplierName`,
  `SupplierAddress`, `ReceiptNumber/Date` — optional, on the valuation-inputs dialog.
- NOA: `NoticeServiceMode.Email` with `ServedToEmail` and the date sent; the received date stays
  the date the appeal period counts from.
- Signatory licence: `AppUser.ReaLicenceNumber` and `ReaLicenceValidUntil`, printed with the
  name; a sign-off by a user without a valid licence on a step marked *licensed signatory
  required* raises a warning, not a block **[I3 — verification required]**.
- `Office.SanggunianName` (e.g. the Sangguniang Panlalawigan), used in the TD note.

### 4.2 L5-2 — form data

Each provider adds a `lam` object; the MRPAAO templates keep reading the existing fields, so
nothing they print changes.

- **FAAS:** registration type; parties with sex (when enabled) and email; back-tax period from
  the assessment's back-tax run; superseded market value; `assessmentRollEntry` (page, by, date)
  from §4.3; `encodedBy` (the TD's creator and date); machinery registration, permit, supplier
  and receipt.
- **TD:** total assessed value in words (§8 Q9); back-tax period; the ordinance citation —
  the Sanggunian from the office and the distinct ordinance numbers and dates of the assessment
  levels the lines applied; per row the sub-class, structural type or machine description and
  brand, and area or capacity; the kind as land, building, machinery or others.
- **TMCR:** cadastral number, previous PIN (the retired PIN the parcel replaced, from PIN
  history), market value.
- **Rolls:** administrator and address; page and line (from §4.3); legal basis on the exempt
  roll **[D3]** (null until exemptions carry one in L3).
- **ORF:** email, TIN, NOA number per property, brand/model/capacity for machinery; past owners
  (no property in force) included only when the run asks for them **[D4]**.
- **ROA:** section–parcel number, building area, market and assessed values split taxable and
  exempt.
- **NOA:** kind and PIN per item, the addressee's email.

### 4.3 L5-3 — Assessment Roll entries

When an Assessment Roll (taxable or exempt) is issued, PRIME records one `AssessmentRollEntry`
per TD: the issued form, the page and line, the date and the issuing user. Rows per page is a
setting of the register (`Registers:AssessmentRollRowsPerPage`), so the page numbers PRIME
stores are the ones printed. The FAAS then shows "entry in the Assessment Roll" (who and when)
and the superseded TD's roll page. A cancelled roll's entries are kept and marked cancelled;
the latest non-cancelled entry wins.

### 4.4 L5-4 — annotation carry-over

When a TD is replaced, every annotation still in force on the old TD is copied to the new one,
pointing back to its source, unless its annotation type is marked as not carried over
(`AnnotationType.CarriesOver`, default yes). Subdivision and consolidation carry them to every
successor TD. The assessor lifts any that no longer apply, with a reason, as today **[D2]**.

### 4.5 L5-5 — ownership record form

The register keeps its stored kind; screens and the LAM version say "Ownership Record Form".
Past owners' forms are never deleted: the ORF run gains *include past owners* (off by default),
which is the "set apart" the LAM describes, without disposal **[D4]**.

### 4.6 L5-6 — LAM templates as content

The ten LAM versions (FAAS land, building, machinery; TD; NOA; TMCR; AR taxable and exempt;
ORF; ROA) are written as Liquid templates in `lgu-content/zamboanga-sibugay/forms/`, authority
`Lam`, new versions of the existing form codes, imported as Drafts and approved by a second
user. They are verified by issuing each against DEMO records in the browser. They are not
committed. The repository's DEMO pack gains nothing LAM-derived. Where an annex layout is an
image, the template follows the written column list and is marked for the province's review.

## 5. Data and migrations

One additive migration, `LamRecords`: `Taxpayers.Sex`; `Properties.CadastralNumber`,
`Parcels.CadastralNumber`; six machinery columns; `NoticesOfAssessment.ServedToEmail` and the
date sent; `AppUsers.ReaLicenceNumber`, `ReaLicenceValidUntil`; `Offices.SanggunianName`;
`AnnotationTypes.CarriesOver`; `TaxDeclarationAnnotations.CarriedFromAnnotationId`; table
`AssessmentRollEntries` (issued form, TD, page, line, entered by, entered on), filtered by
jurisdiction through its TD. Nothing is renamed or dropped. Local database only until the user
asks.

## 6. Delivery steps

| Step | Content | Size |
|---|---|---|
| L5-1 | §4.1 fields, migration, screens (taxpayer, property/parcel, machinery inputs, NOA service, user licence, office) | M |
| L5-2 | §4.2 `lam` form data in the FAAS, TD, NOA and register providers; tests | M |
| L5-3 | §4.3 roll entries on issuance, the FAAS roll entry and superseded page | S |
| L5-4 | §4.4 annotation carry-over | S |
| L5-5 | §4.5 ORF naming and the past-owners option | S |
| L5-6 | LAM templates in `lgu-content/`, import, approve, issue each in the browser | M |

## 7. Exit criteria

1. Every LAM form field listed in §2 is either printed from PRIME data or explicitly left to
   the template as a blank the office fills by hand (the data-privacy notice text, the land
   sketch).
2. A DEMO land, building and machinery unit each issue a LAM FAAS and TD; their NOA, a TMCR, both
   rolls, an ORF and an ROA issue from the same records, values matching the FAAS.
3. The MRPAAO versions still issue unchanged; issued snapshots are untouched.
4. A replaced TD carries its unlifted annotations; the FAAS shows its roll entry.

## 8. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | Where do the LAM templates live? | In `lgu-content/` (untracked), imported as Draft `Lam` versions; the repository keeps the MRPAAO built-ins as reference layouts |
| Q2 | New versions of the existing form codes, or new codes? | New versions of the existing codes: every screen and issue path picks them up by effective date; the MRPAAO versions stay for earlier dates |
| Q3 | Owner's sex **[D5]** | Optional field; printed only when `Forms:PrintOwnerSex` is on (default off) |
| Q4 | Registration type (titled/untitled) | Derived from the title number; no new field |
| Q5 | AR page numbers | Stored on issuance (§4.3), rows per page a register setting |
| Q6 | "Entry in the AR by" and "encoded by" | The user and date that issued the roll containing the TD; the TD's creator and creation date |
| Q7 | Annotation carry-over **[D2]** | Copy unlifted annotations to every successor TD, per-type opt-out, lift as today |
| Q8 | The TD's tax-ordinance citation | The office's Sanggunian plus the ordinance numbers and dates of the assessment levels applied |
| Q9 | Assessed value in words | Uppercase English words for pesos, centavos as "NN/100" (e.g. "ONE HUNDRED TWENTY THOUSAND PESOS AND 50/100"); the template may ignore it |
| Q10 | REA licence **[I3]** | Record and print it; warn, do not block, on a licensed-signatory step until the province confirms the rule |
| Q11 | NOA by email | New service mode with the address and date sent; the appeal period still counts from the received date entered |
| Q12 | Past owners' ORFs **[D4]** | Kept; hidden unless the run asks for them; never disposed of by PRIME |
| Q13 | Notice of Cancellation and the pending-adverse-claim guard (Book III pp.88–89) | Step L3, where CLAUDE.md §97 places them; L5 does not build them |
| Q14 | Machinery registration, permit, supplier, receipt | Optional fields on the machine, entered with the valuation inputs |
| Q15 | Exempt roll legal-basis column **[D3]** | The data carries it (null until L3 records exemptions with a basis); the template prints it |
| Q16 | Cadastral number | On the property, overridable per parcel |

### 8.1 Decisions (user, 2026-10-02: "confirm all")

Q1–Q16 accepted as recommended. The **[D*n*]** defaults apply until the Provincial Assessor answers.

## 9. Implementation log

### L5-1 — data the LAM forms need (2026-10-02)

Done; migration `LamRecords` (additive) applied to the local database only.

- `Taxpayer.Sex` (individuals only; refused for other types), on registration and on a new *Edit details* dialog of
  the taxpayer registry (`PUT /api/taxpayers/{id}/details`, with a reason, audited). Printing waits for L5-2 and
  `Forms:PrintOwnerSex`.
- `Property.CadastralNumber` (description dialog, shown in Basic Information) and `Parcel.CadastralNumber` (add-parcel
  dialog).
- Machinery acquisition documents (engineering registration, importation permit, supplier and address, official
  receipt, with dates) on the machine **description** dialog rather than the valuation-inputs dialog: they are
  descriptive and change no value (deviation from §4.1). Returned as `MachineryDto.Documents`.
- NOA service mode `Email` with `EmailAddress` (required for that mode; check constraint) and `SentDate` (any mode
  other than personal; between issue and receipt). The appeal period still counts from the received date.
- `Office.SanggunianName` on the office screen and in content-pack office items; the letterhead uses it, else the
  `Lgu:SanggunianName` setting.
- `AppUser.ReaLicenceNumber`/`ReaLicenceValidUntil`: *Signatory licences* tab of the Offices page
  (`PUT /api/users/{id}/licence`, both or neither, with a reason). `ApprovalChainStep.RequiresLicensedSignatory`
  (chain editor and content packs). Each `ApprovalRecord` freezes the signer's licence valid on the signing date and
  sets `SignedWithoutValidLicence` when a licensed step is signed without one. The warning is kept on the record and
  shown in the assessment's signature table; it is not returned from the approve call, and nothing is blocked [I3].
- §5 planned one migration for all of L5; `AnnotationTypes.CarriesOver`, `CarriedFromAnnotationId` and
  `AssessmentRollEntries` come with L5-4 and L5-3 in their own migrations.
- Tests: `LamRecordsTests` (5), `EmailedNotice_NeedsTheAddress_AndKeepsTheDateSent`,
  `LicensedSignatoryStep_WarnsWithoutAValidLicence_AndFreezesTheLicence`; 600 tests pass. Verified in the browser:
  taxpayer details with sex, property cadastral number, machine documents, office Sanggunian, licence tab, chain-step
  checkbox. The email service dialog was not exercised in the browser (no issued notice in the dev data).
- Dev DB (DEMO): DEMO-BILL has cadastral number "DEMO Cad-L51"; its machine DEMO Mill has documents; one office has
  Sanggunian "DEMO Sangguniang Panlalawigan"; DEMO Municipal Appraiser has licence DEMO-REA-0001; taxpayer
  "DelaCruz, Juan" (TIN-f0e2ad67) is Female.

### L5-2 — form data (2026-10-02)

Done; no migration, no screen. Each provider adds a `lam` object; the MRPAAO fields are unchanged (all earlier form
tests pass, exit criterion 3).

- **FAAS** (`lam`): `registrationType` (Titled when a title number is recorded), `cadastralNumber`, `owners` and
  `administrators` with TIN, contact, email and `sex`, `backTaxPeriod`, `supersededMarketValue`, `encodedBy` (the TD's
  creator and date), `machines` (acquisition documents in the order of the machine rows).
- **TD** (`lam`): `kind` (Land, Building, Machinery, Others), registration type, cadastral number, parties,
  `assessedValueInWords`, `backTaxPeriod`, `ordinances` (distinct ordinance numbers and dates of the assessment levels
  applied); the Sanggunian is `lgu.sanggunianName` (office, else setting). `rows[]` per assessment line: sub-classes,
  structural type, machines (type, brand, model), area, capacity.
- **NOA**: `lam.addresseeEmail` (the combined notice's addressee, else the first current owner with one), service mode,
  email address, dates sent and received; each item adds `lamKind` (its `pin` was already the unit PIN).
- **Registers**: every row adds `lam`. TMCR: cadastral number (parcel, else property), `previousPin` (the property's
  latest PIN retired by the run date, else the PINs of the properties a posted subdivision or consolidation produced it
  from), market value. Rolls: administrator and address. ORF: notice number, machines (brand, model, capacity); the
  heading's `lam` adds the owner's email and sex. ROA: `sectionParcel` (section index and the parcel number as the PIN
  prints it), building floor area, market and assessed values split taxable/exempt.
- `Forms:PrintOwnerSex` (default false) gates every `sex` value [D5].
- **Single source for amounts in words:** the form renderer already had an `amount_words` filter. Its code moved to
  `Prime.Domain.DomainServices.AmountInWords`, which the filter and the TD data both call. The output is unchanged:
  "… PESOS ONLY" for whole pesos, "… PESOS AND NN/100" with centavos (Q9 gave only the centavos example).
- Left to later steps, as planned: the Assessment Roll entry and page/line (L5-3), past owners on the ORF (L5-5), the
  exempt roll's legal basis (L3).
- Tests: `LamFormDataTests` (5), `AmountInWordsTests` (10 cases), renderer filter test reworked; 615 tests pass.

### L5-3 — Assessment Roll entries (2026-10-02)

Done; migration `AssessmentRollEntries` (one new table, additive) applied to the local database only. No screen.

- `AssessmentRollEntry`: issued form, TD, kind (taxable or exempt roll), page, line, entered on (the local issue date)
  and entered by. Unique per issued form and TD, and per issued form, page and line. Jurisdiction-filtered through its TD.
- The register provider numbers each roll row (`lam.page`, `lam.line`) from `Registers:AssessmentRollRowsPerPage` and
  returns the lines with the form data; `FormService.IssueAsync` saves them with the issued form, in the same save. This
  covers rolls issued by hand and those issued by the monthly submission to the province (LP-6). A re-issue of an
  already issued roll returns it and records nothing.
- Rows per page belongs to the office's roll template. The setting ships as 0, which numbers every line on page 1;
  the office sets it with the LAM template in L5-6. No page size is assumed.
- A cancelled roll keeps its entries. They are not marked separately: an entry counts as cancelled when its issued
  form is (one source for the status). The FAAS (`lam.assessmentRollEntry`: kind, page, line, date, by) prints the
  TD's latest entry in a roll still valid, and `lam.supersededRollEntry` the previous TD's.
- Tests: `RollPosition_FollowsTheRowsPerPage` (6 cases), `IssuingAnAssessmentRoll_RecordsTheEntry_AndTheFaasPrintsIt_UntilTheRollIsCancelled`;
  622 tests pass.

### L5-4 — annotation carry-over (2026-10-02)

Done; migration `AnnotationCarryOver` (two columns, two indexes, one foreign key; additive) applied to the local
database only.

- `AnnotationType.CarriesOver`: yes for every existing type (set by the migration) and for new ones; a content pack
  may give an optional `carries_over` column for `annotation-types` (yes/no). The column has no database default, so
  a "no" is stored as given.
- `TaxDeclarationAnnotation.CarriedFromAnnotationId` points to the annotation it was copied from. One copy per source
  annotation per TD (unique index).
- **When:** at the final approval of a TD, in the shared approval step, so direct TD approval and transaction approval
  behave alike. Approving a TD that replaces a previous TD copies the previous TD's annotations that are not lifted and
  whose type carries over. The copy keeps the type, text, reference and effective date; the source stays on the
  cancelled TD unchanged.
- **Subdivision and consolidation:** every TD the transaction ends (those it cancels and those its own TDs replace)
  passes its annotations to each TD the transaction issues **of the same kind**: land to land, building to building.
  This reads "every successor TD" (Q7) as the successors of the same unit kind, so a levy on the mother land does not
  appear on a building's TD.
- Lifting is unchanged: a copy is lifted on its own TD, with a reason. Lifting one does not lift the other.
- The annotation list returns `carriedFromAnnotationId` and the source TD number; the TD's *Annotations* dialog shows
  "Carried over from TD …".
- Tests: `ApprovingReplacementTd_CarriesUnliftedAnnotations_PointingBackToTheirSource`,
  `Subdivision_CarriesTheEndedTdsAnnotations_ToTheResultingTdsOfTheSameKind`,
  `AnnotationTypes_CarryOverByDefault_UnlessThePackSaysNo`; 625 tests pass. Browser: the dialog shows the carried-from
  line (DEMO copy inserted in the dev database on DEMO-TD-VA-2027B from DEMO-TD-AE94B8-R5).

### L5-5 — ownership record form (2026-10-02)

Done; migration `OrfPastOwners` (one column, default false, and a check constraint; additive) applied to the local
database only.

- **Name:** screens and messages say "Ownership Record Form". The stored kind (`OwnershipRecordCard`), the form code
  (`ORC`) and the MRPAAO built-in layout titled "Ownership Record Card" are unchanged; the LAM version (L5-6) gives its
  own title.
- **Past owners [D4]:** `RegisterRun.IncludePastOwners`, ORF only (check constraint). Without it, a run for a taxpayer
  who owns nothing in force on the date (in the user's jurisdiction) is refused (`OWNER_HOLDS_NO_PROPERTY`); this is
  the "set apart". With it, the run is allowed, and after the units held the form lists every unit the owner held
  before, as declared on the last day held, with `lam.past` = { `heldUntil`, `endReason` } (null for a unit still
  held). A unit listed once is not repeated. PRIME deletes nothing.
- The registers page has an *Include past owners* checkbox for the ORF; the runs list tags such runs "with past
  holdings".
- Test: `OwnershipRecordForm_ListsPastHoldings_OnlyWhenTheRunAsks`; 626 tests pass. Browser: refusal message, then
  the run created with the box ticked (DEMO-E2E, Maria, who holds nothing).

### L5-6 — LAM templates as content (2026-10-02)

Done. The templates are content, not code: they are in the untracked pack `lgu-content/zamboanga-sibugay/forms/`
(CLAUDE.md §118) and nothing LAM-derived is committed.

- **Ten templates**, authority `Lam`, new versions of the existing codes (Q1, Q2): `FAAS_LAND`, `FAAS_BUILDING`,
  `FAAS_MACHINERY`, `TAX_DECLARATION`, `NOTICE_OF_ASSESSMENT`, `TMCR`, `AR_TAXABLE`, `AR_EXEMPT`, `ORC` (the Ownership
  Record Form) and `ROA`. Each is laid out from its annex's written instructions; the arrangement is PRIME's and every
  form says so in its header line. The pack's `forms/forms.json` lists them; the manifest is at version
  `…+lam-forms-1`. Effective date 2026-10-02, provisional.
- **Form data added** (committed, additive; the MRPAAO templates are unchanged) where an annex asks for something
  PRIME records but did not pass to the forms: building rows' depreciation rate and amount; machine rows' acquisition
  cost and installation-and-other cost; the machine documents' imported flag, country of origin and acquisition cost;
  the building reference's TD number; the title type code (FAAS and TD `lam`); the TMCR's machine count; the rolls'
  section index, parcel number and actual-use code; the ORF row's municipality; the NOA's person served and proof
  reference, and each item's actual use and effectivity.
- **Verified** in one rolled-back transaction on the dev database (a temporary harness, not committed, because it
  reads the untracked pack):
  1. the pack previews clean (only the ten forms new) and imports as one DEMO user;
  2. a second DEMO user approves the ten drafts;
  3. DEMO land, building and machinery units on one property, each valued, assessed and declared, issue the LAM
     FAAS and TD; the taxable roll, the exempt roll, the TMCR, the ORF and the ROA issue for the same barangay and
     owner; the NOA issues after being served by email.

  Values agree across the forms: land MV 500,000, AV 100,000; building 1,030,000 less 25 % = 772,500, AV 386,250;
  machine replacement cost 1,200,000 less 60 % = 480,000, AV 384,000 (in words on the TD); ORF and ROA totals both MV
  1,752,500, AV 870,250. The FAAS print their roll entries (page 1, lines 1–3). Each form was rendered and read in
  the browser. Nothing stayed in the dev database (no `Lam` form definitions after the run).
- **Approved in the dev database** (user decision, 2026-10-02): the pack was imported through the API as the dev
  admin and the ten forms approved as the dev checker; the LAM versions are now in force there (effective
  2026-10-02), the MRPAAO versions ended the day before. The six test classes that assert the reference layouts
  (`MrpaaoFormsTests`, `NoticeOfAssessmentTests`, `PartiesAndTdLifecycleTests`, `FormsFoundationFlowTests`,
  `RegistersTests`, `TaxMapRollsTests`) call `TestSeed.UseReferenceFormsAsync`, which puts the built-in versions
  back in force inside the test's own rolled-back transaction. The shared (Supabase) database has no LAM forms.
- Exit criteria (§7): 1 met, with the blanks below; 2 met in the rolled-back run; 3 met (all earlier form tests
  pass); 4 met (L5-3, L5-4 tests and this run).

**Left for the Provincial Assessor's review** (DOMAIN VERIFICATION REQUIRED):

1. The arrangement of every form, especially those whose annex layout is an image (TMCR, both rolls, ORF, ROA).
2. *Appraised by* and *Assessed by* both print the assessment's preparer; PRIME records one. The chain's earlier
   steps print under *Recommending approval*, its last step under *Approved by*.
3. The date the province adopts the LAM forms (the pack's effective date is provisional).
4. Blanks the office fills by hand: the data-privacy notice, the land sketch and floor plan, the TMCR and ROA page
   numbers, the ORF's province and municipality index numbers, the CCT registration date, and the NOA's *delivered
   by*.
5. The exempt roll's legal-basis column, which the annex does not list (printed by Q15; blank until L3).
