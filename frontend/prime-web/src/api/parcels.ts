import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPost } from '../lib/apiClient';
import type { CreateParcelRequest, ParcelDto } from '../lib/types';

export function useCreateParcel(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateParcelRequest) => apiPost<ParcelDto>('/api/parcels', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}
