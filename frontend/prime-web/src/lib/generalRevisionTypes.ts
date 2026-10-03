// General revision programmes (docs/analysis/smv-preparation-general-revision.md §4.6, step L6-6).
import type { RpuType, WorkflowStatus } from './types';

export type GeneralRevisionStatus = 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
export const generalRevisionStatusColor: Record<GeneralRevisionStatus, string> = { Planned: 'default', InProgress: 'blue', Completed: 'green', Cancelled: 'red' };
export type GeneralRevisionRunMode =
  | 'Compile' | 'Value' | 'Submit' | 'Approve' | 'Reject' | 'Post' | 'SubmitTaxDeclarations' | 'ApproveTaxDeclarations' | 'GenerateNotices' | 'IssueNotices';
export const runModeLabel: Record<GeneralRevisionRunMode, string> = {
  Compile: 'Compile', Value: 'Value', Submit: 'Submit for review', Approve: 'Approve', Reject: 'Return (reject)', Post: 'Post',
  SubmitTaxDeclarations: 'Submit TDs', ApproveTaxDeclarations: 'Approve TDs', GenerateNotices: 'Generate notices', IssueNotices: 'Issue notices',
};
/** The batch workflow actions (L6-6b), in the order they are done. */
export const batchModes: GeneralRevisionRunMode[] = ['Submit', 'Approve', 'Reject', 'Post', 'SubmitTaxDeclarations', 'ApproveTaxDeclarations'];
export type GeneralRevisionInspectionFilter = 'Unassigned' | 'Awaiting' | 'Inspected';
export type GeneralRevisionItemStatus = 'Pending' | 'Assessed' | 'Failed' | 'Excluded';
export const itemStatusColor: Record<GeneralRevisionItemStatus, string> = { Pending: 'default', Assessed: 'green', Failed: 'red', Excluded: 'purple' };
export type JobExecutionStatus = 'Queued' | 'Running' | 'Completed' | 'Failed';
export type GeneralRevisionSuspensionKind = 'LocalCalamity' | 'Extension' | 'NationalEmergency';
export const suspensionKindLabel: Record<GeneralRevisionSuspensionKind, string> = {
  LocalCalamity: 'Local state of calamity',
  Extension: 'Extension (BLGF recommendation, SOF approval)',
  NationalEmergency: 'National emergency (until lifted)',
};

export interface GeneralRevisionSuspensionDto {
  id: string; kind: GeneralRevisionSuspensionKind; fromDate: string; untilDate: string | null; reference: string; remarks: string | null;
  liftedOn: string | null; inForce: boolean;
}

export interface GeneralRevisionRunDto {
  id: string; mode: GeneralRevisionRunMode | null; status: JobExecutionStatus; totalCount: number; processedCount: number; failedCount: number;
  startedAt: string | null; completedAt: string | null; remarks: string | null; reason: string | null; issueCount: number;
}

/** A refusal (failed), or a note on an item the run did process. */
export interface GeneralRevisionRunIssueDto { itemId: string; pin: string; rpuNumber: string; code: string; message: string; failed: boolean }

export interface GeneralRevisionDto {
  id: string; revisionYear: number; effectiveDate: string; smvId: string; smvReference: string; officeOrderReference: string | null;
  ordinanceReference: string | null; description: string | null; status: GeneralRevisionStatus;
  scope: { municipalityId: string; municipalityName: string }[]; itemCount: number;
  itemsByStatus: Partial<Record<GeneralRevisionItemStatus, number>>; assessmentsByStatus: Partial<Record<WorkflowStatus, number>>;
  previousMarketValue: number; previousAssessedValue: number; newMarketValue: number; newAssessedValue: number;
  suspensions: GeneralRevisionSuspensionDto[]; suspended: boolean; runs: GeneralRevisionRunDto[]; runActive: boolean;
  createdAt: string; completedAt: string | null; cancellationReason: string | null;
}

export interface GeneralRevisionSummaryDto {
  id: string; revisionYear: number; effectiveDate: string; smvReference: string; status: GeneralRevisionStatus; scope: string; itemCount: number; createdAt: string;
}

export interface GeneralRevisionItemDto {
  id: string; rpuId: string; propertyId: string; pin: string; rpuNumber: string; rpuType: RpuType; barangayId: string; barangayName: string;
  previousAssessmentId: string | null; previousMarketValue: number | null; previousAssessedValue: number | null; status: GeneralRevisionItemStatus;
  failureReason: string | null; assessmentId: string | null; assessmentStatus: WorkflowStatus | null; newMarketValue: number | null;
  newAssessedValue: number | null; assessedValueChange: number | null; processedAt: string | null;
  taxDeclarationId: string | null; taxDeclarationNumber: string | null; taxDeclarationStatus: WorkflowStatus | null;
  inspectorId: string | null; inspectorName: string | null; inspectionRoute: string | null; inspectedOn: string | null; inspectionNotes: string | null;
  inspectionFoundChanges: boolean | null; exclusionReason: string | null;
}

// L6-6c: notices, the roll gate and register runs of a general revision.
export type NoticeStatus = 'Draft' | 'Issued' | 'Served' | 'Cancelled';
export type NoticeServiceMode = 'Personal' | 'RegisteredMail' | 'ThroughPunongBarangay' | 'Email';
export const noticeServiceModeLabel: Record<NoticeServiceMode, string> = {
  Personal: 'Personal', RegisteredMail: 'Registered mail', ThroughPunongBarangay: 'Through the punong barangay', Email: 'Email',
};
export interface GeneralRevisionNoticeDto {
  id: string; noticeNumber: string | null; addresseeNames: string; itemCount: number; firstPin: string; assessedValue: number; status: NoticeStatus;
  issueDueDate: string; issueOverdue: boolean; issuedAt: string | null; serviceMode: NoticeServiceMode | null; receivedDate: string | null; appealDeadline: string | null;
}
export interface NoticeServiceResultDto { recorded: number; failures: { noticeId: string; noticeNumber: string | null; code: string; message: string }[] }
export interface RollGateDto {
  municipalityId: string; municipalityName: string; units: number; unitsNotPosted: number; taxDeclarationsNotApproved: number; noticesRequired: number; noticesServed: number;
  noticesOutstanding: number; latestReceipt: string | null; waitDays: number; opensOn: string | null; open: boolean; blockers: string[];
}
export type GeneralRevisionRegisterKind = 'AssessmentRollTaxable' | 'AssessmentRollExempt' | 'TaxMapControlRoll' | 'PreTaxMapControlRoll' | 'OwnershipRecordCard';
export const registerKindLabel: Record<GeneralRevisionRegisterKind, string> = {
  AssessmentRollTaxable: 'Assessment roll — taxable', AssessmentRollExempt: 'Assessment roll — exempt', PreTaxMapControlRoll: 'Pre-TMCR',
  TaxMapControlRoll: 'Tax Map Control Roll (post)', OwnershipRecordCard: 'Ownership Record Forms (every owner)',
};
export interface GeneralRevisionRegisterRunDto {
  id: string; kind: GeneralRevisionRegisterKind; formCode: string; asOf: string; barangayName: string | null; taxpayerName: string | null;
  rollGateOverrideReason: string | null; createdAt: string;
}

// L6-6c: completion, gates and the checklist.
export type GeneralRevisionGate = 'Compiled' | 'Valued' | 'Approved' | 'Posted' | 'TaxDeclarationsApproved' | 'NoticesServed' | 'RollWaitElapsed'
  | 'AssessmentRollRun' | 'OwnershipRecordsRun' | 'CompletionReportIssued';
export const gateLabel: Record<GeneralRevisionGate, string> = {
  Compiled: 'Units compiled', Valued: 'Every unit valued', Approved: 'Every assessment approved', Posted: 'Every assessment posted',
  TaxDeclarationsApproved: 'New TDs approved', NoticesServed: 'Notices served', RollWaitElapsed: 'Roll waiting period passed',
  AssessmentRollRun: 'Assessment rolls run', OwnershipRecordsRun: 'Ownership Record Forms run', CompletionReportIssued: 'Completion report issued',
};
export interface GateStatusDto { gate: GeneralRevisionGate; met: boolean; detail: string }
export interface ChecklistStepDto {
  id: string; code: string; sequence: number; title: string; description: string | null; gate: GeneralRevisionGate | null; met: boolean;
  gateDetail: string | null; completedOn: string | null; completedByName: string | null; evidence: string | null;
}
export interface GeneralRevisionReadinessDto { gates: GateStatusDto[]; checklist: ChecklistStepDto[]; checklistLoaded: boolean; blockers: string[] }
export interface ChecklistStepDefinitionDto {
  id: string; code: string; sequence: number; title: string; description: string | null; gate: GeneralRevisionGate | null; legalBasis: string;
  effectiveDate: string; endDate: string | null; status: WorkflowStatus; createdBy: string | null; createdAt: string; approvedBy: string | null;
  approvedAt: string | null; remarks: string | null;
}
