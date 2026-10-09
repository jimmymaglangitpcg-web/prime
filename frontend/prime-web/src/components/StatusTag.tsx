import { Tag } from 'antd';
import type { WorkflowStatus } from '../lib/types';

/** The workflow statuses (CLAUDE.md §45) as people read them: "PendingReview" → "Pending review". */
const workflowStatusLabel = (status: string) =>
  status.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/ ([A-Z])/g, (_, c: string) => ` ${c.toLowerCase()}`);

const workflowStatusColor: Record<WorkflowStatus, string> = {
  Draft: 'default',
  Submitted: 'blue',
  PendingReview: 'gold',
  Approved: 'green',
  Rejected: 'red',
  Posted: 'green',
  Cancelled: 'red',
  Voided: 'red',
};

/** One look for a workflow status wherever a record shows it (production-hardening.md §4.8). */
export function WorkflowStatusTag({ status }: { status: WorkflowStatus }) {
  return <Tag color={workflowStatusColor[status] ?? 'default'}>{workflowStatusLabel(status)}</Tag>;
}
