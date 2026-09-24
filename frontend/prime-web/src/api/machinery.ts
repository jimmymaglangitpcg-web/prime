import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateMachineryRequest, MachineryDto } from '../lib/types';

export function useMachineryByRpu(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'machinery'],
    queryFn: () => apiGet<MachineryDto>(`/api/rpus/${rpuId}/machinery`),
    enabled: !!rpuId,
    retry: false,
  });
}

export function useCreateMachinery(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateMachineryRequest) => apiPost<MachineryDto>('/api/machinery', request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
      queryClient.invalidateQueries({ queryKey: ['rpus', variables.rpuId, 'machinery'] });
    },
  });
}
