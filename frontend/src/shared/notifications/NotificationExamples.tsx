import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Dialog } from '@/components/ui/dialog';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { InlineAlert } from '@/components/ui/inline-alert';
import { PendingState } from '@/components/ui/pending-state';
import { notify } from './notify';

export function NotificationExamples() {
  const [dialogOpen, setDialogOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  return (
    <section
      aria-label="Mẫu thông báo"
      className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6"
    >
      <div>
        <h2 className="text-lg font-semibold text-slate-900">Thông báo và phản hồi</h2>
        <p className="mt-1 text-sm text-slate-600">
          Dữ liệu minh họa. Các nút bên dưới không lưu hoặc thay đổi dữ liệu hệ thống. Nhấn F8 để
          đến toast; Escape để đóng toast đang focus.
        </p>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button
          variant="outline"
          onClick={() =>
            notify.success('Đã lưu bản nháp cấu hình điểm.', { title: 'Lưu bản nháp (minh họa)' })
          }
        >
          Toast thành công
        </Button>
        <Button
          variant="outline"
          onClick={() => notify.info('Danh sách đã được cập nhật theo bộ lọc mới.')}
        >
          Toast thông tin
        </Button>
        <Button
          variant="outline"
          onClick={() => notify.warning('Có thay đổi chưa lưu. Hãy kiểm tra trước khi rời trang.')}
        >
          Toast cảnh báo
        </Button>
        <Button
          variant="outline"
          onClick={() =>
            notify.error('Không thể lưu bản nháp. Nội dung bạn nhập vẫn được giữ lại.')
          }
        >
          Toast lỗi
        </Button>
        <Button
          variant="outline"
          onClick={() =>
            notify.error('Tác vụ mô phỏng chưa hoàn tất.', {
              action: {
                label: 'Thử lại',
                onClick: () => {
                  notify.success('Tác vụ mô phỏng đã hoàn tất.');
                },
              },
            })
          }
        >
          Toast có thao tác
        </Button>
        <Button variant="outline" onClick={() => setDialogOpen(true)}>
          Toast trong hộp thoại
        </Button>
        <Button variant="outline" onClick={() => setConfirmOpen(true)}>
          Mẫu xác nhận
        </Button>
        <Button variant="ghost" onClick={notify.dismissAll}>
          Đóng tất cả toast
        </Button>
      </div>
      <InlineAlert tone="warning" title="Cảnh báo tại bảng">
        Tổng trọng số là 95%. Cần đạt 100% trước khi công bố.
      </InlineAlert>
      <InlineAlert
        title="Lỗi tải dữ liệu"
        action={
          <Button
            variant="outline"
            onClick={() =>
              notify.info('Đây là nút thử lại minh họa; không có request gửi đến máy chủ.')
            }
          >
            Thử lại tải dữ liệu
          </Button>
        }
      >
        Thông báo này ở lại trong vùng bị ảnh hưởng và cung cấp cách xử lý.
      </InlineAlert>
      <PendingState label="Ví dụ trạng thái đang tải dữ liệu" />
      <Dialog
        open={dialogOpen}
        onClose={() => setDialogOpen(false)}
        title="Kiểm tra toast trong hộp thoại"
      >
        <div className="space-y-4 p-5">
          <p className="text-sm text-slate-600">
            Toast cần hiển thị phía trên hộp thoại, nhận được bàn phím và không đóng hộp thoại khi
            bạn đóng toast.
          </p>
          <Button
            onClick={() => notify.info('Thông báo này được phát trong hộp thoại.', { duration: 0 })}
          >
            Hiện toast trong dialog
          </Button>
          <Button variant="outline" onClick={() => setDialogOpen(false)}>
            Đóng hộp thoại minh họa
          </Button>
        </div>
      </Dialog>
      <ConfirmDialog
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        onConfirm={() => {
          setConfirmOpen(false);
          notify.info('Đây chỉ là mẫu xác nhận, không có thay đổi dữ liệu.');
        }}
        type="warning"
        title="Nạp lại cấu hình mẫu?"
        description="Các thay đổi chưa lưu sẽ bị mất khi nạp lại mẫu. Đây là tình huống minh họa."
        confirmText="Nạp lại mẫu"
      />
    </section>
  );
}
