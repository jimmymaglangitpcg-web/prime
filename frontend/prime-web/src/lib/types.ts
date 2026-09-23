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
  propertyIdentificationNumber: string;
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
  area: number | null;
  surveyNumber: string | null;
  lotNumber: string | null;
  blockNumber: string | null;
  status: RecordStatus;
  createdAt: string;
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
  taxDeclarationNumber: string;
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
