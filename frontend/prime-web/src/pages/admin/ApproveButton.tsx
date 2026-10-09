import { Button } from 'antd';
import { useCan } from '../../api/offices';
import type { WorkflowStatus } from '../../lib/types';

/**
 * The approve button of a rule, shown only to a user whose roles allow approving configuration (config.approve); the
 * creator cannot approve (maker-checker, CLAUDE.md §46), which the API enforces.
 */
export function ApproveButton({ status, onApprove, pending }: { status: WorkflowStatus; onApprove: () => void; pending: boolean }) {
  const can = useCan();
  return status === 'Draft' && can('config.approve') ? <Button size="small" loading={pending} onClick={onApprove}>Approve</Button> : null;
}
