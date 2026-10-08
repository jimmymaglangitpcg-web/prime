import { useEffect, useRef } from 'react';
import { useQuery } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type { PagedResult } from '../lib/types';

/** The audit trail (CLAUDE.md §48; docs/analysis/workflow-security.md §4.3). Read-only; `audit.view`. */
export type AuditAction =
  | 'Create' | 'Update' | 'Delete' | 'Approve' | 'Reject' | 'Post' | 'Void' | 'Cancel' | 'Reverse' | 'Login' | 'Logout' | 'Export';

export const auditActions: AuditAction[] = ['Create', 'Update', 'Delete', 'Approve', 'Reject', 'Post', 'Void', 'Cancel', 'Reverse', 'Login', 'Logout', 'Export'];

export interface AuditLogDto {
  id: string; timestamp: string; userId: string | null; userName: string | null; module: string; tableName: string; recordId: string;
  parentTableName: string | null; parentRecordId: string | null; action: AuditAction; oldValue: string | null; newValue: string | null;
  reason: string | null; ipAddress: string | null;
}

export interface AuditLogFilter {
  userId?: string; tableName?: string; recordId?: string; includeChildren?: boolean; propertyId?: string; action?: AuditAction; module?: string;
  from?: string; to?: string; page?: number; pageSize?: number;
}

export const useAuditLogs = (filter: AuditLogFilter, enabled = true) =>
  useQuery({
    queryKey: ['audit-logs', filter],
    queryFn: () => apiGet<PagedResult<AuditLogDto>>('/api/audit-logs', { ...filter }),
    enabled,
    placeholderData: (previous) => previous,
  });

export const useAuditTables = () => useQuery({ queryKey: ['audit-logs', 'tables'], queryFn: () => apiGet<string[]>('/api/audit-logs/tables') });

export type ExportFormat = 'Print' | 'Csv' | 'Excel' | 'Pdf';

/**
 * Reports a print or download made in the browser as an EXPORT audit row. Never blocks or fails the print: a lost
 * report is logged to the console only.
 */
export function recordExport(tableName: string, recordId: string | null | undefined, what: string, format: ExportFormat = 'Print') {
  apiPost<boolean>('/api/audit/exports', { tableName, recordId: recordId ?? null, what: what.slice(0, 200), format }).catch((e) =>
    console.warn('The export could not be recorded in the audit trail', e),
  );
}

export interface PrintSubject { tableName: string; recordId?: string | null; what: string }

/**
 * Records every print of the page (the Print button or the browser's own print command, through `beforeprint`) as an
 * EXPORT row. `subject` is read when the print happens; null while there is nothing to print.
 */
export function usePrintAudit(subject: PrintSubject | null) {
  const latest = useRef(subject);
  useEffect(() => {
    latest.current = subject;
  });
  useEffect(() => {
    const onPrint = () => {
      const s = latest.current;
      if (s) {
        recordExport(s.tableName, s.recordId, s.what, 'Print');
      }
    };
    window.addEventListener('beforeprint', onPrint);
    return () => window.removeEventListener('beforeprint', onPrint);
  }, []);
}
