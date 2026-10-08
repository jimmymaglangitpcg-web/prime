import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { WorkflowStatus } from '../lib/types';
import type { OfficeKind, RoleDto } from './offices';

/** Accounts: sign-up requests and disabling users (docs/analysis/workflow-security.md §4.2). */
export type AppUserStatus = 'Active' | 'Inactive' | 'Pending';

export interface SignUpOfficeOption { id: string; code: string; name: string; kind: OfficeKind }
export interface SignUpOptionsDto { offices: SignUpOfficeOption[]; roles: RoleDto[]; provinceWideRoles: string[] }
export interface SignUpRequestDto {
  id: string; appUserId: string; email: string; fullName: string; position: string; requestedOfficeId: string | null;
  requestedOfficeCode: string | null; requestedOfficeName: string | null; requestedRoles: string[]; note: string | null; status: WorkflowStatus;
  createdAt: string; decidedBy: string | null; decidedByName: string | null; decidedAt: string | null; decisionReason: string | null;
  officeAssignmentId: string | null;
}
export interface SignUpInput { fullName: string; position: string; officeId: string | null; roles: string[]; note?: string | null }
export interface ApproveSignUpInput { id: string; officeId: string | null; roles: string[]; remarks?: string | null }

export interface UserStatusChangeDto {
  id: string; appUserId: string; userName: string; newStatus: AppUserStatus; reason: string; status: WorkflowStatus;
  createdBy: string | null; createdByName: string | null; createdAt: string; decidedBy: string | null; decidedByName: string | null;
  decidedAt: string | null; decisionReason: string | null;
}

export const useSignUpOptions = () => useQuery({ queryKey: ['sign-up', 'options'], queryFn: () => apiGet<SignUpOptionsDto>('/api/sign-up/options') });
export const useMySignUpRequests = () => useQuery({ queryKey: ['sign-up', 'mine'], queryFn: () => apiGet<SignUpRequestDto[]>('/api/sign-up/mine') });
export const useSignUpRequests = (status: WorkflowStatus | undefined, enabled = true) =>
  useQuery({ queryKey: ['sign-up-requests', status ?? 'all'], enabled, queryFn: () => apiGet<SignUpRequestDto[]>('/api/sign-up-requests', { status }) });
export const useUserStatusChanges = () =>
  useQuery({ queryKey: ['user-status-changes'], queryFn: () => apiGet<UserStatusChangeDto[]>('/api/user-status-changes') });

function useInvalidating<T, R>(fn: (v: T) => Promise<R>, keys: string[][]) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => Promise.all(keys.map((queryKey) => qc.invalidateQueries({ queryKey }))) });
}

export const useSubmitSignUp = () => useInvalidating((v: SignUpInput) => apiPost<SignUpRequestDto>('/api/sign-up', v), [['sign-up'], ['me']]);
export const useApproveSignUp = () =>
  useInvalidating(({ id, ...v }: ApproveSignUpInput) => apiPost<SignUpRequestDto>(`/api/sign-up-requests/${id}/approve`, v),
    [['sign-up-requests'], ['users'], ['office-assignments']]);
export const useRejectSignUp = () =>
  useInvalidating((v: { id: string; reason: string }) => apiPost<SignUpRequestDto>(`/api/sign-up-requests/${v.id}/reject`, { reason: v.reason }),
    [['sign-up-requests']]);

export const useProposeUserStatus = () =>
  useInvalidating((v: { userId: string; newStatus: AppUserStatus; reason: string }) =>
    apiPost<UserStatusChangeDto>(`/api/users/${v.userId}/status-changes`, { newStatus: v.newStatus, reason: v.reason }), [['user-status-changes']]);
export const useApproveUserStatus = () =>
  useInvalidating((id: string) => apiPost<UserStatusChangeDto>(`/api/user-status-changes/${id}/approve`, {}), [['user-status-changes'], ['users']]);
export const useRejectUserStatus = () =>
  useInvalidating((v: { id: string; reason: string }) => apiPost<UserStatusChangeDto>(`/api/user-status-changes/${v.id}/reject`, { reason: v.reason }),
    [['user-status-changes']]);
