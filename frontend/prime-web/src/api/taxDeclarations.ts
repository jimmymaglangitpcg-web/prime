import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateTaxDeclarationRequest, TaxDeclarationDto } from '../lib/types';

export function useTaxDeclarationsByRpu(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'tax-declarations'],
    queryFn: () => apiGet<TaxDeclarationDto[]>(`/api/rpus/${rpuId}/tax-declarations`),
    enabled: !!rpuId,
  });
}

export function useCreateTaxDeclaration(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTaxDeclarationRequest) => apiPost<TaxDeclarationDto>('/api/tax-declarations', request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
      queryClient.invalidateQueries({ queryKey: ['rpus', variables.rpuId, 'tax-declarations'] });
    },
  });
}
