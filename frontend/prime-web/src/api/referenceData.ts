import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { BarangayDto, LookupDto, MunicipalityDto, ProvinceDto, StructuralMaterialDto } from '../lib/types';

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
export const useImprovementKinds = () => useLookup('improvement-kinds');
export const useBuildingComponentTypes = () => useLookup('building-component-types');
export const useTitleTypes = () => useLookup('title-types');
export const usePropertyTypes = () => useLookup('property-types');
export const useStructuralParts = () => useLookup('structural-parts');
export const useTaxTypes = () => useLookup('tax-types');

export function useStructuralMaterials() {
  return useQuery({
    queryKey: ['reference', 'structural-materials'],
    queryFn: () => apiGet<StructuralMaterialDto[]>('/api/reference/structural-materials'),
    staleTime: 5 * 60 * 1000,
  });
}
