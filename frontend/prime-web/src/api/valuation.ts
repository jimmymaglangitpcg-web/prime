import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  AdjustmentFactorDto, AssessmentLevelDto, AssessmentPreviewDto, AssessmentSummaryDto, CreateAdjustmentFactorRequest,
  CreateAssessmentLevelRequest, CreateAssessmentRequest, CreateSmvRequest, CreateSmvScheduleRequest, PagedResult, SmvDto, SmvScheduleDto, ValuationDto,
} from '../lib/types';

// --- Value and assess a unit (docs/analysis/value-and-assess.md §3) ---

export function useRpuValuations(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'valuations'],
    queryFn: () => apiGet<ValuationDto[]>(`/api/rpus/${rpuId}/valuations`),
    enabled: !!rpuId,
  });
}

/** Values the whole unit; every run is saved as a valuation (history). */
export function useValueRpu(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => apiPost<ValuationDto>(`/api/rpus/${rpuId}/valuations`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'valuations'] }),
  });
}

export const usePreviewAssessment = () =>
  useMutation({ mutationFn: (request: CreateAssessmentRequest) => apiPost<AssessmentPreviewDto>('/api/assessments/preview', request) });

export function useCreateAssessment(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateAssessmentRequest) => apiPost<AssessmentSummaryDto>('/api/assessments', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'assessments'] }),
  });
}

export type AssessmentAction = 'submit-for-review' | 'approve' | 'reject' | 'post';

/** A workflow step on an assessment; posting may prepare a draft TD, so the unit's TDs are refreshed too. */
export function useAssessmentAction(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: AssessmentAction; reason?: string }) =>
      apiPost<AssessmentSummaryDto>(`/api/assessments/${id}/${action}`, action === 'reject' ? { reason } : {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['rpus', rpuId] });
      queryClient.invalidateQueries({ queryKey: ['properties'] });
    },
  });
}

// --- Valuation rules (docs/analysis/value-and-assess.md §3) ---

export function useSmvs() {
  return useQuery({ queryKey: ['smv', 'list'], queryFn: () => apiGet<PagedResult<SmvDto>>('/api/smv', { pageSize: 100 }) });
}

export function useSmvSchedules(smvId: string | undefined) {
  return useQuery({
    queryKey: ['smv', smvId, 'schedules'],
    queryFn: () => apiGet<SmvScheduleDto[]>(`/api/smv/${smvId}/schedules`),
    enabled: !!smvId,
  });
}

export function useAssessmentLevels() {
  return useQuery({ queryKey: ['assessment-levels'], queryFn: () => apiGet<AssessmentLevelDto[]>('/api/assessment-levels') });
}

export function useAllAdjustmentFactors() {
  return useQuery({ queryKey: ['adjustment-factors'], queryFn: () => apiGet<AdjustmentFactorDto[]>('/api/adjustment-factors') });
}

function useRuleMutation<V, R>(fn: (variables: V) => Promise<R>, keys: unknown[][]) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => keys.forEach((queryKey) => queryClient.invalidateQueries({ queryKey })) });
}

export const useCreateSmv = () => useRuleMutation((r: CreateSmvRequest) => apiPost<SmvDto>('/api/smv', r), [['smv']]);
export const useApproveSmv = () => useRuleMutation((id: string) => apiPost<SmvDto>(`/api/smv/${id}/approve`, {}), [['smv']]);
export const useCreateSmvSchedule = (smvId: string) =>
  useRuleMutation((r: CreateSmvScheduleRequest) => apiPost<SmvScheduleDto>(`/api/smv/${smvId}/schedules`, r), [['smv', smvId]]);
export const useApproveSmvSchedule = (smvId: string) =>
  useRuleMutation((id: string) => apiPost<SmvScheduleDto>(`/api/smv/schedules/${id}/approve`, {}), [['smv', smvId]]);
export const useCreateAssessmentLevel = () =>
  useRuleMutation((r: CreateAssessmentLevelRequest) => apiPost<AssessmentLevelDto>('/api/assessment-levels', r), [['assessment-levels']]);
export const useApproveAssessmentLevel = () =>
  useRuleMutation((id: string) => apiPost<AssessmentLevelDto>(`/api/assessment-levels/${id}/approve`, {}), [['assessment-levels']]);
export const useCreateAdjustmentFactor = () =>
  useRuleMutation((r: CreateAdjustmentFactorRequest) => apiPost<AdjustmentFactorDto>('/api/adjustment-factors', r), [['adjustment-factors']]);
export const useApproveAdjustmentFactor = () =>
  useRuleMutation((id: string) => apiPost<AdjustmentFactorDto>(`/api/adjustment-factors/${id}/approve`, {}), [['adjustment-factors']]);
