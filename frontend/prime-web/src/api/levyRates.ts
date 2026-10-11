import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { WorkflowStatus } from '../lib/types';

/** Levy rates for the report collectibles (docs/analysis/reporting.md §10, Q18); figures only, nothing is billed. */
export type LevyKind = 'Basic' | 'SpecialEducationFund' | 'IdleLand';

export const levyKindLabel: Record<LevyKind, string> = {
  Basic: 'Basic real property tax',
  SpecialEducationFund: 'Special Education Fund',
  IdleLand: 'Idle-land tax',
};

export interface LevyRateDto {
  id: string;
  code: string;
  kind: LevyKind;
  municipalityId: string | null;
  municipalityName: string | null;
  classificationId: string | null;
  classificationName: string | null;
  ratePercent: number;
  legalBasis: string;
  effectiveDate: string;
  endDate: string | null;
  status: WorkflowStatus;
  description: string | null;
  remarks: string | null;
  createdBy: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
}

export interface CreateLevyRateRequest {
  kind: LevyKind;
  municipalityId: string | null;
  classificationId: string | null;
  ratePercent: number;
  legalBasis: string;
  effectiveDate: string;
  description: string | null;
  remarks: string | null;
}

export const useLevyRates = () => useQuery({ queryKey: ['levy-rates'], queryFn: () => apiGet<LevyRateDto[]>('/api/levy-rates') });

export function useCreateLevyRate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (r: CreateLevyRateRequest) => apiPost<LevyRateDto>('/api/levy-rates', r),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['levy-rates'] }),
  });
}

export function useApproveLevyRate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiPost<LevyRateDto>(`/api/levy-rates/${id}/approve`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['levy-rates'] }),
  });
}
