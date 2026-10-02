import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateIndependentAppraisalRequest, IndependentAppraisalDto } from '../lib/types';

/** A unit's independent appraisals, current first (docs/analysis/valuation-foundation.md §4.7). */
export function useIndependentAppraisals(rpuId: string) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'independent-appraisals'],
    queryFn: () => apiGet<IndependentAppraisalDto[]>(`/api/rpus/${rpuId}/independent-appraisals`),
  });
}

export function useCreateIndependentAppraisal(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateIndependentAppraisalRequest) => apiPost<IndependentAppraisalDto>('/api/independent-appraisals', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'independent-appraisals'] }),
  });
}

export function useWithdrawIndependentAppraisal(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => apiPost<IndependentAppraisalDto>(`/api/independent-appraisals/${id}/withdraw`, { reason }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'independent-appraisals'] }),
  });
}
