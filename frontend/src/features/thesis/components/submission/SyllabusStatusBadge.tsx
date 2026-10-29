import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import {
  SYLLABUS_STATUS_LABELS,
  type SyllabusStatus,
} from '../../types/syllabus.types';

const tones: Record<SyllabusStatus, StatusTone> = {
  Draft: 'neutral',
  Submitted: 'info',
  NeedRevision: 'warning',
  Approved: 'success',
  Rejected: 'error',
};

export function SyllabusStatusBadge({
  status,
  className,
}: {
  status: SyllabusStatus;
  className?: string;
}) {
  return (
    <StatusBadge tone={tones[status] || 'neutral'} className={className}>
      {SYLLABUS_STATUS_LABELS[status] || status}
    </StatusBadge>
  );
}