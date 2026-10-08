import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { WorkflowStatus } from '../lib/types';

/** The role–permission matrix and its changes (docs/analysis/workflow-security.md §4.1). */
export interface PermissionDto { code: string; name: string; module: string }
export interface RoleGrantsDto { roleCode: string; roleName: string; permissions: string[] }
export interface RolePermissionChangeDto {
  id: string; roleCode: string; roleName: string; grant: string[]; revoke: string[]; reason: string; status: WorkflowStatus;
  createdBy: string | null; createdAt: string; decidedBy: string | null; decidedAt: string | null; decisionReason: string | null;
}
export interface PermissionMatrixDto { permissions: PermissionDto[]; roles: RoleGrantsDto[]; changes: RolePermissionChangeDto[] }

export const useRolePermissions = () =>
  useQuery({ queryKey: ['role-permissions'], queryFn: () => apiGet<PermissionMatrixDto>('/api/role-permissions') });

function useChange<T>(fn: (v: T) => Promise<RolePermissionChangeDto>) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: fn,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['role-permissions'] });
      qc.invalidateQueries({ queryKey: ['me'] });
    },
  });
}

export const useProposeRolePermissions = () =>
  useChange((r: { roleCode: string; permissions: string[]; reason: string }) => apiPost<RolePermissionChangeDto>('/api/role-permissions/changes', r));
export const useApproveRolePermissionChange = () =>
  useChange((id: string) => apiPost<RolePermissionChangeDto>(`/api/role-permissions/changes/${id}/approve`, {}));
export const useRejectRolePermissionChange = () =>
  useChange((v: { id: string; reason: string }) => apiPost<RolePermissionChangeDto>(`/api/role-permissions/changes/${v.id}/reject`, { reason: v.reason }));
