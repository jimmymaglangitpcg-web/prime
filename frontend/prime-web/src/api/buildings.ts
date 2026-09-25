import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { BuildingDto, CreateBuildingRequest, AddBuildingComponentRequest, AddBuildingUsePortionRequest } from '../lib/types';

export function useBuildingByRpu(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'building'],
    queryFn: () => apiGet<BuildingDto>(`/api/rpus/${rpuId}/building`),
    enabled: !!rpuId,
    retry: false,
  });
}

export function useCreateBuilding(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateBuildingRequest) => apiPost<BuildingDto>('/api/buildings', request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
      queryClient.invalidateQueries({ queryKey: ['rpus', variables.rpuId, 'building'] });
    },
  });
}

function useAddBuildingRow<T>(path: string, buildingId: string, rpuId: string, propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: T) => apiPost<BuildingDto>(`/api/buildings/${buildingId}/${path}`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'building'] });
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}

export const useAddBuildingUsePortion = (buildingId: string, rpuId: string, propertyId: string) =>
  useAddBuildingRow<AddBuildingUsePortionRequest>('use-portions', buildingId, rpuId, propertyId);
export const useAddBuildingComponent = (buildingId: string, rpuId: string, propertyId: string) =>
  useAddBuildingRow<AddBuildingComponentRequest>('components', buildingId, rpuId, propertyId);
