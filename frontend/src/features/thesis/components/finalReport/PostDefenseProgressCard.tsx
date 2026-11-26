import { ArrowRight, CheckCircle2, RefreshCw } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { StatusBadge } from '@/components/ui/status-badge';
import type { getPostDefenseProgress } from '../../utils/postDefenseProgress';

interface Props {
  progress: NonNullable<ReturnType<typeof getPostDefenseProgress>>;
  isRefreshing: boolean;
  onRefresh: () => void;
}

export function PostDefenseProgressCard({ progress, isRefreshing, onRefresh }: Props) {
  return (
    <Card className="overflow-hidden border-line/80 bg-surface-card shadow-xs">
      <div className="flex flex-wrap items-start justify-between gap-4 border-b border-line-subtle px-5 py-5 sm:px-6">
        <div className="flex min-w-0 items-start gap-3">
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-tone-success-bg text-tone-success-text">
            <CheckCircle2 className="size-5" aria-hidden="true" />
          </span>
          <div className="min-w-0">
            <h2 className="text-lg font-semibold text-ink-primary">Đã hoàn tất bảo vệ trước Hội đồng</h2>
            <p className="mt-1 text-sm leading-6 text-ink-secondary">Biên bản chấm điểm của nhóm đã được xác thực và khóa.</p>
          </div>
        </div>
        <StatusBadge tone={progress.tone}>{progress.label}</StatusBadge>
      </div>
      <div className="space-y-4 px-5 py-5 sm:px-6">
        <p className="text-sm leading-7 text-ink-heading">{progress.description}</p>
        <p className="text-sm leading-6 text-ink-muted">Kết quả chính thức chỉ hiển thị tại trang Kết quả khóa luận sau khi được công bố. Báo cáo và góp ý phản biện bên dưới được giữ để tra cứu.</p>
        <div className="flex flex-wrap items-center gap-3">
          <Button asChild><Link to={progress.actionTo}>{progress.actionLabel}<ArrowRight className="ml-2 size-4" aria-hidden="true" /></Link></Button>
          <Button type="button" variant="outline" disabled={isRefreshing} onClick={onRefresh}>
            <RefreshCw className="mr-2 size-4" aria-hidden="true" />{isRefreshing ? 'Đang cập nhật…' : 'Làm mới trạng thái'}
          </Button>
        </div>
      </div>
    </Card>
  );
}
