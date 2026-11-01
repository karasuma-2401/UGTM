import { Card } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Download, Send, AlertCircle, CheckCircle2 } from 'lucide-react';
import { MidtermStatusBadge } from './MidtermStatusBadge';
import type {
  MidtermReportSubmissionHistoryItemDto,
  MidtermReportStatus,
} from '../../types/midterm.types';

interface MidtermHistoryTableProps {
  versions: MidtermReportSubmissionHistoryItemDto[];
  selectedFile: File | null;
  groupCode?: string;
  currentStatus: MidtermReportStatus;
  canSubmit: boolean;
  disabledReason?: string | null;
  isSubmitting: boolean;
  onSubmit: () => void;
  onDownload: (fileId: string) => void;
}

export function MidtermHistoryTable({
  versions,
  selectedFile,
  groupCode = 'Nhóm KLTN',
  currentStatus,
  canSubmit,
  disabledReason,
  isSubmitting,
  onSubmit,
  onDownload,
}: MidtermHistoryTableProps) {
  const hasDraftRow = Boolean(selectedFile && canSubmit);
  const totalItems = versions.length + (hasDraftRow ? 1 : 0);

  return (
    <Card className="p-6 bg-white border-slate-200 shadow-xs rounded-xl space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-base font-semibold text-slate-900">Phiên bản báo cáo giữa kỳ</h3>
      </div>

      <div className="overflow-x-auto border border-slate-200/80 rounded-lg">
        <table className="w-full text-left text-sm">
          <thead className="bg-slate-50 text-slate-500 font-medium text-xs border-b border-slate-200/80">
            <tr>
              <th className="py-3 px-4">Nội dung</th>
              <th className="py-3 px-4">Thông tin</th>
              <th className="py-3 px-4">Trạng thái</th>
              <th className="py-3 px-4 text-right">Chi tiết</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {hasDraftRow && (
              <tr className="bg-blue-50/30">
                <td className="py-3 px-4 font-medium text-slate-900">
                  <div className="flex items-center gap-2">
                    <span className="text-primary font-semibold">
                      v{versions.length + 1} -
                    </span>
                    <span className="truncate max-w-[200px] md:max-w-[280px]">
                      {selectedFile!.name}
                    </span>
                  </div>
                </td>
                <td className="py-3 px-4 text-slate-600">{groupCode}</td>
                <td className="py-3 px-4">
                  <MidtermStatusBadge status="Draft" />
                </td>
                <td className="py-3 px-4 text-right text-xs text-slate-400 italic">
                  Chưa gửi
                </td>
              </tr>
            )}

            {versions.map((item) => {
              const status: MidtermReportStatus =
                item.review?.decision === 'Approved'
                  ? 'Approved'
                  : item.review?.decision === 'NeedRevision'
                    ? 'NeedRevision'
                    : item.review?.decision === 'Rejected'
                      ? 'Rejected'
                      : 'Submitted';

              return (
                <tr key={item.versionNo} className="hover:bg-slate-50/60 transition-colors">
                  <td className="py-3.5 px-4 font-medium text-slate-900">
                    <div className="flex items-center gap-2">
                      <span className="text-slate-500 font-semibold">
                        v{item.versionNo} -
                      </span>
                      <span className="truncate max-w-[200px] md:max-w-[280px]">
                        {item.file.fileName}
                      </span>
                    </div>
                    {item.review?.feedback && (
                      <div className="mt-1 text-xs text-amber-700 bg-amber-50/80 p-2 rounded border border-amber-200/50">
                        <span className="font-medium">Ý kiến Khoa:</span> {item.review.feedback}
                      </div>
                    )}
                  </td>
                  <td className="py-3.5 px-4 text-slate-600">{groupCode}</td>
                  <td className="py-3.5 px-4">
                    <MidtermStatusBadge status={status} />
                  </td>
                  <td className="py-3.5 px-4 text-right">
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="h-8 text-xs font-medium text-primary hover:text-primary hover:bg-blue-50"
                      onClick={() => onDownload(item.file.id)}
                    >
                      <Download className="size-3.5 mr-1" />
                      Xem trước
                    </Button>
                  </td>
                </tr>
              );
            })}

            {totalItems === 0 && (
              <tr>
                <td colSpan={4} className="py-8 text-center text-slate-400 text-xs">
                  Chưa có phiên bản nào được nộp. Vui lòng chọn tệp và bấm "Nộp báo cáo giữa kỳ".
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between pt-2">
        <span className="text-xs text-slate-500">
          {totalItems} mục trong danh sách
        </span>

        <Button
          type="button"
          onClick={onSubmit}
          disabled={!selectedFile || !canSubmit || isSubmitting}
          className="bg-primary hover:bg-primary/90 text-white font-medium px-5 h-10 shadow-xs"
        >
          {isSubmitting ? (
            <span className="flex items-center gap-2">
              <span className="size-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Đang nộp báo cáo...
            </span>
          ) : (
            <span className="flex items-center gap-1.5">
              <Send className="size-4" />
              Nộp báo cáo giữa kỳ
            </span>
          )}
        </Button>
      </div>

      {!canSubmit && disabledReason && (
        <div className="flex items-center gap-2.5 p-3 rounded-lg bg-amber-50/80 border border-amber-200/70 text-xs text-amber-800">
          <AlertCircle className="size-4 shrink-0 text-amber-600" />
          <span>{disabledReason}</span>
        </div>
      )}

      {!canSubmit && currentStatus === 'Submitted' && (
        <div className="flex items-center gap-2.5 p-3 rounded-lg bg-blue-50 border border-blue-200/70 text-xs text-blue-800">
          <AlertCircle className="size-4 shrink-0 text-blue-600" />
          <span>
            Báo cáo giữa kỳ của nhóm đang ở trạng thái <strong>Chờ thẩm định</strong>. Bạn sẽ có thể nộp lại phiên bản mới nếu Văn phòng Khoa yêu cầu chỉnh sửa.
          </span>
        </div>
      )}

      {currentStatus === 'Approved' && (
        <div className="flex items-center gap-2.5 p-3 rounded-lg bg-emerald-50 border border-emerald-200/70 text-xs text-emerald-800">
          <CheckCircle2 className="size-4 shrink-0 text-emerald-600" />
          <span>
            Báo cáo giữa kỳ của nhóm đã được Văn phòng Khoa <strong>phê duyệt Đạt</strong>. Chúc mừng bạn đã hoàn thành Cổng kiểm soát 2!
          </span>
        </div>
      )}
    </Card>
  );
}