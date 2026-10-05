import type { WorkflowStatus } from './types';

/** SMV simulations and valuation testing (docs/analysis/smv-preparation-general-revision.md §4.3). */
export type JobExecutionStatus = 'Queued' | 'Running' | 'Completed' | 'Failed';
export type RpuType = 'Land' | 'Building' | 'Machinery' | 'OtherImprovement';
export type AreaMeasure = 'SquareMetre' | 'Hectare';

export interface MunicipalityRef { id: string; name: string }

export interface SmvSimulationSummaryDto {
  simulated: number; failed: number; withoutCurrent: number; compared: number;
  currentMarketValue: number; simulatedMarketValue: number; currentAssessedValue: number; simulatedAssessedValue: number;
  simulatedTaxableAssessedValue: number; higher: number; lower: number; unchanged: number; classificationChanged: number;
}

export interface SmvSimulationRunDto {
  id: string; smvId: string; smvReference: string; smvRevisionYear: number; smvStatus: WorkflowStatus; asOf: string; description: string | null;
  municipalities: MunicipalityRef[]; status: JobExecutionStatus; totalCount: number; processedCount: number; failedCount: number;
  startedAt: string | null; completedAt: string | null; remarks: string | null; summary: SmvSimulationSummaryDto | null;
}

export interface SmvSimulationResultDto {
  id: string; rpuId: string; propertyId: string; pin: string; rpuNumber: string; rpuType: RpuType; barangayName: string | null;
  currentClassification: string | null; currentMarketValue: number | null; currentAssessedValue: number | null;
  simulatedClassification: string | null; simulatedMarketValue: number | null; simulatedAssessedValue: number | null;
  simulatedTaxableAssessedValue: number | null; marketValueChange: number | null; marketValueChangePercent: number | null;
  classificationChanged: boolean; failureReason: string | null;
}

export interface StartSmvSimulationRequest { smvId: string; asOf: string; municipalityIds: string[]; description: string | null }

export interface ValuationTestGroupDto {
  level: 'SubClass' | 'Class' | 'Municipality' | 'All'; name: string; count: number; medianRatio: number | null;
  coefficientOfDispersion: number | null; medianWithinBenchmark: boolean | null; dispersionWithinBenchmark: boolean | null;
}

export interface ValuationTestingBenchmarks {
  medianRatioLow: number | null; medianRatioHigh: number | null; maximumCoefficientOfDispersion: number | null;
}

export interface ValuationTestDto {
  id: string; smvId: string; smvReference: string; smvRevisionYear: number; asOf: string; salesFrom: string | null; salesTo: string | null;
  description: string | null; municipalities: MunicipalityRef[]; salesCount: number; testedCount: number; createdAt: string;
  groups: ValuationTestGroupDto[] | null; benchmarks: ValuationTestingBenchmarks | null;
}

export interface ValuationTestSaleDto {
  id: string; marketTransactionId: string; transactionDate: string; municipalityName: string | null; barangayName: string | null;
  classificationName: string | null; subClassificationName: string | null; landArea: number | null; landAreaUnit: AreaMeasure;
  price: number | null; rateUnit: string | null; unitValue: number | null; value: number | null; ratio: number | null; exclusionReason: string | null;
}

export interface CreateValuationTestRequest {
  smvId: string; asOf: string; municipalityIds: string[]; salesFrom: string | null; salesTo: string | null; description: string | null;
}

export const groupLevelLabel: Record<ValuationTestGroupDto['level'], string> = {
  SubClass: 'Sub-class', Class: 'Class', Municipality: 'City/municipality', All: 'All',
};

export const jobStatusColor: Record<JobExecutionStatus, string> = { Queued: 'default', Running: 'processing', Completed: 'green', Failed: 'red' };
