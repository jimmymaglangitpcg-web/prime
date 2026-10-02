import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type { WorkflowStatus } from '../lib/types';

/** Territorial changes and barangay parts (docs/analysis/identification-numbering.md §4.3). */
export type TerritorialChangeKind = 'CreatedLgu' | 'TransferredTerritory';
export type TerritorialChangePinMode = 'KeepParcelNumbers' | 'TemporaryPins';
export interface TerritorialChangeDto {
  id: string; kind: TerritorialChangeKind; legalBasis: string; effectiveDate: string; pinMode: TerritorialChangePinMode; status: WorkflowStatus;
  runStatus: 'Queued' | 'Running' | 'Completed' | 'Failed' | null; totalCount: number; processedCount: number; failedCount: number;
  createdAt: string; approvedAt: string | null; completedAt: string | null; remarks: string | null;
  mappings: { sourceBarangayId: string; sourceBarangayName: string; targetBarangayId: string; targetBarangayName: string }[];
  items: { propertyId: string; oldPin: string; newPin: string | null; status: 'Pending' | 'Done' | 'Failed'; error: string | null }[];
}
export interface CreateTerritorialChangeRequest {
  kind: TerritorialChangeKind; legalBasis: string; effectiveDate: string; pinMode: TerritorialChangePinMode;
  mappings: { sourceBarangayId: string; targetBarangayId: string }[]; remarks: string | null;
}
export interface BarangayPartDto { sequence: number; barangayId: string; barangayName: string; barangayIndex: string | null; area: number; assessedValueShare: number }

export const useTerritorialChanges = () =>
  useQuery({ queryKey: ['territorial-changes'], queryFn: () => apiGet<TerritorialChangeDto[]>('/api/territorial-changes') });
export const useTerritorialChange = (id: string | undefined) =>
  useQuery({ queryKey: ['territorial-changes', id], queryFn: () => apiGet<TerritorialChangeDto>(`/api/territorial-changes/${id}`), enabled: !!id,
    refetchInterval: (q) => (q.state.data?.runStatus === 'Running' || q.state.data?.runStatus === 'Queued' ? 2000 : false) });

function useChangeMutation<V>(fn: (v: V) => Promise<TerritorialChangeDto>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['territorial-changes'] }) });
}
export const useCreateTerritorialChange = () =>
  useChangeMutation((r: CreateTerritorialChangeRequest) => apiPost<TerritorialChangeDto>('/api/territorial-changes', r));
export const useApproveTerritorialChange = () =>
  useChangeMutation((id: string) => apiPost<TerritorialChangeDto>(`/api/territorial-changes/${id}/approve`, {}));
export const useResumeTerritorialChange = () =>
  useChangeMutation((id: string) => apiPost<TerritorialChangeDto>(`/api/territorial-changes/${id}/resume`, {}));

export const useBarangayParts = (propertyId: string) =>
  useQuery({ queryKey: ['properties', propertyId, 'barangay-parts'], queryFn: () => apiGet<BarangayPartDto[]>(`/api/properties/${propertyId}/barangay-parts`) });
export function useSetBarangayParts(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (r: { parts: { barangayId: string; area: number; assessedValueShare: number }[]; reason: string }) =>
      apiPut<BarangayPartDto[]>(`/api/properties/${propertyId}/barangay-parts`, r),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['properties', propertyId, 'barangay-parts'] }),
  });
}
