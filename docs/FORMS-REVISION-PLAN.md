# PRIME — Forms & Records Revision Plan

**FAAS, Tax Declaration and related assessment forms: from the initial
system to the forms prescribed under the Local Assessment Manual (LAM)**

Status: **A1–A8 implemented 2026-09-25**; A9–A10 not started. See §11
(A1–A3), §12 (A4), §13 (A5), §14 (A6), §15 (A7) and §16 (A8) for what was
built and where it deviates from this plan.

---

## 1. Summary

The current forms are "prescribed under the Local Assessment Manual (LAM)"
(DOF Department Circular No. 004-2025; PVS 106 §30.1). The LAM and the target
LGU's ordinances are **not yet available** to the project, and when they
arrive they must not be committed to the repository (§7).

So PRIME cannot build the official FAAS or TD today. It **can** be made
**form-ready**, so that adopting the LAM later is mostly **configuration and
data entry, not a rewrite**. The approach has three layers:

1. **Capture the facts** that any FAAS or TD must carry. These come from the
   statute, which is available now, not from a form layout.
2. **Keep every form-specific detail as effective-dated configuration**:
   layouts, field labels, numbering formats, transaction codes, signatory
   chains and rounding. PRIME ships with clearly labelled **provisional**
   versions.
3. **Issue documents as frozen snapshots.** An issued FAAS or TD never
   changes when the live data or the form definition changes later.

When the LAM arrives, a new **form version** is added with its own
effective date. The provisional version is retired, and history keeps
showing the version that was in force when each document was issued.

### What can be changed later, and what it costs

| When the LAM or ordinances arrive and change… | Cost if this plan is followed |
|---|---|
| Layout, labels, field order, footers, legal notes on a form | **Configuration**: a new form version (template) |
| PIN, ARP/TD, FAAS or Notice numbering formats | **Configuration**: a new numbering scheme version |
| Transaction codes and their names | **Configuration**: the transaction-type catalogue |
| Signatory chain (who appraises, recommends, approves) | **Configuration**: the approval-chain definition |
| Rates, levels, SMV, caps, discounts, interest | **Data entry** through the existing rule tables (admin screens still needed) |
| Rounding rules | **Configuration**: one setting per stage |
| A field PRIME does not capture at all | **Small**: an extension attribute on the form version (§4.6), or a migration if it must be queried or reported on |
| A new computation method (e.g. marginal bracket levels, a building depreciation table) | **Code**, confined to the calculators, with unit tests |

The last two rows are the residual risk. This plan reduces them by
capturing everything the statute already requires (§3) and by keeping
calculators pure and isolated, which they already are.

---

## 2. What is known now and what waits for the LAM

Source: docs/analysis/current-real-property-regulatory-baseline.md.

| Can be built now (statute and current rules) | Waits for the LAM or ordinances |
|---|---|
| Property listed in the name of the owner, administrator, anyone with legal interest, estates and heirs, co-owners, the beneficial user of government property, or an **unknown owner** (LGC §§204–205) | FAAS and TD layouts and field lists (Q3) |
| Classification by **actual use** (§217) | PIN and ARP/TD number formats (Q4) |
| Uniform filing and identification system, and tax mapping (§§207, 472(b)(4)–(5)) — the system itself is chosen by the assessor | FAAS signatory chain by LGU type (Q5) |
| Notice of Assessment within 30 days: personal service, registered mail or through the punong barangay (§223). Appeal clocks (§§226, 229) | Transaction codes and ranks (Q9) |
| Transfer prerequisites: transfer-tax proof before cancelling the old TD (§135(b)); RPT clearance (§209(b)); notices (§§203, 208) | Rounding at each stage (Q8) |
| Levy annotation on the TD; new TD after an auction (LTOM §150) | Whether bracket levels apply to the whole value or marginally (Q6) |
| Machinery valued individually (§224/§225); building cost approach **less depreciation** (PVS 103) | Building depreciation schedules and extra items (LGU-specific, in the SMV) |
| New TDs issued at each general revision, with owners notified (S3 §IV) | Superseded-record block content; claimant and duplicate annotations (Q10, Q12–Q13) |
| Certified copies of assessment records (§472(b)(9)); semestral report of assessments, cancellations and modifications (§472(b)(10)) | eOR numbering format from BLGF (S7 §7.2) |
| An audit trail in every system (DOF Order 054.2024 §5.5) | Rates, levels, SMV, caps (LGU ordinances) |

---

## 3. Current PRIME against a form-ready system

| Area | PRIME today | Gap |
|---|---|---|
| PIN / TD number | Free text, max 50 characters, unique | No format rules, no generation, no link to the parcel/barangay code structure |
| FAAS | No entity. Its content is spread across `Land`/`Building`/`Machinery` → `Valuation` (breakdown JSON) → `Assessment` | No single appraisal record to render, no signatory steps, no FAAS number |
| TD | `TaxDeclaration` with a `PreviousTaxDeclarationId` chain | No "this declaration cancels…" snapshot, no annotations, no issued-document record, no signatories |
| Owner roles | `PropertyTaxpayer` holds owners and shares | No administrator, beneficial-user, claimant or **unknown owner** roles |
| Transactions | Not built (CLAUDE.md §34) | No transaction record to hang issuance, cancellation and annotation on |
| Notice of Assessment | Not built | No notice record, service mode, proof of service or appeal deadline |
| Documents | `Document` metadata plus storage (uploads only) | No templates, generation, numbering or issued snapshots |
| Signatories / branding | LGU name and office come from frontend env variables (tax map, statement of account) | No configurable signatories, positions or effective dates (CLAUDE.md §58) |
| Building valuation | SMV rate × area × completion | **No depreciation**, which the statute requires (baseline §6) |

---

## 4. Target design (layers 1–3)

### 4.1 Form registry — `FormDefinition` (effective-dated, versioned)

```text
FormDefinition     Code (FAAS_LAND | FAAS_BUILDING | FAAS_MACHINERY | TD |
                   NOTICE_OF_ASSESSMENT | TAX_BILL | STATEMENT_OF_ACCOUNT |
                   CERTIFIED_TRUE_COPY | …), Version, Title,
                   Authority (PRIME_PROVISIONAL | LAM | LGU_ORDINANCE | BLGF),
                   LegalBasis, SourceReference (e.g. "LAM Form __, p. __"),
                   EffectiveDate, EndDate, Status (Draft → Approved),
                   TemplateId, FieldMap, ExtensionSchema, NumberingSchemeId,
                   ApprovalChainId
```

- The same approval-time supersession as the billing rules: approving a
  version ends its predecessor.
- `PRIME_PROVISIONAL` versions carry a watermark:
  **"PROVISIONAL — NOT AN OFFICIAL FORM"**. They do **not** copy the
  2004/2006 manual layouts, which are superseded as a source.
- `FieldMap` binds each form field to a PRIME data path (e.g.
  `assessment.assessedValue`) or an extension attribute. The template never
  queries the database.

### 4.2 Templates

- A server-side HTML template, rendered to PDF with a sandboxed, logic-light
  engine. Templates are **stored in the database per deployment, not in
  git**, because LAM-derived layouts may be proprietary (§7).
- Branding (LGU name, office, logo, footer) comes from configuration
  (CLAUDE.md §58, §85).

### 4.3 Issued documents — `IssuedForm` (immutable)

```text
IssuedForm   FormDefinitionId + Version, DocumentNumber, SubjectType/Id
             (TD, Assessment, Bill, Notice…), DataSnapshot (JSON of every
             mapped value), RenderedFileId (Document, hash stored),
             Signatures [step, role, user, name, position, signedAt],
             IssuedAt/By, Status (Issued → Cancelled/Superseded),
             CancelledBy/Reason, SupersededById
```

- A reprint or certified true copy re-renders **the snapshot**, never the
  live data. This meets §472(b)(9) and CLAUDE.md §76–§77.
- Issuing is transactional and idempotent: one issued document per subject
  and version.

### 4.4 Numbering — `NumberingScheme` (effective-dated)

```text
NumberingScheme   Code, AppliesTo (PIN | TD | FAAS | NOTICE | BILL | OR…),
                  Pattern (tokens: {PROV} {MUN} {BRGY} {SECTION} {PARCEL}
                  {YEAR} {GR} {SEQ:n} {SUFFIX}), SequenceScope,
                  ValidationRegex, AllowManualEntry, EffectiveDate/EndDate
NumberSequence    Scheme + scope key → next value (row-locked, gap policy
                  configurable)
```

- Existing free-text numbers remain valid. A scheme validates or generates
  **new** numbers only from its effective date.
- Legacy and migrated numbers are kept as **aliases** and stay searchable
  (Phase 13).
- The same mechanism serves Phase 9's eOR numbering, which must follow the
  BLGF-assigned format when it is issued (S7 §7.2).

### 4.5 Approval chains — `ApprovalChain` (effective-dated, per LGU type)

```text
ApprovalChain   AppliesTo (FAAS | TD | NOTICE | …), LguType, Steps
                [Sequence, StepName (e.g. APPRAISED_BY / RECOMMENDED_BY /
                APPROVED_BY — labels configurable), RequiredPermission,
                SeparationFromPreviousSteps]
```

- This generalises the existing maker-checker (CLAUDE.md §46) from two steps
  to N configured steps. The provincial/municipal delegation (§472(b)(12))
  becomes data.
- Each completed step records the user **and** the name and position shown
  on the form at that time.

### 4.6 Extension attributes (the safety valve)

`FormDefinition.ExtensionSchema` declares extra fields a LAM form needs that
PRIME does not model. These are stored as validated JSON on the subject
record (`ExtensionAttributes`, with a GIN index).

Rule: a field that is **computed with, filtered on or reported on** gets
promoted to a real column in a later additive migration. Extension
attributes are only for fields that are displayed or recorded.

### 4.7 Transaction catalogue and `PropertyTransaction`

- A `TransactionType` reference table covering the statutory events from
  baseline Q9: new declaration or discovery, new or added improvement,
  removal or destruction, subdivision, consolidation, transfer, reassessment,
  correction, cancellation, reclassification and general revision.
- Each type has a **configurable code and rank** (the LAM supplies them
  later), its legal basis, its prerequisites checklist, and the forms it
  issues and cancels.
- `PropertyTransaction` (CLAUDE.md §34) becomes the single place where TDs
  are cancelled and issued. Every issued form points to its transaction.

---

## 5. Workstreams and sequence

### Stage A — now, without the LAM (form-ready foundation)

| # | Work | Depends on | Why now |
|---|---|---|---|
| A1 | **Numbering schemes and sequences** (§4.4). PIN, TD and FAAS schemes shipped **disabled**; PRIME-provisional bill numbering | — | Phase 9 needs it for receipts; bills already lack numbers |
| A2 | **Form registry, templates and issued-form snapshots** (§4.1–§4.3), plus the PDF rendering pipeline and configurable branding and signatories | A1 | Everything printable depends on it |
| A3 | **Approval chains** (§4.5), generalising the maker-checker already used by SMV, assessment levels, assessments and billing rules | — | The FAAS chain is data, not code |
| A4 | **Statutory data gaps**: owner roles from §§204–205 (administrator, beneficial user, claimant, unknown owner); a TD cancellation snapshot and "cancels / cancelled by"; TD annotations (levy, transfer, adverse claim, free text) | — | The statute requires these whatever the form looks like |
| A5 | **`PropertyTransaction` and the transaction catalogue** (§4.7), with prerequisite checklists (e.g. transfer-tax proof per §135(b)) and document attachments | A4 | TD issuance and cancellation must go through a transaction |
| A6 | **Notice of Assessment**: record, 30-day deadline, service mode (the three statutory modes), proof-of-service attachment, receipt date → appeal clock (§§223, 226) | A2, A5 | Statute; mode and proof records are needed regardless of layout |
| A7 | **Appraisal record (FAAS aggregate)**: one read model over property, RPU, land/building/machinery, valuation breakdown, assessment and approval steps, which the FAAS templates render | A2, A3 | Gives FAAS a stable data source whatever its eventual layout |
| A8 | **Provisional template set**: FAAS (Land, Building, Machinery), TD, Notice of Assessment, Tax Bill, Statement of Account, all watermarked PROVISIONAL | A2, A7 | Lets users test issuance end to end |
| A9 | **Building depreciation hook**: a configurable depreciation table by structural type and age, applied when configured; LGU values later | — | Statute requires depreciation "in all instances"; today it is absent |
| A10 | **LAM intake checklist** (§6) prepared as a working document | — | Makes the LAM workshop fast |

Suggested order: **A1 → A2 → A3** as a "Forms Foundation" step before
Phase 9, since Phase 9's eOR needs A1 and A2. Then A4–A5 with Phase 9, and
A6–A9 before Phase 11 (Reporting). A10 can happen any time.

### Stage B — when the LAM is acquired (mostly configuration)

1. **Intake workshop** with the assessor's office to complete the checklist
   (§6) against the LAM, recording page and form references.
2. **Configure** new form versions, numbering schemes, transaction codes,
   approval chains and rounding settings, each with its legal basis and
   effective date.
3. **Promote or extend**: add extension attributes for display-only fields,
   and additive migrations for fields that are computed or reported on.
4. **Calculator changes**, if the LAM prescribes methods: bracket
   application and building depreciation mechanics, with unit tests against
   **worked examples from the LAM**.
5. **Golden-sample tests**: render each form from fixture data and compare it
   with the LAM samples; a reviewer signs off each form.
6. **Parallel run** of the provisional and LAM versions on a copy of real
   data, then switch on the agreed effective date. Documents issued before
   the switch keep their provisional version.

### Stage C — when LGU ordinances are acquired (data entry)

- SMV (with its DOF certification record), assessment levels, tax rates,
  caps and phasing, discounts, interest and penalties go into the existing
  rule tables with their legal basis. Maker-checker applies.
- **Needed before this:** admin screens for the SMV, assessment-level and
  billing rules. Today they can only be set up through the API.

### Stage D — legacy records (Phase 13)

- Import existing TDs and FAAS data. Legacy numbers are kept as aliases;
  legacy forms are recorded as **issued under the legacy form version**, with
  the scanned image attached.
- Pre-PRIME tax history is also the missing baseline for the 6% cap
  (docs/BILLING.md §6.1).

---

## 6. LAM intake checklist (to complete when the LAM is available)

For each item, record the LAM reference, the value or rule, and the PRIME
setting it maps to.

1. **Form list**: every form prescribed for assessment operations, its code,
   title and version date.
2. **Per form**: every field, label, source of value, mandatory or optional,
   format, and who fills it in.
3. **PIN**: structure, segment lengths, source of each segment, and suffixes
   for buildings, machinery and condominium units.
4. **ARP/TD number**: structure, general revision indicator, sequence scope,
   what happens to the number on transfer, subdivision and cancellation.
5. **FAAS signatory chain** by LGU type, and delegation rules.
6. **Transaction codes and ranks**, and which forms each one issues or
   cancels.
7. **Rounding** of market value, assessed value and tax.
8. **Assessment-level bracket** application (whole value or marginal).
9. **Building**: construction types, depreciation method and tables, extra
   items.
10. **Machinery**: how §224 and §225 combine; condition categories.
11. **Superseded-record block** content; "cancels / cancelled by" wording.
12. **Annotations**: kinds, wording, who may annotate.
13. **Claimants, duplicates and untitled land**: declaration and
    cross-annotation rules.
14. **Notice of Assessment**: form, proof of service, any electronic
    service.
15. **Transfer prerequisites** (BIR CAR, transfer tax, clearances).
16. **Condominiums and linear infrastructure** treatment.
17. **Records retention** periods.
18. **Reports**: semestral report and assessment roll formats.

---

## 7. Handling proprietary material

The repository has a GitHub remote. Therefore:

- **Never commit** the LAM, LGU ordinances, or templates and form images
  derived from them. Keep them in the deployment's secure document storage
  (CLAUDE.md §59) or a git-ignored local folder (`docs/References/private/`,
  to be added to `.gitignore` in A2).
- **Configuration is data**, not source. Form versions, templates, numbering
  and codes live in the deployment database, entered through admin screens
  or a private seed file kept outside git.
- The repo may contain **citations** (e.g. "LAM Form 2, p. 14"), which is
  enough for traceability without reproducing content. Public issuances
  (RA 12001, the IRR, DOF orders, the superseded 2004 manual) may stay in
  `docs/References/`.
- Provisional templates in the repo contain **no LAM content**.

---

## 8. Risks

| Risk | Mitigation |
|---|---|
| The LAM needs data PRIME never captured | Statutory superset now (A4–A7); extension attributes; additive migrations |
| The LAM prescribes a different workflow | N-step approval chains (A3); transactions own issuance (A5) |
| Provisional forms get used as official documents | Watermark; `Authority = PRIME_PROVISIONAL` shown on the page; no LGU seal (CLAUDE.md §85) |
| Changing numbering mid-stream breaks references | Schemes are effective-dated; old numbers become aliases; nothing is renumbered |
| Proprietary content leaks through git | §7 rules; `.gitignore`; review before pushing |
| Heavy dependence on a template engine | Logic-light templates rendered from snapshots; engine swappable behind an interface |
| The LAM arrives late | Stage A stands on its own; provisional forms support training and testing |

---

## 9. Decisions needed from you

1. **Order**: insert the Forms Foundation (A1–A3) **before Phase 9**
   (recommended, since receipts need numbering and issued snapshots), or
   start Phase 9 and backfill?
2. **Provisional forms**: a neutral PRIME layout (recommended), or the
   2004/2006 manual layouts as a clearly labelled "legacy reference"?
3. **PDF rendering**: server-side HTML-to-PDF (recommended, one pipeline for
   all forms), or browser print only for now?
4. **Where private material lives** during development: a git-ignored
   folder, or only the deployment's document storage?

## 10. Done criteria for Stage A

- A form's layout, numbering, codes or signatory chain can be changed by
  adding a new approved version, **with no code change**. This is shown by a
  test that swaps a provisional version for a second fixture version.
- Every issued FAAS, TD and Notice is reproducible from its snapshot after
  the underlying data and the form version have changed.
- Provisional forms carry the watermark and never display an LGU seal.
- No proprietary content in git (`git grep` check against the private
  folder listing).

---

## 11. Implementation status — Forms Foundation (A1–A3), 2026-09-25

Decisions for §9 taken on the user's go-ahead ("build the forms foundation
first"): the Foundation comes before Phase 9; provisional forms use a
**neutral PRIME layout**; private material goes in the git-ignored
`docs/References/private/` folder; and printing uses the **browser, from a
server-rendered, frozen HTML snapshot**. A server-side PDF converter is
deferred, because it would add a headless-browser dependency to
deployments; it can be added behind `IFormRenderer` later.

**A1 — numbering** (`NumberingScheme`, `NumberSequence`, `NumberPattern`):
- Tokens `{YEAR} {PROV} {MUN} {BRGY} {SEQ}` / `{SEQ:n}`. Numbering restarts
  per scope, which is the pattern without its sequence.
- Allocation is a PostgreSQL upsert inside the caller's transaction, so no
  number is consumed on rollback.
- Wired into PIN (property registration), TD number (`{YEAR}` = the
  effectivity year) and bill number (assigned on posting; `{YEAR}` = the tax
  year). With no scheme in force, numbers are typed as before and bills get
  no number.
- A scheme can forbid or allow typed numbers, with an optional format
  regex.
- The PIN, ARP and FAAS formats themselves still await the LAM. New tokens
  (e.g. section and parcel) are a small code change in `NumberPattern`.

**A2 — forms** (`FormDefinition`, `IssuedForm`, `IFormDataProvider`,
`FluidFormRenderer`):
- Liquid templates stored in the database. Output is HTML-encoded, the page
  forbids scripts through its CSP and is shown in a sandboxed frame, and
  provisional versions carry a watermark the template cannot remove.
- Issuing freezes the data snapshot (jsonb) and the rendered HTML with its
  SHA-256 hash. It is idempotent per (form version, record); cancel and
  reissue to reflect changed data.
- `TAX_BILL` and `TAX_DECLARATION` provisional v1 are seeded at startup
  (`ProvisionalFormSeeder`) only when the code has no version at all.
- Preview renders without saving (e.g. a draft bill).
- UI: Print/Preview buttons on the Billing tab and the TD list; a document
  viewer at `/documents/:id` and `/documents/preview`; and administration
  at `/admin/forms` (create draft versions, approve).

**A3 — approval chains** (`ApprovalChain`, `ApprovalChainStep`,
`ApprovalRecord`):
- An N-step, configured signatory chain, with codes, printed labels and
  positions stored as data.
- A different person must sign each step, and the record's creator may not
  sign any of them.
- Signer names and positions are frozen per step and printed on the TD
  form.
- Wired into assessment approval. With no chain in force, the existing
  two-person maker-checker applies unchanged.

**Also fixed while verifying:** business dates used the server's **UTC**
date. At 6:30 AM in Manila that is still yesterday, so a scheme effective
"today" was not yet in force. There is now an `IClock` on the LGU's time
zone (`Lgu:TimeZone`, default `Asia/Manila` in appsettings.json), used
everywhere "today" was computed, including the older valuation, GIS layer
and general-revision code. Form dates are printed on the LGU calendar as
well.

**Deviations from §4:**
- **One chain per record type for the deployment**, not per LGU type: a
  deployment serves one LGU.
- **The approval chain is enforced for assessments only** so far. SMV,
  assessment-level and billing-rule approvals keep their two-person check.
- A rejected assessment cannot be resubmitted, which is unchanged. Adding
  resubmission later needs an approval-round number on `ApprovalRecord`
  (its unique index is per step).
- Issuing a newer form version does not cancel the record's earlier
  issued forms. Both remain valid, each under its own version.
- The rendered file is kept in the database, not in object storage (the
  storage module is not built yet).

**Verified:**
- 17 `NumberPattern` unit tests.
- 10 renderer and clock tests: encoding, CSP, watermark, filters,
  embedded templates, and the UTC/local date boundary.
- 8 integration flows: sequential TD numbers, the manual-entry refusal,
  the no-scheme fallback, bill numbers on posting, frozen and idempotent
  issue with cancel and reissue, a new form version replacing the
  provisional one with no code change while earlier issues are unchanged
  (the §10 done criterion), draft preview only, and a three-step chain
  with separation of duties and signatures printed on the TD.
- Live in the browser against the dev DB: create a scheme in the admin UI,
  self-approval refused, second-user approval, a generated TD number, post
  → bill number, Print → issued form, reprint returns the same document,
  draft bill → preview only, TD form with signatory.

**Dev-DB note:** integration tests share `prime_dev`. An approved
numbering scheme that forbids typed numbers makes the HTTP registration
test fail (correctly). The DEMO TD scheme used for verification was
end-dated afterwards.

---

## 12. Implementation status — A4 statutory data gaps, 2026-09-25

**Parties (LGC §§204–205).** `PropertyTaxpayer.Role` =
Owner | Administrator | LegalInterestHolder | BeneficialUser | Claimant |
UnknownOwner.
- The capacities are statutory, so they are a code enum; printed labels
  can later come from the LAM.
- Existing links became Owner.
- Only an unknown owner has no taxpayer, only owners carry an ownership type
  and count toward 100%, and an unknown owner holds no share. The database
  enforces these rules with check constraints, plus one current
  unknown-owner declaration per property.
- Declaring an unknown owner is refused while owners are current. Adding
  the first owner ends the declaration automatically ("Owner identified",
  ending the day before the owner's start).
- Any party can be ended with a date and a reason (`POST
  /api/property-owners/{id}/end`); history is kept.
- One projection (`PropertyParties`) now serves the profile, the ownership
  history and the forms; it replaces three copies.

**Tax Declaration lifecycle.**
- Draft → PendingReview → Approved (two-person check, or the configured
  approval chain, which now also covers `TaxDeclaration`) → Cancelled, plus
  Rejected.
- At most one Approved TD per RPU (`UX_TaxDeclarations_Rpu_Approved`). A
  new TD must name the current one as its previous TD, and approving it
  cancels that one in the same transaction ("Cancelled by TD No. …",
  `SupersededByTaxDeclarationId`).
- An approved TD can be cancelled outright with a reason (e.g. a
  duplicate).
- "The current TD" (`TaxDeclarationLookup`) now prefers the approved one
  and never returns a cancelled or rejected TD.
- Cancelled TDs stay printable (certified copies, LGC §472(b)(9)), with a
  CANCELLED banner.

**Annotations.**
- `TaxDeclarationAnnotation`: kind from the `AnnotationTypes` lookup
  (LGU-configurable, since the kinds come from the LAM), text, reference
  number and date, and effective date.
- Annotations are never deleted: lifting records who, when, why and a
  reference. Cancelled, rejected and voided TDs cannot be annotated.

**Forms.**
- Provisional `TAX_DECLARATION` v2 shows each party's capacity, the
  annotations (lifted ones struck through) and the cancellation banner.
- The seeder now installs newer provisional versions: effective today, and
  the predecessor ends yesterday. A deployment with any non-provisional
  version is never touched.

**UI.**
- Owners tab: a capacity column, "Add Party" with a capacity selector
  (unknown owner hides the taxpayer field), and End with a reason.
- TD list: "Replaces" column; Submit, Approve, Reject and Cancel TD with
  confirmation or a required reason; an annotations dialog (add and lift)
  with an active-count badge.
- New-TD form: a "Replaces TD" field that defaults to the current TD.

**Open (DOMAIN VERIFICATION REQUIRED):**
- How liability is shared among non-owner parties.
- The claimant and untitled-land rules.
- Whether annotations carry over to a superseding TD (they are not copied).
- A resubmission path for rejected TDs.

**Known gap:** there is no reference-data admin UI or API (CLAUDE.md §78),
so annotation types, like the other lookups, are inserted directly into
the database. The DEMO type `DEMO-LEVY` was added to the local dev DB.

**Verified:**
- 8 integration tests: the unknown-owner lifecycle, capacity validation,
  administrator and ending a party, replacement TD cancelling its
  predecessor with both forms marked, reject and outright cancel, a
  two-step TD approval chain, and annotation add, lift and print.
- Live in the browser against the dev DB: unknown owner declared; TD
  submitted, self-approval refused, approved by a second user; a
  replacement TD entered with "Replaces" defaulted, then approved, with
  the old one cancelled; annotation added (badge shows 1); both forms
  printed.

Migration `PartiesAndTdLifecycle`: additive. The only change to existing
columns is making `PropertyTaxpayers.TaxpayerId` and `OwnershipTypeId`
nullable. Applied to the local dev DB only.

---

## 13. Implementation status — A5 property transactions, 2026-09-25

**Catalogue.** `TransactionType` is versioned configuration: a new version
must be approved by a second user, and approving it ends the previous
version of the same code.
- Each type has an LGU/LAM `Code`, `Name` and `Rank` over a statutory
  `Kind`, which is the 12 events of CLAUDE.md §34 and is what PRIME acts
  on.
- Each type carries a prerequisite checklist (code, label, mandatory,
  legal basis).
- Administered in Forms & Numbering → Transaction types.

**Transactions.** `PropertyTransaction` records:
- the type code, name and kind, frozen from the type version it was opened
  under;
- an optional number from a `PropertyTransaction` numbering scheme;
- the property, effective date and description;
- a checklist copied from the type, where each item is marked satisfied
  with an evidence reference;
- its own TDs (`TaxDeclaration.PropertyTransactionId`);
- TDs it cancels outright;
- related source and result properties;
- for a transfer, the new parties.

Lifecycle: Draft → PendingReview (submit) → Approved (two-person check, or
an approval chain of subject `PropertyTransaction`) | Rejected | Cancelled
(withdrawn).

**Rules:**
- Submit requires every mandatory prerequisite to be satisfied, and the
  transaction must do something (a TD, a TD to cancel, or new parties).
- A transfer must name owners totalling exactly 100%, or a single
  unknown-owner declaration, and must take effect after the current
  parties started. This is checked at submit and again at approval.
- Only a transfer may change parties.
- A TD to cancel must be a current TD of the property or of a related
  property.
- TDs inside a transaction cannot be submitted, approved or rejected on
  their own; they move with the transaction.

**Approval** applies everything in one database transaction, in this
order:
1. The listed TDs are cancelled ("Cancelled by transaction …").
2. The transaction's own TDs are approved through the same helper as direct
   TD approval (`TaxDeclarationApproval`), which cancels the TDs they
   replace.
3. For a transfer, the current owners and any unknown-owner declaration are
   ended the day before the effective date, and the new parties start on
   it.

Both ends of the ownership change point to the transaction
(`PropertyTaxpayer.StartedByTransactionId` / `EndedByTransactionId`,
CLAUDE.md §35). Rejecting or withdrawing the transaction rejects its TDs,
which never took effect.

**Deviations and open items:**
- **Subdivision and consolidation** record source and result properties
  only through the API (no UI yet); child properties are registered
  separately rather than created by the transaction.
- **Attachments:** evidence is a reference number until document storage
  exists.
- **Partial transfers** of co-owned property are not supported (the whole
  ownership passes). DOMAIN VERIFICATION REQUIRED.
- Direct TD approval (outside a transaction) remains possible. Making
  transactions mandatory for every TD change is a policy switch still to
  add.

**Also fixed while verifying:** a confirmation dialog stayed open over the
error when its action failed (transactions, TD list, bill posting). It now
closes and the error shows in the page.

**Verified:**
- 7 integration tests: a full transfer (prerequisite gate, TD in the
  transaction, owner handover with links, replaced TD cancelled), the
  100% share rule, the effective-date rule at submit, outright
  cancellation, rejection rejecting its TDs, refusal cases, and numbering.
- Live in the browser against the dev DB: transfer type created in the
  admin UI and approved by a second user; transfer opened with a buyer;
  TD added inside it with "Replaces" defaulted; submit blocked until
  evidence was recorded; self-approval refused; checker approval replaced
  the owner and the TD.

Migration `PropertyTransactions`: seven new tables plus three nullable
link columns. Applied to the local dev DB only.

---

## 14. Implementation status — A6 Notice of Assessment, 2026-09-25

**Record.** `NoticeOfAssessment` (LGC §223) is generated from a **posted**
assessment, only when a notice is required:
- a first assessment; or
- an increase or decrease against the previous assessment (the one it
  names, else the latest earlier posted assessment of the RPU).

An unchanged value is refused (`NOTICE_NOT_REQUIRED`). There is at most
one live notice per assessment.

Frozen on the notice:
- the addressees (the current declared parties; legal-interest holders
  are excluded);
- the values and the reason;
- the periods applied, which come from configuration with their citations
  (`Notices:IssuePeriodDays` = 30 for §223, `Notices:AppealPeriodDays` =
  60 for §226) and are required at startup.

**Lifecycle.** Draft → Issued (numbered by a `NoticeOfAssessment` scheme
if one is in force) → Served, or Cancelled before service.
- Recording service needs one of the **three §223 modes** (personal,
  registered mail, through the punong barangay), the receipt date
  (between the issue date and today), the recipient and a proof reference.
- The **appeal deadline** = receipt date + the frozen appeal period.
  Electronic service is not offered (no authoritative source, baseline
  Q11).
- A notice not issued by its due date (the approval date + the issue
  period) is flagged Overdue.

**Form.** Provisional `NOTICE_OF_ASSESSMENT` v1. It uses only PRIME data
and the statute's own words about the appeal right. A draft previews but
cannot be issued.

**UI.** A Notices tab on the Property Profile: generate (choose RPU and
posted assessment), issue, record service, print, cancel.

**Also fixed while verifying:** the issue due date counted from the
approval's UTC date, which is one day early for approvals before 8 AM in
Manila. `IClock.LocalDate(instant)` now converts timestamps to the LGU
calendar.

**Open (DOMAIN VERIFICATION REQUIRED):**
- Periods are counted in calendar days; whether a deadline falling on a
  non-working day moves is not applied.
- The LAM's notice form and its proof-of-service requirements.
- Back-tax interest from receipt of notice (§222) belongs to billing and
  is not yet applied.

**Verified:**
- 4 integration tests: the full generate → issue → serve cycle with the
  appeal deadline; increase detection and the unchanged-value refusal;
  refusals (no addressee, unposted assessment); draft preview versus
  issue, and cancel then regenerate.
- A time-zone unit test and the template parse test.
- Live in the browser: a first-assessment notice generated, issued,
  served by registered mail (appeal deadline 2026-11-24), and printed.

Migration `NoticesOfAssessment`: one new table. Applied to the local dev
DB only.

---

## 15. Implementation status — A7 appraisal record (FAAS aggregate), 2026-09-25

**Read model.** `AppraisalRecordService` assembles one record per
**assessment**. PRIME values each land, building or machinery item
separately, so an assessment covers exactly one appraised item. The record
holds:
- the property's location and survey/title references;
- the parties declared **on the assessment's effective date** (LGC
  §§204–205). Parties recorded later are left out, so a reprint of an old
  FAAS never shows today's owner;
- the RPU and the Tax Declaration **in force on that date**: approved,
  effective by then, and not cancelled before it;
- the appraised item's registered details (land, building with its
  components, or machinery);
- the valuation: method, SMV ordinance, schedule rate and the full
  calculation breakdown;
- the assessment: the classification, actual use and property type of the
  assessment-level row it used, the frozen percentage, the bracket, and the
  level's ordinance;
- the previous assessment and the change in assessed value (the same rule
  as the Notice of Assessment, now shared in `AssessmentHistory`);
- who recorded it, the frozen approval signatures, and its notices.

It computes nothing (CLAUDE.md Rule 9). Every value is one PRIME already
recorded.

**Breakdown order.** The breakdown is stored as `jsonb`, which does not
keep key order. `ValuationCalculator.BreakdownOrder` fixes the reading
order (inputs, intermediate values, market value). A unit test keeps it in
step with the keys the calculator writes.

**FAAS number.** `Assessment.FaasNumber` is assigned when the approval
completes, only if a `Faas` numbering scheme is in force (none is shipped).
It is never typed by hand and is unique.

**Serving it.**
- `GET /api/assessments/{id}/appraisal-record`.
- Form subject `Assessment`: its data provider wraps the same record under
  `appraisal`. It can be issued once the assessment is Approved or Posted;
  a Draft or pending one can only be previewed. There is no FAAS template
  yet; that is A8.
- UI: an **Assessments** table under each RPU on the Property Profile, with
  an **Appraisal record** drawer.

**Deviation from §4.1:** there is one form subject for all three FAAS
kinds. A8 decides whether to use one FAAS form that branches on
`appraisal.kind`, or three codes (FAAS_LAND/BUILDING/MACHINERY) with a
kind check.

**Open (DOMAIN VERIFICATION REQUIRED):**
- The LAM's FAAS field list (checklist §6 item 2). Missing fields are added
  to the read model, not to templates.
- The asset's descriptive fields are its current registered values. They
  cannot be edited today (no update endpoints), and an issued FAAS freezes
  them. If editing is added later, those fields need versioning.

**Verified:**
- 4 integration tests: a land record with a party effective later
  excluded; a reassessment showing the previous value and the change; a
  FAAS number assigned on approval, with the signer shown and the form
  provider serving the same data; a draft that previews but cannot be
  issued, and an unknown id.
- A unit test for the breakdown order.
- Full suite passes (90 domain, 37 application, 105 integration tests).
- Production frontend build and lint pass.
- Live in the browser on the DEMO property: the drawer shows the TD in
  force, the land, the SMV, the ordered breakdown, the level bracket, the
  approver and the served notice, with no console errors.

Migration `AssessmentFaasNumber`: one nullable column and a unique index.
Applied to the local dev DB only.

---

## 16. Implementation status — A8 provisional template set, 2026-09-25

Every A8 form now has a provisional version. All are installed at startup
by `ProvisionalFormSeeder` and always render with the PROVISIONAL
watermark:

| Code | Version | Subject | Issuable when |
|---|---|---|---|
| `TAX_BILL` | 1 | tax bill | posted (Phase 8) |
| `TAX_DECLARATION` | 2 | Tax Declaration | not rejected or voided (A4) |
| `NOTICE_OF_ASSESSMENT` | 1 | notice | issued or served (A6) |
| `FAAS` | 1 | assessment | approved or posted — **new** |
| `STATEMENT_OF_ACCOUNT` | 1 | property | never — preview only — **new** |

**FAAS: one form, not three.** This settles the §15 deviation: a single
`FAAS` code whose sections follow `appraisal.kind` (Land, Building,
Machinery). The appraisal record already carries the kind, so three codes
would only duplicate the header, valuation and assessment sections. If the
LAM prescribes three separate forms, it arrives as three form codes on the
same `Assessment` subject, with no code change. The template renders only
what the appraisal record holds. Breakdown keys get readable labels in the
template, and money keys are formatted as money, so a later version can
reword them without code changes. The **Print FAAS** button sits next to
**Appraisal record** in the Assessments table. It issues an approved or
posted assessment's FAAS and previews any other.

**Statement of Account: preview only (deviation).** Issuing is once per
form and subject (`UX_IssuedForms_Definition_Subject_Valid`), and reprints
return the frozen copy. That fits a bill or a TD, but a statement is a
point-in-time view that changes with every posted bill and, from Phase 9,
every payment. Freezing the first one issued would serve stale balances
forever. The form data provider therefore always returns an issue blocker.
Issuing statements needs its own statement record (a number, an as-of
moment, the lines), which fits Phase 9/10. New subject type:
`FormSubjectType.StatementOfAccount` (the subject id is the property id;
stored as a string, so no migration). The data is the same
`StatementOfAccountDto` that `GET /api/properties/{id}/statement-of-account`
serves, plus the property and its current declared parties. A **Preview**
button on the Statement of Account page opens it.

**Open (DOMAIN VERIFICATION REQUIRED):** the LAM's FAAS and statement
layouts and field lists (checklist §6). Missing fields go into the read
models, not the templates.

**Verified:**
- Integration tests: FAAS issued for an approved assessment (number, Land
  section only, SMV label, money-formatted breakdown, signer, no draft
  banner); a draft previews with the NOT APPROVED banner but cannot be
  issued; the statement preview shows the billed total and cannot be
  issued. Both new templates parse (renderer theory).
- Full suite passes (90 domain, 37 application, 107 integration tests).
- Production frontend build and lint pass.
- Live in the browser on `DEMO-BILL-AE94B8`: Print FAAS issued the FAAS
  (Land, SMV rate, breakdown, 20% DEMO level, 100,000.00 assessed value,
  recorder and approver signatures). The Statement of Account preview
  showed the 2026 bill (2,070.00) with the preview-only notice. No console
  errors.

No migration. In the local dev DB only, the FAAS v1 template row was
refreshed in place after the money-format fix. That version was never
installed anywhere else, and the one FAAS issued during testing keeps its
frozen HTML.
