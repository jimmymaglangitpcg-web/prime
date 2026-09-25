import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { NoticeDto, NoticeServiceMode } from '../lib/types';

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

export const useGenerateNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, (assessmentId: string) => apiPost<NoticeDto>('/api/notices', { assessmentId }));

export const useIssueNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, (id: string) => apiPost<NoticeDto>(`/api/notices/${id}/issue`, {}));

export const useRecordNoticeService = (propertyId: string) =>
  useNoticeMutation(propertyId, ({ id, ...body }: { id: string; serviceMode: NoticeServiceMode; receivedDate: string; servedTo: string; proofReference: string; notes?: string }) =>
    apiPost<NoticeDto>(`/api/notices/${id}/service`, body));

export const useCancelNotice = (propertyId: string) =>
  useNoticeMutation(propertyId, ({ id, reason }: { id: string; reason: string }) => apiPost<NoticeDto>(`/api/notices/${id}/cancel`, { reason }));
