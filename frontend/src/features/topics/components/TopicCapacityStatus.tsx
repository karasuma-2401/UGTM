import { StatusBadge } from '@/components/ui/status-badge';
import type { PublishedTopic } from '../types/studentTopic.types';

export function TopicCapacityStatus({ topic }: { topic: PublishedTopic }) {
  const confirmed = topic.confirmedStudentsCount;
  const reserved = topic.reservedSlots;
  const available = topic.availableSlots;
  return (
    <div className="space-y-1.5">
      <p className="text-xs text-slate-600">
        <span className="font-semibold tabular-nums text-slate-800">
          {confirmed ?? '—'}/{topic.studentNumber ?? '—'}
        </span>{' '}
        SV đã đăng ký
      </p>
      <StatusBadge
        tone={
          typeof available !== 'number'
            ? 'neutral'
            : available > 0
              ? 'success'
              : reserved
                ? 'warning'
                : 'neutral'
        }
      >
        {typeof available !== 'number'
          ? 'Chưa có số liệu'
          : available > 0
            ? `Còn ${available} chỗ`
            : reserved
              ? 'Tạm hết chỗ'
              : 'Đã đủ chỉ tiêu'}
      </StatusBadge>
      {typeof reserved === 'number' && reserved > 0 && (
        <p className="text-[11px] leading-5 text-amber-700">{reserved} chỗ chờ xác nhận</p>
      )}
    </div>
  );
}
