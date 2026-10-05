import type { SmvPreparationStatus } from './smvPreparationTypes';

/** Sales analyses of an SMV preparation (docs/analysis/smv-preparation-general-revision.md §4.2). */
export type AreaMeasure = 'SquareMetre' | 'Hectare';

export interface TimeAdjustmentFactorDto { id: string; periodFrom: string; periodTo: string; factor: number; source: string }

export interface SalesAnalysisSummaryDto {
  id: string; classificationName: string; actualUseName: string | null; areaUnit: AreaMeasure; saleCount: number; analysedCount: number;
  groupCount: number; adoptedCount: number;
}

export interface SalesAnalysisSaleDto {
  id: string; marketTransactionId: string; transactionDate: string; barangayName: string | null; location: string | null;
  taxDeclarationNumber: string | null; pin: string | null; subClassificationName: string | null; area: number | null; price: number | null;
  unitPrice: number | null; timeFactor: number | null; otherAdjustmentPercent: number; adjustedUnitPrice: number | null;
  roundedUnitValue: number | null; leftOut: boolean; exclusionReason: string | null; note: string | null;
}

export interface SalesAnalysisValueDto { number: number; saleId: string; adjustedUnitPrice: number; roundedUnitValue: number; intervalPercent: number | null }
export interface SalesAnalysisRangeDto { number: number; low: number; mid: number; high: number; frequency: number; groupId: string | null }
export interface SalesAnalysisGroupDto {
  id: string; sequence: number; fromValue: number; toValue: number; subClassificationId: string | null; subClassificationName: string | null;
  frequency: number; proposedValue: number | null; adoptedValue: number | null; basis: string | null; smvScheduleId: string | null; adoptedAt: string | null;
}

export interface SalesAnalysisDto {
  id: string; smvPreparationId: string; preparationStatus: SmvPreparationStatus; editable: boolean; baseValuationDate: string | null;
  classificationId: string; classificationName: string; actualUseId: string | null; actualUseName: string | null; municipalities: string[];
  salesFrom: string | null; salesTo: string | null; areaUnit: AreaMeasure; roundingIncrement: number; rangeWidthPercent: number | null;
  averageIntervalPercent: number | null; effectiveWidthPercent: number | null; notes: string | null;
  sales: SalesAnalysisSaleDto[]; values: SalesAnalysisValueDto[]; ranges: SalesAnalysisRangeDto[]; groups: SalesAnalysisGroupDto[]; warnings: string[];
}

export interface SalesAnalysisGroupInput {
  fromValue: number; toValue: number; subClassificationId: string | null; adoptedValue: number | null; basis: string | null;
}

export const areaUnitLabel: Record<AreaMeasure, string> = { SquareMetre: 'per sqm', Hectare: 'per hectare' };
