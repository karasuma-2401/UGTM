import React from 'react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import type { MergedResultRow } from '../../types/results.types';
import { formatScoreDecimal } from '../../utils/scoreSummaryFormatting';

interface PublishScoresConfirmDialogProps {
  open: boolean;
  rows: readonly MergedResultRow[];
  isConfirming: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export const PublishScoresConfirmDialog: React.FC<PublishScoresConfirmDialogProps> = ({
  open,
  rows,
  isConfirming,
  onClose,
  onConfirm,
}) => {
  const groupSummary =
    rows.length === 1 ? rows[0].groupCode : `${rows.length} nhóm (${rows.map((r) => r.groupCode).join(', ')})`;

  return (
    <ConfirmDialog
      open={open}
      onClose={onClose}
      onConfirm={onConfirm}
      type="brand"
      isConfirming={isConfirming}
      className="max-w-[480px]"
      title="Công bố kết quả"
      confirmText={rows.length === 1 ? `Công bố ${rows[0].groupCode}` : `Công bố ${rows.length} nhóm`}
      cancelText="Hủy"
      description={
        <div className="space-y-3.5">
          <div className="rounded-xl border border-slate-200/80 bg-slate-50/70 px-3.5 py-3 space-y-1.5">
            <p className="text-xs font-semibold text-slate-900">Phạm vi công bố</p>
            <p className="text-xs text-slate-600 leading-relaxed">
              Công bố chính thức bảng điểm cho{' '}
              <strong className="font-semibold text-slate-900">{groupSummary}</strong>. Thao tác áp
              dụng nguyên tử (All-or-Nothing): nếu bất kỳ nhóm nào thay đổi dữ liệu hoặc chưa đạt
              lưu chiểu, toàn bộ lượt công bố sẽ bị hủy.
            </p>
            {rows.length > 0 && rows.length <= 5 && (
              <ul className="pt-1 space-y-1 text-xs text-slate-700">
                {rows.map((row) => (
                  <li key={row.groupId} className="flex items-center justify-between gap-2">
                    <span className="truncate">
                      <strong className="font-semibold">{row.groupCode}</strong> · {row.topicTitleVi}
                    </span>
                    <span className="shrink-0 font-semibold text-slate-900 tabular-nums">
                      {formatScoreDecimal(row.finalScore)}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="rounded-xl border border-amber-200/80 bg-amber-50/70 px-3.5 py-3 space-y-1">
            <p className="text-xs font-semibold text-amber-900">Lưu ý khóa bất biến</p>
            <p className="text-xs text-amber-800 leading-relaxed">
              Sau khi công bố, bảng điểm và biên bản hội đồng của các nhóm này sẽ bị{' '}
              <strong className="font-semibold">khóa vĩnh viễn</strong> (không thể tính lại hoặc
              thu hồi). Sinh viên đủ điều kiện lưu chiểu sẽ xem được kết quả chính thức.
            </p>
          </div>
        </div>
      }
    />
  );
};
