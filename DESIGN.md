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

- Sidebar dùng nền slate-50, chữ menu 13px theo `--font-size-md`, icon Lucide 16px nét 1.75 và hàng 36px trên desktop / tối thiểu 44px trên mobile. Mục đang chọn dùng nền primary/10, chữ và icon primary, không gradient hoặc bóng. Giữ logo và tên ứng dụng ở dòng dưới. Nút thu gọn/mở rộng desktop là chevron đơn trái/phải trong nút tròn 28px nằm vắt qua mép phải sidebar, tâm cách đỉnh 64px, theo ảnh tham chiếu của người dùng; không đặt trong thanh tiêu đề nội dung hoặc dưới logo.
- Điều hướng chia thành Tổng quan, Tổ chức & kế hoạch, Đề tài & hướng dẫn, Tiến độ & đánh giá và Hệ thống; giữ nguyên route và phân quyền. Admin có nhóm đóng/mở, lưu trạng thái theo tài khoản và tự mở nhóm khi chuyển tới trang bên trong; vai trò có ít mục dùng danh sách chia nhóm. Dải icon trên desktop vẫn hiển thị mọi liên kết được phép; mobile luôn có nhãn đầy đủ, đóng bằng Escape và giữ focus trong menu khi mở.
- Thông tin đợt ở sidebar dùng vùng gọn có đường phân cách, không bọc nhiều thẻ. Giữ tên/năm/học kỳ của tất cả đợt hiện hành từ API cùng trạng thái loading, lỗi/thử lại và thiếu quyền.

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

## Thông báo và phản hồi

Toast dùng Sonner qua API chung `notify`, giao diện Tailwind và nội dung tiếng Việt. Thành công/thông tin tự đóng sau 5 giây; cảnh báo/lỗi/có thao tác giữ đến khi đóng. Tối đa ba toast hiển thị, không tạo trùng cùng nội dung. Giữ lỗi nhập liệu cạnh trường cùng summary có liên kết; lỗi tải và cảnh báo cần xử lý lâu dài dùng InlineAlert tại vùng bị ảnh hưởng. ConfirmDialog dành cho quyết định có hậu quả, warning dùng amber. Không thay inbox nghiệp vụ bằng toast. Quy ước và ví dụ: [docs/notifications.md](docs/notifications.md).

Toast dùng thẻ trắng bo góc 12px, viền và bóng nhẹ, icon tròn đặc căn giữa cạnh nội dung. Giữ màu thành công emerald, thông tin blue, cảnh báo amber và lỗi rose; bỏ dải màu cố định phía trên. Toast tự đóng có thanh thời gian 3px ở cạnh dưới, co dần theo thời gian và tạm dừng cùng Sonner khi hover, focus hoặc tab bị ẩn. Toast giữ đến khi đóng và loading không hiển thị thanh đếm ngược. Khi giảm chuyển động, thanh cập nhật theo nhịp giây. Nhiều toast xếp chồng và mở rộng khi hover hoặc dùng F8.

## Quản lý đợt & Kế hoạch đào tạo

- Phong cách chủ đạo của trang Khởi tạo đợt & Deadline (`/academic/periods`): Kết hợp **Modern Clean SaaS** và **Fluent Design** — tối giản, thanh lịch, nền slate canvas thoáng đãng, thẻ `bg-white` phẳng viền `border-slate-200/80` bo góc 16px (`rounded-2xl`), triệt tiêu hoàn toàn tình trạng thẻ lồng thẻ (card soup) và nhãn chú thích thừa.
- Điểm nhấn nhận diện (Brand Accent): Sử dụng màu xanh primary (`#2563eb`) và dải gradient chuyển sắc nhận diện từ xanh dương `#2054A5` sang cam `#FA7921` (`linear-gradient(90deg, #2054a5 0%, #2563eb 55%, #fa7921 100%)` hoặc `--gradient-brand`) trên thanh accent đỉnh của `PageHeader`, viền/hover active và vạch trạng thái; tuyệt đối không áp dụng gradient lên văn bản nội dung (`gradient-text`) hoặc hàng dữ liệu.
- Dòng thời gian kế hoạch (`PeriodTimeline`):
  - Dải tiến trình phân đoạn ngang dùng bảng màu trung tính Tonal Slate (`bg-slate-100/70` bọc các pill `bg-white/95`), thay thế hoàn toàn bảng màu sặc sỡ đa sắc trước đây.
  - Áp dụng thang tỷ lệ trọng số động (weighted flex scaling) kết hợp `min-width: 78px` và `whitespace-nowrap`, đảm bảo các giai đoạn ngắn 1 tuần (*Đề xuất, Đăng ký, Giữa kỳ, Công bố*) luôn hiển thị trọn vẹn tên mà không bao giờ bị cắt chữ `...`.
  - Hiệu ứng Fluent Motion: Chi tiết từng giai đoạn chỉ trượt xuống từ trên khi rê chuột vào dải tiến trình (`slide-in-from-top-3` với `animate-in fade-in duration-300 ease-out`), không giữ khung rỗng tĩnh khi chưa hover để giữ giao diện luôn gọn gàng.
- Cấu hình cổng thẩm định (`PeriodReviewGateDialog`):
  - Tự động điền trước (auto-prefill) các mốc gợi ý chuẩn suy diễn từ hạn nộp đề cương và giữa kỳ của đợt (`defaultSyllabusDeadline`, `defaultMidtermDeadline`), giải quyết dứt điểm lỗi trường trống `dd/mm/yyyy`.
  - Quy trình 3 bước trực quan: *Sinh viên nộp đề cương/báo cáo ban đầu ➔ Khoa / GV phản hồi & thẩm định ➔ Hạn chót đóng cổng & nộp hoàn thiện*.
- Biểu mẫu & điều hướng:
  - Dropdown chọn đợt mở rộng (`w-full sm:w-80 md:w-96`) hiển thị đầy đủ tên đợt và huy hiệu trạng thái; `PageHeader` dùng cấu trúc `relative z-20` không có `overflow-hidden` để menu con hiển thị nổi mà không bị cắt xén.
  - Nút bấm `Select` có `select-none` để tránh hiện con trỏ văn bản `|` khi tương tác.
  - Nhãn trạng thái đợt (*Đang hoạt động*, *Bản nháp*, v.v.) luôn có `whitespace-nowrap shrink-0` để không bị gãy dòng.
