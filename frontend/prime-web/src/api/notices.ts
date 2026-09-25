import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { NoticeCandidateDto, NoticeDto, NoticeReason, NoticeServiceMode } from '../lib/types';

export function usePropertyNotices(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'notices'],
    queryFn: () => apiGet<NoticeDto[]>(`/api/properties/${propertyId}/notices`),
    enabled: !!propertyId,
  });
}

function useNoticeMutation<T>(propertyId: string, fn: (v: T) => Promise<NoticeDto>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['properties', propertyId, 'notices'] }) });
}

/** Reason omitted: derived from the values (LGC §223); or one of the MRPAAO's descriptive reasons. */
export const useGenerateNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, ({ assessmentId, reason }: { assessmentId: string; reason?: NoticeReason }) =>
    apiPost<NoticeDto>('/api/notices', { assessmentId, reason }));

/** One notice to one declared owner for several of the owner's assessments (MRPAAO Att. 10). */
export const useGenerateCombinedNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, (body: { taxpayerId: string; assessmentIds: string[] }) => apiPost<NoticeDto>('/api/notices/combined', body));

export function useNoticeCandidates(taxpayerId: string | undefined) {
  return useQuery({
    queryKey: ['notice-candidates', taxpayerId],
    queryFn: () => apiGet<NoticeCandidateDto[]>('/api/notices/candidates', { taxpayerId }),
    enabled: !!taxpayerId,
  });
}

export const useIssueNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, (id: string) => apiPost<NoticeDto>(`/api/notices/${id}/issue`, {}));

export const useRecordNoticeService = (propertyId: string) =>
  useNoticeMutation(propertyId, ({ id, ...body }: { id: string; serviceMode: NoticeServiceMode; receivedDate: string; servedTo: string; proofReference: string; notes?: string }) =>
    apiPost<NoticeDto>(`/api/notices/${id}/service`, body));

export const useCancelNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, ({ id, reason }: { id: string; reason: string }) => apiPost<NoticeDto>(`/api/notices/${id}/cancel`, { reason }));
