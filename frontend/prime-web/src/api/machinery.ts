import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type { CreateMachineryRequest, MachineryDto, UpdateMachineryValuationInputsRequest } from '../lib/types';

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

/** Every machine of a machinery RPU (one FAAS row each; docs/analysis/mrpaao-forms-model.md §8.3). */
export function useMachineryUnitsByRpu(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'machinery', 'units'],
    queryFn: () => apiGet<MachineryDto[]>(`/api/rpus/${rpuId}/machinery-units`),
    enabled: !!rpuId,
  });
}

/** What the derived replacement cost reads, changed with a reason (audited; valuation-foundation.md §4.6). */
export function useUpdateMachineryValuationInputs(machineryId: string, rpuId: string, propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateMachineryValuationInputsRequest) => apiPut<MachineryDto>(`/api/machinery/${machineryId}/valuation-inputs`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'machinery'] });
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}
