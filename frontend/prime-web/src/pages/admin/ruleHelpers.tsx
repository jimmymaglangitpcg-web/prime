import { Tag, message } from 'antd';
import type { Dayjs } from 'dayjs';
import { ApiRequestError } from '../../lib/apiClient';
import type { LookupDto, WorkflowStatus } from '../../lib/types';

/** Helpers shared by the valuation-rule admin tabs (docs/analysis/value-and-assess.md §3). */
export const errorText = (e: unknown) => (e instanceof ApiRequestError ? e.apiError.message : (e as Error)?.message);
export const day = (d: Dayjs | null | undefined) => (d ? d.format('YYYY-MM-DD') : null);
export const lookup = (rows: LookupDto[]) => rows.map((r) => ({ value: r.id, label: `${r.code} — ${r.name}` }));
export const statusTag = (s: WorkflowStatus) => <Tag color={s === 'Approved' ? 'green' : s === 'Draft' ? 'default' : 'orange'}>{s}</Tag>;
export const period = (from: string, to: string | null) => `${from} → ${to ?? 'open'}`;
export const pct = (v: number) => `${new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 }).format(v)}%`;

export function useToast() {
  const [toast, context] = message.useMessage();
  return { context, fail: (e: unknown) => toast.error(errorText(e)) };
}
