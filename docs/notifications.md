# Thông báo trong UGTM

Phần toast dùng Sonner, được cấu hình tại `shared/notifications/NotificationProvider.tsx`. Giao diện tùy chỉnh bằng Tailwind `className`; không có file CSS toast riêng. Chỉ gọi `notify` từ `@/shared/notifications` để giữ cùng một chính sách trên toàn hệ thống.

| Tình huống | Thành phần | Vị trí / hành vi |
| --- | --- | --- |
| Lưu, cập nhật hoặc hoàn tất thao tác | `notify.success` | Toast góc phải trên, tự đóng sau 5 giây |
| Kết quả trung tính | `notify.info` | Toast 5 giây |
| Thao tác thất bại, cảnh báo ngắn | `notify.error` / `notify.warning` | Toast giữ đến khi đóng, có thể kèm thao tác |
| Trường nhập sai | `Input error` và `FormErrorSummary` | Cạnh trường; summary nhận focus sau submit và liên kết đến trường |
| Lỗi tải, thiếu quyền, trạng thái cần xử lý lâu dài | `InlineAlert` | Trong vùng dữ liệu bị ảnh hưởng; giữ nút thử lại khi có |
| Mất dữ liệu chưa lưu hoặc hành động cần quyết định | `ConfirmDialog` | Xác nhận rõ đối tượng và hậu quả; warning màu amber, destructive màu đỏ |
| Request đang chạy | Loading trên nút / `PendingState` | Ngay cạnh thao tác; chặn gửi lặp |
| Thông báo nghiệp vụ cần lịch sử, đã đọc/chưa đọc | Inbox dùng API riêng | Chưa triển khai inbox khi chưa có API tương ứng |

Không đưa lỗi validation hoặc lỗi tải trang chỉ vào toast. Không hiện đồng thời hai thông báo cho cùng một kết quả. Không dùng toast loading cho mọi request khi nút đã có trạng thái pending.

```tsx
import { notify } from '@/shared/notifications';

notify.success('Đã lưu cấu hình điểm.');
notify.error('Không thể tải danh sách.', {
  action: { label: 'Thử lại', onClick: () => reload() },
});
const id = notify.loading('Đang xuất báo cáo…');
notify.success('Báo cáo đã sẵn sàng.', { id });
```

`id` cập nhật một toast tại chỗ; `dedupeKey` gom các thông báo cùng tác vụ. Nội dung giống nhau mặc định được gom, không tạo nhiều toast. `duration: 0` giữ thông báo đến khi đóng. Nút action chạy một lần trong lúc pending, giữ khả năng thử lại nếu thất bại; chỉ dùng thông điệp công khai, không hiển thị nguyên lỗi nội bộ. Đổi phiên hoặc đăng xuất sẽ đóng toàn bộ toast và bỏ kết quả action cũ.

Provider đặt một Sonner Toaster ở cấp ứng dụng, tối đa ba toast nhìn thấy cùng lúc. Sonner quản lý stacking, animation, thời gian và tạm dừng khi hover hoặc tab bị ẩn. Khi focus bàn phím vào vùng toast, thời gian được tạm dừng và cấp lại khoảng đọc khi rời vùng. F8 chuyển đến toast, Tab đến nút, Escape đóng toast đang focus. Toast có nút đóng và nội dung tiếng Việt. Giữ reduced motion của thư viện.

Giao diện thẻ trắng gọn với icon tròn đặc và màu trạng thái giữ nguyên: emerald / blue / amber / rose. Thanh ở cạnh dưới biểu thị thời gian còn lại của toast tự đóng; không hiển thị cho toast giữ đến khi đóng hoặc loading. `useToastCountdown` chỉ điều khiển animation hiển thị, Sonner vẫn quyết định thời điểm đóng. Thanh dừng theo trạng thái mở rộng của Sonner, tương tác, focus và visibility của trang; duration hoặc nội dung cập nhật tiếp tục theo timer của thư viện. Không dùng pseudo-element của Sonner vì chúng phục vụ hit area khi xếp chồng/swipe. Người bật reduced motion thấy các bước theo giây thay vì chuyển động liên tục.

Native dialog làm phần ngoài dialog inert; portal ổn định được chuyển vào dialog đang mở và dùng Popover API để hiển thị trên top layer. Vì không remount Toaster, mở/đóng dialog không tạo lại toàn bộ toast. Escape trong toast không đóng dialog bên dưới.

`OperationToast` là cầu nối cho các hook đang trả chuỗi success; code mới nên gọi `notify` tại nơi nhận kết quả thành công, thay vì thêm state chỉ để hiển thị thông báo.

Mẫu thử tương tác ở `/ui-test`, phần **Thông báo và phản hồi**. Các nút minh họa không gửi request ghi dữ liệu. Các lỗi form mẫu được áp dụng ở dialog thêm thành phần điểm.
