# UGTM AI — kế hoạch nghiên cứu và hoàn thiện kỹ thuật trong 6 tuần

Cập nhật **29/10/2026** theo quyết định mới của người thực hiện. Tài liệu này thay thế cách phân công A/B/C/D và mục tiêu triển khai kinh doanh ở bản trước.

## 1. Phạm vi đã chốt

- **Một người thực hiện toàn bộ công việc phát triển AI.** Không chia task cho 4 thành viên.
- **Giữ nguyên scope nâng cấp AI đã nghiên cứu:** dữ liệu/benchmark, parsing/chunking, hybrid retrieval, reranking, alignment, citation context, classifier/calibration, robustness, reproducibility và đánh giá khả năng sử dụng bằng chứng.
- Qwen/BGE sparse, OCR nâng cao và fine-tune vẫn là phần mở rộng có điều kiện như bản trước; không chuyển các hạng mục AI cốt lõi thành tùy chọn chỉ vì một người làm.
- Mục tiêu là **dịch vụ AI đạt nền tảng kỹ thuật để có thể thương mại hóa**, tích hợp được vào phần mềm quản lý khóa luận tốt nghiệp UGTM.
- Bỏ nghiên cứu thị trường, tìm khách hàng, định giá, billing, doanh thu, triển khai bán dịch vụ và cam kết SLA thương mại.
- Website, backend AI và database hiện có là **môi trường phát triển/kiểm chứng**. Chỉ sửa giao diện ở mức cần thiết để kiểm tra evidence và API, không xây lại website UGTM hoặc nền tảng SaaS.
- Hạ tầng phát triển: **Render cho API/điều phối CPU, Neon cho PostgreSQL/vector, Google Colab làm Remote GPU Inference Worker**. Ưu tiên native Python, Docker là phương án đóng gói tùy chọn.
- Mốc thời gian còn khoảng 1,5 tháng. Lịch tham chiếu: **30/10–10/12/2026**.

**Danh sách việc thực hiện chính thức:** [Checkpoints và task dành cho một người](./ai-development-checkpoints.md). Mỗi task có ID, đầu vào, bước thực hiện, đầu ra và Definition of Done. Tất cả đang ở trạng thái chưa thực hiện trong kế hoạch này.

Khối lượng giữ nguyên là đáng kể: ước lượng lập kế hoạch **240 giờ công việc + 30 giờ dự phòng = 270 giờ**, tương đương khoảng 45 giờ/tuần trong 6 tuần. Đây là ước lượng ban đầu, không phải cam kết năng suất hay số giờ bạn đã xác nhận. Nếu tốc độ thực tế thấp hơn, cập nhật ngày hoàn tất và báo phần còn lại; không âm thầm bỏ scope AI hoặc đánh dấu hoàn thành khi chưa đạt.

## 2. Đánh giá hiện trạng từ repository

Đây là review tĩnh mã nguồn và tài liệu tại thời điểm lập kế hoạch, chưa phải kiểm định triển khai production. Không đọc bí mật trong `.env`, không gửi corpus lên dịch vụ ngoài. Đã thử chạy unit tests bằng `.venv`, nhưng Python nền tại máy trả `Access is denied`; vì vậy không xác nhận tests đang pass và chưa đo end-to-end/GPU trong lượt lập kế hoạch này.

| Hạng mục | Bằng chứng hiện có | Ý nghĩa đối với kế hoạch |
|---|---|---|
| Pipeline nền | `app/tasks.py`, `app/search.py`: dense BGE-M3 + trigram + RRF + reranker + classifier tùy chọn | Giữ kiến trúc và dùng làm baseline B0 |
| Training | `training/train_classifier.py`: GroupShuffleSplit, validation chọn threshold, test tách riêng | Cơ sở tốt; cần kiểm tra group thực sự bao phủ quan hệ nguồn và biến thể |
| Độ lớn đánh giá | Hai manifest trong `artifacts/`: 24 cặp, 6/3/3 nhóm train/val/test; confusion matrix test có 6 cặp | F1=1,0 chỉ phản ánh 6 dự đoán; chưa chứng minh chất lượng thực tế |
| Chất lượng dữ liệu | `docs/corpus-triage-report.md`: batch cũ 600 cặp có 529 cặp gợi ý loại, 43 cặp nội dung cần review | Đây là thống kê batch cũ, không phải kết quả sau sửa hay tỷ lệ của toàn corpus |
| Lọc section | Annotation script truyền `exclude_sections`; `app/tasks.py:parse_job` gọi `make_chunks` không truyền lọc | Cần thống nhất chính sách annotation/index/scan và reindex có phiên bản |
| Parser DOCX | `extract_pages` ghép toàn bộ paragraph thành một TextPage, rồi phân loại section một lần | Nếu bật lọc trực tiếp, một nhãn cover có thể làm mất cả nội dung DOCX; phải sửa mức block trước |
| Truy xuất | `top_k=6`, `fused_k=5`; prefilter dense 0.48 hoặc lexical 0.25 | Nguy cơ loại mất nguồn trước rerank; phải đo candidate recall, không khẳng định ngưỡng đang tối ưu |
| Alignment | `app/alignment.py` nhóm chunk kề nhau | Chưa phải căn chỉnh chính xác câu/ký tự hoặc tọa độ PDF |
| Báo cáo | Worker giới hạn `all_findings[:200]`; endpoint trả nguyên report | Cần tách kết quả đầy đủ và phân trang giao diện; hiện cap có thể ảnh hưởng nhóm/độ phủ |
| Hợp đồng FE–BE | Worker trả `summary` và `findings`; `ReportTab` đọc `passage_groups`, `max_rerank_score`, `total_chunks`; client fetch không biến đổi | Có lệch schema trong mã đã xem; cần contract test và adapter, xác minh bằng chạy thực tế |
| Ý nghĩa điểm | FE lấy `max_rerank_score * 100` và hiển thị `%` | Điểm reranker không phải tỷ lệ nội dung trùng hoặc xác suất vi phạm |
| Xác thực | `/api/v1/client-config` không yêu cầu key nhưng trả `settings.api_key`; FE lưu key vào localStorage | P0 trước triển khai có người dùng; Compose hiện bind localhost nên chưa suy ra đã lộ Internet |
| Model runtime | `app/models.py` vô hiệu hóa `check_torch_load_is_safe`; remote request chưa có header xác thực trong client | Bỏ bypass bằng bộ dependency tương thích; thêm xác thực, kiểm tra model identity và output |
| Vận hành | Có Celery/Redis, upload guard, idempotency ở job insert; DB commit và enqueue là hai bước | Giữ phần tốt, bổ sung phục hồi job, chống xử lý trùng, timeout và quan sát hệ thống |

Các điểm trên chưa được sửa trong lượt này; đây là kế hoạch triển khai. Không suy ra trạng thái tích hợp .NET/UGTM từ repo AI vì chưa review repo đó.

## 3. Nghiên cứu tham khảo và quyết định áp dụng

Rà soát có chọn lọc nguồn sơ cấp, cập nhật đến 29/10/2026; không tuyên bố bao phủ toàn bộ công bố mới nhất. Các khuyến nghị triển khai là suy luận thiết kế cho UGTM, cần kiểm chứng bằng thực nghiệm nội bộ. Phân biệt paper hội nghị, working notes, preprint và tài liệu benchmark.

| Nguồn | Kết quả/phạm vi liên quan | Áp dụng vào UGTM và giới hạn |
|---|---|---|
| [PlagBench, NAACL 2025](https://aclanthology.org/2025.naacl-long.384/) | 46,5 nghìn cặp synthetic, gồm copy, paraphrase, summarization | Thiết kế benchmark nhiều dạng tái sử dụng; dữ liệu synthetic và kết quả paper không thay thế đánh giá luận văn Việt |
| [PAN 2025 overview, CLEF working notes](https://arxiv.org/abs/2510.06805) | Đánh giá căn chỉnh nội dung sinh lại; chỉ ra hạn chế tổng quát hóa sang PAN 2015 | Tách retrieval khỏi alignment, kiểm tra khác miền và nhiều dạng biến đổi |
| [PAN 2026, task specification](https://pan.webis.de/clef26/pan26-web/generated-plagiarism-detection.html) | Tìm nguồn của tài liệu được tổng hợp từ nhiều nguồn; dùng nDCG@10, Recall@10/100 | Thêm case tổng hợp đa nguồn và đánh giá ở cấp tài liệu. Dataset chỉ cho nghiên cứu, không tái phân phối theo trang task |
| [VN-MTEB, Findings EACL 2026](https://aclanthology.org/2026.findings-eacl.86/) | Benchmark embedding tiếng Việt gồm 41 datasets, 6 nhóm tác vụ; có dữ liệu được dịch | Dùng để chọn shortlist model; benchmark này không trực tiếp đo plagiarism và không thay tập luận văn viết bằng tiếng Việt |
| [MMTEB, 2025](https://arxiv.org/abs/2502.13595) | Đánh giá embedding đa ngôn ngữ, nhiều tác vụ | Xem độ phù hợp theo task/ngôn ngữ, tránh chọn model chỉ theo một thứ hạng tổng |
| [Qwen3 Embedding, technical report 2025](https://arxiv.org/abs/2506.05176) | Embedding/reranker nhiều cỡ 0.6B, 4B, 8B | Chỉ thử cỡ 0.6B trước trên phần cứng Colab thực tế; chưa khẳng định hơn BGE trong luận văn Việt |
| [M3-Embedding, 2024; bản cập nhật 2025](https://arxiv.org/abs/2402.03216) | Một họ model hỗ trợ dense, sparse, multi-vector | BGE hiện chỉ dùng dense; sparse là ablation tùy chọn, chưa cần đổi toàn bộ model |
| [OmniDocBench, CVPR 2025](https://openaccess.thecvf.com/content/CVPR2025/html/Ouyang_OmniDocBench_Benchmarking_Diverse_PDF_Document_Parsing_with_Comprehensive_Annotations_CVPR_2025_paper.html) | Benchmark parsing có nhiều dạng trang và cấu trúc | Đánh giá text, thứ tự đọc và layout riêng; không chỉ kiểm tra parser có trả chuỗi |
| [PureDocBench, preprint 2026, v2 ngày 27/09](https://arxiv.org/abs/2605.07492) | Đối chiếu trang sạch, suy giảm số và điều kiện chụp thực | Test OCR trên tài liệu xấu; phần OCR toàn diện thuộc stretch, preprint chưa mặc định được phản biện |
| [Stress-testing MGT Detection, Findings ACL 2025](https://aclanthology.org/2025.findings-acl.156/) | Thử độ bền của AI-text detector dưới thay đổi phong cách | Không đưa “phát hiện AI viết” vào tiêu chí thành công của pipeline đối chiếu nguồn |
| [SciFigPlag-Bench, preprint 31/07/2026](https://arxiv.org/abs/2607.29124) | Đánh giá tái sử dụng hình khoa học, nguồn và vùng tương ứng | Hướng mở rộng hình/biểu đồ sau khi text pipeline ổn định; không gộp vào cam kết 6 tuần |

Không cần dùng tất cả kỹ thuật trong bảng. Trục nghiên cứu chính được chọn là **truy tìm nguồn và căn chỉnh bằng chứng tái sử dụng Việt–Anh, có xét cấu trúc tài liệu và ngữ cảnh trích dẫn**.

## 4. Đóng góp nghiên cứu và tiêu chí hoàn thiện

Tên định hướng: **Structure- and Citation-Aware Evidence Retrieval for Vietnamese–English Academic Text Reuse**. Chưa khẳng định tính mới trước khi đối chiếu prior art sâu hơn.

- **RQ1:** Lọc cấu trúc theo block giảm báo động boilerplate đến đâu mà không làm mất nội dung?
- **RQ2:** Hybrid retrieval và sentence/span alignment cải thiện tìm nguồn và vị trí bằng chứng ở paraphrase/dịch Việt–Anh đến đâu?
- **RQ3:** Citation context và chính sách review giảm tải xử lý đến đâu khi vẫn giữ khả năng phát hiện tái sử dụng?

Giữ dataset mục tiêu **600–1.000 cặp đã review**, khoảng **40–60 document families độc lập** và **60–100 query documents end-to-end**, trong đó khoảng 20–30 bài test cuối, tùy quyền dữ liệu thực tế. Đây là mục tiêu cần xây, không phải dữ liệu đang có. Ghi rõ số đạt được và giới hạn thống kê nếu nguồn thực tế thiếu.

Một người phát triển không đồng nghĩa giả lập hai annotator. Tự gán nhãn và review lại được ghi là single-annotator; nếu nhờ reviewer độc lập kiểm tra test hoặc mẫu dữ liệu thì lưu đúng provenance. Nếu không có người thứ hai, không báo inter-annotator agreement. Việc giữ mục tiêu chất lượng nhãn không tạo lại phân công đội phát triển.

Dataset giữ đủ các slice: exact/near-copy, paraphrase, summarization, multi-source, VI–VI, VI–EN/EN–VI, cùng chủ đề nhưng độc lập, boilerplate, reuse có citation và nguồn ngoài kho. Giữ nhóm nguồn/phiên bản và biến thể cùng split, không tune bằng test.

Ma trận nghiên cứu giữ B0 hiện tại, lexical-only (trigram/BM25), dense-only, hybrid RRF, hybrid+reranker, rồi thêm structure-aware chunking/alignment và citation/review policy. So heuristic, reranker-only, LR trên candidate set chung. Thực hiện ablation filter, chunking/alignment, citation và prefilter/K. Thử Qwen 0.6B hoặc BGE sparse khi nhánh cốt lõi đã đo được; fine-tune cần nhãn độc lập và giả thuyết lỗi domain rõ.

Metrics giữ: source Recall@10/50/100, nDCG@10; span precision/recall/F1, IoU; PR-AUC, precision/recall, confusion matrix; calibration/Brier khi phù hợp; false alarms mỗi bài; latency từng stage, p50/p95, GPU time và thời gian review. Bootstrap theo document family, công bố kết quả riêng từng slice. Mục tiêu dev ban đầu: Recall@50 >= 0,90; precision cờ ưu tiên >= 0,90 kèm recall; giảm >= 30% cảnh báo boilerplate so B0 với recall nội dung giảm không quá 2 điểm phần trăm. Đây là mục tiêu thử nghiệm, không phải số đã đạt hay quy chế học thuật.

Coverage là độ dài hợp các vùng matched đủ điều kiện chia độ dài nội dung đủ điều kiện; không cộng overlap, không dùng top-200 UI làm toàn bộ dữ liệu và không gọi là “phần trăm đạo văn”.

## 5. Kiến trúc phát triển đã chọn

```text
UGTM backend / client thử nghiệm
             |
             v
Render: FastAPI AI API ---------> Neon PostgreSQL
   |                              jobs, sources, chunks, reports
   |                              pgvector + pg_trgm
   v
Broker Redis-compatible -----> CPU orchestration worker
(Render Key Value hoặc          (Render Background Worker)
 dịch vụ tương thích)               |        |
                                    |        +--> Object storage / UGTM file store
                                    v
                         Colab Remote GPU Inference Worker
                         encode + rerank, có auth và model revision
```

**Phân biệt hai worker:** CPU worker xử lý job, parsing, truy vấn Neon, gọi GPU và lưu report. Colab chỉ xử lý inference nặng; không cần giữ DB chính hay pipeline orchestration 24/7. Backend AI không thay backend nghiệp vụ UGTM.

Neon hỗ trợ [pgvector](https://neon.com/docs/extensions/pgvector) và [pg_trgm](https://neon.com/docs/extensions/pg_trgm). Dùng connection phù hợp cho runtime, direct connection cho migration/backup; kiểm tra driver và pool bằng smoke test. [Neon pooling](https://neon.com/docs/connect/connection-pooling)

Render hỗ trợ [Python native runtime](https://render.com/docs/native-runtimes), nên phát triển dịch vụ AI không cần chạy cả Compose mỗi ngày. Celery cần worker và broker riêng. Render Background Worker không thuộc nhóm dịch vụ có Free instances; không giả định một Free Web Service chạy job nền bền vững. Phương án chính là Render API + Background Worker + Key Value; nếu chưa cấp ngân sách worker, chạy CPU worker local trong phiên phát triển và giữ cùng broker/storage/contract. Đây là thay môi trường chạy, không giảm tính năng AI. [Render Celery](https://render.com/docs/deploy-celery), [Render free services](https://render.com/docs/free)

API và worker không dùng chung đường dẫn local. File upload, corpus và artifacts phải có nơi lưu bền vững dùng chung: ưu tiên file store của UGTM nếu đã có API, nếu chưa thì object storage dev với adapter. Neon lưu object key/hash/metadata; không coi filesystem của Render là kho file. Render Free Web Service có thể sleep khi không có traffic và mất file local khi restart/redeploy. [Render filesystem và sleep](https://render.com/docs/free)

Không cài/load BGE, Torch và GPU dependencies vào API chỉ để gọi remote worker. Tách remote inference client khỏi loader local, chia dependencies API/CPU worker/GPU notebook. Giữ local inference như lựa chọn cấu hình rõ ràng; không fallback âm thầm khiến Render thiếu RAM.

## 6. Cách dùng Colab theo quyết định của bạn

Giữ notebook `notebooks/colab_gpu_worker.ipynb` làm điểm vào và chuẩn hóa inference contract. Trong phiên phát triển, bật Colab worker, xác nhận health/model identity, cấu hình endpoint qua đường kết nối được phép và chạy quét từ backend Render. Transport có thể thay mà không đổi pipeline AI.

Google công bố quyền lợi Colab cho Google AI plans từ 22/09/2026; quyền lợi cụ thể phụ thuộc gói đủ điều kiện và tài khoản. Kiểm tra Subscription/compute units thực tế, không mặc định có GPU cố định hoặc chạy không giới hạn. [Google announcement](https://developers.googleblog.com/colab-is-now-part-of-your-google-ai-plan/), [Colab FAQ](https://research.google.com/colaboratory/faq.html)

Thiết kế vận hành: auth token riêng; model/revision và vector dimension được kiểm tra; batch có giới hạn; deadline/retry; GPU offline chuyển job sang trạng thái chờ và không giữ DB transaction trong lúc đợi. Runtime restart/endpoint đổi có bước reconnect, không phải index lại toàn bộ corpus. Ghi nhận điều kiện sử dụng của account/transport trong CP-01; không dùng cơ chế né giới hạn Colab.

Cache theo content hash + parser/chunker/model revision + config; checkpoint và kết quả lưu ngoài Colab. Mọi cloud upload phải theo quyền dữ liệu tương ứng. Các số latency báo cả GPU compute và network/cold-start để không nhầm giới hạn môi trường dev thành chất lượng thuật toán.

## 7. Lịch và trình tự checkpoint

| Checkpoint | Thời điểm tham chiếu | Ước lượng | Kết quả cần có |
|---|---|---:|---|
| CP-00 | 30/10, đầu ngày | 6 giờ | Baseline snapshot, phạm vi và API boundary |
| CP-01 | Phần còn lại tuần 1 | 40 giờ | Render–Neon–Colab chạy xuyên luồng, storage và queue rõ |
| CP-02 | Tuần 2 | 38 giờ | Parser/corpus/report schema đúng và có test hồi quy |
| CP-03 | Tuần 3 | 44 giờ | Dataset/split/evaluator và baseline nghiên cứu |
| CP-04 | Tuần 4 | 42 giờ | Retrieval/alignment/citation/classifier đã ablation trên dev |
| CP-05 | Tuần 5 | 36 giờ | Resilience, evidence workbench và hợp đồng tích hợp UGTM |
| CP-06 | Tuần 6 | 34 giờ | Test cuối, research report, readiness report và demo |
| Dự phòng | Phân bổ xuyên suốt | 30 giờ | Lỗi môi trường, dữ liệu, tích hợp và diễn tập |

Tổng 270 giờ là ước lượng để thấy rõ áp lực lịch. Checkpoint là cổng nghiệm thu theo đầu ra; ngày chỉ là lịch tham chiếu. Đọc tài liệu, gán nhãn và ghi experiment log có thể làm xen kẽ trong thời gian chờ GPU, không cần giả định có người phát triển thứ hai.

## 8. Định nghĩa “đạt nền tảng kỹ thuật để có thể thương mại hóa”

Giữ các yêu cầu kỹ thuật: interface thay GPU provider được; kết quả tái lập; có benchmark và giới hạn; auth và dữ liệu tách theo quyền; queue phục hồi; file/artifact không mất khi restart; migration/backup/restore; observability; model/data versioning; dependency/license inventory và tài liệu vận hành.

Báo cáo cuối phân biệt:
- **Đã chứng minh trong môi trường dev:** có test/log/evidence cụ thể.
- **Đã thiết kế nhưng chưa kiểm định:** ví dụ chuyển sang GPU thường trực, tải lớn, vận hành dài ngày.
- **Điều kiện còn lại nếu triển khai thương mại:** hạ tầng có SLA, quyền dữ liệu/giấy phép phù hợp và kiểm định vận hành.

Không xây billing, khảo sát người mua, định giá, sale, customer onboarding hoặc SaaS nhiều tổ chức trong đợt này. User study nếu có chỉ đo hiệu quả xem evidence và review, không phải nghiên cứu thị trường.

PyMuPDF công bố AGPL/commercial; ghi nhận và kiểm tra bản đang dùng trong inventory. PAN 2026 giới hạn dữ liệu cho nghiên cứu; tách quyền benchmark khỏi quyền corpus thực tế. [PyMuPDF license](https://pymupdf.readthedocs.io/en/latest/about.html#license-and-copyright), [PAN data terms](https://pan.webis.de/clef26/pan26-web/generated-plagiarism-detection.html)

## 9. Hồ sơ bàn giao

- AI API/schema và ví dụ tích hợp UGTM; màn hình dev kiểm chứng evidence.
- Dataset/model cards, splits, provenance, permission manifest.
- Benchmark configs, predictions, metrics/CI, ablation và error analysis.
- Runbook Render–Neon–Colab; khôi phục khi GPU offline; dependency lock và artifact manifest.
- Báo cáo nghiên cứu, bảng readiness kỹ thuật, slide và demo 5–7 phút.
- Backlog mở rộng giữ nguyên: Qwen/BGE sparse, OCR nâng cao, fine-tune; figure/code plagiarism và tìm toàn Internet là định hướng dài hạn như bản trước.

Không thay test để đạt con số mong muốn; không nhận kết quả synthetic là độ chính xác thực tế; không tự động kết luận đạo văn. Scope AI chỉ thay đổi nếu bạn chủ động chốt lại.
