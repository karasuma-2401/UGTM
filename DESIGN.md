# UGTM design context

Tệp này ghi nhận nền tảng giao diện từ source hiện tại, không phải một đợt redesign hay chứng nhận rằng UI đã đạt accessibility.

## Nền tảng hiện có

- React 19, TypeScript, Vite 8 và Tailwind CSS 3.
- Component dùng chung: `frontend/src/components/ui/`.
- Layout/điều hướng: `frontend/src/shared/layout/`.
- Token CSS: `frontend/src/styles/theme.css`; font chính đang khai báo là Inter với system fallback.
- Token HSL của Tailwind/shadcn nằm ở `frontend/src/index.css`, ánh xạ tại `frontend/tailwind.config.js`.
- Màu primary hiện tại là xanh `#2563eb`. Ant Design vẫn được sử dụng ở một số nơi; kiểm tra component đang dùng trước khi thay đổi.

## Cách mở rộng giao diện

- Ưu tiên tái sử dụng component và token hiện có; kiểm tra cả hai nguồn token trước khi thêm giá trị mới.
- Với chỉnh sửa cục bộ, giữ bố cục, nội dung nghiệp vụ và nhận diện ngoài phạm vi yêu cầu. Thay toàn bộ phong cách cần một yêu cầu redesign rõ ràng.
- Bảng và form cần nhãn rõ, focus bàn phím nhìn thấy được, phản hồi loading/lỗi và trạng thái không chỉ phân biệt bằng màu.
- Font size nhỏ hiện có trong source là hiện trạng cần đánh giá theo ngữ cảnh, không phải tiêu chuẩn để sao chép vào mọi màn hình mới.
- Kiểm tra giao diện hẹp, chuỗi tiếng Việt dài, dữ liệu thiếu và trạng thái disabled/pending của các thao tác bị ảnh hưởng.
- Nguồn tên người dùng, đợt, trạng thái và số liệu là API; không tự thêm thông tin giả định để làm đầy bố cục.

## Bằng chứng và cập nhật

Trước khi sửa UI, xem component và giao diện được render. Sau khi sửa, kiểm tra các viewport và trạng thái có liên quan, lưu ảnh/trace trong `artifacts/playwright/` nếu cần.

Khi một quyết định thiết kế được chấp nhận, cập nhật tệp này hoặc nguồn token/component tương ứng. Kết quả tra cứu từ skill là gợi ý; không tự ghi đè các quyết định hiện có.
