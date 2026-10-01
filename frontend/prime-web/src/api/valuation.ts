import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  AdjustmentFactorDto, AssessmentLevelDto, AssessmentPreviewDto, AssessmentSummaryDto, CreateAdjustmentFactorRequest,
  CreateAssessmentLevelRequest, CreateAssessmentRequest, EffectivityDto, CreateSmvRequest, CreateSmvScheduleRequest, PagedResult, SmvDto, SmvScheduleDto, ValuationDto,
  BuildingCostDto, CreateBuildingCostRequest, CreateDepreciationScheduleRequest, CreateExtraItemCostRequest, DepreciationScheduleDto, ExtraItemCostDto,
  CreateExchangeRateRequest, CreatePriceIndexRequest, ExchangeRateDto, PriceIndexDto,
} from '../lib/types';

// --- Value and assess a unit (docs/analysis/value-and-assess.md §3) ---

export function useRpuValuations(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'valuations'],
    queryFn: () => apiGet<ValuationDto[]>(`/api/rpus/${rpuId}/valuations`),
    enabled: !!rpuId,
  });
}

/**
 * Values the whole unit as of a date (default today), under the rules in force then
 * (docs/analysis/valuation-foundation.md §4.1), for a transaction when one is chosen: it
 * decides whether a building takes a new depreciation (§4.5). Every run is saved (history).
 */
export function useValueRpu(rpuId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ asOf, transactionTypeId }: { asOf?: string; transactionTypeId?: string }) => {
      const query = new URLSearchParams();
      if (asOf) query.set('asOf', asOf);
      if (transactionTypeId) query.set('transactionTypeId', transactionTypeId);
      const qs = query.toString();
      return apiPost<ValuationDto>(`/api/rpus/${rpuId}/valuations${qs ? `?${qs}` : ''}`, {});
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'valuations'] }),
  });
}

/** The effectivity an assessment under this transaction type would take if made today (valuation-foundation.md §4.2). */
export function useEffectivity(transactionTypeId: string | undefined, causeDate: string | undefined, ready = true) {
  return useQuery({
    queryKey: ['assessments', 'effectivity', transactionTypeId, causeDate],
    queryFn: () => apiGet<EffectivityDto>(`/api/assessments/effectivity?transactionTypeId=${transactionTypeId}${causeDate ? `&causeDate=${causeDate}` : ''}`),
    enabled: !!transactionTypeId && ready,
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

// The SMV's building tables (docs/analysis/valuation-foundation.md §4.5).
export const useBuildingCosts = () =>
  useQuery({ queryKey: ['building-costs'], queryFn: () => apiGet<BuildingCostDto[]>('/api/building-costs') });
export const useCreateBuildingCost = () =>
  useRuleMutation((r: CreateBuildingCostRequest) => apiPost<BuildingCostDto>('/api/building-costs', r), [['building-costs']]);
export const useApproveBuildingCost = () =>
  useRuleMutation((id: string) => apiPost<BuildingCostDto>(`/api/building-costs/${id}/approve`, {}), [['building-costs']]);
export const useExtraItemCosts = () =>
  useQuery({ queryKey: ['extra-item-costs'], queryFn: () => apiGet<ExtraItemCostDto[]>('/api/extra-item-costs') });
export const useCreateExtraItemCost = () =>
  useRuleMutation((r: CreateExtraItemCostRequest) => apiPost<ExtraItemCostDto>('/api/extra-item-costs', r), [['extra-item-costs']]);
export const useApproveExtraItemCost = () =>
  useRuleMutation((id: string) => apiPost<ExtraItemCostDto>(`/api/extra-item-costs/${id}/approve`, {}), [['extra-item-costs']]);
export const useDepreciationSchedules = () =>
  useQuery({ queryKey: ['depreciation-schedules'], queryFn: () => apiGet<DepreciationScheduleDto[]>('/api/depreciation-schedules') });
export const useCreateDepreciationSchedule = () =>
  useRuleMutation((r: CreateDepreciationScheduleRequest) => apiPost<DepreciationScheduleDto>('/api/depreciation-schedules', r), [['depreciation-schedules']]);
export const useApproveDepreciationSchedule = () =>
  useRuleMutation((id: string) => apiPost<DepreciationScheduleDto>(`/api/depreciation-schedules/${id}/approve`, {}), [['depreciation-schedules']]);

// Exchange rates and price indices for machinery (docs/analysis/valuation-foundation.md §4.6).
export const useExchangeRates = () =>
  useQuery({ queryKey: ['exchange-rates'], queryFn: () => apiGet<ExchangeRateDto[]>('/api/exchange-rates') });
export const useCreateExchangeRate = () =>
  useRuleMutation((r: CreateExchangeRateRequest) => apiPost<ExchangeRateDto>('/api/exchange-rates', r), [['exchange-rates']]);
export const useApproveExchangeRate = () =>
  useRuleMutation((id: string) => apiPost<ExchangeRateDto>(`/api/exchange-rates/${id}/approve`, {}), [['exchange-rates']]);
export const usePriceIndices = () =>
  useQuery({ queryKey: ['price-indices'], queryFn: () => apiGet<PriceIndexDto[]>('/api/price-indices') });
export const useCreatePriceIndex = () =>
  useRuleMutation((r: CreatePriceIndexRequest) => apiPost<PriceIndexDto>('/api/price-indices', r), [['price-indices']]);
export const useApprovePriceIndex = () =>
  useRuleMutation((id: string) => apiPost<PriceIndexDto>(`/api/price-indices/${id}/approve`, {}), [['price-indices']]);
