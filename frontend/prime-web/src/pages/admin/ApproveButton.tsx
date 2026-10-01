import { Button } from 'antd';
import type { WorkflowStatus } from '../../lib/types';

/** The approve button of a rule; the creator cannot approve (maker-checker, CLAUDE.md §46). */
export function ApproveButton({ status, onApprove, pending }: { status: WorkflowStatus; onApprove: () => void; pending: boolean }) {
  return status === 'Draft' ? <Button size="small" loading={pending} onClick={onApprove}>Approve</Button> : null;
}
