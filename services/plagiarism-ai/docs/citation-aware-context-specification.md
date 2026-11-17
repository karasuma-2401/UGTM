# Đặc tả Thiết kế & Kết quả Nghiệm thu: Citation-Aware Context Engine (CP-04.3)

> **Mã công việc:** CP-04.3  
> **Module phụ trách:** [`app/citation.py`](file:///c:/Projects/UGTM-Plagiarism-AI/app/citation.py)  
> **Tích hợp hệ thống:** [`app/tasks.py`](file:///c:/Projects/UGTM-Plagiarism-AI/app/tasks.py), [`app/reports.py`](file:///c:/Projects/UGTM-Plagiarism-AI/app/reports.py)  
> **Bộ kiểm thử xác thực:** [`tests/test_citation_context.py`](file:///c:/Projects/UGTM-Plagiarism-AI/tests/test_citation_context.py) (12/12 passed)  
> **Bộ dữ liệu thẩm định:** `data/benchmark_v1/val.csv` (120 pairs)  
> **Trạng thái:** Hoàn thành nghiệm thu (Completed & Verified)

---

## 1. Tổng quan & Động lực Nghiên cứu (RQ3)

Trong hệ thống phát hiện đạo văn truyền thống, mối quan hệ giữa **trích dẫn (citation)** và **sao chép nội dung (text reuse)** thường bị xử lý theo hai hướng cực đoan sai lầm:
1. **Bỏ qua trích dẫn (Blind to Citations):** Mọi đoạn văn giống nhau đều bị coi là đạo văn, dẫn đến vô số cảnh báo giả đối với các trích dẫn học thuật hoàn toàn hợp lệ (ví dụ: trích dẫn nguyên văn định nghĩa có đóng ngoặc kép và ghi nguồn `[1]`).
2. **Tự động xóa bằng chứng khi thấy dấu ngoặc vuông (Naive Suppression):** Một số công cụ thương mại tự động bỏ qua kiểm tra ngay khi bắt gặp chuỗi `[n]`, tạo kẽ hở nghiêm trọng cho hành vi "đạo văn cõng trích dẫn" (sinh viên sao chép nguyên văn đoạn văn từ luận văn của người khác mà trong đoạn đó có sẵn dấu `[12]`, hoàn toàn không tự đọc hay dẫn nguồn bài báo gốc).

Để giải quyết câu hỏi nghiên cứu **RQ3** (*"Citation context và chính sách review giảm tải xử lý đến đâu khi vẫn giữ khả năng phát hiện tái sử dụng?"*), mốc **CP-04.3** thiết lập nguyên tắc **Trực giao (Orthogonality)**:
- **`reuse_label` (Bằng chứng tái sử dụng):** Là sự thật khách quan về mức độ trùng khớp văn bản giữa hai tài liệu, được đo lường bằng Dense, Lexical và Cross-Encoder Reranker.
- **`citation_status` & `citation_context` (Bối cảnh trích dẫn):** Cung cấp siêu dữ liệu bối cảnh độc lập (`present`, `absent`, `uncertain`, phân loại phong cách trích dẫn, kiểm tra ngoặc kép và đối soát tác giả nguồn).
- **Quy tắc bất biến:** *"Có citation không làm evidence biến mất; hệ thống không tự kết luận trích dẫn hợp lệ chỉ vì thấy dấu [n]"*.

---

## 2. Kiến trúc & Phân lớp Nhận diện Trích dẫn

```mermaid
flowchart TD
    subgraph Input ["Đoạn văn & Nguồn đối chiếu"]
        Q["Query Text (Đoạn bài nộp)"]
        SRC["Candidate Source Metadata (Tên file, tác giả, key)"]
    end

    subgraph RegexDetection ["Tầng Quét Mẫu Biểu thức Chính quy"]
        NUM["Numeric Citations: [1], [1, 2], [3-5]"]
        AY["Author-Year Citations: (Vaswani et al., 2017)"]
        NAR["Narrative Citations: 'Theo Vaswani (2017)' / 'Theo [1]'"]
        QUOTE["Direct Quotes: \"...\" / “...” / «...»"]
        EXCL["Exclusions: [Bảng 1], [Hình 2], arr[0], x[i]"]
    end

    subgraph ContextEngine ["Động cơ Bối cảnh Trích dẫn (app/citation.py)"]
        STATUS["Phân loại Status: present / absent / uncertain"]
        STYLE["Phân loại Style: numeric / author_year / narrative / mixed / none"]
        VERIFY["Đối soát Tác giả Nguồn (Source Attribution Verification)"]
    end

    subgraph Output ["Đầu ra Trực giao"]
        FINDING["Finding trong Report Schema v3: giữ nguyên reranker_score & review_required"]
        META["CitationContext Metadata: status, markers, source_attributed, details"]
    end

    Q --> NUM
    Q --> AY
    Q --> NAR
    Q --> QUOTE
    Q --> EXCL

    NUM --> STATUS
    AY --> STATUS
    NAR --> STATUS
    EXCL --> STATUS

    STATUS --> STYLE
    SRC --> VERIFY
    AY --> VERIFY
    NAR --> VERIFY
    NUM --> VERIFY

    STATUS --> META
    STYLE --> META
    VERIFY --> META
    QUOTE --> META

    META --> FINDING
```

### 2.1. Phân loại Mẫu Trích dẫn Chuẩn học thuật
1. **Trích dẫn Số trong Ngoặc vuông (IEEE, ACM, TCVN):**
   - Biểu thức: `(?<![a-zA-Z0-9_])\[\s*(\d{1,4}(?:[-–—]\d{1,4})?(?:\s*,\s*\d{1,4}(?:[-–—]\d{1,4})?)*)\s*\]`
   - Hỗ trợ: số đơn `[1]`, danh sách `[1, 3, 5]`, dải số `[3-5]`, dải số kết hợp `[1-3, 7, 9]`.
   - **Bộ lọc loại trừ (Negative Lookbehind/Lookahead):**
     - Loại bỏ chỉ mục mảng lập trình: `arr[0]`, `x[i]` nhờ ràng buộc `(?<![a-zA-Z0-9_])`.
     - Loại bỏ tham chiếu bảng biểu / hình vẽ: `[Bảng 1]`, `[Hình 2]`, `[Table 3]`, `[Figure 4]`, `[Equation 5]`.
2. **Trích dẫn Tác giả - Năm (APA, Harvard):**
   - Dạng trong ngoặc: `\(\s*([A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]*(?:\s+[A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]*)*(?:\s+(?:et\s+al\.?|và\s+cs\.?|và\s+cộng\s+sự|và\s+[A-ZÀ-Ỹ][a-zà-ỹA-ZÀ-Ỹ0-9]+))?)\s*,\s*((?:19|20)\d{2}(?:[a-z])?(?:\s*,\s*(?:tr\.|p\.|pp\.)\s*\d+)?)\s*\)`
   - Ví dụ nhận diện: `(Vaswani et al., 2017)`, `(Nguyễn và cộng sự, 2021)`, `(Goodfellow et al., 2016, tr. 45)`.
3. **Trích dẫn Tường thuật (Narrative Citation):**
   - Dạng dẫn dắt: `Theo Vaswani et al. (2017)...`, `Nghiên cứu của Lê Văn B (2020)...`, `Theo [1]...`, `Tác giả [3] cho biết...`.
4. **Nhận diện Khối Trích dẫn Nguyên văn (Direct Quotation):**
   - Biểu thức: `r'("[^"\n]{4,}"|“[^”\n]{4,}”|«[^»\n]{4,}»)'`.
   - Khi phát hiện đoạn văn bản trùng khớp nằm trọn vẹn trong dấu ngoặc kép và có trích dẫn, hệ thống bật cờ `is_verbatim_quote = True`.

---

## 3. Khung Dữ liệu & Đặc tả Metadata (`CitationContext`)

```python
@dataclass
class CitationMarker:
    marker_text: str          # Chuỗi trích dẫn thô, ví dụ: "[1, 2]" hoặc "(Vaswani et al., 2017)"
    marker_type: str          # "numeric" | "author_year" | "narrative" | "generic"
    start_char: int           # Offset ký tự bắt đầu trong query_text
    end_char: int             # Offset ký tự kết thúc trong query_text
    author: str | None = None # Tên tác giả trích dẫn (nếu có), ví dụ: "Vaswani et al."
    year: str | None = None   # Năm xuất bản (nếu có), ví dụ: "2017"
    indices: list[int] = field(default_factory=list) # Danh sách chỉ số số học: [1, 2]

@dataclass
class CitationContext:
    status: str               # "present" | "absent" | "uncertain"
    markers: list[CitationMarker] = field(default_factory=list)
    has_quotation_marks: bool = False
    citation_style: str = "none" # "numeric" | "author_year" | "narrative" | "mixed" | "generic" | "none"
    source_attributed: bool | None = None # True (khớp nguồn), False (trích sai/thứ cấp), None (chưa đối soát)
    attribution_details: str = ""
    is_verbatim_quote: bool = False
```

---

## 4. Xác minh Nguồn Trích dẫn & Cảnh báo Đạo văn Thứ cấp

Thuật toán [`evaluate_source_attribution`](file:///c:/Projects/UGTM-Plagiarism-AI/app/citation.py#L229) đối soát trực tiếp giữa tác giả trong trích dẫn và tài liệu nguồn được hệ thống truy xuất:

### 4.1. Trường hợp Khớp Nguồn Trực tiếp (`source_attributed = True`)
- **Tình huống:** Query viết: *"Theo Vaswani et al. (2017) [1], cơ chế Attention..."*. Nguồn đối chiếu là `Vaswani_2017_Attention_Is_All_You_Need.pdf`.
- **Kết luận:** Tác giả trích dẫn trùng khớp với tài liệu nguồn tìm thấy trong kho.

### 4.2. Trường hợp Trích dẫn Sai Nguồn / Đạo văn Thứ cấp (`source_attributed = False`)
- **Tình huống:** Query viết: *"Theo Vaswani et al. (2017) [1], cơ chế Attention..."*. Tuy nhiên, tài liệu nguồn tìm thấy trong kho lại là `Khoa_Luan_Tot_Nghiep_Le_Van_C.docx` (Khóa luận của sinh viên Lê Văn C năm 2022).
- **Cảnh báo tự động:**
  > `Potential misattribution / secondary plagiarism: Query cites 'Vaswani et al.' but text was retrieved from 'Khoa_Luan_Tot_Nghiep_Le_Van_C.docx' without direct attribution.`
- **Ý nghĩa thực tiễn:** Ngăn chặn tuyệt đối chiêu trò sao chép đoạn văn từ một khóa luận đi trước nhưng giữ nguyên citation của bài báo quốc tế để che giấu hành vi sao chép từ khóa luận đó.

### 4.3. Trường hợp Trích dẫn Số chưa đối soát Thư mục (`source_attributed = None`)
- **Tình huống:** Query chỉ chứa `[3]` mà chưa có bảng tra danh mục tài liệu tham khảo cuối bài.
- **Kết luận:** Hệ thống ghi nhận trạng thái chưa đối soát (`unverified`), yêu cầu tra cứu thư mục tham khảo thay vì vội vàng kết luận hợp lệ.

---

## 5. Tích hợp Hệ thống & Report Schema v3

### 5.1. Tích hợp Background Worker (`app/tasks.py`)
Khi quét tài liệu, mỗi `finding` được gắn kèm `citation_status` và `citation_context`:
```python
cit_ctx = detect_citation_context(
    query_text=query_chunk.text,
    source_metadata={
        "source_key": str(item.get("source_key", "")),
        "source_version": int(item.get("source_version", 1)),
        "source_filename": str(item.get("source_filename", "")),
        "document_family": str(item.get("document_family", "")),
    },
)
finding["citation_status"] = cit_ctx.status
finding["citation_context"] = cit_ctx.to_dict()
```
Dù `cit_ctx.status == "present"`, `finding` vẫn được lưu đầy đủ vào cơ sở dữ liệu và chuyển về giao diện người dùng kèm cờ `review_required = True`.

### 5.2. Mở rộng Report Schema v3 (`app/reports.py`)
Bổ sung các chỉ số thống kê trích dẫn vào `summary`:
- `cited_findings`: Tổng số phát hiện có trích dẫn rõ ràng (`status == "present"`).
- `uncited_findings`: Tổng số phát hiện hoàn toàn không có trích dẫn (`status == "absent"`).
- `uncertain_findings`: Tổng số phát hiện có trích dẫn mơ hồ hoặc cần kiểm tra (`status == "uncertain"`).

---

## 6. Kết quả Kiểm thử Tự động (Regression & Unit Tests)

Bộ kiểm thử [`tests/test_citation_context.py`](file:///c:/Projects/UGTM-Plagiarism-AI/tests/test_citation_context.py) gồm 12 ca kiểm thử chuyên sâu:
1. `test_numeric_citation_detection`: Passed (nhận diện `[1]`, `[1, 3, 5]`, `[7-9]`).
2. `test_non_citation_exclusions`: Passed (loại trừ `[Bảng 1]`, `[Hình 2]`, `arr[0]`, `x[i]`).
3. `test_author_year_citation_detection`: Passed (nhận diện `(Vaswani et al., 2017)`, `(Nguyễn và cộng sự, 2021, tr. 34)`).
4. `test_narrative_citation_detection`: Passed (nhận diện `Theo Vaswani (2017)...`, `Theo [1]...`).
5. `test_direct_quotation_markers`: Passed (nhận diện ngoặc kép và cờ `is_verbatim_quote`).
6. `test_absent_citation`: Passed (xác nhận `status="absent"` khi không dẫn nguồn).
7. `test_uncertain_citation_context`: Passed (xác nhận `status="uncertain"` cho câu mơ hồ).
8. `test_source_attribution_matching`: Passed (xác nhận `source_attributed=True` khi tác giả khớp tên file).
9. `test_source_attribution_misattribution_alert`: Passed (cảnh báo `Potential misattribution` khi dẫn nguồn thứ cấp).
10. `test_numeric_citation_requires_bibliography_lookup`: Passed (xác nhận `source_attributed=None` cho trích dẫn số đơn lẻ).
11. `test_evidence_preservation_invariant`: Passed (khẳng định finding không bị ẩn/xóa khi có trích dẫn).
12. `test_report_schema_v3_citation_summary_integration`: Passed (xác nhận Schema v3 tính đúng `cited_findings`, `uncited_findings`, `uncertain_findings`).

Toàn bộ test suite dự án đạt **152/152 passed** (tăng từ 140 trước CP-04.3).

