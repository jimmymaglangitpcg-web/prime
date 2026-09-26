import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  CreatePaymentModeRequest, CreateRevenueAccountMappingRequest, OutstandingDto, PaymentCancellationDto, PaymentCancellationStatus,
  PaymentDto, PaymentItemRequest, PaymentModeDto, PaymentQuoteDto, PaymentReplacementRequest, PaymentStatus, PaymentSummaryDto,
  PostPaymentRequest, RevenueAccountMappingDto,
} from '../lib/types';

// Collection (docs/analysis/collection.md §4). Every change to payments also changes
// balances, bills' statements and the day's receipts, so mutations refresh them all.

function useInvalidateCollection() {
  const queryClient = useQueryClient();
  return () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ['payments'] }),
    queryClient.invalidateQueries({ queryKey: ['properties'] }),
  ]);
}

export function useOutstanding(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'outstanding'],
    queryFn: () => apiGet<OutstandingDto>(`/api/properties/${propertyId}/outstanding`),
    enabled: !!propertyId,
  });
}

export function usePropertyPayments(propertyId: string | undefined) {
  return useQuery({
    queryKey: ['properties', propertyId, 'payments'],
    queryFn: () => apiGet<PaymentSummaryDto[]>(`/api/properties/${propertyId}/payments`),
    enabled: !!propertyId,
  });
}

export function usePayment(paymentId: string | undefined) {
  return useQuery({
    queryKey: ['payments', paymentId],
    queryFn: () => apiGet<PaymentDto>(`/api/payments/${paymentId}`),
    enabled: !!paymentId,
  });
}

export function usePayments(date: string | undefined, status?: PaymentStatus) {
  return useQuery({
    queryKey: ['payments', 'list', date, status],
    queryFn: () => apiGet<PaymentSummaryDto[]>('/api/payments', { date, status }),
  });
}

export function useQuotePayment() {
  return useMutation({
    mutationFn: (items: PaymentItemRequest[]) => apiPost<PaymentQuoteDto>('/api/payments/quote', { items }),
  });
}

export function usePostPayment() {
  const invalidate = useInvalidateCollection();
  return useMutation({
    mutationFn: (request: PostPaymentRequest) => apiPost<PaymentDto>('/api/payments', request),
    onSuccess: invalidate,
  });
}

// --- Void, reversal, correction ---

export function usePaymentCancellations(status?: PaymentCancellationStatus) {
  return useQuery({
    queryKey: ['payments', 'cancellations', status],
    queryFn: () => apiGet<PaymentCancellationDto[]>('/api/payments/cancellation-requests', { status }),
  });
}

export function useRequestCancellation() {
  const invalidate = useInvalidateCollection();
  return useMutation({
    mutationFn: ({ paymentId, reason }: { paymentId: string; reason: string }) =>
      apiPost<PaymentCancellationDto>(`/api/payments/${paymentId}/cancellation-requests`, { reason }),
    onSuccess: invalidate,
  });
}

export function useRequestCorrection() {
  const invalidate = useInvalidateCollection();
  return useMutation({
    mutationFn: ({ paymentId, reason, replacement }: { paymentId: string; reason: string; replacement: PaymentReplacementRequest }) =>
      apiPost<PaymentCancellationDto>(`/api/payments/${paymentId}/correction-requests`, { reason, replacement }),
    onSuccess: invalidate,
  });
}

export function useDecideCancellation() {
  const invalidate = useInvalidateCollection();
  return useMutation({
    mutationFn: ({ id, decision, remarks }: { id: string; decision: 'approve' | 'reject'; remarks: string | null }) =>
      apiPost<PaymentCancellationDto>(`/api/payments/cancellation-requests/${id}/${decision}`, { remarks }),
    onSuccess: invalidate,
  });
}

// --- Setup ---

export function usePaymentModes() {
  return useQuery({
    queryKey: ['collection', 'payment-modes'],
    queryFn: () => apiGet<PaymentModeDto[]>('/api/collection/payment-modes'),
  });
}

export function useCreatePaymentMode() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreatePaymentModeRequest) => apiPost<PaymentModeDto>('/api/collection/payment-modes', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['collection', 'payment-modes'] }),
  });
}

export function useAccountMappings() {
  return useQuery({
    queryKey: ['collection', 'account-mappings'],
    queryFn: () => apiGet<RevenueAccountMappingDto[]>('/api/collection/account-mappings'),
  });
}

export function useCreateAccountMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRevenueAccountMappingRequest) => apiPost<RevenueAccountMappingDto>('/api/collection/account-mappings', request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['collection', 'account-mappings'] }),
  });
}

export function useApproveAccountMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiPost<RevenueAccountMappingDto>(`/api/collection/account-mappings/${id}/approve`, {}),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['collection', 'account-mappings'] }),
  });
}
