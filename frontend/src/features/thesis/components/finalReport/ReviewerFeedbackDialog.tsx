import { MessageSquareQuote, CheckCircle2, Clock } from 'lucide-react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { RichTextContent } from '@/components/ui/rich-text-content';

interface ReviewerFeedbackDialogProps {
  open: boolean;
  onClose: () => void;
  comment: string | null | undefined;
  isLoading?: boolean;
}

export function ReviewerFeedbackDialog({
  open,
  onClose,
  comment,
  isLoading = false,
}: ReviewerFeedbackDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      title={
        <div className="flex items-center gap-2 text-slate-900">
          <MessageSquareQuote className="size-5 text-[#2054A5]" />
          <span>Góp ý từ Giảng viên phản biện</span>
        </div>
      }
      className="max-w-lg"
    >
      <div className="space-y-4 pt-2">
        {isLoading ? (
          <div className="py-8 text-center text-slate-500 text-sm flex flex-col items-center gap-2">
            <Clock className="size-6 animate-spin text-[#2054A5]" />
            <span>Đang tải nhận xét phản biện…</span>
          </div>
        ) : comment ? (
          <div className="space-y-3">
            <div className="p-4 rounded-xl bg-slate-50 border border-slate-200/80 space-y-1.5">
              <p className="text-xs font-semibold text-slate-600">Nội dung góp ý chuyên môn</p>
              <RichTextContent content={comment} className="text-sm text-slate-800" />
            </div>
            <div className="flex items-center gap-2 text-xs text-emerald-700 bg-emerald-50 p-3 rounded-xl border border-emerald-200/70">
              <CheckCircle2 className="size-4 shrink-0 text-emerald-600" />
              <span>Góp ý đã được Giảng viên phản biện chốt và phát hành chính thức.</span>
            </div>
          </div>
        ) : (
          <div className="py-8 text-center space-y-2">
            <div className="size-12 rounded-full bg-slate-100 text-slate-400 mx-auto flex items-center justify-center">
              <Clock className="size-6" />
            </div>
            <p className="text-sm font-semibold text-slate-800">Chưa có góp ý phản biện</p>
            <p className="text-xs text-slate-500 max-w-sm mx-auto leading-relaxed">
              Giảng viên phản biện chưa hoàn tất chấm hoặc chưa phát hành góp ý cho phiên bản này. Góp ý sẽ hiển thị tại đây ngay khi có kết quả.
            </p>
          </div>
        )}

        <div className="flex justify-end pt-2">
          <Button type="button" variant="outline" onClick={onClose}>
            Đóng
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
