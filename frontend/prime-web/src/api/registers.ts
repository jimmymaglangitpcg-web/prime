import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateRegisterRunRequest, RegisterRunDto } from '../lib/types';

/** Dated runs of the MRPAAO registers (docs/analysis/mrpaao-forms-model.md §15). */
export function useRegisterRuns() {
  return useQuery({ queryKey: ['register-runs'], queryFn: () => apiGet<RegisterRunDto[]>('/api/registers') });
}

export function useCreateRegisterRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRegisterRunRequest) => apiPost<RegisterRunDto>('/api/registers', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['register-runs'] }),
  });
}
