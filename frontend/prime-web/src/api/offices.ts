import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { AppUserStatus } from './accounts';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';

// Offices, jurisdictions and office assignments (docs/analysis/province-wide-operation.md §3.1–§3.2).

export type OfficeKind = 'Provincial' | 'Municipal';
export type RecordStatus = 'Active' | 'Inactive';
export type WorkflowStatus = 'Draft' | 'Submitted' | 'PendingReview' | 'Approved' | 'Rejected' | 'Posted' | 'Cancelled' | 'Voided';
export type ApprovalSubjectType = 'Assessment' | 'TaxDeclaration' | 'PropertyTransaction';
export type RpuType = 'Land' | 'Building' | 'Machinery' | 'OtherImprovement';
export type DelegationState = 'Draft' | 'Rejected' | 'Scheduled' | 'InForce' | 'Expired' | 'Revoked';

export interface ApprovalDelegationDto {
  id: string; officeId: string; officeCode: string; officeName: string; delegatingOfficialName: string; delegatingOfficialPosition: string;
  instrumentReference: string; instrumentDate: string; subjectTypes: ApprovalSubjectType[]; propertyKinds: RpuType[];
  validFrom: string; validTo: string; status: WorkflowStatus; state: DelegationState; renewsDelegationId: string | null;
  revokedFrom: string | null; revokedAt: string | null; revokedBy: string | null; revocationReason: string | null; rejectionReason: string | null;
  remarks: string | null; createdBy: string | null; createdAt: string; approvedBy: string | null; approvedAt: string | null;
}
export interface DelegationInput {
  officeId: string; delegatingOfficialName: string; delegatingOfficialPosition: string; instrumentReference: string; instrumentDate: string;
  subjectTypes: ApprovalSubjectType[]; propertyKinds: RpuType[]; validFrom: string; validTo: string; renewsDelegationId: string | null; remarks?: string | null;
}

export const useDelegations = () =>
  useQuery({ queryKey: ['approval-delegations'], queryFn: () => apiGet<ApprovalDelegationDto[]>('/api/approval-delegations') });

export function useDelegationActions() {
  const queryClient = useQueryClient();
  const done = () => queryClient.invalidateQueries({ queryKey: ['approval-delegations'] });
  return {
    create: useMutation({ mutationFn: (input: DelegationInput) => apiPost<ApprovalDelegationDto>('/api/approval-delegations', input), onSuccess: done }),
    approve: useMutation({ mutationFn: (id: string) => apiPost<ApprovalDelegationDto>(`/api/approval-delegations/${id}/approve`, {}), onSuccess: done }),
    reject: useMutation({
      mutationFn: ({ id, reason }: { id: string; reason: string }) => apiPost<ApprovalDelegationDto>(`/api/approval-delegations/${id}/reject`, { reason }),
      onSuccess: done,
    }),
    revoke: useMutation({
      mutationFn: ({ id, revokedFrom, reason }: { id: string; revokedFrom: string; reason: string }) =>
        apiPost<ApprovalDelegationDto>(`/api/approval-delegations/${id}/revoke`, { revokedFrom, reason }),
      onSuccess: done,
    }),
  };
}

export interface OfficeDto {
  /** Row version; sent as If-Match on a write (production-hardening.md §4.4). */
  rowVersion: number;
  id: string; code: string; name: string; kind: OfficeKind; lguName: string | null; headPosition: string | null; address: string | null; contact: string | null; status: RecordStatus;
  /** The Sanggunian whose tax ordinance the office's TDs cite (records-and-forms.md Q8). */
  sanggunianName?: string | null;
}
export interface OfficeJurisdictionDto {
  id: string; officeId: string; officeCode: string; municipalityId: string; municipalityName: string; municipalityPsgcCode: string;
  effectiveDate: string; endDate: string | null; status: WorkflowStatus; legalBasis: string; remarks: string | null;
  createdBy: string | null; createdAt: string; approvedBy: string | null; approvedAt: string | null;
}
export interface OfficeAssignmentDto {
  id: string; appUserId: string; userName: string; officeId: string | null; officeCode: string | null; roles: string[];
  effectiveDate: string; endDate: string | null; status: WorkflowStatus; legalBasis: string; remarks: string | null;
  createdBy: string | null; createdAt: string; approvedBy: string | null; approvedAt: string | null;
}
export interface RoleDto { code: string; name: string }
export interface UserSummaryDto {
  id: string; displayName: string; email: string; status: AppUserStatus; officeCode: string | null; provinceWide: boolean; roles: string[];
  /** Real Estate Appraiser licence printed with the signature (LAM Bk I p.9; records-and-forms.md Q10). */
  reaLicenceNumber?: string | null; reaLicenceValidUntil?: string | null;
}
export interface UserLicenceInput { reaLicenceNumber: string | null; reaLicenceValidUntil: string | null; reason: string }
export interface CurrentUserDto {
  userId: string | null; displayName: string | null; officeId: string | null; officeCode: string | null; officeName: string | null;
  officeKind: OfficeKind | null; assigned: boolean; provinceWide: boolean; roles: string[]; municipalityIds: string[] | null;
  /** What the user's roles allow (docs/analysis/workflow-security.md §4.1). */
  permissions: string[] | null;
  /** Pending until a system administrator approves the sign-up (§4.2). */
  status: AppUserStatus | null;
  /** The roles need a second factor (Q7), and whether this sign-in has one. */
  mfaRequired: boolean; mfaSatisfied: boolean;
  /** Minutes without activity before the browser signs out (Q9). */
  idleMinutes: number;
}
export interface DevUser { key: string; displayName: string; office: string; roles: string[] }

export interface OfficeInput {
  code?: string; name: string; kind?: OfficeKind; lguName?: string | null; headPosition?: string | null; address?: string | null; contact?: string | null;
  status?: RecordStatus; sanggunianName?: string | null;
}
export interface JurisdictionInput { officeId: string; municipalityId: string; effectiveDate: string; legalBasis: string; remarks?: string | null }
export interface AssignmentInput { appUserId: string; officeId: string | null; roles: string[]; effectiveDate: string; legalBasis: string; remarks?: string | null }

export const useOffices = () => useQuery({ queryKey: ['offices'], queryFn: () => apiGet<OfficeDto[]>('/api/offices') });
export const useOfficeJurisdictions = () =>
  useQuery({ queryKey: ['office-jurisdictions'], queryFn: () => apiGet<OfficeJurisdictionDto[]>('/api/office-jurisdictions') });
export const useOfficeAssignments = () =>
  useQuery({ queryKey: ['office-assignments'], queryFn: () => apiGet<OfficeAssignmentDto[]>('/api/office-assignments') });
export const useRoles = () => useQuery({ queryKey: ['roles'], queryFn: () => apiGet<RoleDto[]>('/api/roles') });
export const useUsers = () => useQuery({ queryKey: ['users'], queryFn: () => apiGet<UserSummaryDto[]>('/api/users') });
export const useCurrentUser = () => useQuery({ queryKey: ['me'], queryFn: () => apiGet<CurrentUserDto>('/api/me'), retry: false });
/**
 * Whether the user's roles allow a permission. Only hides what cannot be used: the API refuses it anyway. Unknown while
 * the user is loading, so nothing flickers away.
 */
export function useCan() {
  const { data } = useCurrentUser();
  return (permission: string) => !data?.permissions || data.permissions.includes(permission);
}
/** Development only: the DEMO users the header picker can act as. */
export const useDevUsers = (enabled: boolean) =>
  useQuery({ queryKey: ['dev-users'], queryFn: () => apiGet<DevUser[]>('/api/dev/users'), enabled, retry: false, staleTime: Infinity });

function useInvalidate() {
  const queryClient = useQueryClient();
  return () => Promise.all(['offices', 'office-jurisdictions', 'office-assignments', 'users', 'me'].map((k) => queryClient.invalidateQueries({ queryKey: [k] })));
}

export function useCreateOffice() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (input: OfficeInput) => apiPost<OfficeDto>('/api/offices', input), onSuccess: invalidate });
}
export function useUpdateOffice() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, rowVersion, ...input }: OfficeInput & { id: string; rowVersion?: number }) =>
      apiPut<OfficeDto>(`/api/offices/${id}`, input, { ifMatch: rowVersion }),
    onSuccess: invalidate,
  });
}
export function useUpdateUserLicence() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, ...input }: UserLicenceInput & { id: string }) => apiPut<UserSummaryDto>(`/api/users/${id}/licence`, input),
    onSuccess: invalidate,
  });
}
export function useCreateJurisdiction() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (input: JurisdictionInput) => apiPost<OfficeJurisdictionDto>('/api/office-jurisdictions', input), onSuccess: invalidate });
}
export function useApproveJurisdiction() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => apiPost<OfficeJurisdictionDto>(`/api/office-jurisdictions/${id}/approve`, {}), onSuccess: invalidate });
}
export function useCreateAssignment() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (input: AssignmentInput) => apiPost<OfficeAssignmentDto>('/api/office-assignments', input), onSuccess: invalidate });
}
export function useApproveAssignment() {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (id: string) => apiPost<OfficeAssignmentDto>(`/api/office-assignments/${id}/approve`, {}), onSuccess: invalidate });
}
export function useEndAssignment() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, endDate, reason }: { id: string; endDate: string; reason: string }) =>
      apiPost<OfficeAssignmentDto>(`/api/office-assignments/${id}/end`, { endDate, reason }),
    onSuccess: invalidate,
  });
}
