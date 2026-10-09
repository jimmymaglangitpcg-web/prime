import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';

// Records whose next approval step the signed-in user may sign (docs/analysis/province-wide-operation.md §3.4).

export interface ApprovalQueueItemDto {
  /** Row version; sent as If-Match on a write (production-hardening.md §4.4). */
  rowVersion: number;
  subjectType: 'Assessment' | 'TaxDeclaration' | 'PropertyTransaction';
  subjectId: string;
  propertyId: string;
  pin: string;
  reference: string;
  stepLabel: string;
  /** The delegation the signature would be given under, if any. */
  underDelegation: string | null;
  createdAt: string;
}

export const useApprovalQueue = (municipalityId?: string) =>
  useQuery({
    queryKey: ['approvals', 'awaiting', municipalityId ?? null],
    queryFn: () => apiGet<ApprovalQueueItemDto[]>('/api/approvals/awaiting', { municipalityId }),
  });
