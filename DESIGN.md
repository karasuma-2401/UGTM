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

## Quản lý đề tài & Hướng dẫn khóa luận

- Thẻ phân đoạn chuyển trạng thái (`StatusTabs`):
  - Kế thừa và chuẩn hóa component `StatusTabs` (`frontend/src/components/ui/status-tabs.tsx`) dùng chung cho toàn bộ hệ thống (Tài khoản người dùng, Đề xuất đề tài, Đề tài hướng dẫn).
  - Sử dụng thẻ trượt mượt mà bằng phần cứng (`translate3d`, `width`, cubic-bezier `(0.16, 1, 0.3, 1)` 220ms), nền `bg-slate-100/90`, indicator `bg-white shadow-2xs border border-slate-200/90`, huy hiệu đếm số lượng dạng pill với số định dạng theo locale tiếng Việt (`tabular-nums`).
- Thẻ Bento cột phải (4 cột) trên trang Đề tài:
  - Cụm thống kê (`StatisticsBar` dạng `layout="card"`): Dùng lưới 2 cột (`grid-cols-2 gap-2.5`) hoặc 3 cột cân đối, tuyệt đối không dồn 4 ô vào một hàng ngang hẹp khiến nhãn bị co rút thành 1 ký tự (`T`, `C`, `N`, `S`).
  - Danh sách quy định/chính sách: Căn chỉnh `items-start` với nhãn tự động xuống hàng mềm mại (`leading-snug break-words`), nghiêm cấm áp dụng `whitespace-nowrap` khiến nội dung tràn qua mép thẻ (out khỏi vùng chứa chữ).
- Bảng dữ liệu và định danh thực thể:
  - Loại bỏ các icon lặp đi lặp lại vô nghĩa (như icon `FileText` lặp lại trên từng hàng tiêu đề đề tài); ưu tiên hệ thống phân cấp kiểu chữ (typography) thoáng đãng, sắc nét, dành trọn vẹn bề ngang cho tên đề tài tiếng Việt và tiếng Anh.
  - Tuyệt đối không hiển thị trực tiếp GUID thô (như `99999999-9999-...`) ra giao diện người dùng. Hiển thị huy hiệu định danh thân thiện (ví dụ `Nhóm 01`, `Nhóm 02`) kèm mã băm rút gọn `#{id.slice(0, 8)}` và ngày tạo.
  - Phân trang: Luôn sử dụng component chuẩn `<TablePagination>` với thuộc tính `onPageSizeChange` tích hợp sẵn nút phân đoạn (`[ 10 ] [ 20 ] [ 50 ]`); không tự viết thêm các thẻ `<select>` lẻ tẻ trên giao diện.

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
  - Dropdown chọn đợt mở rộng (`w-full sm:w-96 md:w-[440px]`), menu danh sách tự động co giãn theo độ dài nội dung (`w-max min-w-full sm:min-w-[440px] max-w-[90vw] sm:right-0 sm:left-auto`), hiển thị trọn vẹn tên đợt và học kỳ mà không bị cắt xén chữ `...`; `PageHeader` dùng cấu trúc `relative z-20` không có `overflow-hidden` để menu con hiển thị nổi.
  - Các mục trong dropdown dùng cỡ chữ `text-xs` (12px) đồng bộ tỉ lệ với nút trigger, góc bo mềm mại `rounded-lg`, hover `hover:bg-slate-50` thanh nhã, giữ nguyên cấu trúc văn bản thuần túy không lạm dụng icon hay nhãn trang trí rườm rà.
  - Nút bấm `Select` có `select-none` để tránh hiện con trỏ văn bản `|` khi tương tác.
- Trang Danh sách Sinh viên & Giảng viên (`/academic/roster`):
  - Áp dụng cấu trúc Bento Grid 8-4 cột đồng bộ phong cách Modern Clean SaaS + Fluent Design: Thẻ cấu hình tải tệp bên trái (8 cột) kết hợp Thẻ tiêu chuẩn danh sách dạng quy chuẩn đào tạo bên phải (4 cột).
  - Thẻ tiêu chuẩn danh sách bên phải (`RosterGuideCard`) sử dụng cấu trúc phân tầng thông tin đa lớp đồng bộ với `PeriodSummaryCard`: gồm 2 ô thông số kỹ thuật chuẩn (`Định dạng tệp`, `Cột bắt buộc`), cụm huy hiệu dữ liệu cột trực quan (`Mã số định danh`, `Họ và tên`, `Hộp thư @uit.edu.vn`, `Khoa / Đơn vị`) và danh mục nguyên tắc tài khoản/phân quyền dạng key-value cô đọng. Triệt tiêu hoàn toàn cảm giác trống trải cũng như tình trạng dồn ứ chữ hoặc lạm dụng icon hộp màu ("AI design").
  - Thẻ tiêu chuẩn danh sách bên phải (`RosterGuideCard`) sử dụng cấu trúc phân tầng thông tin đa lớp đồng bộ với `PeriodSummaryCard`: gồm 2 ô thông số kỹ thuật chuẩn (`Định dạng tệp`, `Cột bắt buộc`), cụm huy hiệu dữ liệu cột trực quan (`Mã số định danh`, `Họ và tên`, `Hộp thư @uit.edu.vn`, `Khoa / Đơn vị`) và danh mục nguyên tắc tài khoản/phân quyền dạng key-value cô đọng. Triệt tiêu hoàn toàn cảm giác trống trải cũng như tình trạng dồn ứ chữ hoặc lạm dụng icon hộp màu ("AI design").
  - Nghiêm cấm sử dụng các từ viết tắt gây khó hiểu: Dùng trọn vẹn "Sinh viên & Giảng viên" (thay cho "SV / GV"), "Mã số sinh viên" / "Mã số giảng viên" (thay cho "Mã SV / GV"), "Giảng viên hướng dẫn & phản biện" (thay cho "GVHD/GVPB"), "Học kỳ 1" (thay cho "HK1").
  - Phân loại đối tượng dùng Fluent Segmented Control có icon (`GraduationCap`, `Briefcase`) và `select-none`, chuyển đổi mượt mà giữa danh sách Sinh viên và Giảng viên.
  - Thao tác Excel dùng tông màu xanh lá Emerald chuẩn (`emerald-600`, `emerald-50`, `border-emerald-300`), hover nâng sắc độ nhẹ nhàng (`hover:bg-emerald-100 hover:border-emerald-400`), không đảo màu đột ngột sang nền đặc tối.
  - Vùng tải tệp Excel hỗ trợ kéo thả (`border-2 border-dashed border-slate-300 hover:border-emerald-500 hover:bg-emerald-50/40`), hiển thị trạng thái tệp đã chọn với icon rõ ràng và nút đổi tệp nhanh.
  - Bảng đối chiếu xem trước có hàng tiêu đề tự động điều chỉnh theo vai trò đang chọn ("Mã số sinh viên" hoặc "Mã số giảng viên"), các huy hiệu trạng thái đối chiếu dạng pill có icon rõ nét (`Hợp lệ`, `Cần điều chỉnh`, `Trùng lặp mã trong tệp`).

## Đề tài hướng dẫn (`/topics/advising`)

- Thanh điều hướng chuyển tab (`NavigationTabs`, `TopicGuidancePage`):
  - Thiết kế thành component dùng chung (`frontend/src/components/ui/navigation-tabs.tsx`), kế thừa hiệu ứng chuyển động mượt mà của thanh tab Tài khoản & Phân quyền (`IdentityNavigation`) qua `SelectionIndicator` (`cubic-bezier(0.22, 1, 0.36, 1)`, thời gian trượt 360ms).
  - Chiều cao chuẩn `h-12`, tab active nổi bật với chữ màu `text-primary`, nền mờ nhẹ `bg-primary/5` và đường viền gạch chân đáy 2px chuyển sắc thương hiệu (`after:[background-image:var(--gradient-brand)]`).
  - Hỗ trợ cả định dạng `Link` của React Router lẫn nút chọn thuần `button`, có hỗ trợ badge số lượng (`count`), icon đại diện và tương thích khả năng truy cập WAI-ARIA.
  - Tích hợp mượt mà vào đáy `PageHeader` dạng `compact accent` mà không tạo đường phân cách thừa.
- Cấu trúc Bento Grid 8-4 cột đồng bộ (Hàng 1):
  - Thẻ bên trái (8 cột): Khung tìm kiếm & bộ lọc với tiêu đề rõ ràng, mô tả mục đích, input tìm kiếm từ khóa, menu chọn đợt khóa luận và trạng thái, nút *Áp dụng* (`variant="soft"`) và *Bỏ lọc* (`variant="outline"`), kèm các thẻ chip bộ lọc đang áp dụng có nút xóa nhanh.
  - Thẻ bên phải (4 cột): Tổng quan tình trạng đề tài & tiến độ hướng dẫn, hiển thị thanh số liệu trực quan `StatisticsBar` (cho phép bấm vào từng số liệu để lọc hoặc chuyển tab nhanh), danh mục quy định đào tạo dạng key-value cô đọng (hạn ngạch sinh viên/đề tài, thẩm định từ Khoa, các mốc nộp hồ sơ) và ghi chú hướng dẫn ở chân thẻ.
- Bảng dữ liệu đề tài & nhóm hướng dẫn (Hàng 2):
  - Thẻ bảng dữ liệu toàn màn hình với thanh tiêu đề SaaS hiện đại: Tên danh sách, phụ đề giải thích và huy hiệu đếm tổng số dòng dạng pill (`Tổng số: {total} đề tài / nhóm`).
  - Tiêu đề cột dùng font trung tính cao cấp `text-[11px] font-semibold uppercase tracking-wider text-slate-500` với nền `bg-slate-50/90`.
  - Loại bỏ hoàn toàn các từ viết tắt gây khó hiểu: Thay "Đợt KLTN" bằng "Đợt khóa luận", "{x} SV" bằng "{x} sinh viên", "SV đã xác nhận" bằng "Sinh viên đã xác nhận".
  - Ô thông tin đề tài & sinh viên (`InfoCell`) sử dụng icon đại diện đóng hộp bo góc mềm `rounded-xl bg-blue-50 text-blue-600 ring-1 ring-blue-100/80` cùng tên đề tài rõ nét.
  - Các nút thao tác trong bảng (*Chi tiết*, *Nhân bản*, *Gửi duyệt*) chuẩn hóa bo góc `rounded-xl` với icon và khoảng cách thao tác chuẩn xác.
