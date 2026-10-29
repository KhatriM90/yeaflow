import { statusLabels, statusTone, type PostingStatus } from '../lib/postingStatus';

export function StatusChip({ status }: { status: PostingStatus }) {
  return (
    <span className={`yf-chip yf-chip--${statusTone[status]}`} data-status={status}>
      {statusLabels[status]}
    </span>
  );
}
