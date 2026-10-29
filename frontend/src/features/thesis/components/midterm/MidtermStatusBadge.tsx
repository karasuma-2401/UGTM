import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import {
  MIDTERM_STATUS_LABELS,
  type MidtermReportStatus,
} from '../../types/midterm.types';

const tones: Record<MidtermReportStatus, StatusTone> = {
  Draft: 'neutral',
  Submitted: 'info',
  NeedRevision: 'warning',
  Approved: 'success',
  Rejected: 'error',
};

export function MidtermStatusBadge({
  status,
  className,
}: {
  status: MidtermReportStatus;
  className?: string;
}) {
  return (
    <StatusBadge tone={tones[status] || 'neutral'} className={className}>
      {MIDTERM_STATUS_LABELS[status] || status}
    </StatusBadge>
  );
}