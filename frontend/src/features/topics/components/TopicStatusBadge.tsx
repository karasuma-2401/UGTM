import { Badge, type BadgeProps } from '@/components/ui/badge';
import { TOPIC_STATUS_LABELS, type TopicStatus } from '../types/topic.types';

const variants: Record<TopicStatus, NonNullable<BadgeProps['variant']>> = {
  Draft: 'secondary',
  Submitted: 'info',
  NeedRevision: 'warning',
  Approved: 'success',
  Rejected: 'error',
  Published: 'success',
  Closed: 'secondary',
  Archived: 'secondary',
};

const appearances: Record<TopicStatus, string> = {
  Draft: 'border-slate-200 bg-slate-100 text-slate-500',
  Submitted: 'border-blue-200/70 bg-blue-50 text-blue-700',
  NeedRevision: 'border-amber-200/70 bg-amber-50 text-amber-700',
  Approved: 'border-emerald-200/70 bg-emerald-50 text-emerald-700',
  Rejected: 'border-rose-200/70 bg-rose-50 text-rose-700',
  Published: 'border-emerald-200/70 bg-emerald-50 text-emerald-700',
  Closed: 'border-slate-200 bg-slate-100 text-slate-500',
  Archived: 'border-slate-200 bg-slate-100 text-slate-500',
};

export function TopicStatusBadge({ status }: { status: TopicStatus }) {
  return (
    <Badge
      variant={variants[status]}
      dot
      className={`whitespace-nowrap px-2.5 py-1 text-[11px] font-medium shadow-none ${appearances[status]}`}
    >
      {TOPIC_STATUS_LABELS[status]}
    </Badge>
  );
}
