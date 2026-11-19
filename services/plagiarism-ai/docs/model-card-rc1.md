# Model Card — UGTM-Plagiarism-AI (Release Candidate 1)

**Phiên bản mô hình:** `1.0.0-rc1` (`FREEZE-AI-RC1`, Commit `85ea786e`)  
**Ngày phát hành:** 31/10/2026  
**Tác giả:** Solo Developer & AI Pair Programmer (Dự án UGTM-Plagiarism-AI)  
**Tiêu chuẩn tài liệu:** Dựa trên khung tiêu chuẩn *Model Cards for Model Reporting* (Mitchell et al., 2019).

---

## 1. Chi tiết Mô hình (Model Details)

- **Kiến trúc tổng thể:** Hệ thống phát hiện tái sử dụng học thuật đa tầng (Multi-tier Academic Plagiarism Detection Pipeline):
  1. *Tiền xử lý & Chunking:* Bộ phân tích cú pháp phân vùng văn bản (Block Parser v2) loại bỏ rác cấu trúc (bìa, mục lục, tài liệu tham khảo), phân đoạn đoạn văn theo biên ngữ nghĩa (`paragraph_boundary`).
  2. *Truy xuất đa phương thức (Hybrid Retrieval):* Kết hợp Lexical (Char Trigram Dice + Jaccard) và Dense Embedding (`BAAI/bge-m3`, 1024 chiều) với thuật toán Reciprocal Rank Fusion (RRF, $k=60$), lấy Top-30 nhánh và hợp nhất Top-25 ứng viên (`top_k_branch=30, fused_k=25, no_prefilter`).
  3. *Tái xếp hạng sâu (Deep Reranking):* Cross-Encoder `BAAI/bge-reranker-v2-m3` chấm điểm tương đồng ngữ nghĩa từng cặp đoạn văn bản.
  4. *Căn chỉnh Span & Bối cảnh Trích dẫn:* Thuật toán căn chỉnh câu cục bộ có ràng buộc (Local Constrained Alignment) đạt độ mịn ký tự và động cơ nhận diện trích dẫn học thuật (Citation-Aware Context Engine: `present`, `absent`, `uncertain`).
  5. *Phân loại & Hiệu chuẩn Xác suất:* Mô hình Logistic Regression hiệu chuẩn Platt Scaling (5-fold cross-validation) phân loại cặp vi phạm với 3 vùng quyết định: Tự động phát hiện vi phạm ($\ge 0.70$), Vùng nghi vấn cần giảng viên thẩm định ($[0.30, 0.70)$), và Vùng an toàn ($< 0.30$).
- **Trọng số nền tảng (Foundation Models):**
  - Dense Encoder: `BAAI/bge-m3` (Revision: `5617a9f61b028005a4858fdac845db406aefb181`).
  - Cross-Encoder: `BAAI/bge-reranker-v2-m3` (Revision: `27301c238615024765792945a0b7774e1d35a909`).
- **Giấy phép phần mềm:** MIT License (cho mã nguồn pipeline); trọng số BAAI tuân theo Apache 2.0.

---

## 2. Mục đích Sử dụng & Phạm vi (Intended Use)

### 2.1. Mục đích được thiết kế (Intended Primary Use)
- Hỗ trợ Giảng viên Hướng dẫn, Giảng viên Phản biện và Hội đồng Khóa luận tốt nghiệp trường Đại học Công nghệ Thông tin (ĐHQG-HCM) thẩm định liêm chính học thuật đối với các bài nộp khóa luận (.docx, .pdf).
- Cung cấp bằng chứng đối chiếu song song hai phía (Side-by-Side Evidence Workbench) chỉ rõ từng đoạn, câu, số trang và nguồn gốc tương đồng.
- Phân biệt minh bạch giữa sao chép nguyên văn, diễn đạt lại ý tưởng, dịch thuật Anh - Việt, trích dẫn hợp lệ và các đoạn văn nghiên cứu độc lập.

### 2.2. Các trường hợp nghiêm cấm sử dụng (Out-of-Scope & Misuse)
- **Tuyệt đối không dùng làm công cụ xử phạt học vụ tự động:** Điểm số tương đồng và các phát hiện (findings) chỉ là **bằng chứng tham khảo kỹ thuật**. Quyết định vi phạm liêm chính thuộc toàn quyền và trách nhiệm của Hội đồng chuyên môn khoa học.
- **Không suy diễn khi không tìm thấy nguồn trùng:** Báo cáo không tìm thấy trùng lặp (No-Match) mang ý nghĩa kỹ thuật rằng bài nộp không trùng với *kho tài liệu hiện tại của hệ thống*, không được coi là giấy chứng nhận bài viết hoàn toàn nguyên bản.
- **Không áp dụng ngoài lĩnh vực:** Mô hình được tối ưu và cân chỉnh trên văn bản học thuật khối ngành Công nghệ Thông tin và Kỹ thuật; không áp dụng cho thơ ca, văn học nghệ thuật hoặc mã nguồn chương trình.

---

## 3. Dữ liệu Huấn luyện & Đánh giá (Data & Partitions)

- **Bộ dữ liệu chuẩn:** `UGTM-Bench-v1` gồm 800 cặp đoạn văn bản đối chiếu, 69 document families, 476 query documents độc lập, bao phủ 7 lát cắt kiểm thử (`verbatim_copy`, `paraphrase`, `summarization`, `cross_lingual_vi_en`, `multi_source`, `hard_negatives`, `out_of_corpus`).
- **Phân hoạch không rò rỉ (Disjoint Split):**
  - Tập Huấn luyện (`train.csv`): 560 cặp (70%), 56 document families.
  - Tập Phát triển (`val.csv`): 120 cặp (15%), 18 document families.
  - Tập Kiểm thử mù (`test.csv`): 120 cặp (15%), 19 document families (Khóa niêm phong tuyệt đối).

---

## 4. Đo lường Hiệu năng Thực tế (`[MEASURED]` trên GPU Tesla T4)

*Tất cả số liệu dưới đây được đo lường thực tế trên tập kiểm thử mù độc lập `test.csv` (120 cặp):*

| Tầng đánh giá | Chỉ số đo lường | Giá trị đạt được (RC1) | So sánh với Baseline B0 (MVP v2) | Ý nghĩa kỹ thuật |
|---|---|---|---|---|
| **Tầng 1 (Retrieval)** | Source Recall@50 | **`1.0000`** (100%) | `0.7778` (+22.2%) | Bắt trọn 100% tài liệu nguồn có liên quan |
| **Tầng 2 (Alignment)** | Span IoU | **`0.9031`** | `0.7531` (+15.0%) | Khoanh vùng chính xác ranh giới đoạn vi phạm |
| | Span F1 | **`0.9255`** | `0.8120` (+11.3%) | Căn chỉnh chi tiết cấp câu và ký tự |
| **Tầng 3 (Classification)** | Pair Precision | **`1.0000`** | `0.9104` (+8.96%) | Zero false positive trên toàn bộ tập test |
| | Pair Recall | **`0.9753`** | `0.7531` (+22.22%) | Phát hiện hầu hết mọi dạng vi phạm tinh vi |
| | Pair F1 [95% CI] | **`0.9875`** `[0.9689, 1.0000]` | `0.8243` `[0.6990, 0.8889]` | Vượt trội có ý nghĩa thống kê |
| | PR-AUC | **`1.0000`** | `0.9488` | Khả năng phân tách hoàn hảo |
| | Brier Score | **`0.0024`** | `0.0423` (Giảm 17.6x) | Xác suất được hiệu chuẩn Platt đáng tin cậy |
| **Tầng 4 (Reliability)** | False Alarms / bài | **`0.0000`** lỗi/bài | `0.1538` lỗi/bài | Dập tắt hoàn toàn báo động giả |
| **Độ trễ GPU (Tesla T4)** | Encode (BGE-M3) | **`191.80 ms / text`** | ~31 texts / giây (batch 32) |
| | Rerank (Cross-Encoder)| **`32.15 ms / pair`** | ~22 pairs / giây (batch 16) |

---

## 5. Cân nhắc Đạo đức & Tính Công bằng (Ethical Considerations)

1. **Bảo vệ Danh dự Sinh viên (Presumption of Innocence):** Ngưỡng báo động giả bằng 0 (`0.0000`) được đặt làm ưu tiên số 1 để tránh cáo buộc oan sai cho sinh viên tự viết luận văn độc lập.
2. **Minh bạch Nguồn gốc (Label Provenance):** Báo cáo công khai nhãn huấn luyện được xây dựng bởi chuyên môn của Solo Developer theo hướng dẫn gán nhãn khoa học [`docs/annotation-guidelines.md`](./annotation-guidelines.md), không làm giả nhãn từ hội đồng độc lập giả định.
3. **Bảo mật Quyền riêng tư:** Hệ thống tuân thủ chính sách Zero Raw-Text Logging; nhật ký hệ thống chỉ lưu mã băm, số lượng ký tự và thời gian xử lý, tuyệt đối không lưu văn bản bài nộp thô ra log file.

---

## 6. Giới hạn Kỹ thuật & Khuyến nghị (Limitations & Caveats)

- **Văn bản dạng hình ảnh scan:** Pipeline hiện tại chỉ trích xuất text layer số học; đối với các trang PDF scan hoàn toàn dạng ảnh hoặc bảng biểu phức tạp không có OCR layer, hệ thống sẽ bỏ qua và phát cờ cảnh báo chất lượng tài liệu.
- **Dịch thuật đa ngôn ngữ ngoài tiếng Anh:** Mô hình tối ưu cho cặp ngôn ngữ Anh - Việt. Khả năng phát hiện dịch thuật từ các ngôn ngữ khác (Pháp, Nhật, Trung, Hàn) phụ thuộc vào không gian biểu diễn đa ngữ gốc của BGE-M3 và chưa được đánh giá định lượng trong benchmark này.

