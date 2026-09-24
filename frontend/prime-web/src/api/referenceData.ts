import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { BarangayDto, LookupDto, MunicipalityDto, ProvinceDto } from '../lib/types';

export function useProvinces() {
  return useQuery({
    queryKey: ['reference', 'provinces'],
    queryFn: () => apiGet<ProvinceDto[]>('/api/reference/provinces'),
    staleTime: 5 * 60 * 1000,
  });
}

export function useMunicipalities(provinceId: string | undefined) {
  return useQuery({
    queryKey: ['reference', 'municipalities', provinceId],
    queryFn: () => apiGet<MunicipalityDto[]>('/api/reference/municipalities', { provinceId }),
    enabled: !!provinceId,
    staleTime: 5 * 60 * 1000,
  });
}

export function useBarangays(municipalityId: string | undefined) {
  return useQuery({
    queryKey: ['reference', 'barangays', municipalityId],
    queryFn: () => apiGet<BarangayDto[]>('/api/reference/barangays', { municipalityId }),
    enabled: !!municipalityId,
    staleTime: 5 * 60 * 1000,
  });
}

function useLookup(kind: string) {
  return useQuery({
    queryKey: ['reference', kind],
    queryFn: () => apiGet<LookupDto[]>(`/api/reference/${kind}`),
    staleTime: 5 * 60 * 1000,
  });
}

export const useZones = () => useLookup('zones');
export const useClassifications = () => useLookup('classifications');
export const useActualUses = () => useLookup('actual-uses');
export const useSubClassifications = () => useLookup('sub-classifications');
export const useOwnershipTypes = () => useLookup('ownership-types');
export const useAnnotationTypes = () => useLookup('annotation-types');
export const useRoadTypes = () => useLookup('road-types');
export const useConditions = () => useLookup('conditions');
export const useBuildingTypes = () => useLookup('building-types');
export const useStructuralTypes = () => useLookup('structural-types');
export const useMachineryTypes = () => useLookup('machinery-types');
