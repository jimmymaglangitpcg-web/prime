import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { WorkflowStatus } from '../lib/types';

/** Report row maps and system parameters (docs/analysis/reporting.md §10, Q15–Q16): configuration under maker-checker. */
export type ReportRowSection = 'Taxable' | 'Exempt' | 'Restricted' | 'IdleLand';

export interface RestrictionGroupSpec { code: string; label: string; annotationTypes: string[] }

export interface ReportRowSpec {
  section: ReportRowSection;
  code: string;
  label: string;
  restriction?: string | null;
  classifications?: string[] | null;
  actualUses?: string[] | null;
  exemptionTypes?: string[] | null;
  splitsBuildings?: boolean;
  others?: boolean;
}

export interface ReportRowMapDefinition { restrictions?: RestrictionGroupSpec[] | null; rows: ReportRowSpec[] }

export interface ReportRowMapDto {
  id: string;
  code: string;
  name: string;
  definition: ReportRowMapDefinition;
  legalBasis: string;
  effectiveDate: string;
  endDate: string | null;
  status: WorkflowStatus;
  remarks: string | null;
}

export interface CreateReportRowMapRequest {
  code: string;
  name: string;
  definition: unknown;
  legalBasis: string;
  effectiveDate: string;
  remarks: string | null;
}

export interface SystemParameterDefinition { code: string; name: string; description: string; unit: string }

export interface SystemParameterDto {
  id: string;
  code: string;
  name: string;
  unit: string;
  value: number;
  legalBasis: string;
  effectiveDate: string;
  endDate: string | null;
  status: WorkflowStatus;
  description: string | null;
}

export interface CreateSystemParameterRequest {
  code: string;
  value: number;
  legalBasis: string;
  effectiveDate: string;
  description: string | null;
  remarks: string | null;
}

export const useReportRowMaps = () => useQuery({ queryKey: ['report-row-maps'], queryFn: () => apiGet<ReportRowMapDto[]>('/api/report-row-maps') });

export const useSystemParameters = () => useQuery({ queryKey: ['system-parameters'], queryFn: () => apiGet<SystemParameterDto[]>('/api/system-parameters') });

export const useSystemParameterCatalog = () =>
  useQuery({ queryKey: ['system-parameters', 'catalog'], queryFn: () => apiGet<SystemParameterDefinition[]>('/api/system-parameters/catalog'), staleTime: Infinity });

function useConfigMutation<T, R>(fn: (value: T) => Promise<R>, key: string) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: [key] }) });
}

export const useCreateReportRowMap = () =>
  useConfigMutation((r: CreateReportRowMapRequest) => apiPost<ReportRowMapDto>('/api/report-row-maps', r), 'report-row-maps');

export const useApproveReportRowMap = () =>
  useConfigMutation((id: string) => apiPost<ReportRowMapDto>(`/api/report-row-maps/${id}/approve`, {}), 'report-row-maps');

export const useCreateSystemParameter = () =>
  useConfigMutation((r: CreateSystemParameterRequest) => apiPost<SystemParameterDto>('/api/system-parameters', r), 'system-parameters');

export const useApproveSystemParameter = () =>
  useConfigMutation((id: string) => apiPost<SystemParameterDto>(`/api/system-parameters/${id}/approve`, {}), 'system-parameters');
