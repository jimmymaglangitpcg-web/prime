import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type {
  SmvConsultationMode, SmvPreparationDto, SmvPreparationEventKind, SmvPreparationSummaryDto,
} from '../lib/smvPreparationTypes';

/** SMV preparation work files (docs/analysis/smv-preparation-general-revision.md §4.2). */
export function useSmvPreparations() {
  return useQuery({ queryKey: ['smv-preparation'], queryFn: () => apiGet<SmvPreparationSummaryDto[]>('/api/smv/preparations') });
}

export function useSmvPreparation(id: string | undefined) {
  return useQuery({ queryKey: ['smv-preparation', id], queryFn: () => apiGet<SmvPreparationDto>(`/api/smv/preparations/${id}`), enabled: !!id });
}

function usePreparationMutation<T>(fn: (body: T) => Promise<SmvPreparationDto>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (dto) => {
      qc.setQueryData(['smv-preparation', dto.id], dto);
      qc.invalidateQueries({ queryKey: ['smv-preparation'], exact: true });
      qc.invalidateQueries({ queryKey: ['smv'] });
    },
  });
}

export interface CreateSmvPreparationBody {
  revisionYear: number; title: string; dateOfValuation: string | null; baseValuationDate: string | null; plannedEffectivityDate: string;
  municipalityIds: string[]; notes: string | null;
}

export const useCreateSmvPreparation = () =>
  usePreparationMutation((b: CreateSmvPreparationBody) => apiPost<SmvPreparationDto>('/api/smv/preparations', b));

export const useUpdateSmvPreparation = (id: string) =>
  usePreparationMutation((b: { title: string; dateOfValuation: string | null; baseValuationDate: string | null; plannedEffectivityDate: string; notes: string | null }) =>
    apiPut<SmvPreparationDto>(`/api/smv/preparations/${id}`, b));

export const useAddSmvConsultation = (id: string) =>
  usePreparationMutation((b: { heldOn: string; mode: SmvConsultationMode; venue: string | null; attendance: number | null; minutesReference: string | null; notes: string | null }) =>
    apiPost<SmvPreparationDto>(`/api/smv/preparations/${id}/consultations`, b));

export const useRecordSmvPreparationEvent = (id: string) =>
  usePreparationMutation((b: { kind: SmvPreparationEventKind; occurredOn: string; reference: string | null; note: string | null }) =>
    apiPost<SmvPreparationDto>(`/api/smv/preparations/${id}/events`, b));

export const useCancelSmvPreparation = (id: string) =>
  usePreparationMutation((b: { reason: string }) => apiPost<SmvPreparationDto>(`/api/smv/preparations/${id}/cancel`, b));
