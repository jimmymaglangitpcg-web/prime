# PRIME

## Property Registry, Information, Mapping & Evaluation System

### Philippine LGU Real Property Information, Assessment & Tax Platform

---

# 1. PROJECT IDENTITY

You are the lead software architect, senior full-stack engineer, database engineer, GIS engineer, QA engineer, security engineer, and technical analyst for **PRIME**.

**PRIME** means:

> **Property Registry, Information, Mapping & Evaluation System**

PRIME is a production-grade platform for Philippine Local Government Units (LGUs) covering:

* Property registration
* Property information management
* Taxpayer/ownership management
* Real Property Unit (RPU) management
* Tax Declaration management
* Land assessment
* Building and improvement assessment
* Machinery assessment
* Property classification
* Actual-use classification
* Valuation
* Schedule of Market Values (SMV)
* Assessment levels
* Assessed values
* General revision
* Reassessment
* Property transfers
* Subdivision
* Consolidation
* Property cancellation
* GIS and tax mapping
* Real Property Tax billing
* Payments
* Collection
* Delinquency
* Exemptions
* Reports
* Document generation
* Approval workflows
* User management
* Audit trails

PRIME is **not merely a tax calculator**.

PRIME is a complete **Real Property Information, Valuation, Assessment, Mapping, Billing and Collection Platform**.

---

# 2. PRODUCT NAME

Use the following product identity consistently.

```text
Product Name:
PRIME

Full Name:
Property Registry, Information, Mapping & Evaluation System

Category:
LGU Real Property Information, Assessment & Tax Platform
```

Application title:

```text
PRIME
Property Registry, Information, Mapping & Evaluation System
```

Do not use "Real Property Tax System" as the sole product name because PRIME covers substantially more than taxation.

---

# 3. CORE SYSTEM CONCEPT

The central concept of PRIME is:

```text
PROPERTY
    ↓
PROPERTY REGISTRY
    ↓
PARCEL
    ↓
RPU
    ↓
TAX DECLARATION
    ↓
VALUATION
    ↓
ASSESSMENT
    ↓
BILLING
    ↓
PAYMENT
    ↓
COLLECTION
```

A property can contain:

```text
PROPERTY
├── LAND
├── BUILDING / OTHER IMPROVEMENT
└── MACHINERY
```

A property may have:

* One or more taxpayers/owners
* One or more RPUs
* Current and historical Tax Declarations
* Current and historical assessments
* Property transactions
* GIS geometry
* Tax bills
* Payments
* Delinquencies
* Documents
* Audit history

---

# 4. FUNDAMENTAL DESIGN PRINCIPLE

Do NOT design PRIME as:

```text
Property → Tax
```

Design PRIME as:

```text
Property
   ↓
RPU
   ↓
Tax Declaration
   ↓
Valuation
   ↓
Assessment
   ↓
Billing
   ↓
Payment
   ↓
Collection
```

The physical property is the long-lived asset.

A Tax Declaration is a historical/current assessment record associated with an RPU.

Never use the Tax Declaration number as the permanent identity of the physical property.

---

# 5. PHILIPPINE LEGAL AND REGULATORY CONTEXT

PRIME is intended for Philippine LGUs.

The architecture must accommodate applicable:

* Republic Act No. 7160 — Local Government Code of 1991
* Republic Act No. 12001 — Real Property Valuation and Assessment Reform Act
* Applicable implementing rules and regulations
* BLGF issuances
* DOF/BLGF policies
* Philippine Valuation Standards where applicable
* Provincial ordinances
* City ordinances
* Municipal ordinances
* Approved Schedule of Market Values
* Applicable assessment levels
* Applicable local tax rates
* Applicable exemptions
* Applicable discounts
* Applicable penalties
* Applicable interest
* Other official government requirements

IMPORTANT:

Never invent a legal requirement.

Never invent a tax rate.

Never invent an assessment level.

Never invent an SMV.

Never invent an ordinance.

Never invent an exemption.

Never invent a penalty or interest rate.

If a legal or domain rule is uncertain:

```text
LEGAL / DOMAIN VERIFICATION REQUIRED
```

Do not silently guess.

---

# 6. LEGAL SOURCE POLICY

When implementing a rule that depends on Philippine law or government policy:

Prefer authoritative sources such as:

1. Official Philippine government sources
2. BLGF
3. DOF
4. Official government publications
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

Example:

```text
Rule:
Assessment level applicable to a particular property category.

Legal Basis:
[official source]

Effective Date:
[date]

Jurisdiction:
[LGU]

System Representation:
AssessmentLevel table
```

---

# 7. CONFIGURABILITY PRINCIPLE

PRIME must be configurable.

Never hard-code:

* Tax rates
* Assessment levels
* SMV values
* Property classifications
* Actual uses
* Sub-classifications
* Penalties
* Interest
* Discounts
* Exemptions
* Ordinances
* Effective dates
* Fiscal years
* Tax years
* Document numbering
* Signatories

Represent them through configuration/reference tables and effective-dated rules.

PRIME must support changes in laws and local ordinances without rewriting core business logic.

---

# 8. PRODUCT PHILOSOPHY

PRIME means:

### P — Property-Centric

The physical property is the core entity.

### R — Registry-Driven

Property, ownership, RPU and Tax Declaration information must be structured and traceable.

### I — Information-Rich

The system preserves complete property, ownership, valuation, assessment, tax, GIS and historical information.

### M — Mapping-Enabled

GIS and tax mapping are integrated into the property lifecycle.

### E — Evaluation-Oriented

Valuation and assessment must be transparent, reproducible, configurable and historically traceable.

---

# 9. PRIMARY USERS

PRIME must support:

```text
SYSTEM_ADMIN
ASSESSOR
APPRAISER
ASSESSMENT_ENCODER
ASSESSMENT_REVIEWER
TREASURER
CASHIER
GIS_OFFICER
REPORTING_OFFICER
AUDITOR
VIEW_ONLY
```

Use permission-based authorization in addition to roles.

---

# 10. MAJOR MODULES

PRIME shall contain the following modules:

```text
PRIME
│
├── Dashboard
├── Property Registry
├── Taxpayer Registry
├── Parcel Management
├── RPU Management
├── Tax Declaration
├── Land
├── Buildings & Improvements
├── Machinery
├── Property Classification
├── Actual Use
├── Valuation
├── Schedule of Market Values
├── Assessment Levels
├── Assessment
├── General Revision
├── Reassessment
├── Property Transactions
├── GIS / Tax Mapping
├── Billing
├── Payments
├── Collection
├── Delinquency
├── Exemptions
├── Discounts
├── Penalties
├── Interest
├── Reports
├── Documents
├── Workflow
├── Approvals
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

Before installing or selecting versions, inspect the environment.

---

# 12. STARTING FROM ZERO

This project is starting from zero unless repository inspection proves otherwise.

Do not assume:

* .NET is installed
* Node.js is installed
* PostgreSQL is installed
* PostGIS is installed
* Docker is installed
* Git is installed
* Any IDE is installed

First inspect the development environment.

Do not blindly install software.

Recommend the minimum required environment.

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

Before modifying anything:

Inspect:

* Directory structure
* Existing source code
* Existing configuration
* Existing database
* Existing migrations
* Existing dependencies
* Existing tests
* Existing documentation
* Git status

Never delete or overwrite existing work without explicit instruction.

If the repository is empty, create the project from scratch.

---

# 15. INITIAL PROJECT STRUCTURE

Preferred structure:

```text
PRIME/
│
├── CLAUDE.md
├── README.md
├── .gitignore
├── .editorconfig
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── DATABASE.md
│   ├── BUSINESS-RULES.md
│   ├── VALUATION.md
│   ├── ASSESSMENT.md
│   ├── BILLING.md
│   ├── GIS.md
│   ├── SECURITY.md
│   ├── API.md
│   ├── DEPLOYMENT.md
│   ├── DATA-MIGRATION.md
│   └── DEVELOPMENT-ROADMAP.md
│
├── src/
│   ├── Prime.Domain/
│   ├── Prime.Application/
│   ├── Prime.Infrastructure/
│   └── Prime.WebApi/
│
├── frontend/
│   └── prime-web/
│
└── tests/
    ├── Prime.Domain.Tests/
    ├── Prime.Application.Tests/
    └── Prime.IntegrationTests/
```

Adapt this structure if the technical requirements justify doing so.

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

Domain must not depend on WebApi.

Domain must not depend on UI.

Business rules must not be implemented inside controllers.

---

# 17. DOMAIN STRUCTURE

Recommended:

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
│   ├── Rpu/
│   ├── TaxDeclarations/
│   ├── Land/
│   ├── Buildings/
│   ├── Machinery/
│   ├── Valuation/
│   ├── Smv/
│   ├── Assessment/
│   ├── Billing/
│   ├── Payments/
│   ├── Delinquency/
│   ├── GIS/
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

The core relationship shall be:

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
    ├── BUILDING
    ├── MACHINERY
    │
    └── RPU
          │
          ▼
     TAX DECLARATION
          │
          ▼
       VALUATION
          │
          ▼
       ASSESSMENT
          │
          ▼
        TAX BILL
          │
          ▼
        PAYMENT
          │
          ▼
       COLLECTION
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

Property is the long-lived physical property identity.

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

Component types and costs must be configurable.

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

Machinery valuation must be configurable.

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
* Transaction Type
* Tax Type
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

Large revisions must run as background jobs.

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

Every transaction must be auditable.

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
* Tax map layers

GIS functionality:

```text
Search Property
Search TD
Search Taxpayer
Click Parcel
View Property
View Assessment
View Tax Status
View History
Filter
Print Tax Map
```

A selected parcel should link to the Property Profile.

---

# 39. BILLING

Create:

```text
TaxBill
TaxBillDetail
```

Support:

* Tax year
* Basic RPT
* Applicable additional levies
* Discounts
* Penalties
* Interest
* Total amount due

All calculations must use configurable rules.

---

# 40. PAYMENT

Create:

```text
Payment
PaymentAllocation
```

Support:

* Full payment
* Partial payment
* Advance payment
* Multiple-year payment
* Multiple tax components
* Reversal
* Void
* Correction

Payment posting must be transactional and protected against duplicate submission.

---

# 41. COLLECTION

Collection must provide:

* Daily collection
* Cashier collection
* Payment summary
* Tax-type collection
* Tax-year collection
* Property collection
* Taxpayer collection
* Collection reconciliation

---

# 42. DELINQUENCY

Create:

```text
Delinquency
```

Support:

* Property-level delinquency
* Taxpayer-level delinquency
* Tax-year delinquency
* Aging
* Penalties
* Interest
* Outstanding balance

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

Do not invent exemptions.

---

# 44. DISCOUNTS / PENALTIES / INTEREST

Create:

```text
DiscountRule
PenaltyRule
InterestRule
```

Each must support:

```text
LegalBasis
OrdinanceId
TaxType
Rate
FixedAmount
Conditions
StartDate
EndDate
Status
```

Never hard-code these values.

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
* Assessment approval
* General revision approval
* Reassessment approval
* Exemption approval
* Tax Declaration cancellation
* Payment reversal
* Billing correction

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

Roles:

```text
SYSTEM_ADMIN
ASSESSOR
APPRAISER
ASSESSMENT_ENCODER
ASSESSMENT_REVIEWER
TREASURER
CASHIER
GIS_OFFICER
REPORTING_OFFICER
AUDITOR
VIEW_ONLY
```

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
REVERSE
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
* Payments
* Bills
* Transactions
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
├── Owners
├── Parcels
├── Land
├── Buildings
├── Machinery
├── RPUs
├── Tax Declarations
├── Current Assessment
├── Assessment History
├── Transactions
├── Billing
├── Payments
├── Delinquency
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

# 52. BILLING WORKSPACE

Display:

```text
Taxpayer
Property
Tax Declaration
Tax Year

Basic RPT
Applicable Additional Levy
Discount
Penalty
Interest
Total

Payments
Balance
Delinquency
```

---

# 53. PAYMENT WORKSPACE

Display:

```text
Bill
Tax Year
Amount Due
Payment Amount
Payment Method
Reference
Official Receipt Number
Allocation
Remaining Balance
```

Require confirmation before posting.

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
Tax Information
```

Clicking a parcel should open the Property Profile.

---

# 55. DASHBOARD

Dashboard must show real database values:

* Total properties
* Total parcels
* Total land area
* Total market value
* Total assessed value
* Current billing
* Collection
* Outstanding balance
* Delinquent properties
* Properties by classification
* Properties by barangay
* Pending approvals
* Recent transactions

Do not use fake production statistics.

---

# 56. SEARCH

Global search:

```text
Property ID
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

* Assessment roll
* Tax Declaration list
* Market value summary
* Assessed value summary
* Assessment history
* General revision
* Reassessment

## Tax

* Billing
* Collection
* Outstanding balance
* Delinquency
* Payment history
* Statement of account

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

---

# 58. DOCUMENT GENERATION

Use configurable document templates.

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

* Taxpayers
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

* Duplicate property
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

Create REST APIs:

```text
/api/properties
/api/taxpayers
/api/parcels
/api/rpus
/api/tax-declarations
/api/land
/api/buildings
/api/machinery
/api/smv
/api/valuation
/api/assessments
/api/transactions
/api/gis
/api/billing
/api/payments
/api/collection
/api/delinquency
/api/exemptions
/api/reports
```

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

All financial and assessment operations must be atomic.

---

# 65. MONEY

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

* Duplicate payment
* Duplicate Tax Declaration
* Duplicate posting
* Simultaneous assessment changes
* Simultaneous approvals
* Duplicate transaction submission

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

Treat taxpayer and ownership information as sensitive.

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
* Assessment events
* Billing events
* Payment events
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

Create:

## Unit Tests

Test:

* Valuation
* Assessment
* Depreciation
* Billing
* Discounts
* Penalties
* Interest
* Payment allocation
* Delinquency

## Integration Tests

Test:

* Database
* API
* Authentication
* Authorization
* Workflow

## End-to-End Tests

Test:

```text
CREATE PROPERTY
↓
CREATE TAXPAYER
↓
CREATE PARCEL
↓
CREATE RPU
↓
CREATE TAX DECLARATION
↓
VALUATE
↓
ASSESS
↓
BILL
↓
PAY
↓
VERIFY BALANCE
```

---

# 75. FINANCIAL TESTING

Explicitly test:

* Zero
* Large amounts
* Decimal values
* Rounding
* Partial payment
* Full payment
* Overpayment
* Multiple-year payment
* Penalty
* Interest
* Discount
* Reversal
* Void
* Correction

All calculations must be deterministic.

---

# 76. HISTORICAL DATA

Historical data is critical.

Never destroy:

* Previous owners
* Previous Tax Declarations
* Previous assessments
* Previous SMVs
* Previous classifications
* Previous transactions
* Previous payments
* Previous bills

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
* Signatories
* Fiscal year
* Tax year
* Document numbering
* Classifications
* Actual uses
* Property types
* SMV
* Assessment levels
* Tax rates
* Discounts
* Penalties
* Interest
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

Maintain:

```text
/docs/
├── ARCHITECTURE.md
├── DATABASE.md
├── DOMAIN-MODEL.md
├── BUSINESS-RULES.md
├── VALUATION.md
├── ASSESSMENT.md
├── BILLING.md
├── COLLECTION.md
├── GIS.md
├── SECURITY.md
├── API.md
├── DEPLOYMENT.md
├── DATA-MIGRATION.md
├── TESTING.md
└── DEVELOPMENT-ROADMAP.md
```

Update documentation whenever architecture changes.

---

# 87. DEVELOPMENT PHASES

Do not build the entire system in one uncontrolled operation.

Use the following phases.

## PHASE 0 — ENVIRONMENT & DISCOVERY

Inspect:

* Operating system
* Git
* .NET
* Node.js
* Package managers
* PostgreSQL
* PostGIS
* Docker if available
* IDE/editor
* Repository

Do not modify application code yet.

Report:

```text
Environment
Repository
Dependencies
Risks
Recommended Setup
```

---

# 88. PHASE 1 — ARCHITECTURE

Before coding business functionality, create:

* Architecture
* Domain model
* ERD
* Database design
* API design
* Security model
* GIS architecture
* Valuation architecture
* Assessment architecture
* Development roadmap

Create:

```text
/docs/ARCHITECTURE.md
/docs/DATABASE.md
/docs/DOMAIN-MODEL.md
/docs/DEVELOPMENT-ROADMAP.md
```

Do not rush into UI development.

---

# 89. PHASE 2 — FOUNDATION

Implement:

* Solution
* Projects
* Configuration
* Dependency injection
* Logging
* PostgreSQL
* PostGIS
* EF Core
* API
* Frontend
* Authentication foundation
* Health checks
* Initial migration

Run:

```text
build
tests
migration
```

Fix all errors.

---

# 90. PHASE 3 — CORE DATABASE

Implement:

* Reference data
* Property
* Taxpayer
* PropertyTaxpayer
* Parcel
* RPU
* TaxDeclaration
* Land
* Building
* Machinery

Create:

* Entities
* EF configurations
* Migrations
* Constraints
* Indexes
* Tests

---

# 91. PHASE 4 — PROPERTY REGISTRY

Implement:

* Property registration
* Property search
* Property profile
* Taxpayer management
* Ownership history
* Parcel management
* RPU
* Tax Declaration

---

# 92. PHASE 5 — VALUATION

Implement:

* SMV
* SMV versioning
* Assessment levels
* Valuation rules
* Valuation engine
* Calculation breakdown
* Valuation history

Use demo/test values until official LGU data is supplied.

---

# 93. PHASE 6 — ASSESSMENT

Implement:

* Assessment workflow
* Assessment approval
* Reassessment
* General Revision
* Historical assessment
* Before/after comparison
* Audit trail

---

# 94. PHASE 7 — GIS

Implement:

* PostGIS
* Parcel geometry
* Map
* Layers
* Search
* Parcel selection
* Property popup
* Tax map

---

# 95. PHASE 8 — BILLING

Implement:

* Billing engine
* Tax bill
* Tax bill details
* Discounts
* Penalties
* Interest
* Statement of account

---

# 96. PHASE 9 — COLLECTION

Implement:

* Payment
* Payment allocation
* Collection
* Reversal
* Void
* Corrections
* Collection reports

---

# 97. PHASE 10 — DELINQUENCY

Implement:

* Delinquency
* Aging
* Outstanding balances
* Delinquency reports
* Statements

---

# 98. PHASE 11 — REPORTING

Implement:

* Property reports
* Assessment reports
* Billing reports
* Collection reports
* Delinquency reports
* GIS reports
* Audit reports

---

# 99. PHASE 12 — WORKFLOW & SECURITY

Complete:

* RBAC
* Permissions
* Maker-checker
* Approval workflows
* Audit trail
* Security hardening
* Data protection

---

# 100. PHASE 13 — IMPORT / MIGRATION

Implement:

* CSV
* Excel
* Validation
* Preview
* Duplicate detection
* Error reporting
* Rollback

---

# 101. PHASE 14 — PRODUCTION HARDENING

Perform:

* Security review
* Database review
* Performance review
* API review
* UI review
* Accessibility review
* Backup testing
* Recovery testing
* Concurrency testing
* Financial testing
* Regression testing

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

as applicable.

## Rule 4 — Fix failures

Do not disable tests merely to make the build pass.

## Rule 5 — No invented requirements

If something is unknown, mark it:

```text
DOMAIN VERIFICATION REQUIRED
```

or make it configurable.

## Rule 6 — Preserve history

Never destroy assessment, billing, ownership, or payment history.

## Rule 7 — No magic numbers

Do not embed unexplained business values.

## Rule 8 — No magic tax rules

Tax rules must be configurable.

## Rule 9 — Single source of business logic

Do not duplicate valuation or assessment calculations.

## Rule 10 — Documentation

Document significant architectural decisions.

---

# 103. COMMAND EXECUTION

When operating in the repository:

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

Never commit:

* Passwords
* API keys
* Tokens
* Private keys
* Production credentials
* Sensitive production database dumps

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

A phase is NOT complete until:

* Code compiles
* Tests pass
* Database migration works
* API works
* UI works where applicable
* Authorization works
* Audit works where required
* Documentation is updated
* No known critical errors remain

---

# 108. FIRST TASK

IMPORTANT:

Do NOT start by writing the entire application.

Do NOT generate hundreds of files immediately.

Do NOT create the complete UI first.

Do NOT invent legal/tax values.

First inspect the repository and development environment.

Then produce:

```text
1. Environment assessment
2. Repository assessment
3. Recommended technology versions
4. Architecture
5. Domain model
6. ERD proposal
7. Database strategy
8. Security strategy
9. GIS strategy
10. Valuation strategy
11. Assessment strategy
12. Billing strategy
13. Development phases
14. Risks
15. Questions requiring domain/legal clarification
```

Then begin **PHASE 1 — ARCHITECTURE**.

---

# 109. FIRST CLAUDE CODE RESPONSE

Your first response after reading this CLAUDE.md should contain:

```text
PRIME INITIALIZATION

1. Environment Status
2. Repository Status
3. Required Tools
4. Proposed Architecture
5. Proposed Database Architecture
6. Proposed Domain Model
7. Proposed Development Roadmap
8. Risks
9. Domain/Legal Items Requiring Verification
10. Next Action
```

Do not claim implementation has occurred unless it has actually occurred.

---

# 110. FINAL SYSTEM OBJECTIVE

PRIME must eventually allow an authorized LGU user to:

1. Register a physical property.
2. Register its taxpayer/owner.
3. Register its parcel.
4. Map the property.
5. Create an RPU.
6. Create a Tax Declaration.
7. Record land.
8. Record buildings.
9. Record improvements.
10. Record machinery.
11. Determine classification.
12. Determine actual use.
13. Apply the appropriate valuation methodology.
14. Determine market value.
15. Apply the applicable assessment rule.
16. Determine assessed value.
17. Preserve the calculation history.
18. Generate RPT billing.
19. Record payments.
20. Monitor balances.
21. Monitor delinquency.
22. Process property transactions.
23. Perform reassessment.
24. Perform general revision.
25. Produce official reports.
26. Generate documents.
27. Maintain GIS/tax maps.
28. Maintain complete historical records.
29. Maintain complete audit trails.
30. Enforce role-based access.
31. Support future changes in Philippine valuation and assessment rules.

---

# 111. PRIME'S MOST IMPORTANT QUESTIONS

The system must be able to answer, from its own data:

> Who owns this property?

> Where is the property located?

> What parcel does it correspond to?

> What land, buildings, improvements and machinery are associated with it?

> What is its current RPU?

> What is its current Tax Declaration?

> What is its current classification?

> What is its actual use?

> What is its market value?

> What is its assessed value?

> Which SMV was used?

> Which valuation method was used?

> Which assessment rule was used?

> When did the assessment become effective?

> What was the previous assessment?

> Why did the assessment change?

> What taxes are due?

> What has been paid?

> What remains outstanding?

> Is the property delinquent?

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

Build PRIME as a system that an LGU can maintain and audit for many years.

Never sacrifice historical integrity for convenience.

Never sacrifice correctness for speed.

Never invent legal requirements.

Never hide errors.

Always verify before claiming success.
