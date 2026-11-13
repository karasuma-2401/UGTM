import React, { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export interface ScoreConfigCloneReasonDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
  isSubmitting?: boolean;
}

export const ScoreConfigCloneReasonDialog: React.FC<ScoreConfigCloneReasonDialogProps> = ({
  open,
  onClose,
  onConfirm,
  isSubmitting = false,
}) => {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) {
      setError('Vui lòng nhập lý do thay đổi để lưu vết phiên bản cấu hình.');
      return;
    }
    setError(null);
    onConfirm(reason.trim());
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Tạo phiên bản cấu hình nháp mới"
      className="max-w-md"
      headerClassName="border-b border-slate-200/80 bg-white px-5 py-4 sm:px-6"
    >
      <form onSubmit={handleSubmit}>
        <div className="p-5 sm:p-6 space-y-4 text-sm">
          <p className="text-slate-600 leading-relaxed">
            Cấu hình hiện tại đang ở trạng thái <strong>Đang áp dụng</strong>. Theo quy định hệ thống,
            khi chỉnh sửa sẽ tự động tạo một phiên bản <strong>Bản nháp</strong> kế tiếp.
          </p>

          {error && (
            <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium">
              {error}
            </div>
          )}

          <div className="space-y-1.5">
            <label className="font-semibold text-slate-700 text-sm">Lý do thay đổi cấu hình *</label>
            <Input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="VD: Cập nhật tỷ lệ điểm hội đồng theo quy chế mới..."
              className="h-10 rounded-xl text-sm"
              required
              autoFocus
            />
          </div>
        </div>

        <div className="border-t border-slate-200/80 bg-slate-50/70 px-5 py-3.5 flex items-center justify-end gap-2.5 sm:px-6">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            disabled={isSubmitting}
            className="h-9 rounded-xl px-4 text-xs font-semibold"
          >
            Hủy
          </Button>
          <Button
            type="submit"
            disabled={isSubmitting}
            className="h-9 rounded-xl px-4 text-xs font-semibold"
          >
            {isSubmitting ? 'Đang tạo...' : 'Xác nhận tạo bản nháp'}
          </Button>
        </div>
      </form>
    </Dialog>
  );
};
