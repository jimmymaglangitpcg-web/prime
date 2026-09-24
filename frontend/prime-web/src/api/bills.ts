import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { GenerateBillRequest, StatementOfAccountDto, TaxBillDto } from '../lib/types';

export function usePropertyBills(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'bills'],
    queryFn: () => apiGet<TaxBillDto[]>(`/api/properties/${propertyId}/bills`),
    enabled: !!propertyId,
  });
}

export function useStatementOfAccount(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'statement-of-account'],
    queryFn: () => apiGet<StatementOfAccountDto>(`/api/properties/${propertyId}/statement-of-account`),
    enabled: !!propertyId,
  });
}

function useBillMutation<TVariables>(propertyId: string, mutationFn: (variables: TVariables) => Promise<TaxBillDto>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['properties', propertyId] }),
  });
}

export function useGenerateBill(propertyId: string) {
  return useBillMutation(propertyId, (request: GenerateBillRequest) => apiPost<TaxBillDto>('/api/bills', request));
}

export function usePostBill(propertyId: string) {
  return useBillMutation(propertyId, (billId: string) => apiPost<TaxBillDto>(`/api/bills/${billId}/post`, {}));
}

export function useCancelBill(propertyId: string) {
  return useBillMutation(propertyId, ({ billId, reason }: { billId: string; reason: string }) =>
    apiPost<TaxBillDto>(`/api/bills/${billId}/cancel`, { reason }),
  );
}
