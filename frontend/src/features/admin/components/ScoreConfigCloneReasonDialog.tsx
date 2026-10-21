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
    >
      <form onSubmit={handleSubmit} className="p-5 space-y-4 text-xs">
        <p className="text-slate-600 leading-relaxed">
          Cấu hình hiện tại đang ở trạng thái <strong>Đang áp dụng (Active)</strong>. Theo quy định hệ thống, khi chỉnh sửa sẽ tự động tạo một phiên bản <strong>Bản nháp (Draft)</strong> kế tiếp.
        </p>

        {error && (
          <div className="p-2.5 rounded-lg bg-rose-50 border border-rose-200 text-rose-700 font-medium">
            {error}
          </div>
        )}

        <div className="space-y-1.5">
          <label className="font-semibold text-slate-700">
            Lý do thay đổi cấu hình *
          </label>
          <Input
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="VD: Cập nhật tỷ lệ điểm hội đồng theo quy chế mới..."
            className="h-10 text-xs"
            required
            autoFocus
          />
        </div>

        <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onClose}
            disabled={isSubmitting}
          >
            Hủy
          </Button>
          <Button
            type="submit"
            size="sm"
            className="bg-primary text-white hover:bg-primary/90"
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Đang tạo...' : 'Xác nhận tạo bản nháp'}
          </Button>
        </div>
      </form>
    </Dialog>
  );
};