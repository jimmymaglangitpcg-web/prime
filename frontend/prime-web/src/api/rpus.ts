import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateRpuRequest, RpuDto } from '../lib/types';

/** A property's units with their unit PINs and links (docs/analysis/mrpaao-forms-model.md §6.2–6.3). */
export function usePropertyRpus(propertyId: string) {
  return useQuery({
    queryKey: ['properties', propertyId, 'rpus'],
    queryFn: () => apiGet<RpuDto[]>(`/api/properties/${propertyId}/rpus`),
  });
}

export function useCreateRpu(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRpuRequest) => apiPost<RpuDto>('/api/rpus', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}
