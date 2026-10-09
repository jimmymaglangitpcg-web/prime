import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  AddTaxDeclarationAnnotationRequest,
  CreateTaxDeclarationRequest,
  TaxDeclarationAnnotationDto,
  TaxDeclarationDto,
} from '../lib/types';

export function useTaxDeclarationsByRpu(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'tax-declarations'],
    queryFn: () => apiGet<TaxDeclarationDto[]>(`/api/rpus/${rpuId}/tax-declarations`),
    enabled: !!rpuId,
  });
}

export function useCreateTaxDeclaration(propertyId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateTaxDeclarationRequest) => apiPost<TaxDeclarationDto>('/api/tax-declarations', request),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
      queryClient.invalidateQueries({ queryKey: ['rpus', variables.rpuId, 'tax-declarations'] });
    },
  });
}

function useInvalidateTds(propertyId: string, rpuId: string | undefined) {
  const queryClient = useQueryClient();
  return () => {
    queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
    queryClient.invalidateQueries({ queryKey: ['rpus', rpuId, 'tax-declarations'] });
  };
}

/**
 * Lifecycle actions (docs/FORMS-REVISION-PLAN.md A4). Reject and the cancellation actions need a reason, except
 * approving a cancellation: an outright cancellation is asked for by one user and decided by another
 * (workflow-security.md Q19).
 */
export type TdAction = 'submit-for-review' | 'approve' | 'reject' | 'request-cancellation' | 'approve-cancellation' | 'reject-cancellation';

function tdActionPath(id: string, action: TdAction, cancellationRequestId?: string) {
  switch (action) {
    case 'request-cancellation':
      return `/api/tax-declarations/${id}/cancellation-requests`;
    case 'approve-cancellation':
      return `/api/tax-declarations/cancellation-requests/${cancellationRequestId}/approve`;
    case 'reject-cancellation':
      return `/api/tax-declarations/cancellation-requests/${cancellationRequestId}/reject`;
    default:
      return `/api/tax-declarations/${id}/${action}`;
  }
}

export function useTdAction(propertyId: string, rpuId: string) {
  const invalidate = useInvalidateTds(propertyId, rpuId);
  return useMutation({
    mutationFn: ({ id, action, reason, cancellationRequestId, rowVersion }:
      { id: string; action: TdAction; reason?: string; cancellationRequestId?: string; rowVersion?: number }) =>
      apiPost<TaxDeclarationDto>(tdActionPath(id, action, cancellationRequestId), reason === undefined ? {} : { reason },
        // The TD's version guards actions on the TD itself; a cancellation decision is on the request's own route.
        { ifMatch: action === 'approve-cancellation' || action === 'reject-cancellation' ? undefined : rowVersion }),
    onSuccess: invalidate,
  });
}

export function useTdAnnotations(tdId: string | undefined) {
  return useQuery({
    queryKey: ['tax-declarations', tdId, 'annotations'],
    queryFn: () => apiGet<TaxDeclarationAnnotationDto[]>(`/api/tax-declarations/${tdId}/annotations`),
    enabled: !!tdId,
  });
}

export function useAddAnnotation(propertyId: string, tdId: string | undefined, rpuId: string | undefined) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateTds(propertyId, rpuId);
  return useMutation({
    mutationFn: (request: AddTaxDeclarationAnnotationRequest) =>
      apiPost<TaxDeclarationAnnotationDto>(`/api/tax-declarations/${tdId}/annotations`, request),
    onSuccess: () => {
      invalidate();
      queryClient.invalidateQueries({ queryKey: ['tax-declarations', tdId, 'annotations'] });
    },
  });
}

export function useLiftAnnotation(propertyId: string, tdId: string | undefined, rpuId: string | undefined) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidateTds(propertyId, rpuId);
  return useMutation({
    mutationFn: ({ id, reason, reference }: { id: string; reason: string; reference?: string }) =>
      apiPost<TaxDeclarationAnnotationDto>(`/api/tax-declaration-annotations/${id}/lift`, { reason, reference }),
    onSuccess: () => {
      invalidate();
      queryClient.invalidateQueries({ queryKey: ['tax-declarations', tdId, 'annotations'] });
    },
  });
}
