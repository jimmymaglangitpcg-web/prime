# PRIME — Domain Model

Status: Phase 1 (proposed), **implemented in Phase 3** (2026-09-24) for
§3.1–§3.10, plus AppUser/Role/Permission and Document. Derived directly
from CLAUDE.md §18–§49; no fields or entities invented beyond what the
specification and standard effective-dating/audit conventions require.

## 0. Phase 3 implementation decisions (record of real calls made while coding)

These resolve ambiguities in CLAUDE.md that only became concrete once
actual C# types and EF configurations had to be written. Documented here
rather than silently decided, per Rule 5.

1. **The `Property` entity is named `PropertyEntity` in code.** Plain
   `Property` collides too easily with EF Core's own vocabulary (entity
   *properties*, `IProperty` metadata, `EntityTypeBuilder.Property(...)`)
   throughout the codebase. This is a C#-identifier-only decision — the
   domain concept, table name (`Property`), API contracts, and all
   documentation still call it "Property."
2. **Structural lifecycle status vs. LGU-configurable classification vs.
   maker-checker workflow status — three different mechanisms, not one:**
   - `RecordStatus` (C# enum: Active/Inactive/Cancelled/Subdivided/
     Consolidated/Superseded) — used by Property, Taxpayer, Parcel, RPU,
     Land, Building, Machinery, AppUser, Document. CLAUDE.md §27 lists
     "Status" among reference tables, but these values describe PRIME's
     own record lifecycle, not an LGU legal/business classification an
     ordinance could vary — so a fixed enum, not a configurable table.
   - `WorkflowStatus` (existing enum, §3.21) — used by TaxDeclaration only
     so far; the fixed maker-checker set from CLAUDE.md §45.
   - Reference tables — used wherever CLAUDE.md §27 calls something out as
     configurable *and* does not also give it a closed enumerated list
     elsewhere in the spec.
3. **Fixed-list fields → enum; §27-named-but-open-ended fields → reference
   table.** Applying that rule: `TaxpayerType`, `RpuType`, `Taxability` are
   enums (CLAUDE.md gives their closed value lists explicitly, in §20/§22/
   §23). `RoadType` and `OwnershipType` are reference tables (CLAUDE.md's
   raw field lists in §24/§20 omit the "Id" suffix other FK fields get,
   but §27 explicitly names both as configurable reference data with no
   closed list given elsewhere) — implemented as `RoadTypeId`/
   `OwnershipTypeId` foreign keys despite the spec's field-name spelling.
   `BuildingComponentType` is a reference table even though §25 gives an
   illustrative list (Foundation, Structural Frame, ...), because §25
   itself says "must be configurable."
4. **`Status` reference table (generic, §27) was NOT built.** A
   module-scoped generic lookup was considered and rejected as unjustified
   complexity beyond what CLAUDE.md's own examples call for — see #2.
5. **Audit fields on every entity, even where CLAUDE.md's raw field list
   for that section didn't repeat them** (e.g. RPU's listed fields stop at
   `CreatedBy`/`ApprovedBy`/`CreatedAt` — `UpdatedAt`/`UpdatedBy` were added
   anyway for consistency with CLAUDE.md §64/§77's general requirement).
6. **`CreatedBy`/`UpdatedBy`/`ApprovedBy`/`UploadedBy` are plain nullable
   `Guid` columns, not DB-level foreign keys to `AppUser`**, in Phase 3.
   Deliberate simplification — enforcing this on ~20 tables before any
   real user-provisioning flow exists added complexity for no Phase-3
   benefit. Revisit at Phase 12 (hardening) once `AppUser` rows are
   actually being created by a real sign-up flow.

---

## 1. Core concept

The physical property is the permanent identity. Tax Declarations are
historical/current assessment records attached to an RPU, not the identity
of the property itself (CLAUDE.md §4).

```text
TAXPAYER
    │
    ▼
PROPERTY_TAXPAYER
    │
    ▼
PROPERTY
    │
    ├── PARCEL
    ├── LAND
    ├── BUILDING (+ BUILDING_COMPONENT)
    ├── MACHINERY
    │
    └── RPU
          │
          ▼
     TAX_DECLARATION
          │
          ▼
       VALUATION
          │
          ▼
       ASSESSMENT
          │
          ▼
        TAX_BILL
          │
          ▼
        PAYMENT
          │
          ▼
       COLLECTION
```

A `Property` may have one or more `Taxpayer`s (current + historical, via
`PropertyTaxpayer`), one or more `RPU`s, and current + historical Tax
Declarations, Assessments, Transactions, GIS geometry, Bills, Payments,
Delinquencies, Documents, and Audit history.

---

## 2. Aggregate boundaries (proposed, DDD-lite)

Aggregates are grouped around the entity that owns the transactional
invariant and the approval workflow, so each aggregate root is the unit a
repository loads/saves and the unit maker-checker rules attach to.

| Aggregate root | Members | Invariant it protects |
|---|---|---|
| **Property** | Parcel, Land, Building, BuildingComponent, Machinery, RPU | A property's physical composition and its RPUs stay consistent; RPUs reference an existing Property |
| **Taxpayer** | PropertyTaxpayer (ownership links) | Ownership percentage across current owners of a property is valid; ownership history is append-only |
| **TaxDeclaration** | — (links RPU → Assessment lineage) | A TD always has a `PreviousTaxDeclarationId` chain when superseding one; never hard-deleted |
| **SMV** | SMVSchedule, SMVZone, SMVRate | Only one SMV schedule row is effective for a given classification/actual use/zone/date at a time |
| **AssessmentLevel** | — | Same effective-dating exclusivity as SMV, scoped to classification/actual use/property type |
| **Assessment** | — | Immutable once posted; superseding assessment references the prior one, never overwrites |
| **PropertyTransaction** | — | Every mutation of property/ownership/classification is represented as a transaction row with a type and audit trail |
| **TaxBill** | TaxBillDetail | Total is always the sum of details (basic RPT + levies − discounts + penalties + interest) |
| **Payment** | PaymentAllocation | Sum of allocations never exceeds payment amount; postings are transactional and idempotent |
| **Delinquency** | — | Derived/recomputable from TaxBill + Payment state, not an independently-editable source of truth |
| **ExemptionType / PropertyExemption** | ExemptionDocument | An exemption always carries legal basis + approval reference |
| **DiscountRule / PenaltyRule / InterestRule** | — | Each is effective-dated and ordinance-referenced |
| **AppUser / Role / Permission** | UserRole, RolePermission | Least-privilege; permission set is explicit, not inferred. Credentials themselves live in Supabase Auth, outside this aggregate — see §3.22 |
| **AuditLog** | — | Append-only, never edited or deleted |

Cross-aggregate references (e.g. `Land.RpuId`, `Assessment.RpuId`) are by
id only — aggregates do not hold direct object references across the
boundaries above, so each can be loaded/tested independently.

---

## 3. Entities

Field lists below are as specified in CLAUDE.md; types are proposed for
Phase 2. `AuditFields` = `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
(added to every persisted entity even when not explicitly listed per
section, since CLAUDE.md §64/§77 require WHO/WHEN on every record).

### 3.1 Property (CLAUDE.md §19)

The long-lived physical property identity.

```text
PropertyId                      (PK, surrogate, e.g. uuid or bigint identity)
PropertyIdentificationNumber    (unique, LGU-assigned business key)
ProvinceId / MunicipalityId / BarangayId / ZoneId   (FK → reference tables)
Street, Sitio, LotNumber, BlockNumber, SurveyNumber, TitleNumber, TaxMapNumber
Status                          (enum/reference, e.g. ACTIVE, CANCELLED, SUBDIVIDED, CONSOLIDATED)
+ AuditFields
```

### 3.2 Taxpayer (§20)

```text
TaxpayerId          (PK)
TaxpayerType         (INDIVIDUAL | CORPORATION | PARTNERSHIP | GOVERNMENT | ESTATE | ASSOCIATION | OTHER)
LastName, FirstName, MiddleName, Suffix   (individual)
CorporateName                              (non-individual)
TIN
Address, BarangayId, MunicipalityId, ProvinceId
ContactNumber, Email
Status
+ AuditFields
```

### 3.3 PropertyTaxpayer (§20)

```text
PropertyTaxpayerId   (PK)
PropertyId            (FK → Property)
TaxpayerId            (FK → Taxpayer)
OwnershipType          (reference: SOLE, CO-OWNED, etc.)
OwnershipPercentage
StartDate, EndDate
IsCurrent
+ AuditFields
```

Ownership history is append-only: a change of owner inserts a new row and
closes the previous one (`EndDate` set, `IsCurrent = false`) rather than
overwriting it (§20, §35).

### 3.4 Parcel (§21)

```text
ParcelId       (PK)
PropertyId      (FK → Property)
Geometry        (PostGIS geometry(Polygon/MultiPolygon, <SRID – TBD>))
Area
SurveyNumber, LotNumber, BlockNumber
BarangayId, ZoneId
Status
+ AuditFields
```

### 3.5 RealPropertyUnit / RPU (§22)

```text
RpuId              (PK)
PropertyId          (FK → Property)
RpuNumber            (unique business key)
RpuType              (LAND | BUILDING | MACHINERY | OTHER_IMPROVEMENT)
Status
EffectivityDate, EndDate
PreviousRpuId        (self-FK, nullable — supersession chain)
CreatedBy, ApprovedBy, CreatedAt
```

### 3.6 TaxDeclaration (§23)

```text
TaxDeclarationId          (PK)
RpuId                      (FK → RPU)
PropertyId                 (FK → Property, denormalized for query convenience)
TaxDeclarationNumber        (unique business key)
RevisionNumber
EffectivityDate
Taxability                  (TAXABLE | EXEMPT)
ClassificationId, ActualUseId, SubClassificationId   (FK → reference tables)
AssessmentYear
Status
PreviousTaxDeclarationId    (self-FK — supersession chain, never deleted)
Remarks
CreatedBy, ApprovedBy, CreatedAt, UpdatedAt
```

### 3.7 Land (§24)

```text
LandId, RpuId, PropertyId
Area, AreaUnit
ClassificationId, ActualUseId, SubClassificationId, ZoneId
LocationFactor, RoadFrontage, RoadType, IsCornerLot, Zoning
MarketValue, AssessedValue   (computed by ValuationService/AssessmentService — not user-entered)
Status
+ AuditFields
```

### 3.8 Building / BuildingComponent (§25)

```text
Building:
  BuildingId, RpuId, PropertyId
  BuildingTypeId, StructuralTypeId, ActualUseId
  NumberOfStoreys, FloorArea, TotalFloorArea
  YearConstructed, YearCompleted
  ConditionId, CompletionPercentage
  MarketValue, Depreciation, DepreciatedValue, AssessedValue   (computed)
  Status
  + AuditFields

BuildingComponent:
  BuildingComponentId, BuildingId
  ComponentTypeId   (FK → configurable component type: Foundation, Structural
                      Frame, Exterior Walls, Roofing, Flooring, Doors/Windows,
                      Electrical, Plumbing, Mechanical, Finishing, Other)
  Cost / UnitCost, Quantity, Description
  + AuditFields
```

Component types and their costs are configuration data, not enum constants
in code (§25 "Component types and costs must be configurable").

### 3.9 Machinery (§26)

```text
MachineryId, RpuId, PropertyId
MachineryTypeId
Description, Brand, Model, SerialNumber
Capacity, CapacityUnit
DateAcquired, AcquisitionCost, InstallationCost, OtherCost
EconomicLife, RemainingLife
Depreciation, MarketValue, AssessedValue   (computed)
Status
+ AuditFields
```

### 3.10 Reference tables (§27)

Each is a small, effective-dateable lookup table (Id, Code, Name,
Description, Status, sort order, + AuditFields), never a hard-coded enum for
LGU-specific values:

```text
Province, City/Municipality, Barangay, Zone,
Classification, ActualUse, SubClassification,
PropertyType, BuildingType, StructuralType, BuildingComponentType,
MachineryType, RoadType, Condition, OwnershipType,
TransactionType, TaxType, Status(-lookup)
```

`Status` here refers to the reusable status-lookup table where a domain
status set is data-driven (as opposed to `Status` fields backed by a fixed,
code-level enum — CLAUDE.md leaves this an implementation choice per
entity; workflow statuses in §45 are fixed by the spec, so those are C#
enums, while purely LGU-configurable classifications are reference tables).

### 3.11 SMV / SMVSchedule / SMVZone / SMVRate (§28)

```text
Smv:
  SmvId, OrdinanceNumber, OrdinanceDate, ApprovalDate, EffectivityDate,
  RevisionYear, Status, Description

SmvSchedule:
  SmvScheduleId, SmvId, ClassificationId, ActualUseId, PropertyTypeId,
  ZoneId, Unit, MarketValue, MinimumValue, MaximumValue,
  EffectiveDate, EndDate
```

`SMVZone`/`SMVRate` from the section heading are represented as the
`ZoneId` FK on `SMVSchedule` plus the `MarketValue`/`Unit` rate fields — no
separate table is warranted unless a zone can carry multiple simultaneous
rate tiers. Historical SMVs are never overwritten; a new revision is a new
`Smv`/`SmvSchedule` row set (§28).

**Resolved (Phase 5, 2026-09-23)**: one `SmvSchedule` row per
classification+actual use+property type+zone is sufficient — no
multi-tier `SMVZone` sub-table was built. `PropertyType` was added as a
Phase 5 reference table (§3.10 already listed it as a target, but it
wasn't built until this phase actually needed it).

### 3.12 AssessmentLevel (§29)

```text
AssessmentLevelId, OrdinanceId, ClassificationId, ActualUseId, PropertyTypeId
LowerValue, UpperValue, AssessmentPercentage
EffectiveDate, EndDate, Status
```

**Resolved (Phase 5, 2026-09-23)**: `OrdinanceId` above implies an
`Ordinance` table, but none exists anywhere in the codebase — `Smv` itself
doesn't reference one either, storing ordinance fields inline (§3.11).
Built as inline `OrdinanceNumber string` / `OrdinanceDate DateOnly?`
instead, the same way. Built as versioned/effective-dated reference data
only in Phase 5 — not yet consumed by any service; Phase 6's
`AssessmentService` is what applies this to a computed market value.

### 3.12a Valuation (§31, new in Phase 5)

```text
ValuationId, RpuId, PropertyId
SourceType (Land | Building | Machinery), SourceId
SmvId, SmvScheduleId
ValuationMethod (SmvBased | ReplacementCost)
ComputedMarketValue, BreakdownJson
EffectiveDate, ComputedAt
```

The CLAUDE.md §31 calculation-transparency record — one immutable row per
computed market value, holding enough of the resolved rule (which SMV/
schedule) and a JSON snapshot of the calculator's inputs/intermediates to
reconstruct the result without re-running the calculation. Never updated
in place; recomputing creates a new row, giving a free valuation history
per Rpu. Not the same entity as `Assessment` (§3.13, Phase 6) — a
`Valuation` only carries `MarketValue`; `Assessment` is what layers
`AssessmentLevel` on top to produce `AssessedValue`.

### 3.13 Assessment (§32)

```text
AssessmentId, RpuId, AssessmentYear
MarketValue, AssessmentLevel, AssessedValue
ValuationMethod, SmvId
AssessmentStatus   (DRAFT | PENDING_REVIEW | APPROVED | REJECTED | POSTED | CANCELLED | VOIDED)
EffectiveDate
ApprovedBy, ApprovedDate
RevisionReference   (links to the General Revision job/batch that produced it, if any)
Remarks
```

Never overwritten (§32); a new assessment year/revision is a new row
referencing the prior one via `RevisionReference` / the TD supersession
chain.

**Resolved (Phase 6, 2026-09-23)**: built as sketched, with two
refinements found necessary while implementing. First, `AssessmentLevel`
is a foreign key (`AssessmentLevelId`), not just the bare `AssessmentLevel`
value shown above — but the `AssessmentPercentage` value it held at
computation time is *also* copied onto the row directly, since
`AssessmentLevel` rows are themselves versioned/effective-dated (§29): a
historical Assessment must keep meaning the same thing even if its
`AssessmentLevel` is later superseded. Second, `PropertyId` was added
(denormalized from `Valuation`, matching the same convention Land/Building/
Machinery already use) purely for query convenience. `RevisionReference`
is a nullable FK to `GeneralRevisionJob` (§3.13a below), not
`PropertyTransaction` — see that section for why. `PreviousAssessmentId`
(a TaxDeclaration-style self-reference, not explicitly named above but
implied by "referencing the prior one") is what "reassessment" actually
uses: creating a new Assessment with `PreviousAssessmentId` set to the
prior one is the entire reassessment mechanism — no separate service or
entity was needed for it.

### 3.13a GeneralRevisionJob (§33/§72, new in Phase 6)

```text
GeneralRevisionJobId, RevisionYear
JobExecutionStatus (Queued | Running | Completed | Failed)
TotalCount, ProcessedCount, FailedCount
StartedBy, StartedAt, CompletedAt
Remarks
```

Tracks a General Revision batch's background-job execution progress —
distinct from `Assessment.Status` (`WorkflowStatus`, maker-checker
approval state): `JobExecutionStatus` describes whether the job itself
finished running, not whether its output has been reviewed/approved yet.
Deliberately not the generic `PropertyTransaction` (§34, still unbuilt in
this codebase) — General Revision is the only batch transaction type any
phase has needed so far, so a small purpose-built entity was used instead
of inventing Transfer/Subdivision/Consolidation structure ahead of a phase
that actually needs it.

### 3.14 PropertyTransaction (§34)

```text
PropertyTransactionId, PropertyId, RpuId (nullable), TaxDeclarationId (nullable)
TransactionType   (NEW_DISCOVERY | NEW_ASSESSMENT | TRANSFER | SUBDIVISION |
                    CONSOLIDATION | RECLASSIFICATION | REASSESSMENT |
                    GENERAL_REVISION | CANCELLATION | CORRECTION |
                    ADDITION_OF_IMPROVEMENT | REMOVAL_OF_IMPROVEMENT)
EffectiveDate, Status, Remarks
+ AuditFields (+ ApprovedBy/ApprovedDate for maker-checker types)
```

Transfer (§35), Subdivision (§36), and Consolidation (§37) are all
represented as `PropertyTransaction` rows with supporting linkage tables
(e.g. `SubdivisionParcel` parent/child, `ConsolidationSource` source→new)
that preserve parent/source records rather than overwriting them.

### 3.15 GIS (§38)

Geometry lives on `Parcel` (polygon) and optionally `Property` (a
representative point, for cases without a finalized parcel polygon).
Barangay boundaries, zones, and roads are their own spatially-indexed
reference tables (`BarangayBoundary`, `ZoneBoundary`, `Road`), each with a
PostGIS `geometry` column.

### 3.16 Billing (§39)

```text
TaxBill:
  TaxBillId, PropertyId, RpuId, TaxDeclarationId, TaxYear
  TotalAmountDue, Status, DueDate
  + AuditFields

TaxBillDetail:
  TaxBillDetailId, TaxBillId
  ComponentType   (BASIC_RPT | ADDITIONAL_LEVY | DISCOUNT | PENALTY | INTEREST)
  Description, Amount, RuleReference (FK to the Discount/Penalty/Interest/Levy rule applied)
```

### 3.17 Payment (§40)

```text
Payment:
  PaymentId, TaxpayerId, PaymentDate, Amount, PaymentMethod, ReferenceNumber
  OfficialReceiptNumber, Status (POSTED | REVERSED | VOIDED)
  + AuditFields

PaymentAllocation:
  PaymentAllocationId, PaymentId, TaxBillId, TaxBillDetailId (nullable),
  AllocatedAmount, TaxYear
```

Reversal/void/correction (§40) are represented as new `Payment`/
`PaymentAllocation` rows with a `Status`/reference back to the original —
never as a destructive edit.

### 3.18 Delinquency (§42)

```text
DelinquencyId, PropertyId, TaxpayerId, TaxYear
OutstandingPrincipal, OutstandingPenalty, OutstandingInterest
AgingBucket, AsOfDate
```

Recomputed/materialized from Bill + Payment state (see §2 aggregate table)
rather than being an independently editable ledger.

### 3.19 Exemptions (§43)

```text
ExemptionType: ExemptionTypeId, Code, Name, LegalBasisTemplate, Status

PropertyExemption:
  PropertyExemptionId, PropertyId, ExemptionTypeId
  LegalBasis, ApprovalReference, EffectiveDate, ExpirationDate
  ApprovedBy, Status

ExemptionDocument:
  ExemptionDocumentId, PropertyExemptionId, DocumentId (FK → Document)
```

### 3.19a Document (supports §43, §59; backed by Supabase Storage — see ARCHITECTURE.md §3.9)

```text
Document:
  DocumentId          (PK)
  Bucket               (Supabase Storage bucket name, e.g. "property-documents")
  StoragePath          (object path within the bucket)
  FileName, ContentType, SizeBytes
  DocumentTypeId        (FK → reference table: Title, Deed, Tax Declaration
                          scan, Assessment document, Exemption document,
                          Valuation document, Other)
  RelatedEntityType, RelatedEntityId   (e.g. "Property", PropertyId — the
                                         record this document supports)
  UploadedBy, UploadedAt
  Status                (ACTIVE | ARCHIVED)
```

`Document` never stores the file itself — only the Supabase Storage
reference and metadata. Actual bytes never live in PostgreSQL. Access is
always mediated by `Prime.WebApi` (authorization check, then stream or
signed URL) — no `Document` row implies a public URL.

### 3.20 Discount / Penalty / Interest rules (§44)

```text
DiscountRule / PenaltyRule / InterestRule (parallel shape):
  <Rule>Id, LegalBasis, OrdinanceId, TaxType, Rate, FixedAmount,
  Conditions, StartDate, EndDate, Status
```

`Conditions` is a structured/JSON field (e.g. "paid before Q1 deadline") —
exact structure is an implementation detail for Phase 8 (Billing), not
fixed here.

### 3.21 Workflow status (§45)

Shared status enum used by TaxDeclaration, Assessment, PropertyTransaction,
TaxBill correction, PropertyExemption, and other maker-checker-governed
entities:

```text
DRAFT | SUBMITTED | PENDING_REVIEW | APPROVED | REJECTED | POSTED |
CANCELLED | VOIDED
```

### 3.22 User management (§47) — Supabase Auth + PRIME authorization

Credential storage, password hashing, lockout, and MFA are owned by
**Supabase Auth** (`auth.users`, not a PRIME table — see ARCHITECTURE.md
§3.4). PRIME's own `User` entity is renamed **`AppUser`** to make clear it
is a profile/authorization record, not a credential store:

```text
AppUser: AppUserId, SupabaseUserId (uuid, unique, NOT a DB-level FK — see
           DATABASE.md §1), DisplayName, Email (denormalized copy for
           search/display), Status, + AuditFields
Role: RoleId, Code, Name
Permission: PermissionId, Code, Name, Module
UserRole: AppUserId, RoleId
RolePermission: RoleId, PermissionId
```

No `PasswordHash` field — Supabase Auth owns credentials entirely. Every
other place in this document that says `CreatedBy`/`ApprovedBy`/`UserId`
etc. refers to `AppUserId`, not the Supabase Auth id directly, so PRIME's
audit/maker-checker records stay valid even if the auth provider ever
changes.

Roles (fixed set per §9/§47): `SYSTEM_ADMIN, ASSESSOR, APPRAISER,
ASSESSMENT_ENCODER, ASSESSMENT_REVIEWER, TREASURER, CASHIER, GIS_OFFICER,
REPORTING_OFFICER, AUDITOR, VIEW_ONLY`.

### 3.23 AuditLog (§48)

```text
AuditId, UserId, Module, TableName, RecordId
Action   (CREATE | UPDATE | DELETE | APPROVE | REJECT | POST | VOID |
           CANCEL | REVERSE | LOGIN | LOGOUT | EXPORT)
OldValue, NewValue   (structured, e.g. JSON snapshot of changed fields)
Timestamp, IPAddress, Reason
```

Append-only; no update/delete path exists for this table at the application
layer.

---

## 4. Relationship summary (selected, non-exhaustive)

```text
Taxpayer            1───* PropertyTaxpayer *───1 Property
Property             1───* Parcel
Property             1───* RPU
RPU                   1───* TaxDeclaration (history via PreviousTaxDeclarationId)
RPU                   1───1..* Land | Building | Machinery  (per RpuType)
Building              1───* BuildingComponent
RPU                   1───* Assessment (history)
TaxDeclaration        1───1 (current) Assessment context per AssessmentYear
Property/RPU/TD        1───* PropertyTransaction
Property               1───* TaxBill *───* Payment (via PaymentAllocation)
Property               1───* PropertyExemption
Property               1───1 Delinquency (per TaxYear, derived)
AppUser                 *───* Role *───* Permission  (AppUser.SupabaseUserId
                                                        links to Supabase Auth's
                                                        auth.users, by value only)
Every mutable entity above → AuditLog (1 row per mutation)
```

See DATABASE.md for the corresponding ERD and physical schema.

---

## 5. Open domain-model questions (do not guess — verify before Phase 3)

1. ~~Does an `SMVZone` ever need multiple simultaneous rate tiers per zone
   (e.g. by frontage/depth), or is one `SMVSchedule` row per
   classification+actual use+zone sufficient?~~ **Resolved (Phase 5)**:
   one `SmvSchedule` row per classification+actual use+property type+zone
   was built; no multi-tier sub-table. Revisit if real LGU data proves
   this insufficient.
2. Exact `Conditions` structure for Discount/Penalty/Interest rules —
   depends on actual LGU ordinance language. **DOMAIN VERIFICATION
   REQUIRED.**
3. Whether `OwnershipPercentage` needs currency-like precision (e.g.
   fractions of undivided co-ownership) — affects column scale.
4. Whether `Taxability = EXEMPT` on a Tax Declaration is sufficient, or
   whether partial exemption (a percentage) must be representable
   alongside `PropertyExemption` — RA 12001/BLGF guidance should confirm.
