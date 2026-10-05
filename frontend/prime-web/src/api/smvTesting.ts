import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { PagedResult } from '../lib/types';
import type {
  CreateValuationTestRequest, SmvSimulationResultDto, SmvSimulationRunDto, StartSmvSimulationRequest, ValuationTestDto, ValuationTestSaleDto,
} from '../lib/smvTestingTypes';

/** Simulations of a proposed SMV (docs/analysis/smv-preparation-general-revision.md §4.3). */
export function useSmvSimulations() {
  return useQuery({ queryKey: ['smv-simulation'], queryFn: () => apiGet<SmvSimulationRunDto[]>('/api/smv/simulations') });
}

const active = (r: SmvSimulationRunDto | undefined) => r?.status === 'Queued' || r?.status === 'Running';

/** Polls every 3 s while the run is queued or running. */
export function useSmvSimulation(id: string | undefined) {
  return useQuery({
    queryKey: ['smv-simulation', id],
    queryFn: () => apiGet<SmvSimulationRunDto>(`/api/smv/simulations/${id}`),
    enabled: !!id,
    refetchInterval: (q) => (active(q.state.data) ? 3000 : false),
  });
}

export interface SimulationResultSearch { page: number; pageSize: number; failed?: boolean; classificationChanged?: boolean; pin?: string }

/** `progress` (the run's status and count) re-reads the results whenever the run moves on, including its last batch. */
export function useSmvSimulationResults(id: string, q: SimulationResultSearch, poll: boolean, progress: string) {
  return useQuery({
    queryKey: ['smv-simulation', id, 'results', q, progress],
    queryFn: () => apiGet<PagedResult<SmvSimulationResultDto>>(`/api/smv/simulations/${id}/results`, { ...q }),
    refetchInterval: poll ? 3000 : false,
  });
}

export function useStartSmvSimulation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: StartSmvSimulationRequest) => apiPost<SmvSimulationRunDto>('/api/smv/simulations', r),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['smv-simulation'] }),
  });
}

/** Valuation tests of an SMV against accepted land sales. */
export function useValuationTests() {
  return useQuery({ queryKey: ['valuation-test'], queryFn: () => apiGet<ValuationTestDto[]>('/api/smv/valuation-tests') });
}

export function useValuationTest(id: string | undefined) {
  return useQuery({ queryKey: ['valuation-test', id], queryFn: () => apiGet<ValuationTestDto>(`/api/smv/valuation-tests/${id}`), enabled: !!id });
}

export function useValuationTestSales(id: string | undefined) {
  return useQuery({
    queryKey: ['valuation-test', id, 'sales'],
    queryFn: () => apiGet<ValuationTestSaleDto[]>(`/api/smv/valuation-tests/${id}/sales`),
    enabled: !!id,
  });
}

export function useCreateValuationTest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: CreateValuationTestRequest) => apiPost<ValuationTestDto>('/api/smv/valuation-tests', r),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['valuation-test'] }),
  });
}
