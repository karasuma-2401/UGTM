import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';
import { TOPIC_STATUS_LABELS, type TopicStatus } from '../types/topic.types';

const tones: Record<TopicStatus, StatusTone> = {
  Draft: 'neutral',
  Submitted: 'info',
  NeedRevision: 'warning',
  Approved: 'success',
  Rejected: 'error',
  Published: 'success',
  Closed: 'neutral',
  Archived: 'neutral',
};

export function TopicStatusBadge({ status }: { status: TopicStatus }) {
  return (
    <StatusBadge tone={tones[status]}>
      {TOPIC_STATUS_LABELS[status]}
    </StatusBadge>
  );
}
