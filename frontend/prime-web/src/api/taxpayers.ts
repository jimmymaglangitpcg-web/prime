import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  AddOwnerRequest,
  CreateTaxpayerRequest,
  PagedResult,
  PropertyOwnerDto,
  TaxpayerDto,
  TaxpayerSearchParams,
} from '../lib/types';

export function useTaxpayerSearch(params: TaxpayerSearchParams, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: ['taxpayers', 'search', params],
    queryFn: () =>
      apiGet<PagedResult<TaxpayerDto>>('/api/taxpayers', {
        searchTerm: params.searchTerm,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      }),
    enabled: options?.enabled ?? true,
    placeholderData: (previous) => previous,
  });
}

export function useCreateTaxpayer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTaxpayerRequest) => apiPost<TaxpayerDto>('/api/taxpayers', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['taxpayers', 'search'] });
    },
  });
}

export function useOwnershipHistory(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'owners'],
    queryFn: () => apiGet<PropertyOwnerDto[]>(`/api/properties/${propertyId}/owners`),
    enabled: !!propertyId,
  });
}

export function useEndParty(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, endDate, reason }: { id: string; endDate: string; reason: string }) =>
      apiPost<PropertyOwnerDto>(`/api/property-owners/${id}/end`, { endDate, reason }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['properties', propertyId] }),
  });
}

export function useAddOwner(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: AddOwnerRequest) => apiPost<PropertyOwnerDto>(`/api/properties/${propertyId}/owners`, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    },
  });
}
