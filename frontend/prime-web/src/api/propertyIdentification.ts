import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';

// The Real Property Identification System's configuration (docs/analysis/property-identification.md, step 10a-1).

export interface ProvinceIndexDto { id: string; name: string; psgcCode: string; pinIndexNumber: string | null }
export interface MunicipalityIndexDto { id: string; provinceId: string; name: string; psgcCode: string; isCity: boolean; pinIndexNumber: string | null; districtCount: number }
export interface CityDistrictDto { id: string; municipalityId: string; indexNumber: string; name: string; isActive: boolean }
export interface BarangayIndexDto {
  id: string;
  municipalityId: string;
  name: string;
  psgcCode: string;
  cityDistrictId: string | null;
  cityDistrictIndexNumber: string | null;
  pinIndexNumber: string | null;
  retiredOn: string | null;
  retirementReason: string | null;
  splitFromBarangayId: string | null;
  sectionCount: number;
}
export interface TaxMapSectionDto {
  id: string;
  barangayId: string;
  indexNumber: string;
  remarks: string | null;
  splitFromSectionId: string | null;
  retiredOn: string | null;
  retirementReason: string | null;
}

const base = '/api/property-identification';

function useRefresh() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['property-identification'] });
}

export const useIndexProvinces = () =>
  useQuery({ queryKey: ['property-identification', 'provinces'], queryFn: () => apiGet<ProvinceIndexDto[]>(`${base}/provinces`) });

export const useIndexMunicipalities = (provinceId: string | undefined) =>
  useQuery({
    queryKey: ['property-identification', 'municipalities', provinceId],
    queryFn: () => apiGet<MunicipalityIndexDto[]>(`${base}/provinces/${provinceId}/municipalities`),
    enabled: !!provinceId,
  });

export const useDistricts = (municipalityId: string | undefined) =>
  useQuery({
    queryKey: ['property-identification', 'districts', municipalityId],
    queryFn: () => apiGet<CityDistrictDto[]>(`${base}/municipalities/${municipalityId}/districts`),
    enabled: !!municipalityId,
  });

export const useIndexBarangays = (municipalityId: string | undefined) =>
  useQuery({
    queryKey: ['property-identification', 'barangays', municipalityId],
    queryFn: () => apiGet<BarangayIndexDto[]>(`${base}/municipalities/${municipalityId}/barangays`),
    enabled: !!municipalityId,
  });

export const useSections = (barangayId: string | undefined) =>
  useQuery({
    queryKey: ['property-identification', 'sections', barangayId],
    queryFn: () => apiGet<TaxMapSectionDto[]>(`${base}/barangays/${barangayId}/sections`),
    enabled: !!barangayId,
  });

export function useSetIndexNumber() {
  const refresh = useRefresh();
  return useMutation({
    mutationFn: ({ kind, id, indexNumber, reason, cityDistrictId }: {
      kind: 'provinces' | 'municipalities' | 'barangays'; id: string; indexNumber: string | null; reason: string | null; cityDistrictId?: string | null;
    }) => apiPut<unknown>(`${base}/${kind}/${id}/index-number`, kind === 'barangays' ? { indexNumber, cityDistrictId: cityDistrictId ?? null, reason } : { indexNumber, reason }),
    onSuccess: refresh,
  });
}

export function useCreateDistrict() {
  const refresh = useRefresh();
  return useMutation({
    mutationFn: ({ municipalityId, indexNumber, name }: { municipalityId: string; indexNumber: string | null; name: string }) =>
      apiPost<CityDistrictDto>(`${base}/municipalities/${municipalityId}/districts`, { indexNumber, name }),
    onSuccess: refresh,
  });
}

export function useSplitBarangay() {
  const refresh = useRefresh();
  return useMutation({
    mutationFn: ({ id, reason, effectiveDate, successors }: { id: string; reason: string; effectiveDate: string; successors: { name: string; psgcCode: string }[] }) =>
      apiPost<BarangayIndexDto[]>(`${base}/barangays/${id}/split`, { reason, effectiveDate, successors }),
    onSuccess: refresh,
  });
}

export function useCreateSection() {
  const refresh = useRefresh();
  return useMutation({
    mutationFn: ({ barangayId, indexNumber, remarks, splitFromSectionId }: { barangayId: string; indexNumber: string | null; remarks: string | null; splitFromSectionId: string | null }) =>
      apiPost<TaxMapSectionDto>(`${base}/barangays/${barangayId}/sections`, { indexNumber, remarks, splitFromSectionId }),
    onSuccess: refresh,
  });
}

export function useRetireSection() {
  const refresh = useRefresh();
  return useMutation({
    mutationFn: ({ id, reason, effectiveDate }: { id: string; reason: string; effectiveDate: string }) =>
      apiPost<TaxMapSectionDto>(`${base}/sections/${id}/retire`, { reason, effectiveDate }),
    onSuccess: refresh,
  });
}

// --- A property's PIN (step 10a-2) ---

export type PinKind = 'Registered' | 'Temporary' | 'Permanent';

export interface PinAssignmentDto {
  id: string;
  pin: string;
  kind: PinKind;
  parcelId: string | null;
  sectionIndex: string | null;
  parcelNumber: number | null;
  assignedAt: string;
  assignedByTransactionId: string | null;
  /** Tax mapping tie-up of a temporary PIN (step 10a-5): office mark, then field confirmation. */
  officeTieUpAt: string | null;
  fieldConfirmedAt: string | null;
  tieUpRemarks: string | null;
  retiredAt: string | null;
  retirementReason: string | null;
  propertyTransactionId: string | null;
}

export interface PropertyPinDto {
  propertyId: string;
  pin: string;
  kind: PinKind | null;
  lguIndex: string | null;
  municipalityIndex: string | null;
  barangayIndex: string | null;
  sectionIndex: string | null;
  parcelNumber: number | null;
  parcelId: string | null;
  history: PinAssignmentDto[];
}

export const usePropertyPin = (propertyId: string) =>
  useQuery({ queryKey: ['properties', propertyId, 'pin'], queryFn: () => apiGet<PropertyPinDto>(`/api/properties/${propertyId}/pin`) });

export type TieUpStage = 'Office' | 'Field';

/** Records or withdraws a tie-up mark on the property's temporary PIN (MRPAAO Ch. II §2 A). */
export function useRecordTieUp(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { stage: TieUpStage; withdraw: boolean; remarks: string | null }) =>
      apiPost<PropertyPinDto>(`/api/properties/${propertyId}/pin/tie-up`, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['properties', propertyId, 'pin'] }),
  });
}

export function usePlaceInSection(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: { parcelId: string; sectionId: string; parcelNumber: number | null }) =>
      apiPost<PropertyPinDto>(`/api/properties/${propertyId}/pin/place-in-section`, request),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] }),
      queryClient.invalidateQueries({ queryKey: ['property-identification'] }),
    ]),
  });
}
