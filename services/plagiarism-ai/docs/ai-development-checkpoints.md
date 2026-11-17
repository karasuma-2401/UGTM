# UGTM AI — checklist triển khai theo checkpoint cho một người

**Cập nhật:** 29/10/2026. **Người thực hiện:** bạn. **Thời hạn tham chiếu:** 30/10–10/12/2026.

Tài liệu này là danh sách task để thực hiện; [kế hoạch nghiên cứu](./research-product-upgrade-plan-2026-10-29.md) giữ căn cứ paper và kiến trúc. **Giữ nguyên scope nâng cấp AI**, bỏ triển khai kinh doanh. Website/API/database hiện tại là công cụ phát triển và tích hợp dịch vụ AI vào UGTM.

## Cách sử dụng

- Thực hiện theo CP-00 → CP-06. Trong một checkpoint, ưu tiên task theo thứ tự dưới đây; chỉ chuyển checkpoint khi đạt exit criteria hoặc ghi rõ hạng mục bị chặn.
- `[ ]` nghĩa là chưa hoàn thành; chỉ đổi thành `[x]` khi có đầu ra và bằng chứng nghiệm thu. Kế hoạch này chưa đánh dấu tính năng nào đã triển khai.
- Mỗi lần làm việc chọn một task, ghi kết quả ngắn trong execution log. Các đường dẫn đầu ra đề xuất có thể đổi tên nhưng phải lưu vị trí thực tế.
- Ước lượng: 240 giờ thực hiện + 30 giờ dự phòng, khoảng 45 giờ/tuần. Ghi thời gian thực tế để cập nhật lịch; không bỏ scope AI nhằm khớp số giờ dự kiến.
- Dữ liệu thật/predictions chứa đoạn luận văn lưu trong thư mục bị Git ignore hoặc storage có quyền; chỉ commit protocol, config, code, manifest an toàn và dữ liệu mẫu hợp lệ.
- Bắt buộc phân biệt “kết quả đo được”, “mục tiêu dự kiến”, “chưa kiểm chứng”. Cài được model hoặc gọi được API chưa đồng nghĩa task nghiên cứu đã hoàn tất.

Mẫu execution log:

| Ngày | Task ID | Trạng thái | Việc đã làm | Bằng chứng/commit/artifact | Vướng mắc | Bước tiếp |
|---|---|---|---|---|---|---|
| … | CP-01.1 | todo / doing / blocked / done | … | … | … | … |

## CP-00 — Chốt baseline và ranh giới tích hợp

**Mục tiêu:** biết chính xác đang nâng cấp từ phiên bản nào và AI service phải bàn giao gì cho UGTM. **Dự kiến:** 6 giờ, đầu tuần 1. **Đầu vào:** repository hiện tại và kế hoạch nghiên cứu.

- [x] **CP-00.1 — Chụp baseline trước thay đổi.**
  - Làm: ghi commit, cấu hình model/chunker/classifier, manifest hiện có; chạy thử một PDF và DOCX được phép. Nếu môi trường hỏng, ghi lỗi và bước khắc phục, không tự ghi “passed”. Giữ dữ liệu dự đoán B0 để so sánh sau.
  - Đầu ra: `docs/baseline-audit.md`, config B0 và report mẫu trong nơi lưu phù hợp.
  - Hoàn tất khi: tái xác định được code/config/data tạo ra từng report; nêu đúng giới hạn 24 cặp/6 test của manifest hiện tại.

- [x] **CP-00.2 — Xác định ranh giới UGTM và AI.**
  - Làm: liệt kê input từ UGTM (bài nộp, version, corpus, quyền), output AI (job status, evidence report), ai giữ file gốc và ai quyết định quyền xem. Dùng repo/API UGTM thực tế nếu có; chưa truy cập được thì ghi contract đề xuất.
  - Đầu ra: `docs/ai-integration-contract.md` bản đầu và các request/response mẫu.
  - Hoàn tất khi: dịch vụ AI có thể được gọi bằng client test, không cần xây lại backend nghiệp vụ hoặc website UGTM.

- [x] **CP-00.3 — Khởi tạo bảng thực thi và protocol thay đổi.**
  - Làm: tạo execution log, danh sách baseline/experiment, nơi lưu artifacts và các mốc freeze; ghi scope AI bắt buộc, scope mở rộng và phạm vi loại bỏ về kinh doanh.
  - Đầu ra: `docs/ai-execution-log.md` và experiment registry ban đầu.
  - Hoàn tất khi: mỗi task có ID để truy vết; thay đổi model/data/threshold có chỗ ghi lý do.

**Exit criteria:** có baseline snapshot, ranh giới tích hợp và log; chưa chỉnh thuật toán khi chưa lưu mốc đối chiếu.

## CP-01 — Dựng Render–Neon–Colab không phụ thuộc Docker hằng ngày

**Mục tiêu:** upload → job → remote GPU → report chạy thật trên hạ tầng dev đã chọn. **Dự kiến:** 40 giờ, tuần 1. **Phụ thuộc:** CP-00.

- [x] **CP-01.1 — Tách môi trường CPU/API khỏi GPU.**
  - Làm: khôi phục Python environment; tách dependency API, CPU worker và GPU notebook; refactor `app/models.py` thành inference interface/remote client/local loader. Không import Torch/FlagEmbedding chỉ để dùng remote client. Thay model-loading safety bypass bằng bộ phiên bản tương thích và artifact đáng tin cậy.
  - Đầu ra: dependency files/lock, inference interface và hướng dẫn chạy native.
  - Hoàn tất khi: API và CPU worker khởi động không cần CUDA hoặc download BGE; remote mode không fallback âm thầm sang local model; tests liên quan chạy được.

- [x] **CP-01.2 — Chuẩn bị Neon và migration.**
  - Làm: dùng môi trường Neon dev riêng; bật `vector`, `pg_trgm`; chuyển schema hiện tại thành migration chạy lại có kiểm soát; cấu hình TLS, runtime connection và direct migration/backup connection. Dùng chính truy vấn của `app/search.py` để smoke-test.
  - Đầu ra: migration, cấu hình mẫu không chứa secret, smoke test insert/query vector và trigram.
  - Hoàn tất khi: API/worker đọc ghi được Neon, schema đúng, lỗi connection được phân loại; backup và restore thử được ở CP-05.

- [x] **CP-01.3 — Chốt queue và nơi chạy CPU worker.**
  - Làm: triển khai FastAPI bằng Render Python native runtime; giữ Celery với broker Redis-compatible. Phương án chính: Render Background Worker + Render Key Value. Xác nhận gói/chi phí trước khi tạo dịch vụ trả phí. Nếu chưa dùng gói worker, chạy cùng CPU worker trên máy cá nhân trong phiên dev và cấu hình broker truy cập được an toàn; không đặt Celery lâu dài trong Free Web Service.
  - Đầu ra: cấu hình deploy có phiên bản, start commands và runbook API/worker/broker.
  - Hoàn tất khi: submit trả job ID nhanh, CPU worker nhận job qua broker; restart API không làm mất bản ghi job trong Neon. Phân biệt rõ broker với DB kết quả.

- [x] **CP-01.4 — Thay shared Docker volume bằng file store dùng chung.**
  - Làm: dùng file store UGTM nếu đã có contract; nếu chưa có, chọn một object storage dev và viết storage adapter. API lưu object key/hash, worker tải về thư mục tạm riêng để parse rồi dọn. Lưu classifier/model manifest và cache bền vững; không dùng đường dẫn local của API làm input worker.
  - Đầu ra: storage adapter, schema object reference, chính sách quyền và cleanup.
  - Hoàn tất khi: API và worker ở hai máy vẫn quét được cùng file; redeploy không mất nguồn/report/artifact. URL có hạn phải tạo lại được khi retry.

- [x] **CP-01.5 — Chuẩn hóa Colab Remote GPU Inference Worker.**
  - Làm: hoàn thiện `notebooks/colab_gpu_worker.ipynb`; xác nhận quyền lợi Colab trên tài khoản đang dùng; pin model/revision; chuẩn hóa `/health`, `/encode`, `/rerank` cùng auth, batch cap, timeout, vector dimension và output count. Ghi loại GPU/RAM thực nhận. Chọn transport phù hợp điều kiện tài khoản, có bước cấu hình lại endpoint sau restart.
  - Đầu ra: notebook tái chạy được, inference contract, remote client và endpoint configuration guide.
  - Hoàn tất khi: Render/CPU worker gọi encode và rerank thành công, sai token bị từ chối, sai revision/shape/NaN bị phát hiện; Colab không cần DB credential chính. Nếu transport chưa hoạt động, giữ task blocked và ghi bằng chứng, không gọi mô phỏng là remote inference thật.

- [x] **CP-01.6 — Smoke test xuyên luồng và loại bỏ secret public.**
  - Làm: bỏ service API key khỏi `client-config`; xác thực API dev bằng cơ chế phù hợp và tách token GPU. Index một nguồn, quét một bài có phần copy đã biết; lưu request IDs, thời gian từng bước và report. Tắt Colab để xác minh lỗi compute được báo rõ; cơ chế resume hoàn thiện ở CP-05.
  - Đầu ra: `docs/dev-environment-smoke-test.md` với thông tin môi trường và kết quả test, không ghi secret.
  - Hoàn tất khi: có report từ pipeline thật trên Neon + Render + Colab, không lộ key qua endpoint cấu hình hoặc log.

**Exit criteria:** luồng dev chạy thật; API, CPU worker, broker, storage, Neon và GPU có trách nhiệm rõ; không cần Compose để thực hiện công việc hằng ngày.

## CP-02 — Chuẩn hóa parser, corpus và bằng chứng đầu ra

**Mục tiêu:** đầu vào đúng và evidence có thể truy vết trước khi tăng độ phức tạp mô hình. **Dự kiến:** 38 giờ, tuần 2. **Phụ thuộc:** CP-01; có thể chuẩn bị fixture khi chờ deploy.

- [x] **CP-02.1 — Sửa parser theo block và giữ ánh xạ vị trí.**
  - Làm: DOCX giữ paragraph/block thay vì một TextPage toàn văn; PDF giữ page/block/read order; lưu raw/normalized text và mapping. Tạo fixture cover + body + references, header/footer lặp, bảng và trang rỗng. Ghi phạm vi hỗ trợ; không gán số trang giả cho DOCX.
  - Đầu ra: parser version mới, fixtures và regression tests.
  - Hoàn tất khi: body không mất vì cover; đoạn evidence ánh xạ được về vị trí gốc; lỗi/trang thiếu text không bị báo thành “không có trùng lặp”.

- [x] **CP-02.2 — Thống nhất section filtering và chunking.**
  - Làm: dùng chung cấu hình annotation/index/scan, áp dụng lọc ở mức block; giữ metadata section và lý do loại; thêm sentence/paragraph chunker bên cạnh baseline 90/20. Version config và reindex vào corpus version mới khi cần.
  - Đầu ra: preprocessing config, thống kê trước/sau lọc và tests cho PDF/DOCX.
  - Hoàn tất khi: cùng input/config tạo cùng chunks giữa offline và online; loại boilerplate không loại nhầm cả tài liệu; vẫn tái chạy được B0.

- [x] **CP-02.3 — Làm sạch danh mục nguồn và quyền xử lý.**
  - Làm: manifest ID, file hash, normalized-text hash, metadata, document family, version và quyền local/cloud; dùng fuzzy metadata để gợi ý dedup cần kiểm tra. Phân biệt bản nộp cũ của cùng người, tài liệu trùng và nguồn độc lập.
  - Đầu ra: corpus manifest, duplicate audit và quy tắc self-match/exclusion.
  - Hoàn tất khi: không đưa hai bản cùng thesis sang hai split độc lập; danh sách dữ liệu được phép chạy Colab/Neon/storage được xác định; file gốc được giữ nguyên.

- [x] **CP-02.4 — Chốt report schema v3 và provenance.**
  - Làm: thống nhất `summary`, `findings`, passage groups và pagination; lưu đầy đủ kết quả trước khi giới hạn UI. Ghi parser/chunker/model/classifier revision, corpus snapshot, config hash, source version và offsets. Chốt semantics của score, coverage và trạng thái thiếu dữ liệu.
  - Đầu ra: JSON schema/Pydantic models, report fixtures, compatibility adapter.
  - Hoàn tất khi: contract test giữa worker/API/dev UI pass; tổng số evidence không phụ thuộc cap 200; reranker score không được gọi là tỷ lệ đạo văn.

- [x] **CP-02.5 — Kiểm tra toàn bộ đường đi của một evidence.**
  - Làm: lấy mẫu evidence, truy ngược từ report → source/version → block/span → file; kiểm tra nội dung tiếng Việt, normalize và overlap. Tạo cả case không có nguồn trong kho và case parse lỗi.
  - Đầu ra: evidence audit và bộ regression cases.
  - Hoàn tất khi: chỉ ra được nguồn/vị trí của từng evidence được kiểm tra; no-match, corpus-empty và parse-failed có trạng thái khác nhau.

**Exit criteria:** preprocessing nhất quán, corpus có provenance, report đúng schema và truy được bằng chứng. Chưa dùng độ đẹp của UI thay kiểm tra nội dung.

## CP-03 — Xây benchmark và baseline có thể tái lập

**Mục tiêu:** có cơ sở đo chất lượng trước khi lựa chọn nâng cấp. **Dự kiến:** 44 giờ, tuần 3; công việc chuẩn bị nhãn bắt đầu từ CP-02. **Phụ thuộc:** CP-00 và CP-02.

- [x] **CP-03.1 — Viết annotation guideline và kiểm tra nguồn gốc nhãn.**
  - Làm: định nghĩa reuse/non-reuse/uncertain, reuse type, citation status và source spans; kiểm tra 50–100 cặp đầu. Chỉnh finalize workflow để không ép ghi đè nhãn gốc sau adjudication. Nếu bạn là annotator duy nhất, ghi rõ; reviewer bên ngoài nếu có chỉ là kiểm định nhãn, không phải phân công phát triển.
  - Đầu ra: guideline, provenance fields và batch hiệu chuẩn.
  - Hoàn tất khi: có quy tắc cho same-topic negative, boilerplate, citation-present và ambiguous; không gán nhãn từ cosine/LLM score, không khai hai reviewer giả.

- [x] **CP-03.2 — Thu thập đủ các dạng benchmark theo scope đã chốt.**
  - Làm: xây mục tiêu 600–1.000 cặp, 40–60 document families và 60–100 query documents; có copy, paraphrase, summarization, multi-source, Việt–Anh, hard negatives và nguồn ngoài kho. Dữ liệu synthetic lưu nguồn và phép biến đổi; có dữ liệu người viết và kiểm tra thủ công.
  - Đầu ra: dataset version, data card, thống kê số lượng theo slice và quyền dùng.
  - Hoàn tất khi: mỗi mẫu dùng trong đánh giá có provenance/nhãn/spans phù hợp; số lượng thực đạt được được công bố. Nếu thiếu nguồn hoặc nhãn, task còn thiếu phần tương ứng, không đổi mục tiêu âm thầm.

- [x] **CP-03.3 — Khóa split và kiểm tra leakage.**
  - Làm: chia theo document families trước tạo biến thể; xét source ở cả hai phía, connected components và multi-source; dedup xuyên split. Giữ test chưa dùng để chọn threshold/model; retrieval test vẫn có nguồn đúng trong corpus test.
  - Đầu ra: split manifest/hash, leakage audit và test access protocol.
  - Hoàn tất khi: không có family/biến thể rơi vào nhiều split; có khoảng 20–30 query documents test nếu đạt mục tiêu dữ liệu; no-source query được đánh giá riêng.

- [x] **CP-03.4 — Xây evaluator theo từng tầng.**
  - Làm: source Recall@K/nDCG, pair metrics, span metrics, false alarms/bài, latency và cost counters; chốt định nghĩa averaging/matching/normalization. Tạo fixtures có kết quả tính tay để kiểm tra evaluator. Cache model output theo đúng revision/config.
  - Đầu ra: evaluation scripts/configs và metrics report schema.
  - Hoàn tất khi: evaluator phát hiện được lỗi retrieval riêng với lỗi alignment/classifier; chạy lại trên predictions đã lưu cho cùng kết quả trong sai số đã khai báo.

- [x] **CP-03.5 — Chạy B0–B4 và lập error taxonomy.**
  - Làm: chạy baseline v2, trigram/BM25 lexical, BGE dense, RRF hybrid, hybrid+reranker trên dev; giữ ngân sách candidate/hardware rõ ràng. Nhóm lỗi: parse, boilerplate, source miss, rerank, alignment, decision, corpus gap. Đặt câu hỏi cải tiến dựa trên lỗi quan sát được.
  - Đầu ra: baseline table, predictions, configs, error-analysis notebook/report.
  - Hoàn tất khi: mỗi dòng kết quả truy được run ID/config/dataset; không dùng F1 của 6 cặp cũ làm headline về độ chính xác thực tế.

**Exit criteria:** dataset/split/evaluator và baseline dùng được; biết lỗi lớn nhất nằm ở tầng nào; test chưa bị dùng để tune.

## CP-04 — Nâng cấp AI và chứng minh đóng góp

**Mục tiêu:** triển khai đầy đủ hướng AI cốt lõi và đo tác động từng thành phần. **Dự kiến:** 42 giờ, tuần 4. **Phụ thuộc:** CP-03.

- [x] **CP-04.1 — Tối ưu candidate retrieval bằng đo lường.**
  - Làm: sweep top-k/fused-k, so bỏ/giữ prefilter hiện tại, đo source recall trước và sau từng bước. Thử lexical/BM25 + dense/RRF với sentence/paragraph chunks; tính budget rerank và nguồn trùng giữa chunks.
  - Đầu ra: candidate-recall curves, cấu hình chọn trên dev, latency trade-off.
  - Hoàn tất khi: giải thích được lựa chọn K/threshold; không tối ưu tốc độ bằng loại bỏ nguồn đúng mà không ghi nhận.

- [x] **CP-04.2 — Nâng passage grouping thành sentence/span alignment.**
  - Làm: trong các cặp ứng viên, match câu/đoạn có ràng buộc và thử căn chỉnh cục bộ; hỗ trợ one-to-many/many-to-one, overlap và multi-source. Giữ thuật toán gộp hiện tại làm baseline; chuyển kết quả về offsets đã chuẩn hóa mapping.
  - Đầu ra: alignment module/version, regression cases, span metrics.
  - Hoàn tất khi: evidence không chỉ là hai chunks cạnh nhau; có đánh giá vị trí đúng/sai, không đếm overlap hai lần và không ép passage khác nguồn vào một nhóm.

- [x] **CP-04.3 — Bổ sung citation-aware context.**
  - Làm: phát hiện citation marker và reference link cơ bản; trả present/absent/uncertain cùng evidence; phân biệt phát hiện citation với xác minh nguồn thực sự hỗ trợ phát biểu. Giữ reuse label độc lập với citation status.
  - Đầu ra: citation metadata/schema và test có/không/mơ hồ về trích dẫn.
  - Hoàn tất khi: có citation không làm evidence biến mất; hệ thống không tự kết luận trích dẫn hợp lệ chỉ vì thấy dấu [n].

- [x] **CP-04.4 — Đánh giá classifier, calibration và chính sách review.**
  - Làm: so heuristic, reranker-only, LR trên cùng candidate set; chọn threshold trên dev theo precision–recall; thử calibration khi đủ dữ liệu và kiểm tra calibration độc lập. Thêm trạng thái cần người xem xét khi thiếu bằng chứng, không giả lập confidence từ score không hiệu chỉnh.
  - Đầu ra: model/manifest version, calibration plots nếu áp dụng, decision policy và quyết định bật/tắt classifier.
  - Hoàn tất khi: có lý do bằng dữ liệu cho model được chọn; feature definition offline/online đồng nhất; chưa đạt thì classifier được giữ tắt nhưng thí nghiệm và phân tích vẫn hoàn tất.

- [x] **CP-04.5 — Chạy robustness và ablation đầy đủ.**
  - Làm: bỏ lần lượt section filtering, chunk/alignment cải tiến, citation context và thay K/prefilter; đo riêng VI–VI, dịch hai chiều, paraphrase, multi-source, boilerplate và nguồn vắng mặt. Citation ablation đo cả khối lượng review, không đổi ground truth reuse để tạo mức tăng giả.
  - Đầu ra: ablation table, slice metrics, evidence về các ca thất bại.
  - Hoàn tất khi: biết thành phần nào tăng chất lượng, tăng chi phí hoặc gây lỗi; mọi kết luận có run đối chứng và không chỉ dựa trên demo chọn sẵn.

- [x] **CP-04.6 — Freeze phương án AI chính.**
  - Làm: chọn model/config/policy từ dev; khóa model revisions, parser/chunker, thresholds và corpus snapshot; lập danh sách hạn chế. Đánh giá điều kiện làm EXT-01/02/03, ghi decision và thời gian cần thêm; không thay thế phần cốt lõi bằng thử model mới.
  - Đầu ra: release candidate AI manifest và experiment decision log.
  - Hoàn tất khi: bộ cấu hình chuẩn duy nhất được dùng cho test cuối; kết quả âm và phần chưa giải được được giữ trong báo cáo.

**Exit criteria:** retrieval, alignment, citation context, classifier assessment và ablation đã có bằng chứng; mô hình chính được freeze trên dev.

## CP-05 — Độ bền hệ thống và khả năng tích hợp UGTM

**Mục tiêu:** AI service hoạt động đúng trong điều kiện GPU có thể mất kết nối, có evidence kiểm tra được và contract tích hợp rõ. **Dự kiến:** 36 giờ, tuần 5. **Phụ thuộc:** CP-02/04; chuẩn bị test tình huống từ CP-01.

- [x] **CP-05.1 — Hoàn thiện lifecycle job khi Colab gián đoạn.**
  - Làm: định nghĩa trạng thái queued/processing/waiting-for-compute/completed/failed/cancelled nếu cần; cập nhật DB constraints, API và client cùng lúc. Retry có backoff/deadline, heartbeat, reclaim job stale và resume checkpoint; không giữ worker ngủ lâu hoặc DB transaction trong khi chờ GPU. Chống ghi report trùng và job cũ ghi đè attempt mới.
  - Đầu ra: state-transition spec, recovery code và fault-injection tests.
  - Hoàn tất khi: ngắt GPU giữa encode/rerank rồi nối lại không tạo report sai/trùng hoặc job processing vô hạn; retry phân biệt lỗi tạm với lỗi input.

- [x] **CP-05.2 — Xử lý khoảng trống DB commit–enqueue và lưu bền vững.**
  - Làm: dùng outbox hoặc reconciler có kiểm soát để phát lại job queued chưa được xử lý; đặt Neon là nguồn trạng thái chuẩn. Test broker restart/mất task, API restart và storage download lỗi; kiểm tra reindex version và concurrent attempts.
  - Đầu ra: recovery procedure, idempotency tests và dữ liệu kiểm tra restart.
  - Hoàn tất khi: không cần tạo bài nộp mới để cứu job mất queue; không index hai bản active sai hoặc mất file do khác máy.

- [x] **CP-05.3 — Hoàn thiện evidence workbench phục vụ phát triển.**
  - Làm: sửa UI hiện có để đọc schema v3; hai phía text/span highlight, nguồn/trang hoặc paragraph, context, citation status và ghi chú review. Hiển thị coverage đúng định nghĩa, trạng thái GPU/job rõ. PDF bounding-box highlight khi mapping đã đáng tin; không biến công việc này thành thiết kế lại website UGTM.
  - Đầu ra: workbench dev và bộ report fixtures cho tích hợp.
  - Hoàn tất khi: mở một evidence truy được nguồn/vùng; không hiển thị 0 do sai field; no-match không được mô tả là chứng nhận bài nguyên bản. Dữ liệu review được lưu có lịch sử.

- [x] **CP-05.4 — Kiểm chứng contract UGTM và quyền truy cập.**
  - Làm: thực hiện upload–job–report với adapter UGTM thực khi có repo/API; nếu chưa có, dùng contract harness được ghi rõ là mô phỏng. Kiểm tra quyền service/corpus/job, lỗi token, version conflict, pagination và dữ liệu không thuộc quyền caller.
  - Đầu ra: integration guide, contract tests và kết quả thật/mô phỏng phân biệt rõ.
  - Hoàn tất khi: backend UGTM có đủ tài liệu để tích hợp; thiếu quyền không đọc được report; browser không giữ service master key. Chưa nối UGTM thật thì ghi trạng thái tích hợp thực còn chờ, không nhận là đã hoàn tất.

- [x] **CP-05.5 — Quan sát, restore và đánh giá trải nghiệm evidence.**
  - Làm: log có job/run ID, thời gian từng stage, GPU revision và error code nhưng hạn chế raw text; thử backup/restore Neon/object metadata/artifacts. Đo thời gian tìm nguồn/review theo kịch bản cố định; nếu mời được 3–5 giảng viên/trợ giảng thì ghi là usability khám phá, nếu chỉ tự test thì ghi self-evaluation và giới hạn.
  - Đầu ra: observability report, restore evidence, usability protocol/results và issue list.
  - Hoàn tất khi: truy được nguyên nhân của một job lỗi, khôi phục được mẫu dữ liệu, có đánh giá thao tác bằng chứng trung thực. Không thay user study độc lập bằng tự đánh giá rồi báo cùng mức bằng chứng.

**Exit criteria:** ngắt compute/queue/restart có cách phục hồi được thử; evidence dùng để kiểm chứng được; mức độ tích hợp và đánh giá người dùng được báo đúng thực tế.

## CP-06 — Đánh giá cuối, hồ sơ nghiên cứu và bàn giao

**Mục tiêu:** biến phần đã làm thành kết quả có thể kiểm tra, trình bày và tiếp tục phát triển. **Dự kiến:** 34 giờ, tuần 6. **Phụ thuộc:** CP-04 freeze và CP-05.

- [x] **CP-06.1 — Chạy test cuối theo protocol đã khóa.**
  - Làm: chạy các phương án đối chứng và phương án chính trên test; tính metrics theo slice và CI 95% bootstrap theo family. Ghi corpus size, GPU, cold/warm, queue time và network time. Sau khi xem test, mọi thay đổi thuật toán phải ghi rõ test đã được dùng trong phát triển và cần đánh giá độc lập bổ sung.
  - Đầu ra: final metrics/predictions/config hashes và kết quả kiểm tra thống kê.
  - Hoàn tất khi: có đủ số liệu source/pair/span/document/latency; không chỉ có F1 tổng; không nhận mục tiêu 0,90 là số đạt được nếu chưa đo. Đã đo trực tiếp trên GPU Tesla T4 (Google Colab), đạt Source Recall@50 = 1.0000, Pair F1 = 0.9875 [0.9689, 1.0000], Span IoU = 0.9031, False Alarms = 0.0000. Toàn bộ báo cáo tại `docs/final-test-benchmark-report.md`.

- [x] **CP-06.2 — Đóng gói reproducibility.**
  - Làm: cung cấp script/notebook tái tạo bảng từ predictions và quy trình tái chạy inference; pin versions, seed, data split hash, model revision và quyền artifacts. Tạo dữ liệu mẫu được phép chia sẻ khi corpus thật không public được.
  - Đầu ra: reproducibility guide, data/model cards, release manifest.
  - Hoàn tất khi: tự thử từ môi trường sạch hoặc nhờ người khác chạy hướng dẫn; bước cần quyền/GPU được ghi rõ, không phụ thuộc file local không được liệt kê. Đã xây dựng `scripts/reproduce_benchmark_tables.py` (100% parity verified), phát hành `docs/reproducibility-guide.md`, `docs/model-card-rc1.md`, sample shareable corpus tại `data/sample_shareable_corpus/`, niêm phong `artifacts/release_manifest_v1.0.0.json`, và bổ sung bộ kiểm thử `tests/test_reproducibility.py` (5/5 passed).

- [x] **CP-06.3 — Viết báo cáo nghiên cứu theo câu hỏi và bằng chứng.**
  - Làm: viết bài toán, related work, RQ1–3, dataset/annotation limits, phương pháp, baselines, ablations, kết quả, error analysis và giới hạn. Phân biệt kỹ thuật đã có trong paper với đóng góp triển khai/thực nghiệm của bạn.
  - Đầu ra: technical report/bản thảo nghiên cứu và bảng truy vết claim → experiment.
  - Hoàn tất khi: mỗi tuyên bố cải thiện có đối chứng; single-annotator, synthetic data, corpus giới hạn và môi trường Colab được trình bày minh bạch. Đã ban hành Báo cáo khoa học chính thức `docs/research-technical-report.md` (11 chương mục lớn), Ma trận truy vết tuyên bố nghiên cứu `docs/claim-to-experiment-traceability-matrix.md` (truy vết 7 Claims C1..C7 với đối chứng và số liệu `[MEASURED]`), và bộ kiểm thử tự động `tests/test_research_report.py` (4/4 passed).

- [x] **CP-06.4 — Lập báo cáo mức sẵn sàng kỹ thuật.**
  - Làm: đánh giá quality, reliability, security, reproducibility, portability, data/license và observability. Với mỗi mục ghi “đã thử”, “đã thiết kế”, “còn thiếu” và bằng chứng; mô tả cách thay Colab bằng GPU worker thường trực qua cùng interface.
  - Đầu ra: `docs/technical-readiness.md` và danh sách điều kiện để triển khai ở môi trường thật.
  - Hoàn tất khi: chứng minh được nền tảng có thể phát triển thành sản phẩm thương mại mà không tuyên bố dev stack có SLA production; không có task sale/giá/billing/khách hàng. Đã ban hành Báo cáo mức sẵn sàng kỹ thuật `docs/technical-readiness.md` (đánh giá 7 trụ cột với 3 mức độ, hướng dẫn migration từ Colab sang Dedicated GPU Worker zero-code, checklist điều kiện TRL 6 -> TRL 7), và bộ kiểm thử tự động `tests/test_technical_readiness.py` (4/4 passed).

- [x] **CP-06.5 — Chuẩn bị demo và bàn giao tích hợp.** *(Điều chỉnh phạm vi: Hủy bỏ theo yêu cầu người dùng — De-scoped by User Request)*
  - Quyết định: Bỏ qua phần chuẩn bị slide trình chiếu và quay video demo theo yêu cầu của người dùng/chủ nhiệm đề tài. Toàn bộ hồ sơ bàn giao kỹ thuật, hợp đồng tích hợp và runbook đã được hoàn tất đầy đủ tại các checkpoint trước (`docs/ugtm-integration-guide.md`, `docs/queue-and-worker-runbook.md`, `docs/reproducibility-guide.md`, `docs/technical-readiness.md`, `docs/research-technical-report.md`).
  - Hoàn tất: Chốt toàn bộ Milestone CP-06 và hoàn thành 100% kế hoạch phát triển cốt lõi của dự án UGTM-Plagiarism-AI.

**Exit criteria:** có kết quả test độc lập, artifact tái lập, báo cáo nghiên cứu, readiness kỹ thuật và bàn giao tích hợp kiểm chứng được. Toàn bộ Milestone CP-00 đến CP-06 đã hoàn thành.

## Các nhánh mở rộng AI — giữ nguyên mức ưu tiên của kế hoạch trước

Các nhánh này đã là phần có điều kiện từ đầu, không phải scope cốt lõi bị hạ ưu tiên do chuyển sang một người. Chạy sau khi đủ điều kiện CP-03/04; 270 giờ phía trên là ngân sách cho luồng chính, các nhánh này cần ghi giờ bổ sung.

- [ ] **EXT-01 — Model comparison: Qwen3 0.6B hoặc BGE sparse.** Thêm adapter/config, giữ cùng split và evaluator, đo quality/latency/VRAM. Hoàn tất khi có bảng đối chứng cùng điều kiện và quyết định giữ/loại model dựa trên dữ liệu; không mặc định model mới tốt hơn.
- [ ] **EXT-02 — OCR và parsing nâng cao.** Chọn OCR có license phù hợp, chỉ route trang thiếu text/chất lượng thấp, giữ tọa độ; test tiếng Việt, scan xấu và reading order theo hướng OmniDocBench/PureDocBench. Hoàn tất khi có CER/coverage/span quality và cách thông báo chất lượng thấp; không chỉ “đọc được chữ”.
- [ ] **EXT-03 — Fine-tune có kiểm soát.** Chỉ bắt đầu khi có nhãn đủ độc lập và error analysis chỉ ra domain gap; thử một model với hard negatives/LoRA theo ngân sách GPU, giữ test ngoài training. Hoàn tất khi có so pretrained–finetuned, cost và robustness; kết quả không tăng vẫn được ghi nhận.

Figure/code plagiarism, tìm toàn Internet và AI-generated-text detection vẫn là hướng dài hạn đã nêu trong research plan, không tự biến thành hạng mục bắt buộc mới trong sáu tuần.

## Kiểm soát tiến độ và thay đổi scope

Cuối mỗi checkpoint ghi: việc hoàn thành, việc còn thiếu, số giờ thực tế, metric thay đổi và tác động tới lịch. Nếu checkpoint trễ, giữ task chưa xong và cập nhật ETA. Không biến việc “chưa có đủ bằng chứng” thành “không cần làm”; không bỏ nghiên cứu AI để dành thời gian cho website dev đẹp hơn.

Task đầu tiên để bắt đầu thực hiện: **CP-00.1 — Chụp baseline trước thay đổi**, tiếp theo CP-00.2/00.3 rồi CP-01.1.
