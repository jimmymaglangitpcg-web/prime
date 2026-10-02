// Mirrors Prime.Application's DTOs (src/Prime.Application/Features/**/Dtos.cs).
// Kept hand-written and in sync manually — see docs/ARCHITECTURE.md §5 open
// decision #2 (no codegen tool adopted yet).

export type RecordStatus = 'Active' | 'Inactive' | 'Cancelled' | 'Subdivided' | 'Consolidated' | 'Superseded';
export type WorkflowStatus = 'Draft' | 'Submitted' | 'PendingReview' | 'Approved' | 'Rejected' | 'Posted' | 'Cancelled' | 'Voided';
export type TaxpayerType = 'Individual' | 'Corporation' | 'Partnership' | 'Government' | 'Estate' | 'Association' | 'Other';
export type RpuType = 'Land' | 'Building' | 'Machinery' | 'OtherImprovement' | 'MineralRight';
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
  /** The cadastral lot number printed on the LAM TMCR (records-and-forms.md Q16). */
  cadastralNumber?: string | null;
  status: RecordStatus;
  createdAt: string;
  /** Descriptive fields (docs/analysis/mrpaao-forms-model.md §10). */
  titleTypeId: string | null;
  titleTypeName: string | null;
  titleDate: string | null;
  boundaryNorth: string | null;
  boundaryEast: string | null;
  boundarySouth: string | null;
  boundaryWest: string | null;
}

/** Capacity in which property is declared (LGC §§204–205). */
export type PropertyPartyRole = 'Owner' | 'Administrator' | 'LegalInterestHolder' | 'BeneficialUser' | 'Claimant' | 'UnknownOwner';

export const partyRoleLabel: Record<PropertyPartyRole, string> = {
  Owner: 'Owner',
  Administrator: 'Administrator',
  LegalInterestHolder: 'Person with legal interest',
  BeneficialUser: 'Beneficial user',
  Claimant: 'Claimant',
  UnknownOwner: 'Unknown owner',
};

export interface PropertyOwnerDto {
  propertyTaxpayerId: string;
  /** Null only for an unknown owner. */
  taxpayerId: string | null;
  taxpayerDisplayName: string;
  /** Owners only. */
  ownershipTypeName: string | null;
  ownershipPercentage: number;
  startDate: string;
  endDate: string | null;
  isCurrent: boolean;
  role: PropertyPartyRole;
  endReason: string | null;
  address: string | null;
  /** Set when the party holds one unit only (e.g. a building owned apart from the land). */
  rpuId: string | null;
  rpuNumber: string | null;
}

export interface ParcelSummaryDto {
  id: string;
  area: number | null;
  lotNumber: string | null;
  barangayName: string;
  status: RecordStatus;
  barangayId: string;
  sectionIndex: string | null;
  parcelNumber: number | null;
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
  sex?: Sex | null;
}

/** Corrects a taxpayer's contact and personal details; the reason goes to the audit log (records-and-forms.md §4.1). */
export interface UpdateTaxpayerDetailsRequest {
  tin: string | null; address: string | null; contactNumber: string | null; email: string | null; sex: Sex | null; reason: string;
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
  /** Not a party to a property in your office's jurisdiction: name and TIN only (docs/analysis/province-wide-operation.md Q5). */
  limited?: boolean;
  /** Individuals only; printed only when Forms:PrintOwnerSex is on (records-and-forms.md Q3). */
  sex?: Sex | null;
}

export type Sex = 'Male' | 'Female';

export interface TaxpayerSearchParams {
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}

export interface AddOwnerRequest {
  role: PropertyPartyRole;
  /** Omitted for an unknown owner. */
  taxpayerId?: string;
  /** Owners only. */
  ownershipTypeId?: string;
  ownershipPercentage: number;
  startDate: string;
  /** A party of one building/machinery/other-improvement unit; omitted for the whole property. */
  rpuId?: string;
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
  /** Overrides the property's cadastral number (records-and-forms.md Q16). */
  cadastralNumber?: string | null;
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
  cadastralNumber?: string | null;
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
  /** The tax map section and parcel number once the parcel is placed in a section (step 10a-2). */
  sectionIndexNumber: string | null;
  parcelNumber: number | null;
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

export type ReferenceLayerName = 'barangays' | 'zones' | 'roads' | 'sections';

export interface ReferenceLayerFeatureProperties {
  /** PSGC code, zone code, road code, or "psgcCode/section". */
  key: string;
  name: string | null;
  effectiveDate: string;
  endDate: string | null;
  source: string;
  sourceReference: string | null;
}

/** The tax map book's sheets (MRPAAO Ch. II §2 C.4; docs/analysis/property-identification.md §3.7). */
export type TaxMapSheetKind = 'TaxMap' | 'SectionIndex' | 'BarangayIndex';
export type TaxMapSheetRoute = 'tax-map' | 'section-index' | 'barangay-index';

export interface TaxMapSheetDto {
  kind: TaxMapSheetKind;
  title: string;
  asOf: string;
  /** Province or city, municipality or district, barangay, section — with their index numbers. */
  heading: { label: string; name: string | null; indexNumber: string | null }[];
  /** WGS84 [minLon, minLat, maxLon, maxLat]; null when nothing on the sheet has a shape. */
  extent: [number, number, number, number] | null;
  features: {
    type: 'Feature'; id: string; geometry: unknown;
    /** role "area": the sheet's own boundary; "unit": a labelled section or barangay. */
    properties: { label: string; name: string | null; role: 'area' | 'unit' | 'disputed'; source: string; sourceReference: string | null };
  }[];
  /** What the sheet cannot show (no boundary, no index number). */
  missing: string[];
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
  /** The land unit a building, machinery or other improvement stands on. */
  landRpuId?: string | null;
  /** The building a machinery unit is installed in, or the leasing property a unit is part of. */
  hostRpuId?: string | null;
  /** The LAM's structured unit PIN (identification-numbering.md §4.2). */
  isLeasingProperty?: boolean;
  floorPrefix?: string | null;
  floorNumber?: number | null;
  unitNumber?: number | null;
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
  /** PIN postscript: buildings 1001…, machinery 2001… (MRPAAO p.42). */
  pinSuffix: number | null;
  /** Property PIN plus the unit's parts (building, leasing property, floor, unit, machine), per the configured parenthesis convention. */
  unitPin: string;
  ownedSeparately: boolean;
  landRpuId: string | null;
  hostRpuId: string | null;
  isLeasingProperty: boolean;
  floorPrefix: string | null;
  floorNumber: number | null;
  unitNumber: number | null;
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
  /** Draft the TD under this property transaction (approved with it). */
  propertyTransactionId?: string;
  /** A transaction code in force (catalogue), when the TD is not drafted under a transaction. */
  transactionCode?: string;
  /** The posted assessment this TD declares (FAAS = TD + assessment; docs/analysis/mrpaao-forms-model.md §6.1). */
  assessmentId?: string | null;
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
  createdBy: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  supersededByTaxDeclarationId: string | null;
  activeAnnotationCount: number;
  /** Set when the TD was drafted under a property transaction; it is approved with the transaction. */
  propertyTransactionId: string | null;
  /** The assessment this TD declares; the TD with it is the FAAS. */
  assessmentId: string | null;
  /** Null until the TD declares an assessment. */
  faasNumber: string | null;
  /** FAAS transaction code (MRPAAO p.145) and its rank; the highest rank wins when several apply. */
  transactionCode: string | null;
  transactionRank: number | null;
}

export interface TaxDeclarationAnnotationDto {
  id: string;
  taxDeclarationId: string;
  annotationTypeId: string;
  annotationTypeCode: string;
  annotationTypeName: string;
  text: string;
  referenceNumber: string | null;
  referenceDate: string | null;
  effectiveDate: string;
  createdAt: string;
  createdBy: string | null;
  liftedAt: string | null;
  liftedBy: string | null;
  liftReason: string | null;
  liftReference: string | null;
  /** Copied from the replaced TD's annotation when this TD was approved (records-and-forms.md §4.4). */
  carriedFromAnnotationId: string | null;
  carriedFromTaxDeclarationNumber: string | null;
}

export interface AddTaxDeclarationAnnotationRequest {
  annotationTypeId: string;
  text: string;
  referenceNumber?: string;
  referenceDate?: string;
  effectiveDate: string;
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
  /** Appraisal rows (docs/analysis/mrpaao-forms-model.md §8.3). */
  strips: LandStripDto[];
  improvements: LandImprovementDto[];
  adjustments: LandAdjustmentDto[];
  /** What distance and depth factors read (valuation-foundation.md §4.4). */
  distanceToAllWeatherRoadKm: number | null;
  distanceToPoblacionKm: number | null;
  isSubdivisionLot: boolean;
}

export interface UpdateLandAppraisalInputsRequest {
  roadTypeId: string | null; roadFrontage: number | null; isCornerLot: boolean; distanceToAllWeatherRoadKm: number | null;
  distanceToPoblacionKm: number | null; isSubdivisionLot: boolean; reason: string;
}

export interface LandStripDto {
  id: string; sequence: number; classificationId: string; classificationName: string; subClassificationId: string | null;
  subClassificationName: string | null; actualUseId: string; actualUseName: string; zoneId: string | null; zoneName: string | null; area: number;
  /** The class and sub-class that price the strip when they differ from its own (valuation-foundation.md §4.3). */
  valuationClassificationId: string | null; valuationClassificationName: string | null;
  valuationSubClassificationId: string | null; valuationSubClassificationName: string | null;
  depthBand: number | null;
}

export interface LandImprovementDto {
  id: string; sequence: number; improvementKindId: string; improvementKindName: string; quantity: number; isProductive: boolean | null;
  classificationId: string | null; classificationName: string | null; actualUseId: string | null; actualUseName: string | null; description: string | null;
  /** The other-improvement unit that owns it apart from the land; null: the land's own. */
  separateRpuId: string | null; separateRpuNumber: string | null;
}

export interface LandAdjustmentDto { id: string; factorCode: string; landStripId: string | null; stripSequence: number | null; remarks: string | null }

export interface AddLandStripRequest {
  classificationId: string; subClassificationId: string | null; actualUseId: string; zoneId: string | null; area: number;
  valuationClassificationId?: string | null; valuationSubClassificationId?: string | null; depthBand?: number | null;
}

export interface AddLandImprovementRequest {
  improvementKindId: string; quantity: number; isProductive: boolean | null; classificationId: string | null; actualUseId: string | null; description: string | null;
  separateRpuId?: string | null;
}

export interface AddLandAdjustmentRequest { factorCode: string; landStripId: string | null; remarks: string | null }

/** A market value adjustment factor of an SMV ordinance (LGU data). */
export interface AdjustmentFactorDto {
  id: string; smvId: string; smvOrdinanceNumber: string; code: string; name: string; percent: number; classificationId: string | null;
  classificationName: string | null; description: string | null; legalBasis: string; effectiveDate: string; endDate: string | null;
  status: WorkflowStatus; createdAt: string;
  ruleKind: AdjustmentRuleKind; distanceReference: DistanceReference | null; standardDepth: number | null; rows: AdjustmentFactorRowDto[];
}

/** How a factor finds its percentage for a land (valuation-foundation.md §4.4). */
export type AdjustmentRuleKind = 'Flat' | 'ByRoadType' | 'ByDistance' | 'Corner' | 'Depth';
export type DistanceReference = 'AllWeatherRoad' | 'Poblacion';
export const adjustmentRuleKinds: { value: AdjustmentRuleKind; label: string }[] = [
  { value: 'Flat', label: 'Flat percent (chosen by the appraiser)' },
  { value: 'ByRoadType', label: 'By kind of road' },
  { value: 'ByDistance', label: 'By distance' },
  { value: 'Corner', label: 'Corner lot' },
  { value: 'Depth', label: 'By depth band' },
];
export const distanceReferences: { value: DistanceReference; label: string }[] = [
  { value: 'AllWeatherRoad', label: 'to an all-weather road' },
  { value: 'Poblacion', label: 'to the poblacion' },
];

/** The SMV's building tables (valuation-foundation.md §4.5); every cost and percent is LGU data. */
export interface BuildingCostDto {
  id: string; smvId: string; smvReference: string; structuralTypeId: string; structuralTypeName: string; buildingTypeId: string | null;
  buildingTypeName: string | null; classificationId: string | null; classificationName: string | null; costPerSquareMetre: number;
  legalBasis: string; effectiveDate: string; endDate: string | null; status: WorkflowStatus;
}
export interface CreateBuildingCostRequest {
  smvId: string; structuralTypeId: string; buildingTypeId: string | null; classificationId: string | null; costPerSquareMetre: number;
  legalBasis: string; effectiveDate: string; remarks: string | null;
}
export interface ExtraItemCostDto {
  id: string; smvId: string; smvReference: string; componentTypeId: string; componentTypeName: string; unit: string; unitCost: number;
  legalBasis: string; effectiveDate: string; endDate: string | null; status: WorkflowStatus;
}
export interface CreateExtraItemCostRequest {
  smvId: string; componentTypeId: string; unit: string; unitCost: number; legalBasis: string; effectiveDate: string; remarks: string | null;
}
/** How an SMV's depreciation table reads (Q8). */
export type DepreciationReading = 'Cumulative' | 'YearlyWithinBand';
export const depreciationReadings: { value: DepreciationReading; label: string }[] = [
  { value: 'Cumulative', label: 'Total percent for an age in the band' },
  { value: 'YearlyWithinBand', label: 'Percent per year within the band' },
];
export interface DepreciationRowDto { sequence: number; fromAge: number; toAge: number | null; percent: number }
export interface DepreciationScheduleDto {
  id: string; smvId: string; smvReference: string; structuralTypeId: string; structuralTypeName: string; reading: DepreciationReading;
  minimumRemainingPercent: number; rows: DepreciationRowDto[]; legalBasis: string; effectiveDate: string; endDate: string | null; status: WorkflowStatus;
}
export interface CreateDepreciationScheduleRequest {
  smvId: string; structuralTypeId: string; reading: DepreciationReading; minimumRemainingPercent: number;
  rows: { fromAge: number; toAge: number | null; percent: number }[]; legalBasis: string; effectiveDate: string; remarks: string | null;
}

export interface AdjustmentFactorRowDto {
  sequence: number; roadTypeId: string | null; roadTypeName: string | null; overValue: number | null; upToValue: number | null; depthBand: number | null; percent: number;
}

export interface AdjustmentFactorRowRequest { roadTypeId: string | null; overValue: number | null; upToValue: number | null; depthBand: number | null; percent: number }

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
  /** Mixed use: floor area per classification and use (docs/analysis/mrpaao-forms-model.md §8.3). */
  usePortions: BuildingUsePortionDto[];
  components: BuildingComponentDto[];
  buildingPermitNumber: string | null;
  buildingPermitDate: string | null;
  condominiumCertificateNumber: string | null;
  certificateOfCompletionDate: string | null;
  certificateOfOccupancyDate: string | null;
  dateConstructed: string | null;
  dateOccupied: string | null;
  floors: { id: string; floorNumber: number; area: number }[] | null;
  materials: { id: string; structuralPartId: string; structuralPartName: string; structuralMaterialId: string | null; materialName: string; floorNumber: number | null }[] | null;
}

export interface BuildingUsePortionDto {
  id: string; sequence: number; classificationId: string; classificationName: string; actualUseId: string; actualUseName: string; floorArea: number;
}

export interface BuildingComponentDto {
  id: string; componentTypeId: string; componentTypeName: string; description: string | null; quantity: number | null; unitCost: number | null;
  cost: number | null; isAdditionalItem: boolean; buildingUsePortionId: string | null;
}

export interface AddBuildingUsePortionRequest { classificationId: string; actualUseId: string; floorArea: number }

export interface AddBuildingComponentRequest {
  componentTypeId: string; description: string | null; quantity: number | null; unitCost: number | null; cost: number | null;
  isAdditionalItem: boolean; buildingUsePortionId: string | null;
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
  /** Null: the unit's Tax Declaration's. */
  classificationId?: string | null;
  actualUseId?: string | null;
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
  classificationId: string | null;
  classificationName: string | null;
  actualUseId: string | null;
  actualUseName: string | null;
  yearInstalled: number | null;
  yearOfInitialOperation: number | null;
  conversionFactor: number | null;
  /** Derived replacement cost (valuation-foundation.md §4.6). */
  isImported: boolean;
  acquisitionCurrency: string | null;
  foreignAcquisitionCost: number | null;
  originCountry: string | null;
  priceIndexSeries: string | null;
  dateInstalled: string | null;
  isInOperation: boolean;
  costItems: MachineryCostItemDto[];
  /** Acquisition documents of the LAM machinery FAAS (records-and-forms.md Q14). */
  documents?: MachineryDocumentsDto | null;
}

export interface MachineryDocumentsDto {
  engineeringRegistrationNumber: string | null; engineeringRegistrationDate: string | null;
  importPermitNumber: string | null; importPermitDate: string | null;
  supplierName: string | null; supplierAddress: string | null;
  receiptNumber: string | null; receiptDate: string | null;
}

/** An item of a machine's acquisition cost (LAM Bk III p.75); freight and insurance are part of the cost, insurance and freight. */
export type MachineryCostItemKind =
  'Freight' | 'Insurance' | 'BankCharges' | 'Brokerage' | 'Arrastre' | 'Duties' | 'InlandTransport' | 'Installation' | 'Other';
export const machineryCostItemKinds: { value: MachineryCostItemKind; label: string }[] = [
  { value: 'Freight', label: 'Freight' }, { value: 'Insurance', label: 'Insurance' }, { value: 'BankCharges', label: 'Bank charges' },
  { value: 'Brokerage', label: 'Brokerage' }, { value: 'Arrastre', label: 'Arrastre' }, { value: 'Duties', label: 'Duties and taxes' },
  { value: 'InlandTransport', label: 'Inland transport' }, { value: 'Installation', label: 'Installation' }, { value: 'Other', label: 'Other' },
];
export interface MachineryCostItemDto { sequence: number; kind: MachineryCostItemKind; amount: number; description: string | null }
export interface MachineryCostItemRequest { kind: MachineryCostItemKind; amount: number; description: string | null }
export interface UpdateMachineryValuationInputsRequest {
  isImported: boolean; acquisitionCurrency: string | null; foreignAcquisitionCost: number | null; originCountry: string | null;
  priceIndexSeries: string | null; dateInstalled: string | null; isInOperation: boolean; costItems: MachineryCostItemRequest[]; reason: string;
}

/** Exchange rates and price indices for machinery (valuation-foundation.md §4.6); source data, approved by a second user. */
export interface ExchangeRateDto {
  id: string; currency: string; rateDate: string; pesosPerUnit: number; source: string; status: WorkflowStatus; remarks: string | null;
}
export interface CreateExchangeRateRequest { currency: string; rateDate: string; pesosPerUnit: number; source: string; remarks: string | null }
export interface PriceIndexDto { id: string; series: string; year: number; value: number; source: string; status: WorkflowStatus; remarks: string | null }
export interface CreatePriceIndexRequest { series: string; year: number; value: number; source: string; remarks: string | null }

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
  /** The tax each assessment line bears, at its classification's rate. */
  lines: { classificationId: string | null; assessedValue: number; taxRateId: string; ratePercent: number; tax: number }[];
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
  principalOwed: number;
  principalPaid: number;
  outstandingPrincipal: number;
  /** Settling what is outstanding on the statement's as-of date. */
  dueAsOf: number;
}

export interface StatementPaymentDto {
  paymentId: string;
  officialReceiptNumber: string;
  paymentDate: string;
  payorName: string;
  amount: number;
  status: PaymentStatus;
}

export interface StatementOfAccountDto {
  propertyId: string;
  propertyIdentificationNumber: string;
  generatedAt: string;
  asOfDate: string;
  bills: StatementLineDto[];
  totalBilled: number;
  totalPrincipalPaid: number;
  totalOutstandingPrincipal: number;
  totalDueAsOf: number;
  payments: StatementPaymentDto[];
}

// --- Collection (docs/analysis/collection.md) ---

export type PaymentStatus = 'Posted' | 'Voided' | 'Reversed';
export type CollectionYearCategory = 'Current' | 'Prior' | 'Advance';
export type PaymentCancellationStatus = 'Pending' | 'Approved' | 'Rejected';
export type PaymentCancellationKind = 'Void' | 'Reversal';

export interface OutstandingInstallmentDto {
  installmentSequence: number;
  dueDate: string;
  principalOwed: number;
  principalPaid: number;
  outstanding: number;
  dueIfPaidAsOf: number | null;
  overpaid: boolean;
}

export interface OutstandingBillDto {
  billId: string;
  billNumber: string | null;
  rpuId: string;
  rpuNumber: string;
  taxDeclarationNumber: string;
  taxYear: number;
  installments: OutstandingInstallmentDto[];
}

export interface OutstandingDto {
  propertyId: string;
  asOfDate: string;
  bills: OutstandingBillDto[];
  totalOutstandingPrincipal: number;
  totalDueAsOf: number;
}

export interface PaymentItemRequest {
  rpuId: string;
  taxYear: number;
  installmentSequence: number;
  principalAmount?: number | null;
}

export interface PaymentTenderRequest {
  paymentModeId: string;
  amount: number;
  reference?: string | null;
  bank?: string | null;
  checkDate?: string | null;
}

export interface PostPaymentRequest {
  idempotencyKey: string;
  payorTaxpayerId: string | null;
  payorName: string;
  payorAddress: string | null;
  items: PaymentItemRequest[];
  tenders: PaymentTenderRequest[];
  expectedTotal: number;
  officialReceiptNumber?: string | null;
  remarks?: string | null;
}

export interface PaymentAllocationDto {
  lineNumber: number;
  billId: string;
  billNumber: string | null;
  taxDeclarationNumber: string;
  propertyId: string;
  rpuId: string;
  rpuNumber: string;
  taxYear: number;
  installmentSequence: number;
  dueDate: string;
  taxTypeId: string;
  taxTypeCode: string;
  taxTypeName: string;
  component: BillingComponent;
  ruleId: string;
  ratePercent: number | null;
  baseAmount: number;
  amount: number;
  months: number | null;
  yearCategory: CollectionYearCategory;
  explanation: string;
  accountCode: string;
  accountName: string;
  fund: string | null;
}

export interface PaymentQuoteDto {
  paymentDate: string;
  allocations: PaymentAllocationDto[];
  total: number;
}

export interface PaymentTenderDto {
  paymentModeId: string;
  modeCode: string;
  modeName: string;
  amount: number;
  reference: string | null;
  bank: string | null;
  checkDate: string | null;
}

export interface PaymentReplacementRequest {
  payorTaxpayerId: string | null;
  payorName: string;
  payorAddress: string | null;
  items: PaymentItemRequest[];
  tenders: PaymentTenderRequest[];
  expectedTotal: number;
  officialReceiptNumber?: string | null;
  remarks?: string | null;
}

export interface PaymentCancellationDto {
  id: string;
  paymentId: string;
  paymentOfficialReceiptNumber: string;
  paymentTransactionNumber: string;
  paymentDate: string;
  paymentAmount: number;
  payorName: string;
  reason: string;
  isCorrection: boolean;
  replacement: PaymentReplacementRequest | null;
  status: PaymentCancellationStatus;
  kind: PaymentCancellationKind | null;
  requestedBy: string | null;
  requestedAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionRemarks: string | null;
  transactionNumber: string | null;
  replacementPaymentId: string | null;
}

export interface PaymentDto {
  id: string;
  transactionNumber: string;
  officialReceiptNumber: string;
  payorTaxpayerId: string | null;
  payorName: string;
  payorAddress: string | null;
  paymentDate: string;
  receivedAt: string;
  office: string | null;
  locationCode: string | null;
  cashierUserId: string | null;
  amountDue: number;
  amountTendered: number;
  change: number;
  status: PaymentStatus;
  cancelledAt: string | null;
  replacesPaymentId: string | null;
  replacedByPaymentId: string | null;
  remarks: string | null;
  tenders: PaymentTenderDto[];
  allocations: PaymentAllocationDto[];
  cancellations: PaymentCancellationDto[];
}

export interface PaymentSummaryDto {
  id: string;
  transactionNumber: string;
  officialReceiptNumber: string;
  payorName: string;
  paymentDate: string;
  receivedAt: string;
  cashierUserId: string | null;
  amountDue: number;
  status: PaymentStatus;
}

export interface PaymentModeDto {
  id: string;
  code: string;
  name: string;
  description: string | null;
  requiresReference: boolean;
  allowsChange: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface CreatePaymentModeRequest {
  code: string;
  name: string;
  description: string | null;
  requiresReference: boolean;
  allowsChange: boolean;
  sortOrder: number;
}

export interface RevenueAccountMappingDto {
  id: string;
  taxTypeId: string;
  taxTypeCode: string;
  component: BillingComponent;
  yearCategory: CollectionYearCategory;
  accountCode: string;
  accountName: string;
  fund: string | null;
  legalBasis: string;
  effectiveDate: string;
  endDate: string | null;
  status: WorkflowStatus;
  createdBy: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  remarks: string | null;
}

export type RemittanceStatus = 'Submitted' | 'Accepted' | 'Returned';
export type CollectionGroupBy = 'Date' | 'Cashier' | 'Mode' | 'TaxType' | 'TaxYear' | 'YearCategory' | 'Fund' | 'Account' | 'Barangay';

export interface RemittanceDto {
  id: string;
  remittanceNumber: string | null;
  cashierUserId: string;
  cashierName: string | null;
  collectionDate: string;
  status: RemittanceStatus;
  paymentCount: number;
  totalAmount: number;
  remarks: string | null;
  submittedAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionRemarks: string | null;
  modeTotals: { paymentModeId: string; modeCode: string; modeName: string; amount: number }[];
  accountTotals: { accountCode: string; accountName: string; fund: string | null; amount: number }[];
  items: { paymentId: string; officialReceiptNumber: string; payorName: string; amount: number; paymentStatus: PaymentStatus }[];
}

export interface CollectionSummaryRowDto {
  key: string;
  label: string;
  receipts: number;
  collected: number;
  reversed: number;
  net: number;
}

export interface CollectionSummaryDto {
  from: string;
  to: string;
  groupBy: CollectionGroupBy;
  rows: CollectionSummaryRowDto[];
  totalCollected: number;
  totalReversed: number;
  net: number;
}

export interface ReconciliationRowDto {
  cashierUserId: string | null;
  cashierName: string | null;
  receipts: number;
  amountDue: number;
  allocationTotal: number;
  tenderedLessChange: number;
  remitted: number;
  unremitted: number;
  unremittedReceipts: number;
  voidedReceipts: number;
  problems: string[];
}

export interface ReconciliationDto {
  date: string;
  cashiers: ReconciliationRowDto[];
  balanced: boolean;
}

export interface CreateRevenueAccountMappingRequest {
  taxTypeId: string;
  component: BillingComponent;
  yearCategory: CollectionYearCategory;
  accountCode: string;
  accountName: string;
  fund: string | null;
  legalBasis: string;
  effectiveDate: string;
  remarks: string | null;
}

// --- Forms foundation (docs/FORMS-REVISION-PLAN.md) ---

export type NumberedDocumentKind =
  | 'PropertyIdentificationNumber'
  | 'TaxDeclaration'
  | 'TaxBill'
  | 'Faas'
  | 'NoticeOfAssessment'
  | 'OfficialReceipt'
  | 'PropertyTransaction'
  | 'SwornStatement'
  | 'PaymentTransaction'
  | 'Remittance'
  | 'TemporaryPin';
export type FormAuthority = 'PrimeProvisional' | 'Lam' | 'Blgf' | 'LguOrdinance' | 'Other' | 'Mrpaao';
export type FormSubjectType = 'TaxBill' | 'TaxDeclaration' | 'NoticeOfAssessment' | 'Assessment' | 'StatementOfAccount' | 'Faas' | 'Register' | 'SwornStatement' | 'Payment';
export type ApprovalSubjectType = 'Assessment' | 'TaxDeclaration' | 'PropertyTransaction';

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

/** Whose staff signs a step (docs/analysis/province-wide-operation.md §3.4). */
export type ApprovalSigner = 'Any' | 'PreparingOffice' | 'ProvincialOffice';

export interface ApprovalStepDto {
  sequence: number;
  stepCode: string;
  label: string;
  signatoryPosition: string | null;
  signerOffice: ApprovalSigner;
  requiredRole: string | null;
  isFinalApproval: boolean;
  /** Warn when the signer has no valid REA licence (records-and-forms.md Q10). */
  requiresLicensedSignatory?: boolean;
}

export interface ApprovalChainDto extends ConfigurationHeader {
  subjectType: ApprovalSubjectType;
  name: string;
  steps: ApprovalStepDto[];
  /** Null for the provincial default. */
  officeId: string | null;
  officeCode: string | null;
}

export interface CreateApprovalChainRequest {
  legalBasis: string;
  effectiveDate: string;
  remarks?: string;
  subjectType: ApprovalSubjectType;
  name: string;
  officeId?: string | null;
  steps: {
    sequence: number; stepCode: string; label: string; signatoryPosition?: string;
    signerOffice?: ApprovalSigner; requiredRole?: string | null; isFinalApproval?: boolean; requiresLicensedSignatory?: boolean;
  }[];
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

// --- Property transactions (CLAUDE.md §34–§37; docs/FORMS-REVISION-PLAN.md A5) ---

export type PropertyTransactionKind =
  | 'NewDiscovery' | 'NewAssessment' | 'Transfer' | 'Subdivision' | 'Consolidation' | 'Reclassification'
  | 'Reassessment' | 'GeneralRevision' | 'Cancellation' | 'Correction' | 'AdditionOfImprovement' | 'RemovalOfImprovement';

export const transactionKinds: PropertyTransactionKind[] = [
  'NewDiscovery', 'NewAssessment', 'Transfer', 'Subdivision', 'Consolidation', 'Reclassification',
  'Reassessment', 'GeneralRevision', 'Cancellation', 'Correction', 'AdditionOfImprovement', 'RemovalOfImprovement',
];

export interface TransactionRequirementDto {
  sequence: number;
  code: string;
  label: string;
  isMandatory: boolean;
  legalBasis: string | null;
}

export interface TransactionTypeDto {
  id: string;
  code: string;
  name: string;
  kind: PropertyTransactionKind;
  rank: number | null;
  description: string | null;
  requirements: TransactionRequirementDto[];
  legalBasis: string;
  effectiveDate: string;
  endDate: string | null;
  status: WorkflowStatus;
  createdBy: string | null;
  createdAt: string;
  approvedBy: string | null;
  approvedAt: string | null;
  remarks: string | null;
  effectivityRule: EffectivityRule | null;
  effectivityLegalBasis: string | null;
  causeWindowDays: number | null;
  /** A building valued for this transaction takes a new depreciation (valuation-foundation.md §4.5). */
  allowsNewDepreciation: boolean;
}

/** How an assessment finds its effectivity date (docs/analysis/valuation-foundation.md §4.2). */
export type EffectivityRule = 'NextJanuary' | 'NextQuarter' | 'Periods' | 'Fixed';
export const effectivityRules: { value: EffectivityRule; label: string }[] = [
  { value: 'NextJanuary', label: 'Next 1 January' },
  { value: 'NextQuarter', label: 'Next quarter (reassessment)' },
  { value: 'Periods', label: 'By period (back taxes)' },
  { value: 'Fixed', label: 'Fixed by the process' },
];

export interface CreateTransactionTypeRequest {
  legalBasis: string;
  effectiveDate: string;
  remarks?: string;
  code: string;
  name: string;
  kind: PropertyTransactionKind;
  rank?: number;
  description?: string;
  requirements: { sequence: number; code: string; label: string; isMandatory: boolean; legalBasis?: string }[];
  effectivityRule?: EffectivityRule | null;
  effectivityLegalBasis?: string | null;
  causeWindowDays?: number | null;
  allowsNewDepreciation?: boolean;
}

export interface NewPartyRequest {
  role: PropertyPartyRole;
  taxpayerId?: string;
  ownershipTypeId?: string;
  ownershipPercentage: number;
}

export interface OpenTransactionRequest {
  transactionTypeId: string;
  propertyId: string;
  effectiveDate: string;
  description: string;
  newParties?: NewPartyRequest[];
  cancelTaxDeclarationIds?: string[];
  /** A transfer of one unit only (not land); omitted for the whole property. */
  transferRpuId?: string;
  /** A subdivision's resulting lots (Result) or a consolidation's sources (Source). */
  relatedProperties?: { propertyId: string; role: 'Source' | 'Result' }[];
}

export interface TransactionRequirementStatusDto extends TransactionRequirementDto {
  id: string;
  satisfiedAt: string | null;
  satisfiedBy: string | null;
  evidenceReference: string | null;
  note: string | null;
}

export interface TransactionTdDto {
  taxDeclarationId: string;
  taxDeclarationNumber: string;
  propertyId: string;
  status: WorkflowStatus;
}

export interface PropertyTransactionDto {
  id: string;
  transactionNumber: string | null;
  transactionTypeId: string;
  typeCode: string;
  typeName: string;
  kind: PropertyTransactionKind;
  propertyId: string;
  propertyIdentificationNumber: string;
  effectiveDate: string;
  description: string;
  status: WorkflowStatus;
  createdAt: string;
  createdBy: string | null;
  submittedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  closedAt: string | null;
  closeReason: string | null;
  requirements: TransactionRequirementStatusDto[];
  newParties: { id: string; role: PropertyPartyRole; taxpayerId: string | null; name: string; ownershipTypeId: string | null; ownershipPercentage: number }[];
  issuedTaxDeclarations: TransactionTdDto[];
  cancelledTaxDeclarations: TransactionTdDto[];
  relatedProperties: { propertyId: string; propertyIdentificationNumber: string; role: 'Source' | 'Result' }[];
  transferRpuId: string | null;
  taxClearance: TransferTaxClearanceDto | null;
}

// --- Notices of Assessment (LGC §§223, 226; docs/FORMS-REVISION-PLAN.md A6) ---

export type NoticeReason = 'FirstAssessment' | 'AssessmentIncreased' | 'AssessmentDecreased'
  | 'DeclaredOwnerChanged' | 'OwnerAddressChanged' | 'LocationChanged';
export type NoticeStatus = 'Draft' | 'Issued' | 'Served' | 'Cancelled';
/** The modes of LGC §223, and email to the declarant's address (LAM Annex I-L; records-and-forms.md Q11). */
export type NoticeServiceMode = 'Personal' | 'RegisteredMail' | 'ThroughPunongBarangay' | 'Email';

export const noticeReasonLabel: Record<NoticeReason, string> = {
  FirstAssessment: 'First assessment',
  AssessmentIncreased: 'Increased',
  AssessmentDecreased: 'Decreased',
  DeclaredOwnerChanged: 'Declared owner changed',
  OwnerAddressChanged: "Owner's address changed",
  LocationChanged: 'Location changed',
};

/** The MRPAAO's reasons for a notice whose value may be unchanged (p.168). */
export const descriptiveNoticeReasons: NoticeReason[] = ['DeclaredOwnerChanged', 'OwnerAddressChanged', 'LocationChanged'];

export interface NoticeItemDto {
  sequence: number; propertyId: string; rpuId: string; assessmentId: string; taxDeclarationId: string | null; reason: NoticeReason;
  previousAssessedValue: number | null; assessedValue: number; marketValue: number; assessmentYear: number; assessmentEffectiveDate: string;
}

export interface NoticeCandidateDto {
  assessmentId: string; propertyId: string; pin: string; rpuId: string; rpuNumber: string; assessmentYear: number; assessedValue: number; reason: NoticeReason;
}

export const serviceModeLabel: Record<NoticeServiceMode, string> = {
  Personal: 'Personal delivery',
  RegisteredMail: 'Registered mail',
  ThroughPunongBarangay: 'Through the punong barangay',
  Email: 'By email',
};

export interface NoticeDto {
  id: string;
  noticeNumber: string | null;
  propertyId: string;
  rpuId: string;
  assessmentId: string;
  taxDeclarationId: string | null;
  reason: NoticeReason;
  previousAssessedValue: number | null;
  assessedValue: number;
  marketValue: number;
  assessmentYear: number;
  assessmentEffectiveDate: string;
  addresseeNames: string;
  addresseeAddress: string | null;
  issuePeriodDays: number;
  issueDueDate: string;
  issueOverdue: boolean;
  appealPeriodDays: number;
  status: NoticeStatus;
  createdAt: string;
  issuedAt: string | null;
  serviceMode: NoticeServiceMode | null;
  receivedDate: string | null;
  servedTo: string | null;
  proofReference: string | null;
  serviceNotes: string | null;
  appealDeadline: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  /** Set for a notice combining several properties of one declared owner. */
  addresseeTaxpayerId: string | null;
  /** One row per property assessment the notice gives (MRPAAO Att. 10). */
  items: NoticeItemDto[] | null;
  /** The address an emailed notice went to. */
  emailAddress?: string | null;
  /** The date mailed or emailed (LAM proof of service). */
  sentDate?: string | null;
}

/** A FAAS "Property Assessment" row (docs/analysis/mrpaao-forms-model.md §8.2). */
export interface AssessmentLineDto {
  id: string;
  sequence: number;
  classificationId: string;
  classificationName: string;
  actualUseId: string;
  actualUseName: string;
  marketValue: number;
  assessmentLevelId: string;
  assessmentPercentage: number;
  assessedValue: number;
}

export interface AssessmentSummaryDto {
  id: string;
  rpuId: string;
  assessmentYear: number;
  marketValue: number;
  assessedValue: number;
  /** Null for a mixed-use assessment: its lines carry a level each. */
  assessmentPercentage: number | null;
  lines: AssessmentLineDto[];
  status: WorkflowStatus;
  effectiveDate: string;
  previousAssessmentId: string | null;
  faasNumber: string | null;
  remarks: string | null;
  /** The Record of Assessment entry. */
  postedAt: string | null;
  postedBy: string | null;
  /** Set by posting only: whether a draft Tax Declaration was prepared, and if not, why. */
  taxDeclarationNote?: string | null;
  effectivityYear: number;
  effectivityQuarter: number;
  transactionCode: string | null;
  effectivityRule: EffectivityRule | null;
  causeDate: string | null;
  causeWindowExceeded: boolean;
  /** The date of final approval; null while a draft. */
  madeOn: string | null;
  effectivityOverrideReason: string | null;
}

// --- Appraisal record / FAAS aggregate (docs/FORMS-REVISION-PLAN.md A7) ---

export type ValuationSourceType = 'Land' | 'Building' | 'Machinery';

export interface AppraisalRecordDto {
  assessmentId: string;
  faasNumber: string | null;
  kind: ValuationSourceType;
  status: WorkflowStatus;
  property: {
    id: string; pin: string; street: string | null; sitio: string | null; lotNumber: string | null; blockNumber: string | null;
    surveyNumber: string | null; titleNumber: string | null; taxMapNumber: string | null; barangay: string; municipality: string; province: string;
  };
  partiesAsOf: string;
  parties: { name: string; role: PropertyPartyRole; roleLabel: string; sharePercent: number; address: string | null }[];
  rpu: { id: string; number: string; type: RpuType };
  taxDeclaration: { id: string; number: string; revisionNumber: number; effectivityDate: string; status: WorkflowStatus; transactionCode: string | null } | null;
  land: {
    id: string; area: number; areaUnit: string; classification: string; actualUse: string; subClassification: string | null; zone: string | null;
    locationFactor: number | null; roadFrontage: number | null; roadType: string | null; isCornerLot: boolean; zoning: string | null;
  } | null;
  building: {
    id: string; buildingType: string; structuralType: string; actualUse: string; numberOfStoreys: number; floorArea: number; totalFloorArea: number;
    yearConstructed: number | null; yearCompleted: number | null; condition: string; completionPercentage: number;
    components: { componentType: string; description: string | null; quantity: number | null; unitCost: number | null; cost: number | null }[];
  } | null;
  machinery: {
    id: string; machineryType: string; description: string | null; brand: string | null; model: string | null; serialNumber: string | null;
    capacity: number | null; capacityUnit: string | null; dateAcquired: string | null; acquisitionCost: number; installationCost: number | null;
    otherCost: number | null; isBrandNew: boolean; replacementCost: number | null; economicLifeYears: number | null; remainingLifeYears: number | null;
  } | null;
  valuation: {
    id: string; method: string; marketValue: number; effectiveDate: string; computedAt: string;
    smv: { id: string; ordinanceNumber: string; ordinanceDate: string; effectivityDate: string; revisionYear: number; description: string | null } | null;
    scheduleUnit: string | null; scheduleRate: number | null;
    breakdown: { key: string; value: number }[];
    /** The FAAS appraisal rows, each with its own rate and breakdown. */
    lines: {
      sequence: number; source: string; description: string | null; classification: string | null; subClassification: string | null;
      actualUse: string | null; quantity: number | null; unit: string | null; unitValue: number | null; marketValue: number;
      breakdown: { key: string; value: number }[];
    }[];
  };
  assessment: {
    /** Single-row fields are the principal (largest) line's; the level percent is null when lines differ. */
    year: number; effectiveDate: string; classification: string; actualUse: string; propertyType: string; marketValue: number;
    assessmentLevelPercent: number | null; levelLowerValue: number; levelUpperValue: number | null; levelOrdinanceNumber: string;
    levelOrdinanceDate: string | null; assessedValue: number; revisionReference: string | null; remarks: string | null;
    lines: {
      sequence: number; classification: string; actualUse: string; propertyType: string; marketValue: number; assessmentLevelPercent: number;
      levelLowerValue: number; levelUpperValue: number | null; levelOrdinanceNumber: string; levelOrdinanceDate: string | null; assessedValue: number;
    }[];
  };
  previous: { assessmentId: string; faasNumber: string | null; year: number; effectiveDate: string; marketValue: number; assessedValue: number; assessedValueChange: number } | null;
  recordedBy: string | null;
  recordedAt: string;
  /** Date of entry in the Record of Assessment (when posted) and by whom. */
  recordEntry: { postedAt: string; postedBy: string | null } | null;
  signatures: {
    label: string; name: string; position: string | null; signedAt: string; underDelegation?: string | null;
    /** REA licence when signed, frozen; and whether a licensed-signatory step was signed without a valid one (records-and-forms.md Q10). */
    licenceNumber?: string | null; licenceValidUntil?: string | null; signedWithoutValidLicence?: boolean;
  }[];
  notices: { id: string; number: string | null; status: NoticeStatus; issuedAt: string | null; receivedDate: string | null; appealDeadline: string | null }[];
}

// --- Descriptive fields (docs/analysis/mrpaao-forms-model.md §10) ---

export interface UpdatePropertyDescriptionRequest {
  street: string | null; sitio: string | null; lotNumber: string | null; blockNumber: string | null; surveyNumber: string | null;
  titleNumber: string | null; titleTypeId: string | null; titleDate: string | null; taxMapNumber: string | null;
  boundaryNorth: string | null; boundaryEast: string | null; boundarySouth: string | null; boundaryWest: string | null; reason: string;
  cadastralNumber?: string | null;
}

export interface UpdateBuildingDescriptionRequest {
  numberOfStoreys: number | null; yearConstructed: number | null; yearCompleted: number | null; buildingPermitNumber: string | null;
  buildingPermitDate: string | null; condominiumCertificateNumber: string | null; certificateOfCompletionDate: string | null;
  certificateOfOccupancyDate: string | null; dateConstructed: string | null; dateOccupied: string | null; reason: string;
}

export interface AddBuildingFloorRequest { floorNumber: number; area: number }

export interface AddBuildingMaterialRequest { structuralPartId: string; structuralMaterialId: string | null; otherSpecify: string | null; floorNumber: number | null }

export interface UpdateMachineryDescriptionRequest {
  description: string | null; brand: string | null; model: string | null; serialNumber: string | null; capacity: number | null;
  capacityUnit: string | null; yearInstalled: number | null; yearOfInitialOperation: number | null; conversionFactor: number | null; reason: string;
  engineeringRegistrationNumber?: string | null; engineeringRegistrationDate?: string | null; importPermitNumber?: string | null;
  importPermitDate?: string | null; supplierName?: string | null; supplierAddress?: string | null; receiptNumber?: string | null;
  receiptDate?: string | null;
}

export interface TransferTaxClearanceDto {
  carNumber: string | null; carDate: string | null; transferorName: string | null; transferorTin: string | null; transfereeTin: string | null;
  capitalGainsTax: number | null; capitalGainsTaxReceipt: string | null; capitalGainsTaxDate: string | null;
  documentaryStampTax: number | null; documentaryStampTaxReceipt: string | null; documentaryStampTaxDate: string | null;
  transferTax: number | null; transferTaxReceipt: string | null; transferTaxDate: string | null; remarks: string | null;
}

export type SetTransferTaxClearanceRequest = TransferTaxClearanceDto;

export interface StructuralMaterialDto { id: string; code: string; name: string; structuralPartId: string; sortOrder: number }


// --- Registers (docs/analysis/mrpaao-forms-model.md §15) ---

export type RegisterKind = 'TaxMapControlRoll' | 'PreTaxMapControlRoll' | 'AssessmentRollTaxable' | 'AssessmentRollExempt' | 'OwnershipRecordCard' | 'RecordOfAssessment';

export const registerKindLabel: Record<RegisterKind, string> = {
  TaxMapControlRoll: 'Tax Map Control Roll',
  PreTaxMapControlRoll: 'Pre-Tax Map Control Roll',
  AssessmentRollTaxable: 'Assessment Roll — Taxable',
  AssessmentRollExempt: 'Assessment Roll — Exempt',
  // The LAM's name (MRPAAO: Ownership Record Card); the stored kind is unchanged.
  OwnershipRecordCard: 'Ownership Record Form',
  RecordOfAssessment: 'Record of Assessment',
};

export interface RegisterRunDto {
  id: string; kind: RegisterKind; formCode: string; asOf: string; fromDate: string | null;
  barangayId: string | null; barangayName: string | null; classificationId: string | null; classificationName: string | null;
  taxpayerId: string | null; taxpayerName: string | null; remarks: string | null; createdAt: string;
  sectionId: string | null; sectionIndexNumber: string | null;
  includePastOwners: boolean;
}

export interface CreateRegisterRunRequest {
  kind: RegisterKind; asOf: string; fromDate: string | null; barangayId: string | null;
  classificationId: string | null; taxpayerId: string | null; remarks: string | null;
  /** Tax Map Control Roll only: one tax map section (the post-TMCR); null lists the whole barangay. */
  sectionId?: string | null;
  /** Ownership Record Form only: also list properties the owner no longer holds (records-and-forms.md §4.5). */
  includePastOwners?: boolean;
}

// --- Sworn statements (docs/analysis/mrpaao-forms-model.md §16) ---

export type SwornStatementStatus = 'Draft' | 'Filed' | 'Superseded' | 'Cancelled';
export type DeclarantCapacity = 'Owner' | 'Administrator' | 'AuthorizedRepresentative';
export type SwornStatementFilingBasis = 'Section202' | 'Section203' | 'Other';
export type SwornStatementItemKind = 'Land' | 'Building' | 'Machinery' | 'OtherImprovement';

export const declarantCapacityLabel: Record<DeclarantCapacity, string> = {
  Owner: 'Owner',
  Administrator: 'Administrator',
  AuthorizedRepresentative: 'Authorized representative',
};
export const filingBasisLabel: Record<SwornStatementFilingBasis, string> = {
  Section202: 'LGC §202 — declaration by the owner or administrator',
  Section203: 'LGC §203 — new property or improvement',
  Other: 'Other',
};
export const swornItemKindLabel: Record<SwornStatementItemKind, string> = {
  Land: 'A. Land',
  Building: 'B. Building / structure',
  Machinery: 'C. Machinery',
  OtherImprovement: 'Trees / plants',
};

export interface SaveSwornStatementRequest {
  declarantName: string; declarantTaxpayerId: string | null; citizenship: string | null; civilStatus: string | null;
  postalAddress: string | null; declarantTin: string | null; capacity: DeclarantCapacity; ownerNames: string | null;
  municipalityId: string; filingBasis: SwornStatementFilingBasis;
  signedOn: string | null; signedAt: string | null; thumbmarked: boolean; witness1: string | null; witness2: string | null;
  swornOn: string | null; swornAt: string | null; administeringOfficer: string | null; officerTin: string | null;
  identityDocument: string | null; identityDocumentIssuedOn: string | null; identityDocumentIssuedAt: string | null;
  receivedOn: string | null; supersedesId: string | null; remarks: string | null;
}

export interface AddSwornStatementItemRequest {
  kind: SwornStatementItemKind; taxDeclarationId: string | null; existingTdNumber: string | null; location: string | null; declaredMarketValue: number;
  lotNumber?: string | null; blockNumber?: string | null; cadastralNumber?: string | null; titleNumber?: string | null;
  area?: number | null; areaUnit?: string | null; classificationId?: string | null;
  floorArea?: number | null; storeys?: number | null; description?: string | null; yearCompleted?: number | null; actualUseId?: string | null; lotOwnerName?: string | null;
  dateAcquired?: string | null; dateOperationCommenced?: string | null; acquisitionCost?: number | null; installationCost?: number | null; depreciation?: number | null;
  improvementKindId?: string | null; productiveCount?: number | null; nonProductiveCount?: number | null; annualProduct?: string | null; ages?: string | null;
}

export interface SwornStatementItemDto {
  id: string; kind: SwornStatementItemKind; sequence: number; taxDeclarationId: string | null; tdNumber: string | null; isNew: boolean;
  propertyId: string | null; rpuId: string | null; rpuNumber: string | null; location: string | null; declaredMarketValue: number;
  lotNumber: string | null; blockNumber: string | null; cadastralNumber: string | null; titleNumber: string | null; area: number | null; areaUnit: string | null;
  classificationId: string | null; classificationName: string | null;
  floorArea: number | null; storeys: number | null; description: string | null; yearCompleted: number | null; actualUseId: string | null; actualUseName: string | null;
  lotOwnerName: string | null; dateAcquired: string | null; dateOperationCommenced: string | null;
  acquisitionCost: number | null; installationCost: number | null; depreciation: number | null;
  improvementKindId: string | null; improvementKindName: string | null; productiveCount: number | null; nonProductiveCount: number | null;
  annualProduct: string | null; ages: string | null;
}

export interface SwornStatementDto extends SaveSwornStatementRequest {
  id: string; number: string | null; status: SwornStatementStatus; municipalityName: string; provinceName: string;
  supersedesNumber: string | null; supersededById: string | null; filedAt: string | null; cancelledAt: string | null; cancellationReason: string | null;
  totalDeclaredValue: number; createdAt: string; items: SwornStatementItemDto[];
}

export interface SwornStatementSummaryDto {
  id: string; number: string | null; status: SwornStatementStatus; declarantName: string; capacity: DeclarantCapacity; municipalityName: string;
  receivedOn: string | null; itemCount: number; totalDeclaredValue: number; createdAt: string;
}

export interface SwornStatementSearchParams {
  declarant?: string; tdNumber?: string; status?: SwornStatementStatus; municipalityId?: string; page?: number; pageSize?: number;
}

export const swornStatusColor: Record<SwornStatementStatus, string> = { Draft: 'default', Filed: 'green', Superseded: 'orange', Cancelled: 'red' };

/** The unit types an item of each kind can declare (trees are declared on the land's FAAS, MRPAAO Att. 1). */
export const unitTypesFor: Record<SwornStatementItemKind, RpuType[]> = {
  Land: ['Land'],
  Building: ['Building', 'OtherImprovement'],
  Machinery: ['Machinery'],
  OtherImprovement: ['Land', 'OtherImprovement'],
};

// --- Value and assess (docs/analysis/value-and-assess.md) ---

export type ValuationLineSource = 'Land' | 'Building' | 'Machinery' | 'LandStrip' | 'LandImprovement' | 'BuildingUsePortion';

export interface ValuationBreakdownItemDto { key: string; value: number }

export interface ValuationLineDto {
  sequence: number; source: ValuationLineSource; sourceId: string | null; description: string | null;
  classificationName: string | null; subClassificationName: string | null; actualUseName: string | null;
  quantity: number | null; unit: string | null; unitValue: number | null; smvScheduleId: string | null; marketValue: number;
  breakdown: ValuationBreakdownItemDto[];
  /** Set when the row was priced by another class and sub-class than the ones that assess it. */
  pricedClassificationName: string | null; pricedSubClassificationName: string | null;
}

export interface ValuationDto {
  id: string; rpuId: string; propertyId: string; sourceType: string; sourceId: string; smvId: string | null; smvScheduleId: string | null;
  valuationMethod: string; computedMarketValue: number; breakdown: Record<string, number>; effectiveDate: string; computedAt: string;
  lines: ValuationLineDto[] | null; smvOrdinanceNumber: string | null; smvRevisionYear: number | null;
}

export interface CreateAssessmentRequest {
  valuationId: string; assessmentYear: number | null; effectiveDate: string | null; previousAssessmentId: string | null;
  revisionReference: string | null; remarks: string | null;
  transactionTypeId?: string | null; causeDate?: string | null; effectivityOverrideReason?: string | null;
}

/** The effectivity an assessment would take if made (finally approved) today. */
export interface EffectivityDto {
  effectiveDate: string; year: number; quarter: number; rule: EffectivityRule | null; derived: boolean; overridden: boolean;
  transactionTypeId: string | null; transactionCode: string | null; legalBasis: string | null; madeOn: string;
  causeDate: string | null; causeWindowDays: number | null; causeWindowExceeded: boolean;
}

export interface AssessmentPreviewDto {
  valuationId: string; rpuId: string; marketValue: number; assessedValue: number; lines: AssessmentLineDto[];
  effectivity: EffectivityDto | null;
}

/** How an SMV came into force: by ordinance (before RA 12001) or certified by the Secretary of Finance. */
export type SmvBasis = 'Ordinance' | 'Certified';

export interface SmvDto {
  id: string; ordinanceNumber: string | null; ordinanceDate: string | null; approvalDate: string | null; effectivityDate: string;
  revisionYear: number; status: WorkflowStatus; description: string | null; createdAt: string;
  basis: SmvBasis; reference: string;
  proposedOn: string | null; publishedForCommentOn: string | null; consultationsHeldOn: string | null; submittedToBlgfOn: string | null;
  certifiedOn: string | null; certificationReference: string | null; publishedOn: string | null; publicationReference: string | null;
  /** Empty: the whole province. */
  coverage: { municipalityId: string; municipalityName: string }[];
}

export interface CreateSmvRequest {
  ordinanceNumber: string | null; ordinanceDate: string | null; approvalDate: string | null; effectivityDate: string; revisionYear: number;
  description: string | null; basis: SmvBasis;
  proposedOn?: string | null; publishedForCommentOn?: string | null; consultationsHeldOn?: string | null; submittedToBlgfOn?: string | null;
  certifiedOn?: string | null; certificationReference?: string | null; publishedOn?: string | null; publicationReference?: string | null;
  municipalityIds?: string[];
}

export interface SmvScheduleDto {
  id: string; smvId: string; classificationId: string; classificationName: string; actualUseId: string | null; actualUseName: string | null;
  propertyTypeId: string; propertyTypeName: string; zoneId: string | null; zoneName: string | null;
  improvementKindId: string | null; improvementKindName: string | null; unit: string; marketValue: number;
  minimumValue: number | null; maximumValue: number | null; effectiveDate: string; endDate: string | null; status: WorkflowStatus; createdAt: string;
  subClassificationId: string | null; subClassificationName: string | null; barangayId: string | null; barangayName: string | null;
}

export interface CreateSmvScheduleRequest {
  classificationId: string; actualUseId: string | null; propertyTypeId: string; zoneId: string | null; unit: string; marketValue: number;
  minimumValue: number | null; maximumValue: number | null; effectiveDate: string; improvementKindId: string | null;
  subClassificationId: string | null; barangayId: string | null;
}

export interface AssessmentLevelDto {
  id: string; ordinanceNumber: string; ordinanceDate: string | null; classificationId: string; classificationName: string;
  actualUseId: string; actualUseName: string; propertyTypeId: string; propertyTypeName: string;
  lowerValue: number; upperValue: number | null; assessmentPercentage: number; effectiveDate: string; endDate: string | null;
  status: WorkflowStatus; createdAt: string;
}

export interface CreateAssessmentLevelRequest {
  ordinanceNumber: string; ordinanceDate: string | null; classificationId: string; actualUseId: string; propertyTypeId: string;
  lowerValue: number; upperValue: number | null; assessmentPercentage: number; effectiveDate: string;
}

export interface CreateAdjustmentFactorRequest {
  smvId: string; code: string; name: string; percent: number; classificationId: string | null; description: string | null;
  legalBasis: string; effectiveDate: string; remarks: string | null;
  ruleKind: AdjustmentRuleKind; distanceReference: DistanceReference | null; standardDepth: number | null; rows: AdjustmentFactorRowRequest[];
}

// --- Independent appraisal (docs/analysis/valuation-foundation.md §4.7) ---

export type IndependentAppraisalSubject = 'Land' | 'Building' | 'BuildingComponent' | 'Machinery';
export type AppraisalApproach = 'Market' | 'Income' | 'Cost';
export const appraisalApproaches: { value: AppraisalApproach; label: string }[] = [
  { value: 'Market', label: 'Market approach' }, { value: 'Income', label: 'Income approach' }, { value: 'Cost', label: 'Cost approach' },
];
export interface IndependentAppraisalInputDto { sequence: number; name: string; value: number; unit: string | null }
export interface IndependentAppraisalDto {
  id: string; rpuId: string; subject: IndependentAppraisalSubject; subjectId: string; subjectLabel: string; approach: AppraisalApproach; value: number;
  appraisedOn: string; basis: string; evidence: string; isCurrent: boolean; endReason: string | null; createdAt: string; inputs: IndependentAppraisalInputDto[];
}
export interface CreateIndependentAppraisalRequest {
  rpuId: string; subject: IndependentAppraisalSubject; subjectId: string; approach: AppraisalApproach; value: number; appraisedOn: string;
  basis: string; evidence: string; inputs: { name: string; value: number; unit: string | null }[];
}

// --- Back taxes (docs/analysis/valuation-foundation.md §4.8) ---

export interface BackTaxRequest { rpuId: string; declaredFromYear: number; basis: string; initialAssessmentYear: number | null; transactionTypeId: string | null }
export interface BackTaxPeriodDto {
  sequence: number; startDate: string; endDate: string | null; smvReference: string | null; valuationId: string | null; assessmentId: string | null;
  marketValue: number | null; assessedValue: number | null; assessmentStatus: WorkflowStatus | null;
}
export interface BackTaxRunDto {
  id: string | null; rpuId: string; declaredFromYear: number; basis: string; initialAssessmentYear: number; yearsLimit: number; yearsLimitLegalBasis: string;
  buildingRules: 'ByPeriod' | 'Current'; machineryRules: 'ByPeriod' | 'Current'; periods: BackTaxPeriodDto[]; createdAt: string | null;
}
