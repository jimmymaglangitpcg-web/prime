import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../lib/apiClient';
import type { WorkflowStatus } from '../lib/types';
import type { GeneralRevisionStatus } from '../lib/generalRevisionTypes';

/** The dashboard (CLAUDE.md §55; docs/analysis/reporting.md §4.3): the user's jurisdiction as of today. */
export interface DashboardFiguresDto {
  properties: number;
  parcels: number;
  propertiesWithFaas: number;
  unitsInForce: number;
  landAreaSqm: number;
  unconvertedLandUnits: number;
  taxableMarketValue: number;
  exemptMarketValue: number;
  taxableAssessedValue: number;
  exemptAssessedValue: number;
}

export interface DashboardGroupDto {
  key: string | null;
  label: string;
  detail: string | null;
  properties: number;
  marketValue: number;
  assessedValue: number;
  isOthers: boolean;
}

export interface DashboardGeneralRevisionDto {
  id: string;
  revisionYear: number;
  status: GeneralRevisionStatus;
  scope: string;
  items: number;
  valued: number;
  failed: number;
  excluded: number;
  posted: number;
  declared: number;
}

export interface DashboardTransactionDto {
  id: string;
  propertyId: string;
  pin: string;
  transactionNumber: string | null;
  typeName: string;
  status: WorkflowStatus;
  createdAt: string;
}

export interface DashboardApprovalDto {
  kind: 'TaxDeclaration' | 'Assessment' | 'Transaction';
  id: string;
  propertyId: string;
  pin: string;
  reference: string;
  approvedBy: string | null;
  approvedAt: string;
}

export interface DashboardDto {
  asOf: string;
  /** When the cached figures were read (cached a minute per jurisdiction, Q8). */
  computedAt: string;
  figures: DashboardFiguresDto;
  byClassification: DashboardGroupDto[];
  byBarangay: DashboardGroupDto[];
  generalRevisions: DashboardGeneralRevisionDto[];
  recentTransactions: DashboardTransactionDto[];
  recentApprovals: DashboardApprovalDto[];
}

export const useDashboard = () =>
  useQuery({ queryKey: ['dashboard'], queryFn: () => apiGet<DashboardDto>('/api/dashboard'), staleTime: 60 * 1000 });
