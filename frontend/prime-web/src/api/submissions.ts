import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';

// What municipal offices send the province (docs/analysis/province-wide-operation.md §3.7, LP-6).

export interface ApprovedDocumentDto {
  taxDeclarationId: string; taxDeclarationNumber: string; kind: 'Land' | 'Building' | 'Machinery' | 'OtherImprovement';
  propertyId: string; pin: string; municipalityId: string; municipality: string; barangay: string;
  approvedAt: string; approvedBy: string | null; currentStatus: string;
  /** The frozen printed copies issued at approval; null when no form was in force. */
  taxDeclarationFormId: string | null; faasFormId: string | null;
}

export type RollStatus = 'Submitted' | 'Acknowledged' | 'Returned';

export interface RollSubmissionItemDto {
  id: string; kind: 'AssessmentRollTaxable' | 'AssessmentRollExempt'; barangayId: string; barangay: string; entryCount: number;
  registerRunId: string; issuedFormId: string;
}

export interface RollSubmissionDto {
  id: string; municipalityId: string; municipality: string; officeId: string; officeCode: string; year: number; month: number;
  status: RollStatus; remarks: string | null; submittedAt: string; submittedBy: string | null;
  reviewedAt: string | null; reviewedBy: string | null; reviewRemarks: string | null; items: RollSubmissionItemDto[];
}

export const useApprovedDocuments = (from: string, to: string, municipalityId?: string) =>
  useQuery({
    queryKey: ['submissions', 'approved', from, to, municipalityId ?? null],
    queryFn: () => apiGet<ApprovedDocumentDto[]>('/api/submissions/approved-documents', { from, to, municipalityId }),
  });

export const useRollSubmissions = (municipalityId?: string) =>
  useQuery({
    queryKey: ['submissions', 'rolls', municipalityId ?? null],
    queryFn: () => apiGet<RollSubmissionDto[]>('/api/submissions/rolls', { municipalityId }),
  });

export function useRollActions() {
  const client = useQueryClient();
  const done = () => client.invalidateQueries({ queryKey: ['submissions', 'rolls'] });
  return {
    submit: useMutation({
      mutationFn: (input: { municipalityId: string; year: number; month: number; remarks?: string }) =>
        apiPost<RollSubmissionDto>('/api/submissions/rolls', input),
      onSuccess: done,
    }),
    acknowledge: useMutation({
      mutationFn: ({ id, remarks }: { id: string; remarks?: string }) => apiPost<RollSubmissionDto>(`/api/submissions/rolls/${id}/acknowledge`, { remarks }),
      onSuccess: done,
    }),
    returnRoll: useMutation({
      mutationFn: ({ id, remarks }: { id: string; remarks: string }) => apiPost<RollSubmissionDto>(`/api/submissions/rolls/${id}/return`, { remarks }),
      onSuccess: done,
    }),
  };
}
