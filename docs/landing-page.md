# Landing page và kho khóa luận

## Đường dẫn

- `/`: khách xem landing page; tài khoản đã xác thực chuyển tới `/dashboard`.
- `/welcome`: luôn mở landing page, kể cả khi đã đăng nhập.
- `/dashboard`: dashboard cũ; các đường dẫn nghiệp vụ khác không đổi.
- `/library`: tra cứu công khai, bộ lọc lưu trong URL.
- `/library/:slug`: metadata công khai; đăng nhập để lấy PDF được duyệt công bố.

Chạy frontend: `npm --prefix frontend run dev`. API dùng `VITE_API_BASE_URL`, mặc định development `http://localhost:5287`.

## Công bố metadata và tài liệu

Backend đọc danh sách biên tập `ThesisLibrary:Publications` từ cấu hình ASP.NET hiện có. Mặc định rỗng. Không tự đưa mọi hồ sơ `ArchiveConfirmed` ra công khai, không đưa dữ liệu mẫu vào production và không sửa quyền của API lưu chiểu.

Mỗi entry là một quyết định công bố metadata được người vận hành phê duyệt. Các trường dưới đây là bản thông tin đã duyệt để xuất hiện công khai. Không điền MSSV, email, số điện thoại, điểm hay nhận xét nội bộ. `PublishedAt` ở tương lai chưa xuất hiện trong danh sách và facets.

Ví dụ cấu trúc **để điền thông tin thật đã được duyệt**, không phải dữ liệu được tự động nạp:

```json
{
  "ThesisLibrary": {
    "Publications": [{
      "Slug": "ten-khoa-luan-da-duyet",
      "Title": "Tên khóa luận đã được duyệt công bố",
      "Abstract": "Tóm tắt được phép hiển thị công khai.",
      "Authors": ["Tên tác giả được phép hiển thị"],
      "Advisor": "Tên giảng viên hướng dẫn",
      "Department": "Khoa / ngành",
      "AcademicYear": "2025-2026",
      "Semester": "Học kỳ 2",
      "Keywords": ["Từ khóa"],
      "PublishedAt": "2026-11-17T00:00:00Z"
    }]
  }
}
```

Nếu chỉ có metadata, bỏ cả ba trường binding tài liệu. Để cho **mọi tài khoản UGTM đang Active** đọc PDF của entry đã được duyệt, thêm đủ `SubmissionId`, `PackageVersion`, `ManifestSha256` lấy từ hồ sơ lưu chiểu đã xác nhận. Đây là quyền đọc theo danh sách công bố, độc lập với quyền quản lý hồ sơ gốc. Không nhập object key hoặc URL tài liệu vào cấu hình.

API download kiểm tra xác thực, trạng thái tài khoản hiện tại, entry đã công bố, trạng thái lưu chiểu, phiên bản, archive proof, manifest và file PDF. Chỉ cấp URL private TTL 300 giây; không cấp DOCX, bản cũ hoặc tài liệu ngoài danh sách. Bỏ binding để ngừng cấp URL mới; URL đã cấp tồn tại tối đa đến hết TTL. Thay đổi danh sách cần khởi động lại API (`IOptions` snapshot), không có màn hình biên tập trong phạm vi landing page này. Cấu hình sai/slug trùng làm validation khởi động thất bại.

## API

- `GET /api/library/theses?q=&year=&semester=&department=&sort=newest&page=1&pageSize=12`: anonymous; tìm không phân biệt dấu tiếng Việt, các từ khóa kết hợp AND; giới hạn 200 ký tự, 24 mục/trang. Facets từ toàn bộ entry đã công bố.
- `GET /api/library/theses/{slug}`: anonymous; chỉ DTO metadata, không chứa binding hay URL.
- `GET /api/library/theses/{slug}/document`: Bearer token, tài khoản Active; trả `{ downloadUrl, expiresAt }` sau kiểm tra binding. Chưa công bố/binding sai trả 404, tài khoản inactive trả 403, chưa xác thực trả 401. Các response không cache.

Envelope dùng `ApiResponse<T>` hiện có. UI phân biệt loading, rỗng, không có kết quả và lỗi kết nối. Metadata client không dùng private auth interceptor; tra cứu lỗi không kích hoạt refresh/logout.

## Campus 3D

- Asset: `frontend/public/models/uit-campus.glb`, khoảng 5 MB. Chỉ có một cảnh 3D tương tác ở hero; bỏ phần mô hình trùng bên dưới. Three.js tải riêng khi vùng cảnh đến gần viewport; GLB được cache. Cả lúc tải chunk 3D và GLB đều dùng chung skeleton, không hiển thị ảnh UIT trong trạng thái loading. Ảnh chỉ dùng khi lỗi GPU hoặc tải asset. Dừng render khi ngoài màn hình/tab ẩn.
- Tạo lại bằng `npm --prefix frontend run build:campus`. Script merge geometry theo vật liệu và xuất GLB, không có custom WebGL renderer.
- Nguồn nhận diện: [hướng dẫn tòa nhà UIT 2026](https://www.uit.edu.vn/bai-viet/danhgianangluc2026-thi-sinh-luu-y-khi-tham-du-ky-thi-danh-gia-nang-luc-2026-tai-uit), [UIT 360°](https://tuyensinh.uit.edu.vn/uit-tour-360/), ảnh `frontend/assets/background_campus.png` có sẵn trong dự án. Không đóng gói lại ảnh panorama của bên thứ ba.
- Bố trí tương đối lấy từ [sơ đồ ĐGNL UIT 2026](https://www.uit.edu.vn/media/658974355_1425929439573056_5507253893703980182_n_aab0be5d2b.jpg): chuyển tâm tòa nhà và nút đường trên sơ đồ về hệ trục khuôn viên. Nhà A và thư viện dùng khối đa giác nối nhau; E có kính xanh/core thang trắng; B uốn quanh sân; C là dãy giảng đường; bổ sung khối nhà D và căn tin. **Sơ đồ là minh họa, số tầng/kích thước chưa đo đạc.** Đây là bản dựng tham chiếu được trau chuốt, chưa phải digital twin. Cần ảnh đối chiếu bổ sung để chốt độ giống; thay GLB không cần đổi giao diện.
- Đối chiếu thêm 23 ảnh người dùng chụp: bổ sung cổng A/B, chữ UIT, cột cờ, bậc thềm và mặt đứng A/E, hành lang mở nhà B, khoảng thông tầng và hồ cây xanh nhà C. Thêm mái che nối các tòa, giàn che nan vàng/đỏ ở sân sự kiện, ghế đá, bồn cây, cây cọ, đèn, nhà xe, xe đỗ, căn tin/nhà ăn và sân bóng rổ/cầu lông/pickleball. Vị trí và tỷ lệ chi tiết vẫn là xấp xỉ từ ảnh; không đưa trang trí sự kiện tạm thời vào kiến trúc cố định.
- Góc mặc định khi reload/reset nhìn từ phía cổng A vào trường theo ảnh người dùng xác nhận: cổng ở tiền cảnh, khối A/thư viện bên trái và nhà E bên phải. Giữ độ phóng lớn ban đầu, mở rộng canvas theo bounds từng mesh thay vì đẩy camera lùi ra để vừa khung. FOV chỉ co giãn theo tỷ lệ màn hình và giới hạn chiều rộng viewport. Vùng cảnh giữ chiều cao cố định theo breakpoint; canvas tuyệt đối neo chân mô hình sát phía trên khung điều khiển. Phần vẽ mở rộng khi xoay không tham gia layout, không cập nhật margin/chiều cao trang theo frame. Canvas tương tác dùng `touch-action: none` để thao tác kéo không cuộn trang.
- Tour riêng gồm 21 điểm bay Catmull–Rom khép kín, lấy cảm hứng từ chuyển động trong file preview người dùng cung cấp: hạ thấp vào cổng A, lướt giữa A/E, vào sân B, qua C, ngước nhìn và vượt mái E rồi vòng quanh toàn khuôn viên. Một vòng 20 giây (trước 24/36 giây). Mỗi đoạn có thời lượng riêng: chậm ở khoảng giữa các tòa và khi ngước nhìn E, nhanh ở các đoạn chuyển cảnh/toàn cảnh. Nội suy Hermite đơn điệu trên độ dài cung giữ vận tốc nối liên tục, kể cả cuối vòng; FOV chuyển mượt 34–64°. Đường camera đã đối chiếu va chạm trên GLB. Nhãn góc cập nhật từ vị trí camera thực. Dừng giữ nguyên vị trí; tiếp tục đi từ đó. Chọn góc hoặc xoay tự do dừng tour; reset về cổng A. Giảm chuyển động dùng các góc tĩnh thay cho đường bay liên tục; skeleton không nhấp nháy.
- Điều khiển thu phóng: nút +/− bật tương tác và dừng tour; trong chế độ tự xoay dùng con lăn chuột hoặc chụm/tách hai ngón. Camera có giới hạn khoảng cách 35–450; reset/chọn góc khôi phục toàn cảnh, độ phóng ban đầu. Khi bắt đầu tương tác, chuyển vùng vẽ mở rộng sang viewport cố định với cùng phép chiếu để auto-fit không triệt tiêu zoom. Hàng dưới cùng chứa nút dừng người đi bộ và liên kết UIT 360° đã bỏ theo yêu cầu.
- Cụm sân thể thao đặt trên phần nền nối thêm ra ngoài hàng cây phía sau nhà phục vụ sinh viên, đúng vùng mũi tên người dùng đánh dấu và xác nhận. Nền mới nối cạnh cũ `z = -63`, kéo tới `z = -101`; có lối đi qua hàng cây nối khu nhà với sân. Cả bóng rổ/cầu lông/pickleball, hàng rào, ghế và trụ nước chuyển đồng bộ sang phần nền này; bỏ sân ở vị trí trong khuôn viên cũ. Đường vào cổng B kết thúc trong mép nền, không còn phần đường nhô ra ngoài.
- Người đi bộ dùng instanced meshes, có chuyển động tay/chân, hai tuyến sân trường đã đối chiếu GLB. Kiểm tra khoảng trống theo chiều cao nhân vật cho phép đi dưới mái che nhưng phát hiện cột, tường và đồ vật chắn đường. Hạt giống từ ID ẩn danh và giờ server giữ tuyến đi nhất quán giữa người xem. `prefers-reduced-motion` giữ nhân vật đứng yên.
- Font Manrope tự lưu trữ qua Fontsource (OFL-1.1); CSS giới hạn trong `.public-site`, không đổi theme dashboard.

## Hiện diện trên sân trường

- `POST /api/campus/presence`: anonymous hoặc Bearer token của phiên UGTM hợp lệ. Body `{ visitorId, tabId, leaving?: boolean }`, hai ID phải là UUID khác rỗng. Trả `{ visitors: string[], serverTime: number }`, không cache; không công khai tên, email, user ID hay access token.
- Provider chạy cho khách đang xem landing page và tài khoản đã đăng nhập trên mọi route. Đồng bộ bằng heartbeat mỗi 3 giây khi xem, 15 giây ở trang khác/tab ẩn; một nhân vật cho mỗi khách trên một browser hoặc mỗi tài khoản. Nhiều tab cùng browser và cùng tài khoản trên nhiều browser được gộp. Không thể nhận diện cùng một khách ẩn danh qua các browser/thiết bị khác nhau.
- `pagehide`/unmount gửi leave best effort. Mất mạng/đóng cưỡng bức tự hết hạn sau 75 giây không heartbeat. Khi API lỗi, UI báo chưa đồng bộ và bỏ nhân vật cũ; tự kết nối lại. Presence không kích hoạt refresh/logout của auth client.
- Dùng Redis khi cấu hình qua các khóa Redis hiện có; Lua cập nhật lease nguyên tử, dùng giờ Redis, cùng hash slot để chạy Redis Cluster. Các API replica phải dùng chung Redis và `Redis:InstanceName`. Nếu không cấu hình Redis, local dùng singleton memory (chỉ đồng bộ trong một API process). Redis lỗi trả 503, không fallback sang danh sách cục bộ gây sai số.
- Giới hạn 5.000 kết nối/tab đồng thời, 600 request/phút/IP; đây là chỉ báo hiện diện để minh họa sân trường, không phải số liệu thống kê truy cập hoặc bằng chứng về danh tính khách ẩn danh. Không lưu lịch sử truy cập.

## Kiểm tra

`npm --prefix frontend run test:auth`, `node --test frontend/test/thesis-library.test.mjs`, lint, build; `dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --filter FullyQualifiedName~ThesisLibraryTests -m:1`.

Presence/tour: `node --test frontend/test/campus-journey.test.mjs`; `dotnet test backend/tests/ThesisManagement.Tests/UGTM.Modules.ThesisManagement.Tests.csproj --filter "FullyQualifiedName~CampusPresenceTests|FullyQualifiedName~CampusRedisPresenceTests" -m:1`. Redis test dùng container tạm `redis:7-alpine`, cần Docker. HTTP test kiểm tra input, phiên không hợp lệ và metadata ẩn danh; store tests kiểm tra nhiều khách/tab, guest → login, logout, hết hạn và cạnh tranh.

Browser QA dùng desktop/mobile, rỗng/lỗi/danh sách, chi tiết và quay lại sau đăng nhập. Dữ liệu mô phỏng phục vụ browser QA chỉ đặt trong `artifacts/`, không ship làm dữ liệu thật. Cấp URL S3 với hồ sơ thật cần backend, database và object storage đang hoạt động.
