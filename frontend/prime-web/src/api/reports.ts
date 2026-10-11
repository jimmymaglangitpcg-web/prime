import { useMutation, useQuery } from '@tanstack/react-query';
import { apiDownload, apiGet, apiPost } from '../lib/apiClient';

/** Reports (CLAUDE.md §57; docs/analysis/reporting.md §4.1). */
export type ReportColumnType = 'Text' | 'Integer' | 'Money' | 'Area' | 'Date';
export type ReportParameter = 'AsOf' | 'Municipality' | 'Barangay' | 'Period' | 'TdStatus' | 'TransactionCode' | 'Pin' | 'Month' | 'HalfYear';

export interface ReportColumn { key: string; title: string; type: ReportColumnType }

export interface ReportDefinitionDto {
  code: string;
  title: string;
  group: string;
  description: string;
  parameters: ReportParameter[];
  columns: ReportColumn[];
}

export interface ReportRunRequest {
  asOf?: string | null;
  municipalityId?: string | null;
  barangayId?: string | null;
  fromDate?: string | null;
  toDate?: string | null;
  status?: string | null;
  transactionCode?: string | null;
  pin?: string | null;
}

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
  /** The file's header block (LGU, office, title, parameters, run by); only when asked for (the print view). */
  headerLines?: string[] | null;
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

/** The URL of the print view of a run (pages/reports/ReportPrintPage.tsx). */
export const printUrl = (code: string, run: ReportRunRequest) =>
  `/reports/print?${new URLSearchParams({ report: code, run: JSON.stringify(run) })}`;

/** A whole report for the print view, read a page of 200 at a time up to the download limit. */
export const useReportPrint = (code: string | undefined, parameters: ReportRunRequest | undefined) =>
  useQuery({
    queryKey: ['reports', code, 'print', parameters],
    enabled: !!code && !!parameters,
    staleTime: Infinity,
    queryFn: async () => {
      const pageSize = 200;
      const first = await apiPost<ReportPreviewDto>(`/api/reports/${code}/preview`, { parameters, page: 1, pageSize, withHeader: true });
      const rows = [...first.rows];
      const pages = Math.ceil(Math.min(first.totalRows, first.syncRowLimit) / pageSize);
      for (let page = 2; page <= pages; page++) {
        rows.push(...(await apiPost<ReportPreviewDto>(`/api/reports/${code}/preview`, { parameters, page, pageSize })).rows);
      }
      return { ...first, rows };
    },
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

/** A register run or a sales report run (step R3): downloaded from its issued snapshot, else read now; the API audits it. */
export type RunExportKind = 'register-runs' | 'sales-report-runs';

export const useRunDownload = () =>
  useMutation({
    mutationFn: ({ kind, id, format }: { kind: RunExportKind; id: string; format: ReportFileFormat }) =>
      apiDownload(`/api/reports/${kind}/${id}/export?format=${format}`, `${kind}-${id}.${format}`),
  });
