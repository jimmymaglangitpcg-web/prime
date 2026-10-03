import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch, apiGet, apiPost, apiPut } from '../lib/apiClient';
import type { PagedResult } from '../lib/types';
import type {
  BuildingPermitDto, MachineryRegistrationDto, MarketDataReportKind, MarketDataReportRunDto, MarketDataReview, MarketImportResultDto,
  MarketTransactionDto, SaveBuildingPermitRequest, SaveMachineryRegistrationRequest, SaveMarketTransactionRequest,
} from '../lib/marketDataTypes';

/** Market data for the SMV (docs/analysis/smv-preparation-general-revision.md §4.1). */
export interface MarketSearch {
  municipalityId?: string;
  classificationId?: string;
  review?: MarketDataReview;
  from?: string;
  to?: string;
  search?: string;
  page: number;
  pageSize: number;
}

export function useMarketTransactions(q: MarketSearch) {
  return useQuery({
    queryKey: ['market-data', 'transactions', q],
    queryFn: () => apiGet<PagedResult<MarketTransactionDto>>('/api/market-data/transactions', { ...q }),
  });
}

function useMarketMutation<V, R>(fn: (v: V) => Promise<R>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['market-data'] }) });
}

export const useSaveMarketTransaction = () =>
  useMarketMutation(({ id, ...body }: SaveMarketTransactionRequest & { id?: string }) =>
    id ? apiPut<MarketTransactionDto>(`/api/market-data/transactions/${id}`, body) : apiPost<MarketTransactionDto>('/api/market-data/transactions', body));

export const useReviewMarketTransaction = () =>
  useMarketMutation(({ id, ...body }: {
    id: string; review: Exclude<MarketDataReview, 'Unreviewed'>; exclusionReason?: string | null; fieldValidatedOn?: string | null; note?: string | null;
  }) => apiPost<MarketTransactionDto>(`/api/market-data/transactions/${id}/review`, body));

export const useCancelMarketRecord = (path: 'transactions' | 'building-permits' | 'machinery-registrations') =>
  useMarketMutation(({ id, reason }: { id: string; reason: string }) => apiPost<unknown>(`/api/market-data/${path}/${id}/cancel`, { reason }));

function upload(path: string, file: File) {
  const body = new FormData();
  body.append('file', file);
  return apiFetch<MarketImportResultDto>(path, { method: 'POST', body });
}

export const usePreviewMarketImport = () => useMutation({ mutationFn: (file: File) => upload('/api/market-data/transactions/import/preview', file) });

export const useMarketImport = () =>
  useMarketMutation(({ file, fingerprint }: { file: File; fingerprint: string }) =>
    upload(`/api/market-data/transactions/import?fingerprint=${encodeURIComponent(fingerprint)}`, file));

export interface AbstractSearch {
  municipalityId?: string;
  unlinkedOnly?: boolean;
  search?: string;
  page: number;
  pageSize: number;
}

export function useBuildingPermits(q: AbstractSearch) {
  return useQuery({
    queryKey: ['market-data', 'building-permits', q],
    queryFn: () => apiGet<PagedResult<BuildingPermitDto>>('/api/market-data/building-permits', { ...q }),
  });
}

export const useSaveBuildingPermit = () =>
  useMarketMutation(({ id, ...body }: SaveBuildingPermitRequest & { id?: string }) =>
    id ? apiPut<BuildingPermitDto>(`/api/market-data/building-permits/${id}`, body) : apiPost<BuildingPermitDto>('/api/market-data/building-permits', body));

export const useLinkBuildingPermit = () =>
  useMarketMutation(({ id, targetId }: { id: string; targetId: string | null }) =>
    apiPost<BuildingPermitDto>(`/api/market-data/building-permits/${id}/link`, { targetId }));

export function useMachineryRegistrations(q: AbstractSearch) {
  return useQuery({
    queryKey: ['market-data', 'machinery-registrations', q],
    queryFn: () => apiGet<PagedResult<MachineryRegistrationDto>>('/api/market-data/machinery-registrations', { ...q }),
  });
}

export const useSaveMachineryRegistration = () =>
  useMarketMutation(({ id, ...body }: SaveMachineryRegistrationRequest & { id?: string }) =>
    id ? apiPut<MachineryRegistrationDto>(`/api/market-data/machinery-registrations/${id}`, body)
      : apiPost<MachineryRegistrationDto>('/api/market-data/machinery-registrations', body));

export const useLinkMachineryRegistration = () =>
  useMarketMutation(({ id, targetId }: { id: string; targetId: string | null }) =>
    apiPost<MachineryRegistrationDto>(`/api/market-data/machinery-registrations/${id}/link`, { targetId }));

export function useMarketReports() {
  return useQuery({ queryKey: ['market-data', 'reports'], queryFn: () => apiGet<MarketDataReportRunDto[]>('/api/market-data/reports') });
}

export const useCreateMarketReport = () =>
  useMarketMutation((body: { kind: MarketDataReportKind; municipalityId: string; fromDate: string; toDate: string; remarks?: string }) =>
    apiPost<MarketDataReportRunDto>('/api/market-data/reports', body));
