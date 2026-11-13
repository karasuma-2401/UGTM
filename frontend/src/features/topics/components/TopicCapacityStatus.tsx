import { StatusBadge } from '@/components/ui/status-badge';
import type { PublishedTopic } from '../types/studentTopic.types';

export function TopicCapacityStatus({ topic }: { topic: PublishedTopic }) {
  const hasMetrics =
    typeof topic.availableSlots === 'number' &&
    typeof topic.confirmedStudentsCount === 'number' &&
    (topic.studentNumber === 1 || topic.studentNumber === 2);
  const available = topic.availableSlots ?? 0;
  const reserved = topic.reservedSlots ?? 0;
  const confirmed = topic.confirmedStudentsCount ?? 0;
  const totalSlots = topic.studentNumber ?? 0;

  return (
    <div className="space-y-1.5">
      <p className="text-xs font-semibold tabular-nums text-slate-800">
        {hasMetrics
          ? `${confirmed}/${topic.studentNumber} SV đã đăng ký`
          : `Chỉ tiêu: ${topic.studentNumber ? `${topic.studentNumber} SV` : 'Chưa rõ'}`}
      </p>

      {hasMetrics && (
        <div
          aria-hidden="true"
          className="grid gap-1.5"
          style={{ gridTemplateColumns: `repeat(${totalSlots}, minmax(0, 1fr))` }}
        >
          {Array.from({ length: totalSlots }, (_, index) => {
            const isConfirmed = index < confirmed;
            const isReserved = !isConfirmed && index < confirmed + reserved;
            const segmentClass = isConfirmed
              ? 'bg-primary'
              : isReserved
                ? 'bg-amber-400'
                : 'bg-slate-200';

            return <span key={index} className={`h-1.5 rounded-full transition-colors ${segmentClass}`} />;
          })}
        </div>
      )}

      <div className="flex flex-wrap items-center gap-1.5">
        <StatusBadge
          tone={!hasMetrics ? 'neutral' : available > 0 ? 'success' : reserved ? 'warning' : 'neutral'}
        >
          {!hasMetrics
            ? 'Chưa có số liệu'
            : available > 0
              ? `Còn ${available} chỗ`
              : reserved
                ? 'Tạm hết chỗ'
                : 'Đã đủ chỉ tiêu'}
        </StatusBadge>
        {hasMetrics && reserved > 0 && (
          <span className="text-[11px] font-medium tabular-nums text-amber-700">
            {reserved} chỗ chờ xác nhận
          </span>
        )}
      </div>
    </div>
  );
}
