import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../lib/apiClient';
import type {
  ApprovalChainDto,
  CreateApprovalChainRequest,
  CreateFormDefinitionRequest,
  CreateNumberingSchemeRequest,
  FormDefinitionDto,
  FormPreviewDto,
  IssuedFormDto,
  NumberingSchemeDto,
} from '../lib/types';

// --- Issuing and viewing forms (docs/FORMS-REVISION-PLAN.md §4.1–§4.3) ---

export function useIssuedForm(id: string | undefined) {
  return useQuery({
    queryKey: ['issued-forms', id],
    queryFn: () => apiGet<IssuedFormDto>(`/api/issued-forms/${id}`),
    enabled: !!id,
  });
}

export function useFormPreview(formCode: string | undefined, subjectId: string | undefined) {
  return useQuery({
    queryKey: ['form-preview', formCode, subjectId],
    queryFn: () => apiPost<FormPreviewDto>('/api/forms/preview', { formCode, subjectId }),
    enabled: !!formCode && !!subjectId,
    gcTime: 0,
  });
}

export function useIssueForm() {
  return useMutation({
    mutationFn: (request: { formCode: string; subjectId: string }) => apiPost<IssuedFormDto>('/api/forms/issue', request),
  });
}

// --- Administration: numbering, form versions, approval chains ---

function useAdminList<T>(key: string, path: string) {
  return useQuery({ queryKey: ['admin', key], queryFn: () => apiGet<T[]>(path) });
}

function useAdminMutation<TVariables, TResult>(key: string, mutationFn: (variables: TVariables) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', key] }) });
}

export const useNumberingSchemes = () => useAdminList<NumberingSchemeDto>('numbering', '/api/numbering-schemes');
export const useCreateNumberingScheme = () =>
  useAdminMutation('numbering', (r: CreateNumberingSchemeRequest) => apiPost<NumberingSchemeDto>('/api/numbering-schemes', r));
export const useApproveNumberingScheme = () =>
  useAdminMutation('numbering', (id: string) => apiPost<NumberingSchemeDto>(`/api/numbering-schemes/${id}/approve`, {}));

export const useFormDefinitions = () => useAdminList<FormDefinitionDto>('forms', '/api/forms/definitions');
export const useFormDefinition = (id: string | undefined) =>
  useQuery({ queryKey: ['admin', 'forms', id], queryFn: () => apiGet<FormDefinitionDto>(`/api/forms/definitions/${id}`), enabled: !!id });
export const useCreateFormDefinition = () =>
  useAdminMutation('forms', (r: CreateFormDefinitionRequest) => apiPost<FormDefinitionDto>('/api/forms/definitions', r));
export const useApproveFormDefinition = () =>
  useAdminMutation('forms', (id: string) => apiPost<FormDefinitionDto>(`/api/forms/definitions/${id}/approve`, {}));

export const useApprovalChains = () => useAdminList<ApprovalChainDto>('chains', '/api/approval-chains');
export const useCreateApprovalChain = () =>
  useAdminMutation('chains', (r: CreateApprovalChainRequest) => apiPost<ApprovalChainDto>('/api/approval-chains', r));
export const useApproveApprovalChain = () =>
  useAdminMutation('chains', (id: string) => apiPost<ApprovalChainDto>(`/api/approval-chains/${id}/approve`, {}));
