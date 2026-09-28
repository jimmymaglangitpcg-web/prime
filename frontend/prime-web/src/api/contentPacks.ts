import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiFetch, apiGet, apiPost } from '../lib/apiClient';

// LGU content packs (docs/analysis/lgu-content-pack.md; CLAUDE.md §118): list, upload, preview (dry run), import, history.

export type ContentIssueSeverity = 'Error' | 'Warning';
export type ContentChangeAction = 'New' | 'Changed';

export interface ContentPackInfo { pack: string; hasManifest: boolean }
export interface ContentIssueDto { severity: ContentIssueSeverity; code: string; message: string; line: number | null; field: string | null }
export interface ContentFieldChangeDto { field: string; from: string | null; to: string | null }
export interface ContentChangeDto { key: string; name: string; action: ContentChangeAction; fields: ContentFieldChangeDto[] }
export interface ContentFilePreviewDto {
  kind: string;
  lookup: string | null;
  path: string;
  source: string | null;
  sha256: string | null;
  supported: boolean;
  rows: number;
  new: number;
  changed: number;
  unchanged: number;
  missingFromPack: number;
  missingKeys: string[];
  changes: ContentChangeDto[];
  issues: ContentIssueDto[];
}
export interface ContentPackPreviewDto {
  pack: string;
  version: string | null;
  description: string | null;
  manifestSha256: string | null;
  fingerprint: string | null;
  canImport: boolean;
  errorCount: number;
  warningCount: number;
  issues: ContentIssueDto[];
  files: ContentFilePreviewDto[];
}
export interface ContentImportFileDto { kind: string; lookup: string | null; path: string; sha256: string | null; source: string | null; created: number; changed: number; unchanged: number }
export interface ContentImportDto {
  id: string;
  pack: string;
  packVersion: string;
  description: string | null;
  manifestSha256: string;
  fingerprint: string;
  importedAt: string;
  importedBy: string;
  importedByName: string | null;
  createdCount: number;
  changedCount: number;
  files: ContentImportFileDto[];
  warnings: ContentIssueDto[];
}
export interface ContentImportItemDto {
  sequence: number;
  entityType: string;
  entityId: string;
  key: string;
  action: 'Created' | 'Changed';
  changes: ContentFieldChangeDto[];
  source: string;
  filePath: string;
  line: number;
}
export interface ContentImportResultDto { applied: boolean; message: string; import: ContentImportDto | null }
export interface Paged<T> { items: T[]; totalCount: number; page: number; pageSize: number; totalPages: number }

export const useContentPacks = () =>
  useQuery({ queryKey: ['content-packs'], queryFn: () => apiGet<ContentPackInfo[]>('/api/content-packs'), retry: false });

/** A dry run: nothing is written. Run on demand (a mutation), since it reads the pack's files afresh each time. */
export const usePreviewContentPack = () =>
  useMutation({ mutationFn: (pack: string) => apiPost<ContentPackPreviewDto>(`/api/content-packs/${encodeURIComponent(pack)}/preview`, {}) });

export function useUploadContentPack() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => {
      const body = new FormData();
      body.append('file', file);
      return apiFetch<ContentPackInfo>('/api/content-packs/upload', { method: 'POST', body });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['content-packs'] }),
  });
}

export function useImportContentPack() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ pack, fingerprint }: { pack: string; fingerprint: string }) =>
      apiPost<ContentImportResultDto>(`/api/content-packs/${encodeURIComponent(pack)}/import`, { fingerprint }),
    // Geography, lookups and configuration drafts may all have changed.
    onSuccess: () => queryClient.invalidateQueries(),
  });
}

export const useContentImports = (page: number) =>
  useQuery({ queryKey: ['content-imports', page], queryFn: () => apiGet<Paged<ContentImportDto>>('/api/content-imports', { page, pageSize: 10 }) });

export const useContentImportItems = (id: string, page: number) =>
  useQuery({
    queryKey: ['content-imports', id, 'items', page],
    queryFn: () => apiGet<Paged<ContentImportItemDto>>(`/api/content-imports/${id}/items`, { page, pageSize: 20 }),
  });
