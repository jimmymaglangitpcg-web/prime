import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  AddLandAdjustmentRequest, AddLandImprovementRequest, AddLandStripRequest, AdjustmentFactorDto, CreateLandRequest, LandDto,
} from '../lib/types';

// A LAND_NOT_FOUND response here just means "not registered yet" (Land is
// 1:1 with its RPU) — not a transient failure worth retrying.
export function useLandByRpu(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'land'],
    queryFn: () => apiGet<LandDto>(`/api/rpus/${rpuId}/land`),
    enabled: !!rpuId,
    retry: false,
  });
}

export function useCreateLand(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateLandRequest) => apiPost<LandDto>('/api/land', request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
      queryClient.invalidateQueries({ queryKey: ['rpus', variables.rpuId, 'land'] });
    },
  });
}

/** Adds one appraisal row; the land and the profile are reloaded. */
function useAddLandRow<T>(path: string, landId: string, rpuId: string, propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: T) => apiPost<LandDto>(`/api/land/${landId}/${path}`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'land'] });
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}

export const useAddLandStrip = (landId: string, rpuId: string, propertyId: string) =>
  useAddLandRow<AddLandStripRequest>('strips', landId, rpuId, propertyId);
export const useAddLandImprovement = (landId: string, rpuId: string, propertyId: string) =>
  useAddLandRow<AddLandImprovementRequest>('improvements', landId, rpuId, propertyId);
export const useAddLandAdjustment = (landId: string, rpuId: string, propertyId: string) =>
  useAddLandRow<AddLandAdjustmentRequest>('adjustments', landId, rpuId, propertyId);

export function useAdjustmentFactors() {
  return useQuery({
    queryKey: ['adjustment-factors'],
    queryFn: () => apiGet<AdjustmentFactorDto[]>('/api/adjustment-factors'),
    staleTime: 60 * 1000,
  });
}
