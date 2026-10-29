/** Posting states from scope section 8, plus TIMS signed (04_Brand_and_UI_Guide, status chips). */
export type PostingStatus =
  | 'Draft'
  | 'AwaitingApproval'
  | 'Queued'
  | 'Submitting'
  | 'Posted'
  | 'Failed'
  | 'OutcomeUnknown'
  | 'TimsSigned';

export const statusLabels: Record<PostingStatus, string> = {
  Draft: 'Draft',
  AwaitingApproval: 'Awaiting approval',
  Queued: 'Queued',
  Submitting: 'Submitting',
  Posted: 'Posted',
  Failed: 'Failed',
  OutcomeUnknown: 'Outcome unknown',
  TimsSigned: 'TIMS signed',
};

export const statusTone: Record<PostingStatus, string> = {
  Draft: 'draft',
  AwaitingApproval: 'approval',
  Queued: 'queued',
  Submitting: 'queued',
  Posted: 'posted',
  Failed: 'failed',
  OutcomeUnknown: 'unknown',
  TimsSigned: 'tims',
};
