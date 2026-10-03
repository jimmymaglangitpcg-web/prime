// Market data (docs/analysis/smv-preparation-general-revision.md §4.1, step L6-1).

export type MarketDataSource = 'RegistryAbstract' | 'TransferDeed' | 'SwornStatement' | 'FieldDataSheet' | 'Other';
export const marketDataSourceLabel: Record<MarketDataSource, string> = {
  RegistryAbstract: 'Registry of Deeds abstract',
  TransferDeed: 'Deed of a transfer',
  SwornStatement: 'Sworn statement',
  FieldDataSheet: 'Field data sheet',
  Other: 'Other',
};
export type MarketDataReview = 'Unreviewed' | 'Accepted' | 'Excluded';
export const marketReviewColor: Record<MarketDataReview, string> = { Unreviewed: 'gold', Accepted: 'green', Excluded: 'default' };
export type AreaMeasure = 'SquareMetre' | 'Hectare';
export const areaMeasureLabel: Record<AreaMeasure, string> = { SquareMetre: 'sqm', Hectare: 'ha' };
export type BuildingPermitScope = 'NewConstruction' | 'Addition' | 'Repair' | 'Renovation' | 'Demolition' | 'Other';
export const buildingPermitScopeLabel: Record<BuildingPermitScope, string> = {
  NewConstruction: 'New construction', Addition: 'Addition', Repair: 'Repair', Renovation: 'Renovation', Demolition: 'Demolition', Other: 'Other',
};
export type MarketDataReportKind = 'TransactionsAbstract' | 'BuildingPermitsAbstract' | 'MachineryRegistrationsAbstract' | 'SalesReport';
export const marketDataReportKindLabel: Record<MarketDataReportKind, string> = {
  TransactionsAbstract: 'Abstract of registered transactions',
  BuildingPermitsAbstract: 'Abstract of building permits',
  MachineryRegistrationsAbstract: 'Abstract of machinery registrations',
  SalesReport: 'Lowest to highest recorded sales',
};

export interface SaveMarketTransactionRequest {
  source: MarketDataSource;
  conveyanceModeId: string | null;
  transactionDate: string;
  documentReference: string | null;
  documentFileNumber: string | null;
  grantorNames: string | null;
  granteeNames: string | null;
  granteeAddress: string | null;
  municipalityId: string;
  barangayId: string | null;
  location: string | null;
  propertyId: string | null;
  pin: string | null;
  taxDeclarationNumber: string | null;
  lotNumber: string | null;
  previousTitleNumber: string | null;
  newTitleNumber: string | null;
  conveysLand: boolean;
  conveysBuilding: boolean;
  classificationId: string | null;
  subClassificationId: string | null;
  actualUseId: string | null;
  buildingTypeId: string | null;
  structuralTypeId: string | null;
  landArea: number | null;
  landAreaUnit: AreaMeasure;
  buildingFloorArea: number | null;
  consideration: number;
  landConsideration: number | null;
  remarks: string | null;
}

export interface MarketTransactionDto extends SaveMarketTransactionRequest {
  id: string;
  conveyanceModeName: string | null;
  municipalityName: string;
  barangayName: string | null;
  propertyPin: string | null;
  classificationName: string | null;
  subClassificationName: string | null;
  actualUseName: string | null;
  buildingTypeName: string | null;
  structuralTypeName: string | null;
  landUnitPrice: number | null;
  buildingUnitPrice: number | null;
  review: MarketDataReview;
  exclusionReason: string | null;
  fieldValidatedOn: string | null;
  reviewNote: string | null;
  reviewedAt: string | null;
  propertyTransactionId: string | null;
  importBatch: string | null;
  createdAt: string;
  cancelledAt: string | null;
  cancellationReason: string | null;
}

export interface SaveBuildingPermitRequest {
  permitNumber: string;
  issuedOn: string;
  proposedConstructionDate: string | null;
  expectedCompletionDate: string | null;
  permitteeName: string;
  permitteeAddress: string | null;
  taxDeclarationNumber: string | null;
  municipalityId: string;
  barangayId: string | null;
  blockLotNumber: string | null;
  street: string | null;
  scope: BuildingPermitScope;
  buildingTypeId: string | null;
  structuralTypeId: string | null;
  storeys: number | null;
  totalFloorArea: number | null;
  estimatedCost: number | null;
  classificationId: string | null;
  receivedOn: string | null;
  remarks: string | null;
}

export interface BuildingPermitDto extends SaveBuildingPermitRequest {
  id: string;
  municipalityName: string;
  barangayName: string | null;
  buildingTypeName: string | null;
  structuralTypeName: string | null;
  classificationName: string | null;
  buildingId: string | null;
  buildingPropertyId: string | null;
  buildingPin: string | null;
  suggestedBuildingId: string | null;
  createdAt: string;
  cancelledAt: string | null;
  cancellationReason: string | null;
}

export interface SaveMachineryRegistrationRequest {
  certificateNumber: string;
  issuedOn: string;
  ownerName: string;
  ownerAddress: string | null;
  taxDeclarationNumber: string | null;
  municipalityId: string;
  barangayId: string | null;
  location: string | null;
  machineryTypeId: string | null;
  description: string | null;
  brandModel: string | null;
  yearAcquired: number | null;
  manufacturer: string | null;
  cost: number | null;
  currentCondition: string | null;
  installationDate: string | null;
  receivedOn: string | null;
  remarks: string | null;
}

export interface MachineryRegistrationDto extends SaveMachineryRegistrationRequest {
  id: string;
  municipalityName: string;
  barangayName: string | null;
  machineryTypeName: string | null;
  machineryId: string | null;
  machineryPropertyId: string | null;
  machineryPin: string | null;
  suggestedMachineryId: string | null;
  createdAt: string;
  cancelledAt: string | null;
  cancellationReason: string | null;
}

export interface MarketImportRowDto { line: number; valid: boolean; errors: string[]; summary: string | null }
export interface MarketImportResultDto {
  fingerprint: string;
  rowCount: number;
  validCount: number;
  rows: MarketImportRowDto[];
  batch: string | null;
  importedCount: number;
}

export interface MarketDataReportRunDto {
  id: string;
  kind: MarketDataReportKind;
  formCode: string;
  municipalityId: string;
  municipalityName: string;
  fromDate: string;
  toDate: string;
  remarks: string | null;
  createdAt: string;
}
