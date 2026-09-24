// Mirrors Prime.Application's DTOs (src/Prime.Application/Features/**/Dtos.cs).
// Kept hand-written and in sync manually — see docs/ARCHITECTURE.md §5 open
// decision #2 (no codegen tool adopted yet).

export type RecordStatus = 'Active' | 'Inactive' | 'Cancelled' | 'Subdivided' | 'Consolidated' | 'Superseded';
export type WorkflowStatus = 'Draft' | 'Submitted' | 'PendingReview' | 'Approved' | 'Rejected' | 'Posted' | 'Cancelled' | 'Voided';
export type TaxpayerType = 'Individual' | 'Corporation' | 'Partnership' | 'Government' | 'Estate' | 'Association' | 'Other';
export type RpuType = 'Land' | 'Building' | 'Machinery' | 'OtherImprovement';
export type Taxability = 'Taxable' | 'Exempt';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// --- Reference data ---------------------------------------------------

export interface LookupDto {
  id: string;
  code: string;
  name: string;
}

export interface ProvinceDto {
  id: string;
  psgcCode: string;
  name: string;
}

export interface MunicipalityDto {
  id: string;
  psgcCode: string;
  name: string;
  provinceId: string;
  isCity: boolean;
}

export interface BarangayDto {
  id: string;
  psgcCode: string;
  name: string;
  municipalityId: string;
}

// --- Property -----------------------------------------------------------

export interface CreatePropertyRequest {
  /** Omit to generate from the PIN numbering scheme in force. */
  propertyIdentificationNumber?: string;
  provinceId: string;
  municipalityId: string;
  barangayId: string;
  zoneId?: string | null;
  street?: string | null;
  sitio?: string | null;
  lotNumber?: string | null;
  blockNumber?: string | null;
  surveyNumber?: string | null;
  titleNumber?: string | null;
  taxMapNumber?: string | null;
}

export interface PropertyDto {
  id: string;
  propertyIdentificationNumber: string;
  provinceId: string;
  provinceName: string;
  municipalityId: string;
  municipalityName: string;
  barangayId: string;
  barangayName: string;
  zoneId: string | null;
  zoneName: string | null;
  street: string | null;
  sitio: string | null;
  lotNumber: string | null;
  blockNumber: string | null;
  surveyNumber: string | null;
  titleNumber: string | null;
  taxMapNumber: string | null;
  status: RecordStatus;
  createdAt: string;
}

export interface PropertyOwnerDto {
  propertyTaxpayerId: string;
  taxpayerId: string;
  taxpayerDisplayName: string;
  ownershipTypeName: string;
  ownershipPercentage: number;
  startDate: string;
  endDate: string | null;
  isCurrent: boolean;
}

export interface ParcelSummaryDto {
  id: string;
  area: number | null;
  lotNumber: string | null;
  barangayName: string;
  status: RecordStatus;
}

export interface RpuSummaryDto {
  id: string;
  rpuNumber: string;
  rpuType: RpuType;
  status: RecordStatus;
  effectivityDate: string;
}

export interface TaxDeclarationSummaryDto {
  id: string;
  taxDeclarationNumber: string;
  revisionNumber: number;
  assessmentYear: number;
  status: WorkflowStatus;
  effectivityDate: string;
}

export interface PropertyProfileDto {
  property: PropertyDto;
  owners: PropertyOwnerDto[];
  parcels: ParcelSummaryDto[];
  rpus: RpuSummaryDto[];
  taxDeclarations: TaxDeclarationSummaryDto[];
}

export interface PropertySearchParams {
  searchTerm?: string;
  barangayId?: string;
  municipalityId?: string;
  page?: number;
  pageSize?: number;
}

// --- Taxpayer -------------------------------------------------------------

export interface CreateTaxpayerRequest {
  taxpayerType: TaxpayerType;
  lastName?: string | null;
  firstName?: string | null;
  middleName?: string | null;
  suffix?: string | null;
  corporateName?: string | null;
  tin?: string | null;
  address?: string | null;
  barangayId?: string | null;
  municipalityId?: string | null;
  provinceId?: string | null;
  contactNumber?: string | null;
  email?: string | null;
}

export interface TaxpayerDto {
  id: string;
  taxpayerType: TaxpayerType;
  displayName: string;
  lastName: string | null;
  firstName: string | null;
  middleName: string | null;
  suffix: string | null;
  corporateName: string | null;
  tin: string | null;
  address: string | null;
  contactNumber: string | null;
  email: string | null;
  status: RecordStatus;
  createdAt: string;
}

export interface TaxpayerSearchParams {
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}

export interface AddOwnerRequest {
  taxpayerId: string;
  ownershipTypeId: string;
  ownershipPercentage: number;
  startDate: string;
}

// --- Parcel -----------------------------------------------------------

export interface CreateParcelRequest {
  propertyId: string;
  barangayId: string;
  zoneId?: string | null;
  geometryWkt?: string | null;
  area?: number | null;
  surveyNumber?: string | null;
  lotNumber?: string | null;
  blockNumber?: string | null;
}

export interface ParcelDto {
  id: string;
  propertyId: string;
  barangayId: string;
  barangayName: string;
  zoneId: string | null;
  geometryWkt: string | null;
  /** Declared area (sqm), as entered. */
  area: number | null;
  /** Area (sqm) measured from the geometry by PostGIS; null without geometry. */
  measuredArea: number | null;
  /** How measuredArea was computed, e.g. "GEODESIC_WGS84" or "PROJECTED_EPSG_3123". */
  measuredAreaBasis: string | null;
  surveyNumber: string | null;
  lotNumber: string | null;
  blockNumber: string | null;
  status: RecordStatus;
  createdAt: string;
  /** Concurrency token (PostgreSQL xmin); send back unchanged on updates. */
  version: number;
}

// --- GIS (docs/GIS.md §4) ------------------------------------------

/** Identifiers only — no owner names/TINs/values on the bulk map layer (CLAUDE.md §68). */
export interface ParcelFeatureProperties {
  parcelId: string;
  propertyId: string;
  propertyIdentificationNumber: string;
  lotNumber: string | null;
  blockNumber: string | null;
  surveyNumber: string | null;
  barangayName: string;
}

export interface ParcelFeature {
  type: 'Feature';
  id: string;
  /** GeoJSON geometry in WGS84 (EPSG:4326). */
  geometry: unknown;
  properties: ParcelFeatureProperties;
}

export interface ParcelFeatureCollection {
  type: 'FeatureCollection';
  features: ParcelFeature[];
  /** True when the extent held more parcels than `limit` — zoom in. */
  truncated: boolean;
  limit: number;
}

export type ReferenceLayerName = 'barangays' | 'zones' | 'roads';

export interface ReferenceLayerFeatureProperties {
  /** PSGC code, zone code, or road code. */
  key: string;
  name: string | null;
  effectiveDate: string;
  endDate: string | null;
  source: string;
  sourceReference: string | null;
}

export interface ReferenceLayerFeatureCollection {
  type: 'FeatureCollection';
  asOf: string;
  features: { type: 'Feature'; id: string; geometry: unknown; properties: ReferenceLayerFeatureProperties }[];
  truncated: boolean;
  limit: number;
}

// --- RPU -----------------------------------------------------------

export interface CreateRpuRequest {
  propertyId: string;
  rpuNumber: string;
  rpuType: RpuType;
  effectivityDate: string;
  previousRpuId?: string | null;
}

export interface RpuDto {
  id: string;
  propertyId: string;
  rpuNumber: string;
  rpuType: RpuType;
  status: RecordStatus;
  effectivityDate: string;
  endDate: string | null;
  previousRpuId: string | null;
  createdAt: string;
}

// --- Tax Declaration -----------------------------------------------------------

export interface CreateTaxDeclarationRequest {
  rpuId: string;
  /** Omit to generate from the TD numbering scheme in force. */
  taxDeclarationNumber?: string;
  effectivityDate: string;
  taxability: Taxability;
  classificationId: string;
  actualUseId: string;
  subClassificationId?: string | null;
  assessmentYear: number;
  previousTaxDeclarationId?: string | null;
  remarks?: string | null;
}

export interface TaxDeclarationDto {
  id: string;
  rpuId: string;
  propertyId: string;
  taxDeclarationNumber: string;
  revisionNumber: number;
  effectivityDate: string;
  taxability: Taxability;
  classificationId: string;
  classificationName: string;
  actualUseId: string;
  actualUseName: string;
  subClassificationId: string | null;
  assessmentYear: number;
  status: WorkflowStatus;
  previousTaxDeclarationId: string | null;
  remarks: string | null;
  createdAt: string;
}

// --- Land -----------------------------------------------------------

export interface CreateLandRequest {
  rpuId: string;
  area: number;
  areaUnit?: string | null;
  classificationId: string;
  actualUseId: string;
  subClassificationId?: string | null;
  zoneId?: string | null;
  locationFactor?: number | null;
  roadFrontage?: number | null;
  roadTypeId?: string | null;
  isCornerLot: boolean;
  zoning?: string | null;
}

export interface LandDto {
  id: string;
  rpuId: string;
  propertyId: string;
  area: number;
  areaUnit: string;
  classificationId: string;
  classificationName: string;
  actualUseId: string;
  actualUseName: string;
  subClassificationId: string | null;
  zoneId: string | null;
  locationFactor: number | null;
  roadFrontage: number | null;
  roadTypeId: string | null;
  isCornerLot: boolean;
  zoning: string | null;
  marketValue: number | null;
  assessedValue: number | null;
  status: RecordStatus;
  createdAt: string;
}

// --- Building -----------------------------------------------------------

export interface CreateBuildingRequest {
  rpuId: string;
  buildingTypeId: string;
  structuralTypeId: string;
  actualUseId: string;
  numberOfStoreys?: number | null;
  floorArea: number;
  totalFloorArea: number;
  yearConstructed?: number | null;
  yearCompleted?: number | null;
  conditionId: string;
  completionPercentage?: number | null;
}

export interface BuildingDto {
  id: string;
  rpuId: string;
  propertyId: string;
  buildingTypeId: string;
  buildingTypeName: string;
  structuralTypeId: string;
  structuralTypeName: string;
  actualUseId: string;
  actualUseName: string;
  numberOfStoreys: number;
  floorArea: number;
  totalFloorArea: number;
  yearConstructed: number | null;
  yearCompleted: number | null;
  conditionId: string;
  conditionName: string;
  completionPercentage: number;
  marketValue: number | null;
  depreciation: number | null;
  depreciatedValue: number | null;
  assessedValue: number | null;
  status: RecordStatus;
  createdAt: string;
}

// --- Machinery -----------------------------------------------------------

export interface CreateMachineryRequest {
  rpuId: string;
  machineryTypeId: string;
  description?: string | null;
  brand?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  capacity?: number | null;
  capacityUnit?: string | null;
  dateAcquired?: string | null;
  acquisitionCost: number;
  installationCost?: number | null;
  otherCost?: number | null;
  isBrandNew?: boolean;
  replacementCost?: number | null;
  economicLifeYears?: number | null;
  remainingLifeYears?: number | null;
}

export interface MachineryDto {
  id: string;
  rpuId: string;
  propertyId: string;
  machineryTypeId: string;
  machineryTypeName: string;
  description: string | null;
  brand: string | null;
  model: string | null;
  serialNumber: string | null;
  capacity: number | null;
  capacityUnit: string | null;
  dateAcquired: string | null;
  acquisitionCost: number;
  installationCost: number | null;
  otherCost: number | null;
  isBrandNew: boolean;
  replacementCost: number | null;
  economicLifeYears: number | null;
  remainingLifeYears: number | null;
  depreciation: number | null;
  marketValue: number | null;
  assessedValue: number | null;
  status: RecordStatus;
  createdAt: string;
}

// --- Billing (Phase 8; docs/BILLING.md §4–§6) ---

export type BillingComponent = 'Tax' | 'Discount' | 'Penalty' | 'Interest';

export interface GenerateBillRequest {
  rpuId: string;
  taxYear: number;
  asOfDate: string;
}

export interface TaxBillTaxTypeDto {
  taxTypeId: string;
  taxTypeCode: string;
  taxTypeName: string;
  taxRateId: string;
  ratePercent: number;
  computedAnnualTax: number;
  capRuleId: string | null;
  capBaselineTax: number | null;
  capLimit: number | null;
  annualTax: number;
}

export interface TaxBillDetailDto {
  lineNumber: number;
  installmentSequence: number;
  dueDate: string;
  taxTypeId: string;
  taxTypeCode: string;
  component: BillingComponent;
  ruleId: string;
  ratePercent: number | null;
  baseAmount: number;
  amount: number;
  months: number | null;
  explanation: string;
}

export interface TaxBillDto {
  id: string;
  propertyId: string;
  rpuId: string;
  rpuNumber: string;
  taxDeclarationId: string;
  taxDeclarationNumber: string;
  assessmentId: string;
  billNumber: string | null;
  taxYear: number;
  asOfDate: string;
  rulesAsOfDate: string;
  assessedValue: number;
  classificationId: string;
  discountStackingAllowed: boolean;
  notes: string | null;
  status: WorkflowStatus;
  createdAt: string;
  createdBy: string | null;
  postedAt: string | null;
  postedBy: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  supersededByBillId: string | null;
  total: number;
  taxTypes: TaxBillTaxTypeDto[];
  details: TaxBillDetailDto[];
}

export interface StatementLineDto {
  billId: string;
  rpuId: string;
  rpuNumber: string;
  taxDeclarationNumber: string;
  taxYear: number;
  asOfDate: string;
  assessedValue: number;
  tax: number;
  discount: number;
  penalty: number;
  interest: number;
  total: number;
}

export interface StatementOfAccountDto {
  propertyId: string;
  propertyIdentificationNumber: string;
  generatedAt: string;
  bills: StatementLineDto[];
  totalBilled: number;
}

// --- Forms foundation (docs/FORMS-REVISION-PLAN.md) ---

export type NumberedDocumentKind =
  | 'PropertyIdentificationNumber'
  | 'TaxDeclaration'
  | 'TaxBill'
  | 'Faas'
  | 'NoticeOfAssessment'
  | 'OfficialReceipt';
export type FormAuthority = 'PrimeProvisional' | 'Lam' | 'Blgf' | 'LguOrdinance' | 'Other';
export type FormSubjectType = 'TaxBill' | 'TaxDeclaration';
export type ApprovalSubjectType = 'Assessment';

interface ConfigurationHeader {
  id: string;
  legalBasis: string;
  effectiveDate: string;
  endDate: string | null;
  status: WorkflowStatus;
  createdBy: string | null;
  createdAt: string;
  approvedBy: string | null;
  approvedAt: string | null;
  remarks: string | null;
}

export interface NumberingSchemeDto extends ConfigurationHeader {
  appliesTo: NumberedDocumentKind;
  name: string;
  pattern: string;
  validationRegex: string | null;
  allowManualEntry: boolean;
  example: string;
}

export interface CreateNumberingSchemeRequest {
  legalBasis: string;
  effectiveDate: string;
  remarks?: string;
  appliesTo: NumberedDocumentKind;
  name: string;
  pattern: string;
  validationRegex?: string;
  allowManualEntry: boolean;
}

export interface FormDefinitionDto extends ConfigurationHeader {
  code: string;
  version: number;
  title: string;
  subjectType: FormSubjectType;
  authority: FormAuthority;
  sourceReference: string | null;
  templateBody: string | null;
}

export interface CreateFormDefinitionRequest {
  legalBasis: string;
  effectiveDate: string;
  remarks?: string;
  code: string;
  title: string;
  subjectType: FormSubjectType;
  authority: FormAuthority;
  sourceReference?: string;
  templateBody: string;
}

export interface ApprovalStepDto {
  sequence: number;
  stepCode: string;
  label: string;
  signatoryPosition: string | null;
}

export interface ApprovalChainDto extends ConfigurationHeader {
  subjectType: ApprovalSubjectType;
  name: string;
  steps: ApprovalStepDto[];
}

export interface CreateApprovalChainRequest {
  legalBasis: string;
  effectiveDate: string;
  remarks?: string;
  subjectType: ApprovalSubjectType;
  name: string;
  steps: { sequence: number; stepCode: string; label: string; signatoryPosition?: string }[];
}

export interface FormPreviewDto {
  formCode: string;
  formVersion: number;
  title: string;
  authority: FormAuthority;
  issueBlocker: string | null;
  html: string;
}

export interface IssuedFormDto {
  id: string;
  formCode: string;
  formVersion: number;
  title: string;
  authority: FormAuthority;
  subjectType: FormSubjectType;
  subjectId: string;
  documentNumber: string | null;
  status: WorkflowStatus;
  issuedAt: string;
  issuedBy: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  renderedHtmlSha256: string;
  html: string | null;
}
