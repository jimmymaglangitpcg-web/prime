import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreatePropertyRequest, PagedResult, PropertyDto, PropertyProfileDto, PropertySearchParams } from '../lib/types';

export function usePropertySearch(params: PropertySearchParams) {
  return useQuery({
    queryKey: ['properties', 'search', params],
    queryFn: () =>
      apiGet<PagedResult<PropertyDto>>('/api/properties', {
        searchTerm: params.searchTerm,
        barangayId: params.barangayId,
        municipalityId: params.municipalityId,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      }),
    placeholderData: (previous) => previous,
  });
}

export function usePropertyProfile(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId],
    queryFn: () => apiGet<PropertyProfileDto>(`/api/properties/${propertyId}`),
    enabled: !!propertyId,
  });
}

export function useCreateProperty() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePropertyRequest) => apiPost<PropertyDto>('/api/properties', request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties', 'search'] });
    },
  });
}
