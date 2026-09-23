import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { CreateRpuRequest, RpuDto } from '../lib/types';

export function useCreateRpu(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRpuRequest) => apiPost<RpuDto>('/api/rpus', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}
