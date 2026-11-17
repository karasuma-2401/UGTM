# Hướng dẫn gán nhãn passage pairs

## Mục đích

Classifier chỉ học để xếp hạng **candidate tái sử dụng nội dung** cho bước `review_required`. Nhãn không phải là kết luận đạo văn và không được dùng làm quyết định kỷ luật.

## Tạo batch review local

Chạy từ root project. File kết quả chứa đoạn trích nội bộ nên ở trong `training_data/` đã bị Git ignore; không upload lên Kaggle hay Google Sheets nếu chưa có phép xử lý trên cloud.

```powershell
.\.venv\Scripts\python.exe -m scripts.create_annotation_candidates `
  --manifest .\data\corpus_manifest.csv `
  --staging-dir .\corpus_staging `
  --output .\training_data\annotation_candidates.csv `
  --max-tasks 600 `
  --exclude-sections "cover,table_of_contents,abbreviations,references" `
  --dedup-documents
```

Script tự động nhận diện metadata và lọc bỏ trước các phần bìa, mục lục, bảng từ viết tắt, tài liệu tham khảo và kiểm tra khử trùng lặp khóa luận giữa các định dạng (PDF/DOCX) trước khi chạy thuật toán TF-IDF tìm top candidates. Điều này ngăn chặn việc 88% ứng viên bị chiếm bởi mẫu bìa hoặc mục lục chung.

## Cách review từng task

Điền các cột sau trong `annotation_candidates.csv`:

| Cột | Giá trị |
|---|---|
| `reviewer_1_label` | `1` nếu xác nhận tái sử dụng nội dung; `0` nếu độc lập |
| `reviewer_2_label` | Nhãn của người review thứ hai |
| `final_label` | `0` hoặc `1` sau khi giải quyết bất đồng |
| `status` | Chỉ ghi `approved` khi task đủ điều kiện train |
| `note` | Lý do ngắn: `exact_copy`, `paraphrase`, `same_topic_independent`, `boilerplate`, `citation_present`, ... |

Không gán `1` chỉ vì cùng thuật ngữ, cùng phần giới thiệu, biểu mẫu, câu trích dẫn hay score cao. Ngược lại, một đoạn có trích dẫn hợp lệ vẫn có thể là content reuse; phần quyết định quy chế do giảng viên xử lý sau này.

Ưu tiên hai reviewer độc lập cho từng task. Các case bất đồng cần ghi `final_label` và note lý do.

## Bổ sung dữ liệu có kiểm soát

Candidate từ corpus có thể chứa phần boilerplate và không đủ positive. Bổ sung các cặp đã được phép, do nhóm tự tạo/kiểm chứng:

- Copy gần nguyên văn.
- Paraphrase hoặc dịch Việt–Anh nhưng giữ ý chính.
- Hard-negative: cùng lĩnh vực/cùng thuật ngữ nhưng nội dung độc lập.

Mỗi biến thể của cùng một nguồn dùng cùng `group_id`; không để cùng group bị chia sang train và test.

## Tạo `pairs.csv` cho Kaggle

Khi có các hàng `approved`, chạy:

```powershell
.\.venv\Scripts\python.exe -m scripts.finalize_annotation_pairs `
  --input .\training_data\annotation_candidates.csv `
  --output .\training_data\pairs.csv `
  --require-two-reviewers
```

Script dừng nếu còn task chưa duyệt, thiếu nhãn 0/1, trùng task ID hoặc có dưới sáu group độc lập. Chỉ upload `pairs.csv` lên Kaggle Dataset Private khi quyền xử lý các đoạn trích trên cloud đã được xác nhận.
