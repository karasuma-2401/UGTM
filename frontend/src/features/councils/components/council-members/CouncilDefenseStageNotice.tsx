import { Button } from '@/components/ui/button';
import { InlineAlert } from '@/components/ui/inline-alert';
import { formatViDate } from '../../utils/councilApprovalReadiness';
import { isOutsideCouncilWindow, type CouncilDateWindow } from '../../utils/councilDefenseWindow';

export function CouncilDefenseStageNotice({
  window,
  date,
  loading,
  error,
  canRead,
  onRetry,
}: {
  window?: CouncilDateWindow;
  date: string;
  loading: boolean;
  error: boolean;
  canRead: boolean;
  onRetry: () => void;
}) {
  const range = window ? `${formatViDate(window.startDate)} – ${formatViDate(window.endDate)}` : '';
  const outside = isOutsideCouncilWindow(date, window);
  return (
    <div className="space-y-2">
      {!canRead && <p className="text-sm text-ink-muted">Bạn chưa có quyền xem chi tiết tiến độ. Hãy kiểm tra mốc bảo vệ với Khoa.</p>}
      {canRead && loading && <p role="status" className="text-sm text-ink-muted">Đang tải giai đoạn bảo vệ của đợt…</p>}
      {canRead && error && (
        <InlineAlert tone="warning" title="Chưa tải được tiến độ của đợt" action={<Button type="button" variant="outline" size="sm" onClick={onRetry}>Thử lại</Button>}>
          Có thể dùng lịch chung của đợt để tham khảo, nhưng hãy kiểm tra mốc bảo vệ với Khoa trước khi lưu.
        </InlineAlert>
      )}
      {window?.source === 'batch' && !loading && (
        <InlineAlert tone="info" title="Đợt chưa thiết lập mốc bảo vệ riêng">
          Đợt khóa luận diễn ra từ <strong>{range}</strong>. Hệ thống dùng khoảng này để gợi ý ngày; Khoa cần xác nhận lịch bảo vệ cụ thể.
        </InlineAlert>
      )}
      {window?.source === 'stage' && !outside && (
        <p role="status" className="text-sm text-ink-secondary">
          Giai đoạn “{window.stageName}” của đợt: <strong className="font-semibold text-ink-heading">{range}</strong>.
        </p>
      )}
      {!window && !loading && !error && (
        <p className="text-sm text-ink-muted">Chưa có lịch đợt để gợi ý ngày. Hãy kiểm tra với Khoa trước khi xếp lịch.</p>
      )}
      {outside && (
        <InlineAlert tone="warning" title="Ngày đang chọn nằm ngoài lịch của đợt">
          {window?.source === 'stage' ? `Giai đoạn bảo vệ là ${range}.` : `Đợt khóa luận diễn ra từ ${range}.`} Hãy kiểm tra lại ngày này trước khi lưu lịch.
        </InlineAlert>
      )}
    </div>
  );
}
