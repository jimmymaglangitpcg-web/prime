import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { CreateTransactionTypeRequest, OpenTransactionRequest, PropertyTransactionDto, TransactionTypeDto } from '../lib/types';

export function useTransactionTypes(inForceOnly: boolean) {
  return useQuery({
    queryKey: ['transaction-types', inForceOnly],
    queryFn: () => apiGet<TransactionTypeDto[]>(`/api/transactions/types?inForceOnly=${inForceOnly}`),
  });
}

function useTypeMutation<T>(fn: (v: T) => Promise<TransactionTypeDto>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transaction-types'] }) });
}

export const useCreateTransactionType = () =>
  useTypeMutation((r: CreateTransactionTypeRequest) => apiPost<TransactionTypeDto>('/api/transactions/types', r));
export const useApproveTransactionType = () =>
  useTypeMutation((id: string) => apiPost<TransactionTypeDto>(`/api/transactions/types/${id}/approve`, {}));

export function usePropertyTransactions(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'transactions'],
    queryFn: () => apiGet<PropertyTransactionDto[]>(`/api/properties/${propertyId}/transactions`),
    enabled: !!propertyId,
  });
}

/** Any change to a transaction can change the property's owners and TDs, so the whole profile is refreshed. */
function useTxMutation<T>(propertyId: string, fn: (v: T) => Promise<PropertyTransactionDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['properties', propertyId] });
      queryClient.invalidateQueries({ queryKey: ['rpus'] });
    },
  });
}

export const useOpenTransaction = (propertyId: string) =>
  useTxMutation(propertyId, (r: OpenTransactionRequest) => apiPost<PropertyTransactionDto>('/api/transactions', r));

export const useSatisfyRequirement = (propertyId: string) =>
  useTxMutation(propertyId, ({ id, requirementId, evidenceReference, note }: { id: string; requirementId: string; evidenceReference: string; note?: string }) =>
    apiPost<PropertyTransactionDto>(`/api/transactions/${id}/requirements/${requirementId}/satisfy`, { evidenceReference, note }));

export type TransactionAction = 'submit' | 'approve' | 'reject' | 'withdraw';

export const useTransactionAction = (propertyId: string) =>
  useTxMutation(propertyId, ({ id, action, reason }: { id: string; action: TransactionAction; reason?: string }) =>
    apiPost<PropertyTransactionDto>(`/api/transactions/${id}/${action}`, reason === undefined ? {} : { reason }));
