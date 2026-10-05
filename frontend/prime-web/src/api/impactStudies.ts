import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type { PagedResult } from '../lib/types';

/** Revenue compliance and tax impact studies (docs/analysis/smv-preparation-general-revision.md §4.5). */
export interface RateInput { label: string; ratePercent: number; source: string }
export interface LevelInput { classificationId: string; actualUseId: string | null; lowerValue: number; upperValue: number | null; percent: number }
export interface OptionInput { name: string; ratePercent: number; description: string | null; levels: LevelInput[] }
export interface SaveStudyRequest {
  title: string; smvSimulationRunId: string; year: number; referenceDate: string | null; actualCollection: number | null; discounts: number | null;
  collectionSource: string | null; includeAllTaxableUnits: boolean; notes: string | null; rates: RateInput[]; options: OptionInput[];
}

export interface TaxImpactSummary {
  units: number; currentTax: number; scenarioTax: number; lower: number; higher: number; unchanged: number;
  smallestIncrease: number | null; medianIncrease: number | null; largestIncrease: number | null; largestIncreasePercent: number | null;
  reclassified: number; reclassifiedTaxChange: number;
}
export interface Compliance {
  taxableAssessedValue: number; ratePercent: number; taxPotential: number; actualCollection: number; discounts: number; totalCollection: number;
  taxGap: number; complianceRatePercent: number | null; collectionEfficiencyPercent: number | null;
}
export interface StudyDto {
  id: string; title: string; smvSimulationRunId: string; smvReference: string; smvRevisionYear: number; simulationAsOf: string; municipalities: string[];
  year: number; referenceDate: string; actualCollection: number | null; discounts: number | null; collectionSource: string | null;
  includeAllTaxableUnits: boolean; notes: string | null; rates: RateInput[]; existingRatePercent: number;
  options: { sequence: number; name: string; ratePercent: number; description: string | null;
    levels: (LevelInput & { classificationName: string; actualUseName: string | null })[] }[];
  compliance: Compliance | null; scenarios: { key: string; name: string; ratePercent: number; summary: TaxImpactSummary; rowsAtExistingLevel: number }[];
  unitsLeftOut: number; editable: boolean; warnings: string[]; createdAt: string;
}
export interface StudySummaryDto { id: string; title: string; year: number; smvReference: string; simulationAsOf: string; optionCount: number; createdAt: string }
export interface TaxImpactUnitDto {
  rpuId: string; propertyId: string; pin: string; rpuNumber: string; rpuType: string; currentClassification: string | null; newClassification: string | null;
  reclassified: boolean; currentTaxableAssessedValue: number; newTaxableAssessedValue: number; currentTax: number; newTax: number; optionTaxes: number[];
}

export const useImpactStudies = () =>
  useQuery({ queryKey: ['impact-study'], queryFn: () => apiGet<StudySummaryDto[]>('/api/smv/impact-studies') });

export const useImpactStudy = (id: string | undefined) =>
  useQuery({ queryKey: ['impact-study', id], queryFn: () => apiGet<StudyDto>(`/api/smv/impact-studies/${id}`), enabled: !!id });

export const useImpactUnits = (id: string, q: { page: number; pageSize: number; change?: string; pin?: string }, version: string) =>
  useQuery({
    queryKey: ['impact-study', id, 'units', q, version],
    queryFn: () => apiGet<PagedResult<TaxImpactUnitDto>>(`/api/smv/impact-studies/${id}/units`, { ...q }),
  });

export function useCreateImpactStudy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: SaveStudyRequest) => apiPost<StudyDto>('/api/smv/impact-studies', r),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['impact-study'] }),
  });
}

export function useUpdateImpactStudy(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: SaveStudyRequest) => apiPut<StudyDto>(`/api/smv/impact-studies/${id}`, r),
    onSuccess: (dto) => {
      qc.setQueryData(['impact-study', id], dto);
      qc.invalidateQueries({ queryKey: ['impact-study'], exact: true });
      qc.invalidateQueries({ queryKey: ['impact-study', id, 'units'] });
    },
  });
}
