import type { WorkflowStatus } from './types';

/** SMV preparation work files (docs/analysis/smv-preparation-general-revision.md §4.2). */
export type SmvPreparationStatus =
  | 'Preparing' | 'PublishedForComment' | 'Submitted' | 'UnderReview' | 'Remanded' | 'Certified' | 'NotCertified' | 'Published' | 'Cancelled';
export type SmvConsultationMode = 'InPerson' | 'Online' | 'Hybrid';
export type SmvPreparationEventKind =
  | 'PublishedForComment' | 'SubmittedToRegionalOffice' | 'EndorsedByRegionalOffice' | 'EndorsedByBlgf' | 'Remanded' | 'Resubmitted'
  | 'Certified' | 'NotCertified' | 'Published' | 'TransmittedToSanggunian';

export interface SmvPreparationSmvDto {
  id: string; reference: string; status: WorkflowStatus; effectivityDate: string; coverage: string[]; scheduleCount: number;
  proposedOn: string | null; publishedForCommentOn: string | null; consultationsHeldOn: string | null; submittedToBlgfOn: string | null;
  certifiedOn: string | null; certificationReference: string | null; publishedOn: string | null; publicationReference: string | null;
}

export interface SmvConsultationDto {
  id: string; heldOn: string; mode: SmvConsultationMode; venue: string | null; attendance: number | null; minutesReference: string | null; notes: string | null;
}

export interface SmvPreparationEventDto {
  id: string; kind: SmvPreparationEventKind; label: string; occurredOn: string; reference: string | null; note: string | null; recordedAt: string;
}

export interface SmvPreparationDto {
  id: string; revisionYear: number; title: string; dateOfValuation: string | null; baseValuationDate: string | null; status: SmvPreparationStatus;
  notes: string | null; cancellationReason: string | null; proposedSmv: SmvPreparationSmvDto; consultations: SmvConsultationDto[];
  events: SmvPreparationEventDto[]; minimumConsultations: number; nextDue: { what: string; dueOn: string } | null; editable: boolean;
  warnings: string[];
}

export interface SmvPreparationSummaryDto {
  id: string; revisionYear: number; title: string; status: SmvPreparationStatus; proposedSmvId: string; effectivityDate: string;
  consultationCount: number; createdAt: string;
}

export const preparationStatusLabel: Record<SmvPreparationStatus, string> = {
  Preparing: 'Preparing', PublishedForComment: 'Published for comment', Submitted: 'Submitted to the BLGF', UnderReview: 'Under review',
  Remanded: 'Remanded', Certified: 'Certified', NotCertified: 'Not certified', Published: 'Published', Cancelled: 'Cancelled',
};

export const preparationStatusColor: Record<SmvPreparationStatus, string> = {
  Preparing: 'default', PublishedForComment: 'blue', Submitted: 'processing', UnderReview: 'processing', Remanded: 'orange', Certified: 'green',
  NotCertified: 'red', Published: 'green', Cancelled: 'red',
};

export const eventKindLabel: Record<SmvPreparationEventKind, string> = {
  PublishedForComment: 'Published for comment', SubmittedToRegionalOffice: 'Submitted to the BLGF Regional Office',
  EndorsedByRegionalOffice: 'Endorsed by the Regional Office', EndorsedByBlgf: 'Endorsed by the BLGF', Remanded: 'Remanded',
  Resubmitted: 'Resubmitted', Certified: 'Certified by the Secretary of Finance', NotCertified: 'Not certified within the period',
  Published: 'Certified SMV published', TransmittedToSanggunian: 'Transmitted to the LCE and Sanggunian',
};

/** The steps that may follow each status — the same order the server checks. */
export const nextKinds: Record<SmvPreparationStatus, SmvPreparationEventKind[]> = {
  Preparing: ['PublishedForComment', 'SubmittedToRegionalOffice'],
  PublishedForComment: ['PublishedForComment', 'SubmittedToRegionalOffice'],
  Submitted: ['EndorsedByRegionalOffice', 'Remanded', 'Certified', 'NotCertified'],
  UnderReview: ['EndorsedByBlgf', 'Remanded', 'Certified', 'NotCertified'],
  Remanded: ['Resubmitted'],
  Certified: ['Published', 'TransmittedToSanggunian'],
  NotCertified: [],
  Published: ['TransmittedToSanggunian'],
  Cancelled: [],
};

export const consultationModeLabel: Record<SmvConsultationMode, string> = { InPerson: 'In person', Online: 'Online', Hybrid: 'Hybrid' };
