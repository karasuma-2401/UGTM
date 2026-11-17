import React from 'react';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

export interface ScoreConfigResetConfirmDialogProps {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export const ScoreConfigResetConfirmDialog: React.FC<ScoreConfigResetConfirmDialogProps> = ({
  open,
  onClose,
  onConfirm,
}) => {
  return (
    <ConfirmDialog
      open={open}
      onClose={onClose}
      onConfirm={onConfirm}
      type="brand"
      title="CẢNH BÁO!"
      description="Cấu hình điểm hiện tại đã có sự thay đổi. Bạn có chắc chắn muốn nạp lại cấu hình mẫu chuẩn của UIT không? Các thiết lập vừa chỉnh sửa chưa lưu sẽ bị mất."
      confirmText="Đồng ý"
      cancelText="Hủy"
    />
  );
};
