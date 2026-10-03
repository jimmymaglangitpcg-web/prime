import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type { PagedResult, WorkflowStatus } from '../lib/types';
import type {
  GeneralRevisionDto, GeneralRevisionInspectionFilter, GeneralRevisionItemDto, GeneralRevisionItemStatus, GeneralRevisionRunDto,
  GeneralRevisionRunIssueDto, GeneralRevisionRunMode, GeneralRevisionSummaryDto, GeneralRevisionSuspensionKind,
  ChecklistStepDefinitionDto, GeneralRevisionGate, GeneralRevisionReadinessDto, GeneralRevisionNoticeDto, GeneralRevisionRegisterKind, GeneralRevisionRegisterRunDto, NoticeServiceMode, NoticeServiceResultDto, NoticeStatus, RollGateDto,
} from '../lib/generalRevisionTypes';

/** General revision programmes (docs/analysis/smv-preparation-general-revision.md §4.6). */
export function useGeneralRevisions() {
  return useQuery({ queryKey: ['general-revision'], queryFn: () => apiGet<GeneralRevisionSummaryDto[]>('/api/general-revision/programmes') });
}

/** Polls every 3 s while a run is queued or running. */
export function useGeneralRevision(id: string | undefined) {
  return useQuery({
    queryKey: ['general-revision', id],
    queryFn: () => apiGet<GeneralRevisionDto>(`/api/general-revision/programmes/${id}`),
    enabled: !!id,
    refetchInterval: (q) => (q.state.data?.runActive ? 3000 : false),
  });
}

export interface ItemSearch {
  barangayId?: string;
  status?: GeneralRevisionItemStatus;
  assessmentStatus?: WorkflowStatus;
  taxDeclarationStatus?: WorkflowStatus;
  inspection?: GeneralRevisionInspectionFilter;
  search?: string;
  page: number;
  pageSize: number;
}

export function useGeneralRevisionItems(id: string, q: ItemSearch, poll: boolean) {
  return useQuery({
    queryKey: ['general-revision', id, 'items', q],
    queryFn: () => apiGet<PagedResult<GeneralRevisionItemDto>>(`/api/general-revision/programmes/${id}/items`, { ...q }),
    refetchInterval: poll ? 3000 : false,
  });
}

function useGrMutation<V, R>(fn: (v: V) => Promise<R>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['general-revision'] }) });
}

export const useCreateGeneralRevision = () =>
  useGrMutation((body: {
    revisionYear: number; effectiveDate: string; smvId: string; municipalityIds: string[]; officeOrderReference?: string | null;
    ordinanceReference?: string | null; description?: string | null;
  }) => apiPost<GeneralRevisionDto>('/api/general-revision/programmes', body));

export const useUpdateGeneralRevisionReferences = (id: string) =>
  useGrMutation((body: { officeOrderReference: string | null; ordinanceReference: string | null; description: string | null }) =>
    apiPut<GeneralRevisionDto>(`/api/general-revision/programmes/${id}/references`, body));

export const useStartGeneralRevisionRun = (id: string) =>
  useGrMutation((body: { mode: GeneralRevisionRunMode; itemIds?: string[]; includeFailed?: boolean; barangayId?: string; reason?: string }) =>
    apiPost<GeneralRevisionRunDto>(`/api/general-revision/programmes/${id}/runs`, body));

export function useGeneralRevisionRunIssues(id: string, runId: string | undefined) {
  return useQuery({
    queryKey: ['general-revision', id, 'runs', runId, 'issues'],
    queryFn: () => apiGet<GeneralRevisionRunIssueDto[]>(`/api/general-revision/programmes/${id}/runs/${runId}/issues`),
    enabled: !!runId,
  });
}

/** Field review (GRI 9–11). */
export const useAssignGeneralRevisionInspection = (id: string) =>
  useGrMutation((body: { itemIds: string[]; inspectorId: string | null; route: string | null }) =>
    apiPost<number>(`/api/general-revision/programmes/${id}/inspections/assign`, body));

export const useRecordGeneralRevisionInspection = (id: string) =>
  useGrMutation(({ itemId, ...body }: { itemId: string; inspectedOn: string; notes: string | null; foundChanges: boolean }) =>
    apiPost<GeneralRevisionItemDto>(`/api/general-revision/programmes/${id}/items/${itemId}/inspection`, body));

export const useSuspendGeneralRevision = (id: string) =>
  useGrMutation((body: { kind: GeneralRevisionSuspensionKind; fromDate: string; reference: string; remarks?: string | null }) =>
    apiPost<GeneralRevisionDto>(`/api/general-revision/programmes/${id}/suspensions`, body));

export const useLiftGeneralRevisionSuspension = (id: string) =>
  useGrMutation(({ suspensionId, liftedOn }: { suspensionId: string; liftedOn: string }) =>
    apiPost<GeneralRevisionDto>(`/api/general-revision/programmes/${id}/suspensions/${suspensionId}/lift`, { liftedOn }));

export const useCancelGeneralRevision = (id: string) =>
  useGrMutation((reason: string) => apiPost<GeneralRevisionDto>(`/api/general-revision/programmes/${id}/cancel`, { reason }));

/** L6-6c: the revision's notices, the roll gate and its register runs. */
export function useGeneralRevisionNotices(id: string, q: { status?: NoticeStatus; search?: string; page: number; pageSize: number }) {
  return useQuery({
    queryKey: ['general-revision', id, 'notices', q],
    queryFn: () => apiGet<PagedResult<GeneralRevisionNoticeDto>>(`/api/general-revision/programmes/${id}/notices`, { ...q }),
  });
}

export const useRecordGeneralRevisionNoticeService = (id: string) =>
  useGrMutation((body: { noticeIds: string[]; serviceMode: NoticeServiceMode; receivedDate: string; proofReference: string; servedTo: string | null; notes: string | null; sentDate: string | null }) =>
    apiPost<NoticeServiceResultDto>(`/api/general-revision/programmes/${id}/notices/service`, body));

export function useRollGates(id: string) {
  return useQuery({ queryKey: ['general-revision', id, 'roll-gates'], queryFn: () => apiGet<RollGateDto[]>(`/api/general-revision/programmes/${id}/roll-gates`) });
}

export function useGeneralRevisionRegisterRuns(id: string) {
  return useQuery({
    queryKey: ['general-revision', id, 'register-runs'],
    queryFn: () => apiGet<GeneralRevisionRegisterRunDto[]>(`/api/general-revision/programmes/${id}/register-runs`),
  });
}

export const useCreateGeneralRevisionRegisterRuns = (id: string) =>
  useGrMutation((body: { kind: GeneralRevisionRegisterKind; barangayId?: string; asOf?: string; overrideReason?: string }) =>
    apiPost<GeneralRevisionRegisterRunDto[]>(`/api/general-revision/programmes/${id}/register-runs`, body));

/** L6-6c: units taken out, readiness (gates and checklist), completion, and the checklist template. */
export const useExcludeGeneralRevisionItem = (id: string) =>
  useGrMutation(({ itemId, reason }: { itemId: string; reason: string }) =>
    apiPost<GeneralRevisionItemDto>(`/api/general-revision/programmes/${id}/items/${itemId}/exclude`, { reason }));

export const useIncludeGeneralRevisionItem = (id: string) =>
  useGrMutation((itemId: string) => apiPost<GeneralRevisionItemDto>(`/api/general-revision/programmes/${id}/items/${itemId}/include`, {}));

export function useGeneralRevisionReadiness(id: string) {
  return useQuery({
    queryKey: ['general-revision', id, 'readiness'],
    queryFn: () => apiGet<GeneralRevisionReadinessDto>(`/api/general-revision/programmes/${id}/readiness`),
  });
}

export const useLoadGeneralRevisionChecklist = (id: string) =>
  useGrMutation(() => apiPost<GeneralRevisionReadinessDto>(`/api/general-revision/programmes/${id}/checklist`, {}));

export const useCompleteGeneralRevisionStep = (id: string) =>
  useGrMutation(({ stepId, ...body }: { stepId: string; completedOn: string; evidence: string | null }) =>
    apiPost<GeneralRevisionReadinessDto>(`/api/general-revision/programmes/${id}/checklist/${stepId}/complete`, body));

export const useCompleteGeneralRevision = (id: string) =>
  useGrMutation(() => apiPost<GeneralRevisionDto>(`/api/general-revision/programmes/${id}/complete`, {}));

export function useChecklistStepDefinitions() {
  return useQuery({
    queryKey: ['general-revision', 'checklist-steps'],
    queryFn: () => apiGet<ChecklistStepDefinitionDto[]>('/api/general-revision/checklist-steps'),
  });
}

export const useCreateChecklistStepDefinition = () =>
  useGrMutation((body: {
    legalBasis: string; effectiveDate: string; remarks: string | null; code: string; sequence: number; title: string; description: string | null;
    gate: GeneralRevisionGate | null;
  }) => apiPost<ChecklistStepDefinitionDto>('/api/general-revision/checklist-steps', body));

export const useApproveChecklistStepDefinition = () =>
  useGrMutation((stepId: string) => apiPost<ChecklistStepDefinitionDto>(`/api/general-revision/checklist-steps/${stepId}/approve`, {}));
