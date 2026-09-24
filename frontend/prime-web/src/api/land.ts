import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateLandRequest, LandDto } from '../lib/types';

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
