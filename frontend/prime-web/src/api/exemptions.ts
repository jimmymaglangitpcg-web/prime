import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { ClaimExemptionRequest, CreateExemptionTypeRequest, ExemptionTypeDto, PropertyExemptionDto } from '../lib/types';

export const useExemptionTypes = (inForceOnly: boolean) =>
  useQuery({ queryKey: ['exemption-types', inForceOnly], queryFn: () => apiGet<ExemptionTypeDto[]>(`/api/exemptions/types?inForceOnly=${inForceOnly}`) });

function useTypeMutation<T>(fn: (v: T) => Promise<ExemptionTypeDto>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exemption-types'] }) });
}
export const useCreateExemptionType = () => useTypeMutation((r: CreateExemptionTypeRequest) => apiPost<ExemptionTypeDto>('/api/exemptions/types', r));
export const useApproveExemptionType = () => useTypeMutation((id: string) => apiPost<ExemptionTypeDto>(`/api/exemptions/types/${id}/approve`, {}));

export const usePropertyExemptions = (propertyId: string | undefined) =>
  useQuery({
    queryKey: ['exemptions', 'property', propertyId],
    queryFn: () => apiGet<PropertyExemptionDto[]>(`/api/properties/${propertyId}/exemptions`),
    enabled: !!propertyId,
  });

export const useOpenExemptions = () => useQuery({ queryKey: ['exemptions', 'open'], queryFn: () => apiGet<PropertyExemptionDto[]>('/api/exemptions') });

/** Any claim change refreshes every exemption list; a decision may open a reassessment, so the unit's assessments too. */
function useClaimMutation<T>(fn: (v: T) => Promise<PropertyExemptionDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: (c) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ['exemptions'] }),
      queryClient.invalidateQueries({ queryKey: ['rpus', c.rpuId, 'assessments'] }),
    ]),
  });
}
export const useClaimExemption = () => useClaimMutation((r: ClaimExemptionRequest) => apiPost<PropertyExemptionDto>('/api/exemptions', r));
export const useAddExemptionEvidence = () => useClaimMutation((v: { id: string; description: string; referenceNumber: string | null; documentDate: string | null; receivedOn: string | null }) =>
  apiPost<PropertyExemptionDto>(`/api/exemptions/${v.id}/evidence`, v));
export const useApproveExemption = () => useClaimMutation((v: { id: string; effectiveDate: string; expiryDate: string | null; remarks: string | null }) =>
  apiPost<PropertyExemptionDto>(`/api/exemptions/${v.id}/approve`, v));
export const useRejectExemption = () => useClaimMutation((v: { id: string; reason: string }) => apiPost<PropertyExemptionDto>(`/api/exemptions/${v.id}/reject`, v));
export const useEndExemption = () => useClaimMutation((v: { id: string; endedOn: string; reason: string }) => apiPost<PropertyExemptionDto>(`/api/exemptions/${v.id}/end`, v));
