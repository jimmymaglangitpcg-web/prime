# PRIME

## Property Registry, Information, Mapping & Evaluation System

### Philippine LGU Real Property Appraisal & Assessment Platform

---

# 0. SCOPE (revised 2026-09-26)

PRIME is the system of the **local assessor's office**. Its scope follows the
**Manual on Real Property Appraisal and Assessment Operations** (MRPAAO;
DOF-BLGF Local Assessment Regulations No. 1-04, 2004/2006), kept in the
repository as `docs/References/ManualRPAandAO.pdf`. The manual's chapters
define what PRIME covers:

```text
Ch. I    Local Government Assessment Organization      → users, roles, offices (§9, §47)
Ch. II   Real Property Identification System            → PIN, tax mapping, TMCR (§19, §21, §38, §115)
Ch. III  General Revision; Schedule of Fair Market Values → SMV preparation, general revision (§28, §33)
Ch. IV   Real Property Appraisal for Taxation Purposes  → land, buildings, machinery, special purpose (§24–§26, §30)
Ch. V    Assessment of Real Property                    → assessment, listing, exemptions (§29, §32, §43)
Ch. VI   Real Property Assessment Records Management    → forms, codes, numbering (§114)
Ch. VII  Appeals before the LBAA and CBAA               → assessment appeals (§113)
Ch. VIII Miscellaneous (appraisal committee, zonal valuation, land use) → §28, §116
Ch. IX–X Penal and final provisions                     → reference only
```

**How the manual is used (user decision, 2026-09-25):** the MRPAAO is the
source for **structure, forms, fields and procedures**. Its rules and values
are **not** hard-coded: they stay configurable, because the manual is
superseded by the **Local Assessment Manual (LAM, DOF Department Circular
004-2025)** and by RA 12001 and its IRR. When the LAM or an ordinance is
supplied, it overrides the MRPAAO wherever they differ. Never commit
LAM-derived or ordinance-derived content to the repository, which has a
GitHub remote.

**Out of scope: treasury operations.** Tax rates, billing, payment,
collection, remittance, delinquency, discounts, penalties and interest belong
to the Local Treasurer. They are not part of PRIME's objectives.

- Code for billing and collection already exists from the earlier scope
  (Phases 8–9: `Billing`, `Collection` features, `/api/bills`,
  `/api/payments`, `/api/collection*`, their tables and screens). It is
  **frozen**: do not extend it and do not build on it. Remove it only when
  the user explicitly asks, and plan the removal as a reviewed, non-destructive
  change (§105).
- The assessor's outputs that the treasury uses (the assessed value on a
  posted assessment and Tax Declaration, the assessment roll) stay in scope.

**Section numbers are stable.** Code and documents cite this file as
`CLAUDE.md §N`. Removed sections keep their number with a one-line note, and
new sections are appended (§113 onward).

---

# 1. PROJECT IDENTITY

You are the lead software architect, senior full-stack engineer, database engineer, GIS engineer, QA engineer, security engineer, and technical analyst for **PRIME**.

**PRIME** means:

> **Property Registry, Information, Mapping & Evaluation System**

PRIME is a production-grade platform for the real property appraisal and assessment operations of Philippine Local Government Units (LGUs), covering:

* Assessment organization, users and offices
* Property identification (PIN) and tax mapping
* Property registration and property information management
* Owner/administrator (taxpayer) and ownership management
* Real Property Unit (RPU) management
* Land, building/improvement and machinery appraisal
* Property classification and actual use
* Schedule of Fair Market Values (SMV) preparation and versioning
* Assessment levels, assessed values and assessment
* Tax Declarations and FAAS
* Listing of real property, taxable and exempt
* Exemptions
* General revision and reassessment
* Property transactions (transfer, subdivision, consolidation, cancellation …)
* Notices of Assessment and sworn statements
* Assessment records: TMCR, Assessment Rolls, Ownership Record Cards, Records of Assessment
* Assessment appeals (LBAA/CBAA)
* GIS and tax maps
* Reports and document generation
* Approval workflows, user management and audit trails

---

# 2. PRODUCT NAME

```text
Product Name:
PRIME

Full Name:
Property Registry, Information, Mapping & Evaluation System

Category:
LGU Real Property Appraisal & Assessment Platform
```

Application title:

```text
PRIME
Property Registry, Information, Mapping & Evaluation System
```

---

# 3. CORE SYSTEM CONCEPT

```text
PROPERTY
    ↓
PROPERTY IDENTIFICATION (PIN) + TAX MAP
    ↓
PARCEL
    ↓
RPU (land / building / machinery / other improvement)
    ↓
APPRAISAL (FAAS)
    ↓
ASSESSMENT
    ↓
TAX DECLARATION
    ↓
NOTICE OF ASSESSMENT
    ↓
ASSESSMENT RECORDS (TMCR, Assessment Roll, ORC, ROA)
    ↓
APPEAL (where filed)
```

A property can contain:

```text
PROPERTY
├── LAND
├── BUILDING / OTHER IMPROVEMENT
└── MACHINERY
```

A property may have:

* One or more owners/administrators and other parties
* One or more RPUs
* Current and historical Tax Declarations
* Current and historical appraisals and assessments
* Property transactions
* GIS geometry and tax map location
* Notices, sworn statements and appeals
* Documents
* Audit history

---

# 4. FUNDAMENTAL DESIGN PRINCIPLE

Design PRIME as:

```text
Property
   ↓
RPU
   ↓
Appraisal
   ↓
Assessment
   ↓
Tax Declaration
```

The physical property is the long-lived asset, identified by its PIN.

A Tax Declaration is a historical/current assessment record associated with an RPU.

Never use the Tax Declaration number as the permanent identity of the physical property.

---

# 5. PHILIPPINE LEGAL AND REGULATORY CONTEXT

The architecture must accommodate applicable:

* Republic Act No. 7160 — Local Government Code of 1991 (Book II, Title II)
* Republic Act No. 12001 — Real Property Valuation and Assessment Reform Act, and its IRR
* The Local Assessment Manual (DOF DC 004-2025) — supersedes the MRPAAO
* The MRPAAO (LAR 1-04) — structural basis of PRIME (§0)
* BLGF issuances and DOF/BLGF policies
* Philippine Valuation Standards where applicable
* Provincial, city and municipal ordinances
* Approved Schedules of Market Values
* Applicable assessment levels
* Applicable exemptions
* Other official government requirements

IMPORTANT:

Never invent a legal requirement.

Never invent an assessment level.

Never invent an SMV.

Never invent an ordinance.

Never invent an exemption.

Never invent a depreciation rate, adjustment factor or form content.

If a legal or domain rule is uncertain:

```text
LEGAL / DOMAIN VERIFICATION REQUIRED
```

Do not silently guess.

---

# 6. LEGAL SOURCE POLICY

When implementing a rule that depends on Philippine law or government policy, prefer authoritative sources:

1. Official Philippine government sources
2. BLGF
3. DOF
4. Official government publications (including the MRPAAO and the LAM)
5. Official LGU ordinances
6. Other authoritative government sources

Do not rely on random blogs for legal requirements.

For every important legal/business rule, document:

```text
Rule
Legal Basis
Source
Effective Date
Jurisdiction
System Representation
```

The regulatory baseline is kept in `docs/analysis/current-real-property-regulatory-baseline.md`.

---

# 7. CONFIGURABILITY PRINCIPLE

PRIME must be configurable.

Never hard-code:

* Assessment levels
* SMV values
* Property classifications
* Actual uses
* Sub-classifications
* Adjustment factors
* Depreciation rates
* Exemptions
* Ordinances
* Effective dates
* Assessment years
* PIN formats and document numbering
* Transaction codes
* Form layouts
* Signatories and approval chains

Represent them through configuration/reference tables and effective-dated rules.

PRIME must support changes in laws, the LAM and local ordinances without rewriting core business logic.

---

# 8. PRODUCT PHILOSOPHY

### P — Property-Centric

The physical property is the core entity.

### R — Registry-Driven

Property, ownership, RPU and Tax Declaration information must be structured and traceable.

### I — Information-Rich

The system preserves complete property, ownership, appraisal, assessment, GIS and historical information.

### M — Mapping-Enabled

GIS and tax mapping are integrated into the property lifecycle.

### E — Evaluation-Oriented

Appraisal and assessment must be transparent, reproducible, configurable and historically traceable.

---

# 9. PRIMARY USERS

The assessor's office (MRPAAO Ch. I):

```text
SYSTEM_ADMIN
ASSESSOR
APPRAISER
ASSESSMENT_ENCODER
ASSESSMENT_REVIEWER
GIS_OFFICER
REPORTING_OFFICER
AUDITOR
VIEW_ONLY
```

The office's actual positions and delegations (provincial, city and municipal
assessors; MRPAAO Ch. I §4–§5) map onto these roles through configuration.
Their exact mapping is DOMAIN VERIFICATION REQUIRED.

Use permission-based authorization in addition to roles.

---

# 10. MAJOR MODULES

```text
PRIME
│
├── Dashboard
├── Property Registry
├── Owner / Taxpayer Registry
├── Property Identification (PIN) & Tax Mapping
├── Parcel Management
├── RPU Management
├── Land
├── Buildings & Improvements
├── Machinery
├── Property Classification
├── Actual Use
├── Appraisal / Valuation
├── Schedule of Market Values
├── Assessment Levels
├── Assessment
├── Tax Declaration & FAAS
├── Exemptions
├── General Revision
├── Reassessment
├── Property Transactions
├── Notices of Assessment
├── Sworn Statements
├── Assessment Records (TMCR, AR, ORC, ROA)
├── Assessment Appeals
├── GIS / Tax Maps
├── Reports
├── Documents & Forms
├── Workflow & Approvals
├── Audit Trail
├── User Management
└── System Administration
```

---

# 11. TECHNOLOGY STACK

Use the following stack unless there is a documented technical reason to change it.

## Backend

* C#
* ASP.NET Core
* Entity Framework Core
* RESTful API
* Clean Architecture
* Dependency Injection
* FluentValidation or equivalent
* OpenAPI/Swagger

## Frontend

* React
* TypeScript
* Modern component-based architecture
* Responsive enterprise UI

## Database

* PostgreSQL
* PostGIS

## GIS

* PostGIS
* OpenLayers or another mature open-source web mapping framework

## Authentication

* ASP.NET Core Identity or equivalent
* Role-based access
* Permission-based authorization
* MFA-ready architecture

## Testing

* xUnit
* Integration testing
* API testing
* Frontend testing
* End-to-end testing

Use current stable versions appropriate for the development environment.

---

# 12. ENVIRONMENT

The development environment is set up (Phase 0). Before installing or
upgrading anything, inspect what is installed. Do not blindly install
software.

---

# 13. CLAUDE CODE BEHAVIOR

You are operating as a senior engineering team.

Always:

1. Inspect before modifying.
2. Understand before coding.
3. Plan before implementing.
4. Make small coherent changes.
5. Build after meaningful changes.
6. Run tests.
7. Fix errors.
8. Update documentation.
9. Verify the result.
10. Report exactly what was done.

Never claim something works unless it was actually verified.

---

# 14. EXISTING REPOSITORY SAFETY

Before modifying anything, inspect the directory structure, source code,
configuration, database, migrations, dependencies, tests, documentation and
git status.

Never delete or overwrite existing work without explicit instruction.

---

# 15. PROJECT STRUCTURE

```text
PRIME/
├── CLAUDE.md
├── docs/
├── src/
│   ├── Prime.Domain/
│   ├── Prime.Application/
│   ├── Prime.Infrastructure/
│   └── Prime.WebApi/
├── frontend/
│   └── prime-web/
└── tests/
    ├── Prime.Domain.Tests/
    ├── Prime.Application.Tests/
    └── Prime.IntegrationTests/
```

---

# 16. CLEAN ARCHITECTURE

Backend layers:

```text
Prime.Domain
Prime.Application
Prime.Infrastructure
Prime.WebApi
```

Dependency direction:

```text
WebApi
   ↓
Application
   ↓
Domain

Infrastructure
   ↓
Application / Domain
```

Domain must not depend on WebApi or UI.

Business rules must not be implemented inside controllers.

---

# 17. DOMAIN STRUCTURE

Domain:

```text
Prime.Domain/
├── Entities/
├── ValueObjects/
├── Enums/
├── DomainServices/
├── Events/
├── Exceptions/
└── Interfaces/
```

Application:

```text
Prime.Application/
├── Features/
│   ├── Properties/
│   ├── Taxpayers/
│   ├── Parcels/
│   ├── RealPropertyUnits/
│   ├── TaxDeclarations/
│   ├── Lands/
│   ├── Buildings/
│   ├── MachineryUnits/
│   ├── Valuation/
│   ├── Smv/
│   ├── AssessmentLevels/
│   ├── Assessments/
│   ├── GeneralRevision/
│   ├── Transactions/
│   ├── Notices/
│   ├── SwornStatements/
│   ├── Registers/
│   ├── Appeals/
│   ├── Forms/
│   ├── Gis/
│   └── Reports/
├── DTOs/
├── Validators/
├── Services/
└── Interfaces/
```

Infrastructure:

```text
Prime.Infrastructure/
├── Persistence/
├── Identity/
├── GIS/
├── Reporting/
├── Documents/
├── Storage/
└── ExternalServices/
```

---

# 18. DATABASE CORE MODEL

```text
TAXPAYER (owner / administrator)
    │
    ▼
PROPERTY_TAXPAYER (party, capacity, history)
    │
    ▼
PROPERTY (PIN)
    │
    ├── PARCEL (geometry, tax map)
    ├── LAND
    ├── BUILDING
    ├── MACHINERY
    │
    └── RPU
          │
          ▼
       VALUATION (appraisal)
          │
          ▼
       ASSESSMENT
          │
          ▼
     TAX DECLARATION (+ FAAS)
          │
          ├── NOTICE OF ASSESSMENT
          ├── REGISTERS (TMCR, AR, ORC, ROA)
          └── APPEAL
```

---

# 19. PROPERTY ENTITY

Create a stable Property entity.

Suggested fields:

```text
PropertyId
PropertyIdentificationNumber
ProvinceId
MunicipalityId
BarangayId
ZoneId
Street
Sitio
LotNumber
BlockNumber
SurveyNumber
TitleNumber
TaxMapNumber
Status
CreatedAt
UpdatedAt
CreatedBy
UpdatedBy
```

Property is the long-lived physical property identity. The PIN follows the
Real Property Identification System (MRPAAO Ch. II §1) through a configurable
numbering scheme (§115).

---

# 20. TAXPAYER ENTITY

Create:

```text
Taxpayer
```

Support:

* Individual
* Corporation
* Partnership
* Government entity
* Estate
* Association
* Other legal entities

Suggested fields:

```text
TaxpayerId
TaxpayerType
LastName
FirstName
MiddleName
Suffix
CorporateName
TIN
Address
BarangayId
MunicipalityId
ProvinceId
ContactNumber
Email
Status
```

Create:

```text
PropertyTaxpayer
```

Fields:

```text
PropertyTaxpayerId
PropertyId
TaxpayerId
OwnershipType
OwnershipPercentage
StartDate
EndDate
IsCurrent
```

Ownership history must be preserved.

---

# 21. PARCEL

Create:

```text
Parcel
```

Suggested fields:

```text
ParcelId
PropertyId
Geometry
Area
SurveyNumber
LotNumber
BlockNumber
BarangayId
ZoneId
Status
```

Use PostGIS geometry.

---

# 22. RPU

Create:

```text
RealPropertyUnit
```

Fields:

```text
RpuId
PropertyId
RpuNumber
RpuType
Status
EffectivityDate
EndDate
PreviousRpuId
CreatedBy
ApprovedBy
CreatedAt
```

RPU types:

```text
LAND
BUILDING
MACHINERY
OTHER_IMPROVEMENT
```

A property may have multiple RPUs.

---

# 23. TAX DECLARATION

Create:

```text
TaxDeclaration
```

Fields:

```text
TaxDeclarationId
RpuId
PropertyId
TaxDeclarationNumber
RevisionNumber
EffectivityDate
Taxability
ClassificationId
ActualUseId
SubClassificationId
AssessmentYear
Status
PreviousTaxDeclarationId
Remarks
CreatedBy
ApprovedBy
CreatedAt
UpdatedAt
```

Never physically delete historical Tax Declarations.

---

# 24. LAND

Create:

```text
Land
```

Fields:

```text
LandId
RpuId
PropertyId
Area
AreaUnit
ClassificationId
ActualUseId
SubClassificationId
ZoneId
LocationFactor
RoadFrontage
RoadType
IsCornerLot
Zoning
MarketValue
AssessedValue
Status
```

Appraisal of urban and agricultural lands follows MRPAAO Ch. IV, with every
factor configurable.

---

# 25. BUILDING / IMPROVEMENTS

Create:

```text
Building
BuildingComponent
```

Building:

```text
BuildingId
RpuId
PropertyId
BuildingTypeId
StructuralTypeId
ActualUseId
NumberOfStoreys
FloorArea
TotalFloorArea
YearConstructed
YearCompleted
ConditionId
CompletionPercentage
MarketValue
Depreciation
DepreciatedValue
AssessedValue
Status
```

Building components may include:

```text
Foundation
Structural Frame
Exterior Walls
Roofing
Flooring
Doors/Windows
Electrical
Plumbing
Mechanical
Finishing
Other
```

Component types, costs and depreciation must be configurable.

---

# 26. MACHINERY

Create:

```text
Machinery
```

Fields:

```text
MachineryId
RpuId
PropertyId
MachineryTypeId
Description
Brand
Model
SerialNumber
Capacity
CapacityUnit
DateAcquired
AcquisitionCost
InstallationCost
OtherCost
EconomicLife
RemainingLife
Depreciation
MarketValue
AssessedValue
Status
```

Machinery valuation must be configurable (MRPAAO Ch. IV).

---

# 27. REFERENCE TABLES

Create configurable reference data for:

* Province
* City/Municipality
* Barangay
* Zone
* Classification
* Actual Use
* Sub-Classification
* Property Type
* Building Type
* Structural Type
* Building Component
* Machinery Type
* Road Type
* Condition
* Ownership Type
* Transaction Type and Transaction Code
* Title Type
* Status

Do not hard-code LGU-specific values.

---

# 28. SCHEDULE OF MARKET VALUES

Create:

```text
SMV
SMVSchedule
SMVZone
SMVRate
```

SMV:

```text
SmvId
OrdinanceNumber
OrdinanceDate
ApprovalDate
EffectivityDate
RevisionYear
Status
Description
```

SMV schedule:

```text
SmvScheduleId
SmvId
ClassificationId
ActualUseId
PropertyTypeId
ZoneId
Unit
MarketValue
MinimumValue
MaximumValue
EffectiveDate
EndDate
```

Never overwrite historical SMVs.

The preparation of the Schedule of Fair Market Values (MRPAAO Ch. III §4–§5)
and the role of the appraisal committee (Ch. VIII §1) are supported as a
workflow; the values themselves always come from the LGU.

---

# 29. ASSESSMENT LEVEL

Create:

```text
AssessmentLevel
```

Fields:

```text
AssessmentLevelId
OrdinanceId
ClassificationId
ActualUseId
PropertyTypeId
LowerValue
UpperValue
AssessmentPercentage
EffectiveDate
EndDate
Status
```

Assessment percentages must be configurable.

---

# 30. VALUATION ENGINE

Create a dedicated valuation service.

It must determine:

```text
MarketValue
ValuationMethod
ValuationBasis
AssessmentLevel
AssessedValue
```

Potential methods include:

* Land valuation
* Building valuation
* Machinery valuation
* Replacement cost
* Depreciation
* Location adjustment
* SMV-based valuation
* Special purpose property rules (MRPAAO Ch. IV §8)
* Other legally applicable methods

Do not assume one formula applies to every property.

The valuation engine must be configurable.

---

# 31. CALCULATION TRANSPARENCY

Every valuation and assessment should preserve enough information to explain:

```text
Input Data
+
Applicable Rule
+
Valuation Method
+
SMV
+
Assessment Level
=
Result
```

A user with proper permissions must be able to see the calculation breakdown.

---

# 32. ASSESSMENT

Create:

```text
Assessment
```

Fields:

```text
AssessmentId
RpuId
AssessmentYear
MarketValue
AssessmentLevel
AssessedValue
ValuationMethod
SmvId
AssessmentStatus
EffectiveDate
ApprovedBy
ApprovedDate
RevisionReference
Remarks
```

Historical assessments must never be overwritten.

Assessment principles, rules and the listing of real property follow MRPAAO Ch. V.

---

# 33. GENERAL REVISION

Implement:

```text
SELECT
→ PREVIEW
→ CALCULATE
→ COMPARE
→ VALIDATE
→ REVIEW
→ APPROVE
→ POST
```

General revision must:

* Preserve old assessments
* Create new assessment versions
* Record affected properties
* Record old value
* Record new value
* Record reason
* Record effective date
* Record approving user

Large revisions must run as background jobs (§72).

---

# 34. PROPERTY TRANSACTIONS

Create:

```text
PropertyTransaction
```

Support:

```text
NEW_DISCOVERY
NEW_ASSESSMENT
TRANSFER
SUBDIVISION
CONSOLIDATION
RECLASSIFICATION
REASSESSMENT
GENERAL_REVISION
CANCELLATION
CORRECTION
ADDITION_OF_IMPROVEMENT
REMOVAL_OF_IMPROVEMENT
```

Transaction types, their requirements and their codes (MRPAAO Ch. VI §2) are
configurable. Every transaction must be auditable.

---

# 35. PROPERTY TRANSFER

Transfers must preserve:

* Previous owner
* New owner
* Effective date
* Supporting documents
* Previous Tax Declaration
* New Tax Declaration where applicable
* Approval
* Audit trail

Do not simply replace the owner record.

---

# 36. SUBDIVISION

Support subdivision of a property into multiple parcels.

Preserve:

```text
Parent Property
Parent Parcel
Child Properties
Child Parcels
Transaction
Effective Date
Approvals
Audit Trail
```

Do not destroy the historical parent record.

---

# 37. CONSOLIDATION

Support consolidation of multiple properties/parcels.

Preserve:

```text
Source Properties
Source Parcels
New Property
New Parcel
Transaction
Effective Date
Approvals
Audit Trail
```

---

# 38. GIS

Use PostGIS.

Support:

* Parcel polygons
* Property points
* Barangay boundaries
* Zones
* Roads
* Tax map layers (MRPAAO Ch. II §2: base maps, index maps, property identification maps)

GIS functionality:

```text
Search Property
Search TD
Search Owner
Click Parcel
View Property
View Assessment
View History
Filter
Print Tax Map
```

A selected parcel should link to the Property Profile.

---

# 39. BILLING — removed (treasury scope; see §0)

# 40. PAYMENT — removed (treasury scope; see §0)

# 41. COLLECTION — removed (treasury scope; see §0)

# 42. DELINQUENCY — removed (treasury scope; see §0)

---

# 43. EXEMPTIONS

Create:

```text
ExemptionType
PropertyExemption
ExemptionDocument
```

Store:

```text
LegalBasis
ApprovalReference
EffectiveDate
ExpirationDate
SupportingDocument
ApprovedBy
Status
```

Exempt properties are appraised, assessed and listed like taxable ones
(MRPAAO Ch. V §4; Assessment Roll for Exempt Properties).

Do not invent exemptions.

---

# 44. DISCOUNTS / PENALTIES / INTEREST — removed (treasury scope; see §0)

---

# 45. WORKFLOW

Important transactions should follow:

```text
DRAFT
↓
SUBMITTED
↓
PENDING REVIEW
↓
APPROVED / REJECTED
↓
POSTED
```

Supported statuses:

```text
DRAFT
PENDING_REVIEW
APPROVED
REJECTED
POSTED
CANCELLED
VOIDED
```

Prevent unauthorized state changes.

---

# 46. MAKER-CHECKER

For sensitive transactions, support separation of duties.

Examples:

* SMV approval
* Assessment level approval
* Assessment approval
* General revision approval
* Reassessment approval
* Exemption approval
* Tax Declaration approval and cancellation
* Property transaction approval
* Configuration approval (numbering, forms, transaction types)

Where configured, the creator must not be able to approve their own transaction.

---

# 47. USER MANAGEMENT

Create:

```text
User
Role
Permission
UserRole
RolePermission
```

Roles: as in §9.

Use least-privilege security.

---

# 48. AUDIT TRAIL

Create:

```text
AuditLog
```

Fields:

```text
AuditId
UserId
Module
TableName
RecordId
Action
OldValue
NewValue
Timestamp
IPAddress
Reason
```

Log:

```text
CREATE
UPDATE
DELETE
APPROVE
REJECT
POST
VOID
CANCEL
LOGIN
LOGOUT
EXPORT
```

Critical government records should be immutable or versioned.

---

# 49. SOFT DELETE

Do not physically delete:

* Tax Declarations
* Assessments
* Appraisals (valuations)
* Transactions
* Issued forms, notices and registers
* Appeals
* Audit logs
* Historical ownership
* Historical SMVs

Use status/versioning instead.

---

# 50. PROPERTY PROFILE

Create a central Property Profile screen.

It must display:

```text
PROPERTY
│
├── Basic Information
├── GIS Map
├── Owners and other parties
├── Parcels
├── Land
├── Buildings
├── Machinery
├── RPUs
├── Tax Declarations / FAAS
├── Current Assessment
├── Assessment History
├── Transactions
├── Notices of Assessment
├── Sworn Statements
├── Appeals
├── Documents
└── Audit History
```

This is one of the most important screens in PRIME.

---

# 51. ASSESSMENT WORKSPACE

Create an assessment workspace showing:

```text
Property
Owner
RPU
Tax Declaration

LAND
BUILDING
MACHINERY

VALUATION
Market Value
Valuation Method
Valuation Basis

ASSESSMENT
Assessment Level
Assessed Value

HISTORY
Previous Value
New Value
Difference
Reason
Effective Date
```

Show the calculation breakdown.

---

# 52. BILLING WORKSPACE — removed (treasury scope; see §0)

# 53. PAYMENT WORKSPACE — removed (treasury scope; see §0)

---

# 54. GIS WORKSPACE

Provide:

```text
Map
Search
Layers
Filters
Parcel Selection
Property Information
Assessment Information
```

Clicking a parcel should open the Property Profile.

---

# 55. DASHBOARD

Dashboard must show real database values:

* Total properties
* Total parcels
* Total land area
* Total market value
* Total assessed value (taxable and exempt)
* Properties by classification
* Properties by barangay
* Pending approvals
* Recent transactions
* General revision progress
* Pending appeals

Do not use fake production statistics.

---

# 56. SEARCH

Global search:

```text
Property ID (PIN)
Tax Declaration Number
RPU Number
Owner Name
TIN
Lot Number
Title Number
Survey Number
Barangay
Address
Tax Map Number
```

Use database indexes.

---

# 57. REPORTS

Implement:

## Property

* Property inventory
* Property by barangay
* Property by classification
* Property by actual use
* Property by zone

## Assessment

* Assessment Roll — taxable and exempt
* Tax Declaration list
* Market value summary
* Assessed value summary
* Assessment history
* General revision
* Reassessment
* Records of Assessment

## Records (MRPAAO Ch. VI)

* Tax Map Control Roll
* Ownership Record Card

## GIS

* Tax map
* Parcel inventory
* Classification map
* Value map

## Audit

* User activity
* Record changes
* Approval history
* Export history

Support:

```text
PDF
Excel
CSV
Print
```

Reports required by BLGF or the LAM are DOMAIN VERIFICATION REQUIRED until
their content is supplied.

---

# 58. DOCUMENT GENERATION

Use configurable document templates (forms foundation: form definitions,
versions, issued snapshots).

Support:

* LGU name
* Office
* Logo
* Signatory
* Position
* Document number
* Date
* Footer
* Legal reference

Do not hard-code every government document into application source code.

---

# 59. DOCUMENT STORAGE

Support secure documents such as:

* Titles
* Deeds
* Tax Declarations
* Assessment documents
* Exemption documents
* Valuation documents
* Appeal records
* Supporting records

Store metadata in the database.

Store files using secure file/object storage.

Do not expose private documents through public URLs.

---

# 60. DATA IMPORT

Support:

* CSV
* Excel

Import:

* Owners/taxpayers
* Properties
* Parcels
* Tax Declarations
* Land
* Buildings
* Machinery
* Historical assessments
* SMV
* GIS data

Workflow:

```text
UPLOAD
↓
VALIDATE
↓
PREVIEW
↓
ERROR REVIEW
↓
CONFIRM
↓
IMPORT
↓
AUDIT
```

Never silently import invalid data.

---

# 61. DATA QUALITY

Validate:

* Duplicate property / PIN
* Duplicate Tax Declaration
* Duplicate RPU
* Duplicate taxpayer
* Invalid ownership percentage
* Missing classification
* Missing actual use
* Invalid assessment level
* Invalid market value
* Invalid assessed value
* Invalid dates
* Invalid GIS geometry
* Missing supporting documents

---

# 62. API

REST APIs in scope:

```text
/api/properties
/api/taxpayers
/api/parcels
/api/rpus
/api/land
/api/buildings
/api/machinery
/api/tax-declarations
/api/smv
/api/assessment-levels
/api/adjustment-factors
/api/valuation
/api/assessments
/api/general-revision
/api/transactions
/api/notices
/api/sworn-statements
/api/registers
/api/appeals
/api/gis
/api/forms
/api/numbering-schemes
/api/approval-chains
/api/reference
/api/reports
```

`/api/bills`, `/api/billing`, `/api/payments`, `/api/collection` and
`/api/collections` exist from the earlier scope and are frozen (§0).

Support:

* Pagination
* Filtering
* Sorting
* Searching
* Validation
* Authorization
* Audit logging
* Consistent errors

---

# 63. API ERROR FORMAT

Use a consistent structure.

Example:

```json
{
  "code": "ASSESSMENT_RULE_NOT_FOUND",
  "message": "No applicable assessment rule was found for this property.",
  "details": null,
  "traceId": "..."
}
```

Never expose stack traces to users.

---

# 64. DATABASE REQUIREMENTS

Use:

* Primary keys
* Foreign keys
* Unique constraints
* Check constraints
* Indexes
* Spatial indexes
* Effective-date indexes
* Audit fields
* Concurrency tokens
* Transactions

All assessment operations must be atomic.

---

# 65. MONEY

Market values, assessed values, costs and other amounts:

C#:

```text
decimal
```

PostgreSQL:

```text
numeric
```

Never use:

```text
float
double
```

for monetary calculations.

Use consistent precision and scale.

---

# 66. CONCURRENCY

Protect against:

* Duplicate Tax Declaration
* Duplicate posting
* Simultaneous assessment changes
* Simultaneous approvals
* Duplicate transaction submission
* Duplicate document numbers

Use:

* Transactions
* Unique constraints
* Concurrency tokens
* Idempotency where appropriate

---

# 67. SECURITY

Implement:

* HTTPS
* Secure authentication
* Password hashing
* Account lockout
* Password policies
* Role authorization
* Permission authorization
* Input validation
* SQL injection protection
* XSS protection
* CSRF protection where applicable
* Secure file uploads
* Rate limiting
* Audit logging
* Session expiration
* Secure secret storage

Never commit secrets to Git.

---

# 68. PERSONAL DATA

Treat owner and taxpayer information as sensitive (Data Privacy Act;
RA 12001 penalties for unauthorized processing of RPIS data).

Minimize exposure.

Do not display sensitive information to unauthorized users.

Do not log unnecessary personal information.

Do not expose sensitive information through APIs without authorization.

---

# 69. LOGGING

Use structured logging.

Log:

* Errors
* Authentication events
* Authorization failures
* Appraisal and assessment events
* Approval events
* Import/export events
* Important business events

Never log:

* Passwords
* Authentication tokens
* Secret keys

---

# 70. HEALTH CHECKS

Provide health endpoints such as:

```text
/health
```

Check:

* API
* Database
* GIS/PostGIS
* Background jobs where applicable

---

# 71. PERFORMANCE

PRIME must support potentially large LGU datasets.

Use:

* Server-side pagination
* Filtering
* Sorting
* Database indexes
* Spatial indexes
* Efficient queries
* Projection queries
* Caching where appropriate
* Background jobs

Never load the entire property inventory into browser memory.

---

# 72. GENERAL REVISION PERFORMANCE

General Revision may affect thousands or hundreds of thousands of properties.

Do not execute it in one browser request.

Use:

```text
CREATE JOB
↓
QUEUE
↓
PROCESS
↓
VALIDATE
↓
GENERATE RESULTS
↓
REVIEW
↓
APPROVE
↓
POST
```

Provide progress monitoring.

---

# 73. BACKGROUND JOBS

Use a reliable background processing mechanism for:

* General revision
* Large imports
* Report generation
* GIS processing
* Large exports
* Other long-running operations

Jobs must be:

* Trackable
* Retryable where safe
* Auditable
* Idempotent where appropriate

---

# 74. TESTING

## Unit Tests

Test:

* Valuation (land, building, machinery)
* Adjustment factors
* Depreciation
* Assessment levels and brackets
* Assessment
* Numbering patterns
* Transaction code ranking

## Integration Tests

Test:

* Database
* API
* Authentication
* Authorization
* Workflow and maker-checker

## End-to-End Tests

Test:

```text
CREATE PROPERTY
↓
CREATE OWNER
↓
CREATE PARCEL
↓
CREATE RPU
↓
APPRAISE (VALUATE)
↓
ASSESS
↓
APPROVE AND POST
↓
ISSUE TAX DECLARATION / FAAS
↓
NOTICE OF ASSESSMENT
↓
VERIFY RECORDS (Assessment Roll, ROA)
```

---

# 75. CALCULATION TESTING

Explicitly test appraisal and assessment arithmetic:

* Zero
* Large amounts
* Decimal values
* Rounding
* Boundaries between assessment level brackets
* Depreciation limits
* Adjustments
* Multiple classifications on one unit
* Historical rules (a past date uses the rules then in force)

All calculations must be deterministic.

---

# 76. HISTORICAL DATA

Historical data is critical.

Never destroy:

* Previous owners
* Previous Tax Declarations
* Previous appraisals and assessments
* Previous SMVs
* Previous classifications
* Previous transactions
* Issued forms and registers

PRIME must answer:

> What was the property's assessment on a particular date?

and:

> Why did its assessed value change?

---

# 77. AUDITABILITY

Every important calculation and transaction must be reproducible.

Store enough information to determine:

```text
WHO
WHAT
WHEN
WHY
UNDER WHICH RULE
USING WHICH INPUT
RESULTING IN WHAT VALUE
```

---

# 78. CONFIGURATION ADMINISTRATION

Provide administration for:

* LGU information
* Province
* City/Municipality
* Barangays
* Zones
* Offices
* Signatories and approval chains
* Assessment years
* PIN format and document numbering
* Form versions
* Classifications
* Actual uses
* Property types
* Transaction types and codes
* SMV
* Adjustment factors
* Assessment levels
* Exemptions
* System parameters

---

# 79. BACKUP AND RECOVERY

Document:

* Database backup
* Point-in-time recovery
* Backup verification
* Disaster recovery
* Restore testing
* Data export

Do not assume a backup exists simply because a backup script exists.

Recovery must be tested.

---

# 80. DATA RETENTION

Design historical data retention deliberately.

Do not delete government records merely to simplify database maintenance.

Document retention assumptions and make them configurable where appropriate.

---

# 81. SEED DATA

Use only clearly identified demo/test data.

For example:

```text
DEMO PROPERTY
DEMO TAXPAYER
DEMO SMV
DEMO ASSESSMENT RULE
```

Never represent invented values as actual LGU legal data.

---

# 82. UI DESIGN

PRIME should have a professional government enterprise interface.

Use:

* Sidebar
* Top navigation
* Breadcrumbs
* Search
* Data tables
* Advanced filters
* Pagination
* Forms
* Validation
* Confirmation dialogs
* Status badges
* Approval panels
* Audit history
* Printable views

Optimize for desktop office workflows while remaining responsive.

---

# 83. ACCESSIBILITY

Implement reasonable accessibility:

* Keyboard navigation
* Visible focus
* Semantic labels
* Accessible forms
* Sufficient contrast
* Error messages
* Screen-reader-friendly controls where practical

---

# 84. MOBILE

The system is primarily a desktop enterprise application.

However:

* Dashboard should be responsive.
* Search should work on smaller screens.
* Property summary should remain usable.
* GIS should provide a usable responsive experience.

Do not sacrifice desktop productivity to achieve mobile-first design.

---

# 85. BRANDING

Use:

```text
PRIME

Property Registry, Information,
Mapping & Evaluation System
```

Do not use an official LGU seal until the actual LGU provides one.

Create configurable branding:

```text
LGU Name
Logo
Province
City/Municipality
Office
Contact Information
```

---

# 86. PROJECT DOCUMENTATION

Maintain in `/docs/`:

```text
ARCHITECTURE.md
DATABASE.md
DOMAIN-MODEL.md
GIS.md
FORMS-REVISION-PLAN.md
DEVELOPMENT-ROADMAP.md
analysis/   (regulatory baseline, MRPAAO forms model, design docs per step)
```

`BILLING.md` and `analysis/collection.md` document the frozen treasury code (§0).

Update documentation whenever architecture changes.

---

# 87. DEVELOPMENT PHASES

Do not build the entire system in one uncontrolled operation.

```text
Phase 0   Environment & discovery                    done
Phase 1   Architecture                               done
Phase 2   Foundation                                 done
Phase 3   Core database                              done
Phase 4   Property registry                          done
Phase 5   Valuation                                  done (DEMO values)
Phase 6   Assessment                                 done (DEMO values)
Phase 7   GIS                                        done
          Forms foundation and MRPAAO forms model    done
Phase 8   Billing                                    built under the earlier scope; frozen (§0)
Phase 9   Collection                                 built under the earlier scope; frozen (§0)
Phase 10  MRPAAO completeness                        next (§97)
Phase 11  Reporting                                  (§98)
Phase 12  Workflow & security                        (§99)
Phase 13  Import / migration                         (§100)
Phase 14  Production hardening                       (§101)
```

`docs/DEVELOPMENT-ROADMAP.md` holds each phase's status; check it against
the code before trusting it.

---

# 88. PHASE 1 — ARCHITECTURE (done)

Architecture, domain model, ERD, database design, API design, security model,
GIS architecture, valuation and assessment architecture, roadmap.

---

# 89. PHASE 2 — FOUNDATION (done)

Solution, projects, configuration, dependency injection, logging, PostgreSQL,
PostGIS, EF Core, API, frontend, authentication foundation, health checks,
initial migration.

---

# 90. PHASE 3 — CORE DATABASE (done)

Reference data, Property, Taxpayer, PropertyTaxpayer, Parcel, RPU,
TaxDeclaration, Land, Building, Machinery.

---

# 91. PHASE 4 — PROPERTY REGISTRY (done)

Property registration, search and profile; taxpayer management; ownership
history; parcels; RPUs; Tax Declarations.

---

# 92. PHASE 5 — VALUATION (done, DEMO values)

SMV and versioning, assessment levels, valuation rules and engine, calculation
breakdown, valuation history.

---

# 93. PHASE 6 — ASSESSMENT (done, DEMO values)

Assessment workflow and approval, reassessment, general revision, historical
assessment, before/after comparison, audit trail.

---

# 94. PHASE 7 — GIS (done)

PostGIS, parcel geometry, map, layers, search, parcel selection, property
popup, tax map printing.

---

# 95. PHASE 8 — BILLING (frozen; §0)

# 96. PHASE 9 — COLLECTION (frozen; §0)

---

# 97. PHASE 10 — MRPAAO COMPLETENESS

Bring PRIME in line with each chapter of the manual, in checkpointed steps.
Each step starts with a design document under `docs/analysis/` that cites the
manual's pages and lists review questions. No code is written before the
user approves it (§108).

```text
10a  Real Property Identification System (Ch. II)
     PIN structure through configurable numbering; tax mapping operations;
     base, index and property identification maps; pre- and post-tax-map
     control rolls.

10b  Appraisal (Ch. IV)
     Rules for urban lands, agricultural and other lands, buildings and
     other structures (including depreciation), machinery and equipment,
     and special purpose properties, all as configurable rules.

10c  Assessment, listing and exemptions (Ch. V)
     Guiding principles and assessment rules; listing of real property;
     exemptions with their legal basis and the exempt assessment roll.

10d  Records management (Ch. VI)
     Complete the forms and records (FAAS, TD, TMCR, AR, ORC, ROA, NOA,
     sworn statement), the codes used in assessment, and the numbering
     system.

10e  General revision and the Schedule of Fair Market Values (Ch. III, VIII §1–§2)
     General revision of assessments and property classification; support
     for preparing the SMV (mass appraisal data, approaches to value) and
     its review and approval, including the appraisal committee.

10f  Assessment appeals (Ch. VII; §113)
```

Values the manual gives as examples or as then-current rates are
configuration, not code. The LAM supersedes the manual wherever they differ.

---

# 98. PHASE 11 — REPORTING

Property, assessment, records, GIS and audit reports (§57) in PDF, Excel,
CSV and print, as background jobs for large reports (§73).

---

# 99. PHASE 12 — WORKFLOW & SECURITY

RBAC, permissions, maker-checker on every sensitive transaction (§46),
approval workflows, audit trail completeness, security hardening, data
protection, real login.

---

# 100. PHASE 13 — IMPORT / MIGRATION

CSV and Excel import (§60), validation, preview, duplicate detection, error
reporting, rollback; migration of an LGU's existing assessment records.

---

# 101. PHASE 14 — PRODUCTION HARDENING

Security, database, performance, API, UI and accessibility reviews; backup
and recovery testing; concurrency and calculation testing; regression
testing.

---

# 102. DEVELOPMENT RULES

## Rule 1 — Inspect before modifying

Never modify a file without understanding it.

## Rule 2 — Small changes

Make focused changes.

## Rule 3 — Test frequently

After meaningful changes:

```text
build
test
lint
```

as applicable. For frontend changes, also run the production build
(`tsc -b`); the dev server does not catch type errors.

## Rule 4 — Fix failures

Do not disable tests merely to make the build pass.

## Rule 5 — No invented requirements

If something is unknown, mark it:

```text
DOMAIN VERIFICATION REQUIRED
```

or make it configurable.

## Rule 6 — Preserve history

Never destroy appraisal, assessment, ownership or record history.

## Rule 7 — No magic numbers

Do not embed unexplained business values.

## Rule 8 — No magic rules

Appraisal and assessment rules must be configurable.

## Rule 9 — Single source of business logic

Do not duplicate valuation or assessment calculations.

## Rule 10 — Documentation

Document significant architectural decisions.

---

# 103. COMMAND EXECUTION

```text
INSPECT
↓
PLAN
↓
IMPLEMENT
↓
BUILD
↓
TEST
↓
REVIEW
↓
FIX
↓
DOCUMENT
↓
REPORT
```

Do not skip verification.

---

# 104. GIT

Keep changes reviewable.

Commit and push only when the user asks; an earlier instruction to commit
does not carry forward to later work.

Never commit:

* Passwords
* API keys
* Tokens
* Private keys
* Production credentials
* Sensitive production database dumps
* LAM-derived or ordinance-derived content (§0)

Use environment variables or secure secret management.

---

# 105. DATABASE MIGRATION SAFETY

Before destructive migration:

1. Identify affected tables.
2. Explain consequences.
3. Prefer additive migration.
4. Preserve history.
5. Require explicit confirmation for destructive production changes.

Never automatically drop production tables.

Apply migrations to the local development database first; the shared
(Supabase) database is updated only when the user asks.

---

# 106. ERROR HANDLING

Use structured errors.

Users should receive understandable messages.

Developers should receive detailed logs.

Never expose:

* Stack traces
* Connection strings
* Secrets
* Internal infrastructure details

to ordinary users.

---

# 107. QUALITY GATE

A phase or step is NOT complete until:

* Code compiles
* Tests pass
* Database migration works
* API works
* UI works where applicable (verified in a browser)
* Authorization works
* Audit works where required
* Documentation is updated
* No known critical errors remain

---

# 108. WORKING METHOD

For each new phase or step:

1. Inspect the code, the roadmap and the relevant pages of the manual.
2. Write a design document under `docs/analysis/`: what exists, the gaps,
   the proposal, the delivery steps and numbered review questions, each
   with a recommendation.
3. Wait for the user's decisions. Record them in the document.
4. Implement in checkpointed steps; build, test and verify each one, in a
   browser where there is UI.
5. Update the design document, the roadmap and the project memory.
6. Report what was done, what was verified and what remains open.

---

# 109. FIRST RESPONSE — superseded

The project is past its first task; follow §108.

---

# 110. FINAL SYSTEM OBJECTIVE

PRIME must allow an authorized assessor's office user to:

1. Register a physical property and give it its PIN.
2. Register its owners, administrators and other parties.
3. Register and map its parcel on the tax map.
4. Create its RPUs.
5. Record land, buildings, other improvements and machinery.
6. Determine classification and actual use.
7. Apply the appropriate appraisal methodology.
8. Determine market value.
9. Apply the applicable assessment level.
10. Determine assessed value.
11. Preserve the calculation history.
12. Approve and post the assessment.
13. Issue the Tax Declaration and FAAS.
14. Serve the Notice of Assessment.
15. Record sworn statements.
16. Process property transactions.
17. Perform reassessment.
18. Perform general revision and support SMV preparation.
19. Record exemptions.
20. Record assessment appeals and apply their decisions.
21. Maintain the Tax Map Control Roll, Assessment Rolls, Ownership Record
    Cards and Records of Assessment.
22. Produce official reports.
23. Generate documents.
24. Maintain GIS/tax maps.
25. Maintain complete historical records.
26. Maintain complete audit trails.
27. Enforce role-based access.
28. Support future changes in Philippine valuation and assessment rules.

---

# 111. PRIME'S MOST IMPORTANT QUESTIONS

The system must be able to answer, from its own data:

> Who owns this property?

> Where is the property located, and what is its PIN?

> What parcel does it correspond to, and where is it on the tax map?

> What land, buildings, improvements and machinery are associated with it?

> What is its current RPU?

> What is its current Tax Declaration?

> What is its current classification?

> What is its actual use?

> What is its market value?

> What is its assessed value?

> Is it taxable or exempt, and on what basis?

> Which SMV was used?

> Which valuation method was used?

> Which assessment rule was used?

> When did the assessment become effective?

> What was the previous assessment?

> Why did the assessment change?

> Was the owner notified, and when?

> Has the assessment been appealed, and with what result?

> Who created the record?

> Who changed the record?

> Who approved the record?

> When did the change occur?

> What legal/business rule was used?

Every question above must be answerable from the database, configuration, transaction history, or audit trail.

---

# 112. FINAL ENGINEERING PRINCIPLE

Do not optimize PRIME for the amount of code generated.

Optimize PRIME for:

```text
DATA INTEGRITY
SECURITY
AUDITABILITY
HISTORICAL ACCURACY
CONFIGURABILITY
LEGAL TRACEABILITY
GIS INTEGRATION
PERFORMANCE
MAINTAINABILITY
TESTABILITY
```

Build PRIME as a system that an LGU assessor's office can maintain and audit
for many years.

Never sacrifice historical integrity for convenience.

Never sacrifice correctness for speed.

Never invent legal requirements.

Never hide errors.

Always verify before claiming success.

---

# 113. ASSESSMENT APPEALS

MRPAAO Ch. VII sets the rules for appeals before the Local Board of
Assessment Appeals (LBAA) and the Central Board of Assessment Appeals (CBAA).
PRIME records each appeal against an assessment: the appellant, the grounds,
the dates filed and decided, the board's decision and any further appeal.
When a decision requires it, a revised assessment is created through the
normal workflow, without overwriting the original.

Periods, requisites and procedures come from the manual and the current law
as configuration or documented rules. They are DOMAIN VERIFICATION REQUIRED
until checked against RA 7160, RA 12001 and the LAM.

---

# 114. FORMS, CODES AND NUMBERING

MRPAAO Ch. VI and its Attachments 1–11 define the assessment forms and
records:
- FAAS for land/other improvements, buildings and machinery;
- the Tax Declaration;
- the Tax Map Control Roll;
- the Assessment Rolls for taxable and exempt properties;
- the Ownership Record Card and the Record of Assessment;
- the Notice of Assessment and the Sworn Statement.

PRIME renders them through versioned form definitions and issues frozen
snapshots. The MRPAAO layouts are reference layouts, and the LAM's forms
replace them when supplied. Codes used in assessment (e.g. transaction codes)
and document numbering are configuration. The design is in
`docs/analysis/mrpaao-forms-model.md` and `docs/FORMS-REVISION-PLAN.md`.

---

# 115. PROPERTY IDENTIFICATION AND TAX MAPPING

MRPAAO Ch. II describes the Property Identification Number and tax mapping
operations: base maps, section, barangay, municipality and district index
maps, property identification maps, and pre- and post-tax-map control rolls.
PRIME stores the PIN as a configurable pattern (never a hard-coded format),
links every property to its parcel and tax map, and produces the maps and
control rolls from its own data. Unit PINs for buildings and machinery on a
land follow the configured suffix rules.

---

# 116. MISCELLANEOUS ASSESSMENT MATTERS

MRPAAO Ch. VIII (appraisal committee, zonal valuation, land use planning)
informs the SMV preparation workflow (§28) and reference data. Implement only
what a design document has confirmed against current law; treat the rest as
reference material.
