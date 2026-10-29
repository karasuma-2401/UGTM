import { StatusBadge, type StatusTone } from '@/components/ui/status-badge';

export function MilestoneStatusBadge({
  status,
  versionNo,
  className,
}: {
  status?: string | null;
  versionNo?: number | null;
  className?: string;
}) {
  let tone: StatusTone = 'neutral';
  let label = 'Chưa nộp';

  switch (status) {
    case 'Accepted':
      tone = 'success';
      label = versionNo ? `Đạt - v${versionNo}` : 'Đạt';
      break;
    case 'NeedRevision':
      tone = 'warning';
      label = versionNo ? `Cần chỉnh sửa - v${versionNo}` : 'Cần chỉnh sửa';
      break;
    case 'Submitted':
      tone = 'info';
      label = versionNo ? `Chờ đánh giá - v${versionNo}` : 'Chờ đánh giá';
      break;
    case 'Rejected':
      tone = 'error';
      label = 'Không đạt';
      break;
    case 'NotSubmitted':
    default:
      tone = 'neutral';
      label = 'Chưa nộp';
      break;
  }

  return (
    <StatusBadge tone={tone} className={className}>
      {label}
    </StatusBadge>
  );
}