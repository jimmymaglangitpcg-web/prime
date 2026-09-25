import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { AppraisalRecordDto, AssessmentSummaryDto } from '../lib/types';

export function useRpuAssessments(rpuId: string | undefined) {
  return useQuery({
    queryKey: ['rpus', rpuId, 'assessments'],
    queryFn: () => apiGet<AssessmentSummaryDto[]>(`/api/rpus/${rpuId}/assessments`),
    enabled: !!rpuId,
  });
}

/** The data a FAAS shows for one assessment (docs/FORMS-REVISION-PLAN.md A7). */
export function useAppraisalRecord(assessmentId: string | undefined) {
  return useQuery({
    queryKey: ['assessments', assessmentId, 'appraisal-record'],
    queryFn: () => apiGet<AppraisalRecordDto>(`/api/assessments/${assessmentId}/appraisal-record`),
    enabled: !!assessmentId,
  });
}
