# UGTM design context

Tệp này ghi nhận nền tảng giao diện từ source hiện tại, không phải một đợt redesign hay chứng nhận rằng UI đã đạt accessibility.

## Nền tảng hiện có

- React 19, TypeScript, Vite 8 và Tailwind CSS 3.
- Component dùng chung: `frontend/src/components/ui/`.
- Layout/điều hướng: `frontend/src/shared/layout/`.
- Token CSS: `frontend/src/styles/theme.css`; font chính đang khai báo là Inter với system fallback.
- Token HSL của Tailwind/shadcn nằm ở `frontend/src/index.css`, ánh xạ tại `frontend/tailwind.config.js`.
- Màu primary hiện tại là xanh `#2563eb`. Ant Design vẫn được sử dụng ở một số nơi; kiểm tra component đang dùng trước khi thay đổi.
- Phong cách chủ đạo: **Modern Clean SaaS** — giữ bố cục tối giản, thoáng đãng kết hợp lớp nền phân tầng tinh tế (`#f8fafc` canvas + thẻ `bg-white` viền `border-slate-200/80` bóng đổ nhẹ), thanh tiêu đề kính mờ (`bg-white/80 backdrop-blur-md`), điều hướng nhanh `Ctrl+K` và bảng tổng quan dạng Bento Grid dùng dữ liệu thật từ API.
- Không sử dụng chữ chuyển sắc (`gradient-text`) cho tiêu đề hoặc số liệu; nhấn mạnh bằng độ đậm (`font-semibold`/`font-bold`) và tỷ lệ kích thước chữ.

## Cách mở rộng giao diện

- Trang tài khoản dùng header gọn, thống kê dạng bộ lọc nhanh và thanh lọc sát danh sách. Hàng bảng khoảng 72px trên desktop; màn hình nhỏ chuyển thành danh sách gọn. Thao tác thay đổi trạng thái nằm trong menu phụ và giữ bước xác nhận.
- Tab tài khoản liền nhau và sát mép thẻ. Nền tab, vạch chọn hàng và khung chọn vai trò trượt bằng transform trong 360ms; nội dung giữ nguyên vị trí. Khi giảm chuyển động, dấu chọn đổi vị trí ngay.
- Chuyển tab tài khoản ưu tiên commit route ngay (`navigate` với `flushSync`), hiển thị skeleton độc lập khi API đang tải. Không đợi hiệu ứng hoặc dữ liệu; router vẫn chặn rời trang nếu phân quyền chưa lưu. Ctrl/Cmd + bấm giữ hành vi mở liên kết của trình duyệt.
- Nhấp một lần chọn hàng tài khoản; nhấp đúp mở chi tiết giống nút Chi tiết. Thao tác trên nút/menu trong hàng không kích hoạt nhấp đúp của hàng. Bộ lọc và nhãn Vô hiệu hóa dùng màu đỏ, vẫn giữ tên trạng thái bằng chữ.
- Input và Select dùng viền xanh cùng ring xanh nhạt 2px khi focus; không chuyển tiếp outline mặc định. Combobox giữ viền focus khi con trỏ chuyển vào ô tìm kiếm; trường lỗi giữ màu đỏ.
- Phân trang dùng `TablePagination` chung: số trang hiện tại nền xanh, các trang lân cận và dấu “…” cho danh sách dài. Chọn số hàng bằng nhóm nút 10 / 20 / 50 thay dropdown native; giữ giá trị tùy chỉnh nếu trang đang dùng kích thước khác. Mobile dùng chỉ báo trang và nút đầu/trước/sau/cuối, bố cục xuống dòng để tránh tràn ngang.
- Thao tác Excel dùng xanh lá emerald: nút chính `success`, nút phụ `successOutline`, biểu tượng và vùng chọn file cùng tông. Giữ màu đỏ cho lỗi và màu xanh dương cho điều hướng hệ thống.
- Popup nhập sinh viên chia hai bước: chuẩn bị file mẫu và chọn danh sách. Vùng upload hỗ trợ bấm/kéo thả, đổi/xóa file, báo lỗi ngay với file rỗng/sai định dạng/quá 5 MB. Footer giữ trong tầm nhìn khi cuộn; khóa đóng/chỉnh file trong lúc nhập. Giữ giới hạn 5.000 sinh viên/lần và thông báo tài khoản chờ kích hoạt.
- Chi tiết tài khoản dùng `Dialog variant="drawer"` rộng 560px bên phải, toàn màn hình trên mobile. Header là avatar, tên và mã tài khoản; trạng thái nằm bên phải, căn giữa cụm tên/mã. Nội dung mở ở chế độ xem; nút Chỉnh sửa chuyển sang form trong cùng drawer. Nhãn vai trò căn phải cùng hàng tiêu đề. Header/footer giữ khi cuộn.
- Tạo tài khoản dùng popup giữa màn hình, tiêu đề một cấp, trường bắt buộc có dấu *, giải thích email kích hoạt và lựa chọn vai trò theo nhóm. Form chỉ bật Lưu khi có thay đổi; giữ dữ liệu nếu lỗi, xác nhận trước khi đóng/hủy/rời trang với thay đổi chưa lưu và chặn thao tác khi đang lưu. Drawer trượt từ phải trong 360ms; giảm chuyển động dùng opacity 120ms.
- Hộp thoại xác nhận (`ConfirmDialog`) dùng bố cục tối giản không icon theo phong cách sản phẩm thực tế: tiêu đề căn giữa phía trên đường kẻ ngang phân cách rõ nét (`border-slate-300`), nội dung giải thích và đối tượng tác động căn trái bên dưới, cụm nút Hủy và Xác nhận căn phải gọn gàng ở góc dưới (mobile tự xếp chồng).
- Trang Tài khoản & Phân quyền dùng gradient nhận diện ở năm điểm: dải 4px trên PageHeader, gạch chân 2px của tab đang chọn (dùng `--gradient-brand`), nền header drawer xanh nhạt → trắng → cam nhạt, viền chuyển sắc mảnh với nền sáng cho vai trò đang chọn và nền chuyển sắc xanh nhạt cho avatar tài khoản. Giữ chữ màu đơn, màu trạng thái và Excel; không thêm gradient vào hàng dữ liệu hoặc các nút nghiệp vụ.
- Vai trò và phân hệ dùng tên tiếng Việt trên giao diện; mã gửi API giữ nguyên. Tên phân hệ quyền lấy từ `shared/config/modules.ts`. Phân quyền giữ thay đổi chưa lưu, cảnh báo khi chuyển vai trò/rời trang và khóa chỉnh sửa khi tải lỗi.
- Chuyển trang bắt đầu trước khi vẽ bằng `useLayoutEffect`, dịch 10px trong 360ms và giữ opacity từ 0.94 để tránh chớp nội dung. Đổi vai trò dùng panel dịch 8px/360ms; dialog mở dịch/zoom nhẹ trong 320ms, backdrop 260ms. Easing cho nội dung là `cubic-bezier(0.22, 1, 0.36, 1)`. Nút giữ phản hồi nhấn ngắn. Với `prefers-reduced-motion`, chỉ giữ opacity 0.97 → 1 trong 120ms, bỏ chuyển động vị trí/zoom.

- Ưu tiên tái sử dụng component và token hiện có; kiểm tra cả hai nguồn token trước khi thêm giá trị mới.
- Với chỉnh sửa cục bộ, giữ bố cục, nội dung nghiệp vụ và nhận diện ngoài phạm vi yêu cầu. Thay toàn bộ phong cách cần một yêu cầu redesign rõ ràng.
- Bảng và form cần nhãn rõ, focus bàn phím nhìn thấy được, phản hồi loading/lỗi và trạng thái không chỉ phân biệt bằng màu.
- Font size nhỏ hiện có trong source là hiện trạng cần đánh giá theo ngữ cảnh, không phải tiêu chuẩn để sao chép vào mọi màn hình mới.
- Kiểm tra giao diện hẹp, chuỗi tiếng Việt dài, dữ liệu thiếu và trạng thái disabled/pending của các thao tác bị ảnh hưởng.
- Nguồn tên người dùng, đợt, trạng thái và số liệu là API; không tự thêm thông tin giả định để làm đầy bố cục.

## Bằng chứng và cập nhật

- Trường ngày dùng `DatePicker`, trường ngày giờ dùng `DateTimePicker` từ `frontend/src/components/ui/calendar.tsx`; không dùng calendar của Ant Design.
- Calendar dùng tiếng Việt, tuần bắt đầu thứ Hai; hiển thị `dd/MM/yyyy` và giờ 24 giờ. Giá trị controlled giữ `yyyy-MM-dd` hoặc `yyyy-MM-ddTHH:mm` theo giờ địa phương; chuyển đổi UTC thuộc adapter nghiệp vụ.
- Tiêu đề calendar chuyển tầng ngày → tháng → năm theo thập niên; chọn năm → tháng → ngày để quay lại. Không dùng dropdown cho tháng/năm; các nút điều hướng đổi tháng, năm hoặc thập niên theo tầng đang mở.
- Chuyển tầng calendar dùng zoom/fade 200ms theo chiều mở rộng hoặc đi vào chi tiết; `prefers-reduced-motion` dùng phản hồi opacity 80ms, không zoom.
- Giữ `min`, `max`, `required`, `disabled` và nhãn truy cập khi tích hợp. Popup dùng Popover API để hiển thị trên native dialog; hỗ trợ phím mũi tên, Home/End, PageUp/PageDown và Escape.

Trước khi sửa UI, xem component và giao diện được render. Sau khi sửa, kiểm tra các viewport và trạng thái có liên quan, lưu ảnh/trace trong `artifacts/playwright/` nếu cần.

Khi một quyết định thiết kế được chấp nhận, cập nhật tệp này hoặc nguồn token/component tương ứng. Kết quả tra cứu từ skill là gợi ý; không tự ghi đè các quyết định hiện có.
