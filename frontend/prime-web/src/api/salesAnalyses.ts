import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch, apiGet, apiPost, apiPut } from '../lib/apiClient';
import type {
  AreaMeasure, SalesAnalysisDto, SalesAnalysisGroupInput, SalesAnalysisSummaryDto, TimeAdjustmentFactorDto,
} from '../lib/salesAnalysisTypes';

/** Time-adjustment factors and sales analyses of an SMV preparation (docs/analysis/smv-preparation-general-revision.md §4.2). */
export function useTimeFactors(preparationId: string) {
  return useQuery({
    queryKey: ['smv-preparation', preparationId, 'time-factors'],
    queryFn: () => apiGet<TimeAdjustmentFactorDto[]>(`/api/smv/preparations/${preparationId}/time-factors`),
  });
}

function useFactorMutation<T>(preparationId: string, fn: (b: T) => Promise<TimeAdjustmentFactorDto[]>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (rows) => {
      qc.setQueryData(['smv-preparation', preparationId, 'time-factors'], rows);
      qc.invalidateQueries({ queryKey: ['sales-analysis'] });
      qc.invalidateQueries({ queryKey: ['smv-preparation', preparationId, 'analyses'] });
    },
  });
}

export const useAddTimeFactor = (preparationId: string) =>
  useFactorMutation(preparationId, (b: { periodFrom: string; periodTo: string; factor: number; source: string }) =>
    apiPost<TimeAdjustmentFactorDto[]>(`/api/smv/preparations/${preparationId}/time-factors`, b));

export const useRemoveTimeFactor = (preparationId: string) =>
  useFactorMutation(preparationId, (factorId: string) =>
    apiFetch<TimeAdjustmentFactorDto[]>(`/api/smv/preparations/${preparationId}/time-factors/${factorId}`, { method: 'DELETE' }));

export function useSalesAnalyses(preparationId: string) {
  return useQuery({
    queryKey: ['smv-preparation', preparationId, 'analyses'],
    queryFn: () => apiGet<SalesAnalysisSummaryDto[]>(`/api/smv/preparations/${preparationId}/analyses`),
  });
}

export interface CreateSalesAnalysisBody {
  classificationId: string; actualUseId: string | null; municipalityIds: string[]; salesFrom: string | null; salesTo: string | null;
  areaUnit: AreaMeasure; roundingIncrement: number | null; rangeWidthPercent: number | null; notes: string | null;
}

export function useCreateSalesAnalysis(preparationId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (b: CreateSalesAnalysisBody) => apiPost<SalesAnalysisDto>(`/api/smv/preparations/${preparationId}/analyses`, b),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['smv-preparation', preparationId, 'analyses'] }),
  });
}

export function useSalesAnalysis(id: string | undefined) {
  return useQuery({ queryKey: ['sales-analysis', id], queryFn: () => apiGet<SalesAnalysisDto>(`/api/smv/analyses/${id}`), enabled: !!id });
}

function useAnalysisMutation<T>(id: string, fn: (b: T) => Promise<SalesAnalysisDto>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (dto) => {
      qc.setQueryData(['sales-analysis', id], dto);
      qc.invalidateQueries({ queryKey: ['smv-preparation', dto.smvPreparationId] });
      qc.invalidateQueries({ queryKey: ['smv'] });
    },
  });
}

export const useUpdateSalesAnalysis = (id: string) =>
  useAnalysisMutation(id, (b: { roundingIncrement: number; rangeWidthPercent: number | null; notes: string | null }) => apiPut<SalesAnalysisDto>(`/api/smv/analyses/${id}`, b));
export const useRefreshSalesAnalysis = (id: string) =>
  useAnalysisMutation(id, () => apiPost<SalesAnalysisDto>(`/api/smv/analyses/${id}/refresh`, {}));
export const useUpdateAnalysisSale = (id: string) =>
  useAnalysisMutation(id, ({ saleId, ...b }: { saleId: string; leftOut: boolean; exclusionReason: string | null; otherAdjustmentPercent: number; note: string | null }) =>
    apiPut<SalesAnalysisDto>(`/api/smv/analyses/${id}/sales/${saleId}`, b));
export const useSetSalesAnalysisGroups = (id: string) =>
  useAnalysisMutation(id, (groups: SalesAnalysisGroupInput[]) => apiPut<SalesAnalysisDto>(`/api/smv/analyses/${id}/groups`, { groups }));
export const useAdoptSalesAnalysisGroup = (id: string) =>
  useAnalysisMutation(id, (groupId: string) => apiPost<SalesAnalysisDto>(`/api/smv/analyses/${id}/groups/${groupId}/adopt`, {}));
