import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { BackTaxRequest, BackTaxRunDto } from '../lib/types';

/** A unit's back-tax runs (docs/analysis/valuation-foundation.md §4.8). */
export function useBackTaxRuns(rpuId: string) {
  return useQuery({ queryKey: ['rpus', rpuId, 'back-taxes'], queryFn: () => apiGet<BackTaxRunDto[]>(`/api/rpus/${rpuId}/back-taxes`) });
}

export function usePreviewBackTaxes() {
  return useMutation({ mutationFn: (request: BackTaxRequest) => apiPost<BackTaxRunDto>('/api/back-taxes/preview', request) });
}

/** Creates the run: a valuation and a Draft assessment per period. */
export function useCreateBackTaxRun(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BackTaxRequest) => apiPost<BackTaxRunDto>('/api/back-taxes', request),
    onSuccess: () => {
      for (const key of ['back-taxes', 'assessments', 'valuations']) {
        queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, key] });
      }
    },
  });
}
