import { useMutation, useQuery } from '@tanstack/react-query';
import { apiDownload, apiGet, apiPost } from '../lib/apiClient';

/** Reports (CLAUDE.md §57; docs/analysis/reporting.md §4.1). */
export type ReportColumnType = 'Text' | 'Integer' | 'Money' | 'Area' | 'Date';
export type ReportParameter = 'AsOf' | 'Municipality' | 'Barangay';

export interface ReportColumn { key: string; title: string; type: ReportColumnType }

export interface ReportDefinitionDto {
  code: string;
  title: string;
  group: string;
  description: string;
  parameters: ReportParameter[];
  columns: ReportColumn[];
}

export interface ReportRunRequest { asOf?: string | null; municipalityId?: string | null; barangayId?: string | null }

export type ReportCell = string | number | null;

export interface ReportPreviewDto {
  code: string;
  title: string;
  columns: ReportColumn[];
  rows: ReportCell[][];
  totals: ReportCell[] | null;
  totalRows: number;
  page: number;
  pageSize: number;
  notes: string[];
  parameterLines: string[];
  syncRowLimit: number;
}

export const useReports = () =>
  useQuery({ queryKey: ['reports'], queryFn: () => apiGet<ReportDefinitionDto[]>('/api/reports'), staleTime: 5 * 60 * 1000 });

export const useReportPreview = (code: string | undefined, parameters: ReportRunRequest | undefined, page: number, pageSize: number) =>
  useQuery({
    queryKey: ['reports', code, 'preview', parameters, page, pageSize],
    queryFn: () => apiPost<ReportPreviewDto>(`/api/reports/${code}/preview`, { parameters, page, pageSize }),
    enabled: !!code && !!parameters,
    placeholderData: (previous) => (previous?.code === code ? previous : undefined),
  });

export type ReportFileFormat = 'csv' | 'xlsx';

/** Downloads the whole report; the API writes the EXPORT audit row. */
export const useReportDownload = () =>
  useMutation({
    mutationFn: ({ code, parameters, format }: { code: string; parameters: ReportRunRequest; format: ReportFileFormat }) => {
      const query = new URLSearchParams({ format });
      Object.entries(parameters).forEach(([k, v]) => v && query.set(k, v));
      return apiDownload(`/api/reports/${code}/export?${query}`, `${code.toLowerCase()}.${format}`);
    },
  });
