# LGU Content Pack — Design (Step L0-2)

| | |
|---|---|
| Date | 2026-09-28 |
| Status | **Approved 2026-09-28: all recommendations Q1–Q9 accepted.** C1–C5 done (§8); L0-2 complete. SMV, unit-value and level files added in L1-3, adjustment factors in L1-4 (docs/analysis/valuation-foundation.md §9) |
| Rules | CLAUDE.md §7 (configurability), §60 (import workflow), §81 (seed data), §104 and §118 (no LAM or ordinance content in git) |
| Commit status | This document contains no LAM content and may be committed |

## 1. Purpose

PRIME needs a lot of content that must not live in the repository:

- LAM form layouts and code lists;
- the province's geography and index numbers;
- the certified SMV;
- assessment-level ordinances;
- transaction types with their requirement checklists;
- numbering schemes;
- signatories.

Today this content can only be keyed in one record at a time through the API,
or, for lookups and geography, not at all. The **content pack** is a folder of
files that PRIME validates, previews and imports with full provenance. It is
kept in the gitignored `lgu-content/` folder or uploaded by an administrator.

## 2. What exists today

| Content | How it enters PRIME today | Gap |
|---|---|---|
| Form definitions | Built-in MRPAAO/provisional versions seeded at startup (`ProvisionalFormSeeder`, embedded templates); `POST /api/forms/definitions` + approve | One at a time; the template is pasted into a request |
| Numbering schemes, approval chains | `POST` + approve (`/api/numbering-schemes`, `/api/approval-chains`) | One at a time |
| SMV, schedules, adjustment factors, assessment levels | `POST` + approve (`/api/smv`, `/api/adjustment-factors`, `/api/assessment-levels`) | One rate per request; a real SMV has hundreds |
| Transaction types + requirements | Admin tab + API, versioned | One at a time |
| Provinces, municipalities, barangays | **No create path.** Only index numbers can be set (`PUT …/index-number`); DEMO rows were inserted directly | Real geography (PSGC) cannot be loaded |
| Lookups (classification, actual use, sub-class, structural type/part/material, title, annotation, improvement kind …) | **No create path** (`/api/reference/*` is GET only) | The LAM code lists cannot be entered |
| GIS reference layers | GeoJSON import with dry run (`ReferenceLayerService.ImportAsync`) | Works; not part of a pack |

**Conclusion:** PRIME has no bulk path, no preview across content kinds, no
record of which source a configuration row came from, and no create path at
all for lookups and geography.

## 3. Proposal

### 3.1 Pack layout

```text
lgu-content/                         (gitignored)
└── zamboanga-sibugay/               one pack per LGU/deployment
    ├── manifest.json                pack id, version, description, sources, files in load order
    ├── geography/
    │   ├── provinces.csv            psgc_code, name, index_number
    │   ├── municipalities.csv       psgc_code, province_psgc, name, is_city, index_number
    │   └── barangays.csv            psgc_code, municipality_psgc, name, index_number
    ├── lookups/
    │   └── <lookup>.csv             code, name, description, sort_order, is_active, source
    ├── catalogues/
    │   ├── transaction-types.json   code, name, kind, rank?, requirements[], source
    │   └── numbering-schemes.json
    ├── forms/
    │   ├── forms.json               code, version, title, subject, authority, source, effective_date
    │   └── <CODE>.v<N>.liquid
    ├── valuation/                   (step L1-3; factors L1-4; building tables L1-5; rates and indices L1-6)
    │   ├── smv.json                 kind "smv": basis, ordinance or certification, stages, coverage (PSGC)
    │   ├── smv-schedules.csv        kind "smv-schedules": smv, classification, sub-classification?, actual-use?,
    │   │                            property-type, zone?, barangay?, improvement-kind?, unit, market-value, …
    │   ├── adjustment-factors.json  kind "adjustment-factors" (L1-4): smv, code, ruleKind, percent?, rows[]
    │   ├── building-costs.json      kind "building-costs" (L1-5): smv, structuralType, buildingType?, classification?, costPerSquareMetre
    │   ├── extra-item-costs.json    kind "extra-item-costs" (L1-5): smv, componentType, unit, unitCost
    │   ├── depreciation-rates.json  kind "depreciation-rates" (L1-5): smv, structuralType, reading, minimumRemainingPercent, rows[]
    │   ├── exchange-rates.csv       kind "exchange-rates" (L1-6): currency, rate-date, pesos-per-unit, source, remarks
    │   ├── price-indices.csv        kind "price-indices" (L1-6): series, year, value, source, remarks
    │   ├── exemption-types.json     kind "exemption-types" (L3-1a): code, name, description, appliesTo (Land/Building/Machinery/OtherImprovement), requiresProof, assessedValueCeiling, legalBasis, effectiveDate
    │   ├── general-revision-checklist.json  kind "general-revision-checklist" (L6-6c): code, sequence, title, description, gate (optional: Compiled, Valued, Approved, Posted, TaxDeclarationsApproved, NoticesServed, RollWaitElapsed, AssessmentRollRun, OwnershipRecordsRun, CompletionReportIssued), legalBasis, effectiveDate
    │   ├── assessment-level-ceilings.json  kind "assessment-level-ceilings" (L3-2): code, propertyType, classification?, actualUse?, lowerValue, upperValue?, maximumPercentage, description, legalBasis, effectiveDate
    │   └── assessment-levels.csv    kind "assessment-levels"
    ├── reports/                     (Phase 11 R4c)
    │   └── report-row-maps.json     kind "report-row-maps": code ("QRRPA"), name, definition {restrictions[] (code, label,
    │                                annotationTypes[]), rows[] (section Taxable/Exempt/Restricted/IdleLand, code, label,
    │                                restriction?, classifications?, actualUses?, exemptionTypes?, splitsBuildings?, others?)},
    │                                legalBasis, effectiveDate
    └── gis/
        └── <layer>.geojson
```

Every row or item carries a **`source`**: a citation such as
"LAM Book I Annex I-D", "Prov. Ord. No. …" or "PSA PSGC 2025-Q2". The citation
is stored on the imported record.

### 3.2 Workflow (CLAUDE.md §60)

```text
READ (server folder or uploaded zip)
 → VALIDATE   schema, types, references between files, natural-key duplicates,
              Liquid templates parse, codes exist (e.g. a transaction kind)
 → PREVIEW    per file: new / changed / unchanged / would supersede / missing
              from the pack (reported only); errors block the import
 → CONFIRM    by a user holding content.import
 → IMPORT     one database transaction per file group; a background job for
              large files (barangays, schedules, GIS) (§73)
 → AUDIT      a ContentImport record plus the usual audit log rows
```

### 3.3 Import semantics by kind

| Kind | Natural key | On import | Approval |
|---|---|---|---|
| Geography | PSGC code | Insert new; update the name; set index numbers through the existing rules (locked once used) | Applied on confirm; audited |
| Lookups | `Code` per lookup | Insert new; update name, description, order and active flag; the code never changes | Applied on confirm; audited |
| Transaction types, numbering schemes, approval chains, forms | Transaction type code; numbering document kind; approval subject; form code | A **new Draft version** when the content differs from both the version in force and any pending draft; the existing version stays in force until the new one is approved | **Maker-checker:** a second user approves (§46) |
| SMV, schedules, factors, levels | SMV revision + row key | New SMV revision with its rows, as drafts | Maker-checker, as today |
| GIS layers | Layer + feature key | Through the existing `ImportAsync` (dry run → import) | Applied on confirm |

- **Nothing is ever deleted.** Rows missing from a pack are reported in the
  preview. Retiring them is a separate, explicit action.
- **Idempotent.** Re-importing the same pack changes nothing: natural keys and
  file hashes are compared first.
- **Forms** imported with authority `Lam` stop the MRPAAO seeder for that code:
  the seeder already leaves a code alone once a non-built-in version exists.

### 3.4 Provenance

A new `ContentImport` record holds:

- the pack id and version;
- the manifest SHA-256 and each file's SHA-256;
- the mode (dry run or import);
- who confirmed it, and when;
- counts per file and outcome;
- the validation report (JSON).

Each record an import creates or changes gets a `ContentImportItem`: the
entity and its id, the natural key, created or changed, the field changes,
the citation, and the file and line. (Changed in C2 from the first idea of
adding `SourceReference`/`ContentImportId` columns to every imported table.
That would have altered 22 tables, including the frozen treasury lookups
that share the lookup base class, and kept only the latest import. The item
table leaves existing tables untouched and keeps every import's history.)

### 3.5 Entry points

1. **Server folder:** `GET /api/content-packs` lists the packs;
   `POST /api/content-packs/{pack}/preview` is the dry run (C1);
   `POST /api/content-packs/{pack}/import` and `GET /api/content-imports`
   follow in C2. The root is the setting `ContentPacks:RootPath` (environment
   variable `ContentPacks__RootPath`), never a client-supplied path.
2. **Zip upload** from the admin page, for deployments where operators have no
   server access. Size limit, allow-listed file types, no path traversal,
   stored as a private Document (§59).

An admin page, **Content Packs**, handles upload, preview (a diff per file),
confirm, and the import history with its reports.

### 3.6 What goes in the repository

- The loader, validators and tests.
- A **DEMO pack** at `samples/content-demo/`, with every name and code
  prefixed `DEMO`. It exercises every file kind and is used by the
  integration tests.
- No LAM, ordinance or real LGU content (§118).

### 3.7 Province-wide deployment (step LP)

Manifest items may later carry an `office` field: per-office approval chains,
signatories, branding and delegations. The loader accepts the field but
rejects it with a clear message until LP adds offices.

## 4. Security

- New permission `content.import`, granted to SYSTEM_ADMIN only by default.
  Approval of imported legal configuration still needs a second user holding
  the matching approve permission.
- Files are parsed as data. Liquid templates render through the existing
  Fluid renderer (`FluidFormRenderer`), which configures no file provider, so
  `include`/`render` tags cannot read files. A C3 test confirms that an
  imported template cannot reach the file system.
- No personal data is expected in packs. Taxpayer and property records come
  through Phase 13 (import/migration), not content packs.

## 5. Delivery steps

| Step | Scope | Verification |
|---|---|---|
| C1 | Manifest and file schemas; validator; **dry-run preview** for geography and lookups (adds their create/update path in the Application layer) | Unit tests per validator; integration test on the DEMO pack |
| C2 | Import for geography and lookups; `ContentImport` provenance (migration, local only); idempotency | Re-import is a no-op; audit rows written |
| C3 | Versioned configuration: transaction types, numbering schemes, approval chains, forms, all as PendingReview versions | Maker-checker: the importer cannot approve their own |
| C4 | Admin **Content Packs** page: upload, preview, confirm, history | Browser check; `tsc -b` |
| C5 | GIS layers through the existing import; valuation files wait for L1-3 (the SMV model changes there) | Integration test |

## 6. Out of scope

- Taxpayer, property and assessment records (Phase 13).
- Exporting a pack from a database. Possible later, for moving configuration
  between environments.
- Any automatic load at startup.

## 7. Review questions

| # | Question | Recommendation |
|---|---|---|
| Q1 | File formats | JSON manifest and catalogues, CSV for tables, `.liquid` for templates, GeoJSON for maps |
| Q2 | Entry points | Both: the server folder first (C1–C3), zip upload with the admin page (C4) |
| Q3 | Approval | Geography and lookups apply on confirm (audited). Legal and effective-dated configuration enters as PendingReview and needs a second user's approval |
| Q4 | Rows missing from a pack | Report only; never delete or deactivate automatically |
| Q5 | Changing an existing lookup | Name, description, order and active flag may change (audited); the code is immutable |
| Q6 | DEMO pack in the repository | Yes, at `samples/content-demo/`, all DEMO-prefixed |
| Q7 | Load at startup? | No. Packs are applied per environment by an administrator (dev, then Supabase on request, §105) |
| Q8 | Source of Zamboanga Sibugay geography | The PSA's PSGC publication for the names and codes; index numbers from the Provincial Assessor's Office |
| Q9 | Office-scoped content | Accepted in the format now; imported only after LP |

## 8. Implementation log

### C1 — validation and dry-run preview (2026-09-28)

**Built**
- `ContentPackCsv`: an RFC 4180 reader (quotes, embedded line breaks, BOM,
  CRLF). Rows with the wrong field count are errors, never padded.
- `ContentPackService`:
  - manifest checks: schema version 1, pack = folder name, version required,
    known kinds and lookups, safe relative paths, one file per kind or lookup,
    a source citation per file or row;
  - geography preview: PSGC code (a warning if not 9–10 digits), parent in
    the pack or PRIME, no moves between parents, index-number formats and
    uniqueness in the final state, numbers locked by permanent PINs, retired
    barangays keep their number;
  - lookup preview for 17 lookups; structural materials also need their
    part;
  - new / changed / unchanged counts, missing rows (reported only), and a
    warning if the LAND, BUILDING or MACHINERY property type would be
    undefined. Later kinds (forms, transaction types, SMV …) are hashed and
    flagged `NOT_YET_SUPPORTED`.
- `FileSystemContentPackSource` (Infrastructure):
  - the pack name is checked against a pattern;
  - every path is resolved and must stay inside the root and the pack;
  - symlinked packs and files that point outside are refused;
  - a file-size limit (`ContentPacks:MaxFileBytes`, default 50 MB).
- API: `GET /api/content-packs`, `POST /api/content-packs/{pack}/preview`.
  Role gating comes in Phase 12, as for the other configuration endpoints.
- DEMO pack: `samples/content-demo/` (6 files, all DEMO).

**Rules decided in C1**
- A blank optional cell keeps the current value. A pack never clears a
  value.
- Barangays added by a pack get no city district; districts stay on the
  admin screen.

**Verified**
- 19 unit tests (CSV reader, path rules).
- 3 integration tests: the DEMO pack previews clean and writes nothing; a
  broken pack reports every class of error; existing records show changes
  and missing rows, and index numbers locked by permanent PINs are refused.
- Full suite: 431 tests pass.
- Live API against `samples/`: list, preview (`canImport: true`) and a
  traversal attempt refused.

**Found**
- `StructuralMaterial.Code` is unique across **all** parts. The LAM repeats
  material codes per part (e.g. the same concrete code under foundation,
  columns and beams). To be settled in L5: either scope the uniqueness to the
  part (a relaxing migration), or give each part's materials distinct codes
  in the pack.

### C2 — import and provenance (2026-09-28)

**Built**
- `POST /api/content-packs/{pack}/import` with `{ "fingerprint" }`.
  - The import re-runs the preview and applies exactly its plan, so it can
    never disagree with what the user saw.
  - The fingerprint (SHA-256 of the manifest and every file) must still
    match; if a file changed since the preview → 409 `CONTENT_PACK_CONFLICT`.
  - A pack with errors → `CONTENT_PACK_INVALID`; no signed-in user →
    `IMPORTER_UNKNOWN`.
- One transaction; the audit log records the reason
  "Content pack {pack} {version}".
  - Changing index numbers are cleared first, then set, so numbers can move
    between records without tripping the unique indexes.
  - Structural parts are applied before materials.
- **Idempotent:** a pack that matches PRIME returns `applied: false` and
  records nothing.
- `ContentImports` (pack, version, manifest SHA, fingerprint, importer,
  counts, per-file hashes and counts, warnings) and `ContentImportItems`
  (one per record, see §3.4). Migration `ContentImports`: two new tables,
  nothing else changed; **local database only** (§105).
- History: `GET /api/content-imports?pack=`, `GET /api/content-imports/{id}`,
  `GET /api/content-imports/{id}/items` (paged).

**Verified**
- 3 new integration tests:
  - the DEMO pack imports 13 records with sources, lines, entity ids and
    audit reason, and a second import changes nothing;
  - a stale fingerprint, an invalid pack and an unknown user are refused,
    and nothing is written;
  - existing records update, two barangays swap index numbers, and blank
    cells keep their values.
- Full suite: 434 tests pass.
- Live API: preview → stale import 409 → import (recorded under the dev
  user) → re-import "already matches" → history and items. One DEMO zone,
  `DEMO-LIVE-C2`, remains in the dev database from this check.

### C3 — versioned configuration (2026-09-28)

**Built**
- Four JSON catalogue kinds: `transaction-types`, `numbering-schemes`,
  `approval-chains`, `forms`. A form item names its `.liquid` template inside
  the pack; the template's hash joins the fingerprint, so an edited template
  also forces a new preview.
  - Items use camelCase fields; an unknown field is an error, which catches
    typos.
  - Each item needs a source citation (its own or the file's) and an
    `effectiveDate` written yyyy-MM-dd.
- `ContentPackVersionedContent`:
  - validates each item with the **same validator as its admin screen**;
  - refuses treasury kinds (numbering for bills, receipts, payment
    transactions and remittances; forms for bills, statements and payments;
    CLAUDE.md §0), the built-in form authorities (PrimeProvisional, MRPAAO),
    an `office` on approval chains (until LP), and effective dates that could
    never be approved (not after the latest approved version);
  - compares each item with the version in force and any pending draft:
    identical content is unchanged, otherwise the preview lists a new
    version with its field differences (templates as short hashes).
- Import creates each new version as **Draft** through the owning service
  (`CreateTypeAsync`, numbering `CreateAsync`, chains `CreateAsync`,
  `CreateDefinitionAsync`), inside the import's transaction and under the
  importer's name. The existing approval rules then apply unchanged: the
  importer cannot approve their own version, and a second user must. If a
  service refuses an item, the whole import rolls back.
- DEMO pack: one DEMO item per catalogue, effective 2099-01-01, and a DEMO
  template.

**Deviation from the design text:** §3.3 said "PendingReview". Versioned
configuration in PRIME has no PendingReview state (`ConfigurationApproval`:
Draft → Approved by a second user), so imports create Draft versions. The
safeguard is the same.

**Verified**
- 3 new integration tests:
  - the DEMO catalogues become 4 Drafts owned by the importer; the importer's
    approval is refused and a second user's accepted; the next import is a
    no-op;
  - treasury kinds, built-in authorities, offices, bad values, bad dates,
    unknown fields, template path/missing/broken, and an unapprovable date
    are all refused;
  - an edited template after the preview → 409 `CONTENT_PACK_CONFLICT`.
- Full suite: 437 tests pass.
- Live API preview of the DEMO pack: 10 files, 0 errors, 0 warnings.

### C4 — upload and admin page (2026-09-28)

**Built**
- `POST /api/content-packs/upload` (multipart `file`, request limit 110 MB).
  The zip is buffered with a hard size cap, then:
  - `manifest.json` must sit at the root or inside a single top-level folder;
  - the manifest's `pack` must be a valid pack name;
  - every entry path is checked like a manifest path (no `..`, no absolute
    or backslash paths);
  - limits apply: at most 5,000 files, and per-file and total unpacked sizes
    counted from the bytes actually written, which stops zip bombs;
  - it unpacks into a staging folder, which is removed if anything fails.
  - It **replaces** the pack of the same name. The previous copy moves to
    `.previous/{pack}-{UTC time}` and is never deleted. Folders starting with
    `.` are not listed as packs.
- **Content Packs** admin page (`/admin/content-packs`, in the menu):
  - pack list, zip upload (which previews automatically) and a Preview
    button;
  - the preview card: version, status, "would apply", warnings and a
    fingerprint. A table per file shows rows / new / changed / unchanged /
    not in pack / issues. Expanding a row shows its issues (severity, line,
    field, code, message), records to add or change with field-by-field
    changes, and records not in the pack;
  - **Import** through a confirmation, enabled only when the preview is
    clean and would change something; "Import refused" shows the API's
    message (e.g. files changed since the preview);
  - **Import history**: paged imports (when, pack and version, by, counts,
    warnings, fingerprint), each expandable to its records with changes,
    source and file:line.

**Fixed along the way:** a manifest or JSON catalogue saved with a UTF-8
byte-order mark, as Windows editors often do, was refused as invalid JSON.
Both are now read without the mark. The upload tests write files with a mark,
so they guard against this.

**Deviation from §3.5:** an uploaded zip is unpacked into the content root
but not also stored as a private Document. PRIME's document storage (§59) is
not built yet, and the import record already keeps every file's hash. To
revisit when document storage exists.

**Verified**
- 2 new integration tests: a zipped folder unpacks and replaces the pack,
  keeping the old copy; not-a-zip, no manifest, path traversal, a bad pack
  name and an oversized unpack are refused and leave nothing behind.
- Full suite: 439 tests pass. `npm run build` (tsc -b + vite) and
  `npm run lint` are clean.
- Browser (Playwright, dev server + API):
  - a broken pack shows "1 error: cannot import", its REQUIRED issue, and a
    disabled Import button;
  - uploading a zip lists the pack and previews it as "Ready to import"
    (2 new);
  - importing reports "Imported ui-check UI-1: 2 created, 0 changed";
  - history lists the records with their changes, source and file:line;
  - previewing again says "already matches";
  - at 390 px there is no horizontal page overflow; the console is clean at
    both widths.
- The browser check left two DEMO rows in the dev database: zone
  `DEMO-UI-C4` and a Draft transaction type `DEMO-UI-TR` (effective
  2099-01-01).

### C5 — map layers (2026-09-28)

**Built**
- Manifest kind `gis-layer`, with `layer` (barangays, zones, roads or
  sections), `effectiveDate` (yyyy-MM-dd) and a **required** file-level
  `source`, at most 300 characters. Features carry no source of their own.
  A pack carries one file per layer.
- The pack reuses the reference-layer import (`ReferenceLayerService`, docs/GIS.md §3)
  with new `ReferenceLayerImportOptions`:
  - **SkipUnchanged:** a feature whose version in force has the same shape
    (for roads also the same name and road type), and started on or before
    the file's date, is unchanged and gets no new version. Re-importing a
    pack is therefore a no-op. A different shape on a date that is not later
    than the latest version is still refused (`VERSION_NOT_AFTER_EXISTING`):
    history stays append-only.
  - **Pending keys:** barangays, zones and road types the same pack adds
    count as existing in the preview. On import, the layers are applied last,
    after geography, lookups and configuration drafts, inside the pack's
    transaction.
  - The GIS import result now also lists unchanged counts and the features
    it versions. The GIS API output gains these two fields; its behaviour is
    unchanged.
- Preview: rows = features, new / changed (supersedes the version in force) /
  unchanged. Issue lines are **feature numbers** (1-based), not text lines.
- Import: one `ContentImportItem` per new version (BarangayBoundary,
  ZoneBoundary, RoadSegment or SectionBoundary; line = feature number). The
  audit reason reads "Content pack {pack} {version}: GIS import …". The layer
  import used to clear the caller's reason; it now restores it.
- Not reported for layers: records "missing from the pack". A layer file is
  a set of versions for the keys it lists, not a complete list.
- DEMO pack: `gis/barangays.geojson` holds two squares in the open
  Philippine Sea for the pack's two Town A barangays (11 files).
- Admin page: layer files are labelled "map layer: {layer}".

**Verified**
- 3 new integration tests (`ContentPackGisTests`):
  - the DEMO layer imports 2 boundaries with items, source and audit reason,
    and previewing again shows 2 unchanged, so the import is a no-op;
  - a changed shape on the same date is refused, and on a later date it
    supersedes the version in force, which is end-dated;
  - missing layer, bad date, missing source, invalid JSON, a duplicate layer
    and an unknown barangay are all refused.
- Updated tests: DEMO counts (19 records, 11 files); the broken pack uses an
  SMV file for `NOT_YET_SUPPORTED`. The existing reference-layer tests pass
  unchanged.
- `npm run build` and `npm run lint` are clean.
- Full suite: 442 tests pass (140 domain, 56 application, 246 integration).
- Browser (Playwright, dev server + API on `samples/`): previewing the DEMO
  pack lists "map layer: barangays" with 2 rows, 2 new, and "Ready to import"
  (19 new in total). The page has no horizontal overflow at 390 px, and the
  console is clean at both widths. The pack was not imported into the dev
  database.

