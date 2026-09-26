import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch, apiGet, apiPost, apiPut } from '../lib/apiClient';
import type {
  AddSwornStatementItemRequest, PagedResult, SaveSwornStatementRequest, SwornStatementDto, SwornStatementSearchParams, SwornStatementSummaryDto,
} from '../lib/types';

/** The owner's sworn statement of market value (MRPAAO Att. 11; docs/analysis/mrpaao-forms-model.md §16). */
export function useSwornStatementSearch(params: SwornStatementSearchParams) {
  return useQuery({
    queryKey: ['sworn-statements', 'search', params],
    queryFn: () => apiGet<PagedResult<SwornStatementSummaryDto>>('/api/sworn-statements', { ...params }),
    placeholderData: (previous) => previous,
  });
}

export function useSwornStatement(id: string | undefined) {
  return useQuery({
    queryKey: ['sworn-statements', id],
    queryFn: () => apiGet<SwornStatementDto>(`/api/sworn-statements/${id}`),
    enabled: !!id,
  });
}

export function usePropertySwornStatements(propertyId: string) {
  return useQuery({
    queryKey: ['properties', propertyId, 'sworn-statements'],
    queryFn: () => apiGet<SwornStatementDto[]>(`/api/properties/${propertyId}/sworn-statements`),
  });
}

/** Every change returns the whole statement; the cache is refreshed from it. */
function useStatementMutation<V>(fn: (variables: V) => Promise<SwornStatementDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (statement) => {
      queryClient.setQueryData(['sworn-statements', statement.id], statement);
      queryClient.invalidateQueries({ queryKey: ['sworn-statements', 'search'] });
      queryClient.invalidateQueries({ queryKey: ['properties'] });
      if (statement.supersedesId) {
        queryClient.invalidateQueries({ queryKey: ['sworn-statements', statement.supersedesId] });
      }
    },
  });
}

export const useCreateSwornStatement = () =>
  useStatementMutation((request: SaveSwornStatementRequest) => apiPost<SwornStatementDto>('/api/sworn-statements', request));

export const useUpdateSwornStatement = (id: string) =>
  useStatementMutation((request: SaveSwornStatementRequest) => apiPut<SwornStatementDto>(`/api/sworn-statements/${id}`, request));

export const useAddSwornStatementItem = (id: string) =>
  useStatementMutation((request: AddSwornStatementItemRequest) => apiPost<SwornStatementDto>(`/api/sworn-statements/${id}/items`, request));

export const useRemoveSwornStatementItem = (id: string) =>
  useStatementMutation((itemId: string) => apiFetch<SwornStatementDto>(`/api/sworn-statements/${id}/items/${itemId}`, { method: 'DELETE' }));

export const useLinkSwornStatementItem = (id: string) =>
  useStatementMutation(({ itemId, rpuId }: { itemId: string; rpuId: string }) =>
    apiPost<SwornStatementDto>(`/api/sworn-statements/${id}/items/${itemId}/link`, { rpuId }));

export const useFileSwornStatement = (id: string) =>
  useStatementMutation((number: string | null) => apiPost<SwornStatementDto>(`/api/sworn-statements/${id}/file`, { number }));

export const useCancelSwornStatement = (id: string) =>
  useStatementMutation((reason: string) => apiPost<SwornStatementDto>(`/api/sworn-statements/${id}/cancel`, { reason }));
