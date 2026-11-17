# Đặc Tả Quy Tắc Tự Loại Trừ (Self-Match Exclusion) & Chủ Quyền Dữ Liệu (CP-02.3)

> **Mã mốc:** `CP-02.3`  
> **Áp dụng cho:** `app/search.py`, `app/tasks.py`, `app/corpus.py`, Database PostgreSQL Neon  
> **Trạng thái:** Đã triển khai và có bộ kiểm thử tự động

---

## 1. Vấn Đề Cần Giải Quyết

Trong hệ thống rà soát tính liêm chính học thuật, hai lỗi nghiêm trọng thường xảy ra nếu không có cơ chế quản lý danh mục và gia đình tài liệu:

1. **Self-Match False Positive (Dương tính giả do trùng chính mình):**
   - Sinh viên nộp bản thảo DOCX để kiểm tra trước, sau đó nộp bản PDF hoàn chỉnh cuối kỳ.
   - Khi quét bản PDF, hệ thống quét trúng bản DOCX trong kho ngữ liệu và báo "đạo văn 99%" đối với bài của chính sinh viên đó.
2. **Train/Val/Test Data Leakage (Rò rỉ dữ liệu giữa các tập huấn luyện):**
   - Nếu bản DOCX rơi vào tập huấn luyện (Train) và bản PDF rơi vào tập kiểm thử (Test), mô hình học sâu sẽ "nhớ" bài viết, dẫn đến kết quả đánh giá F1/Recall ảo, không phản ánh năng lực khái quát hóa thực tế.
3. **Vi Phạm Chủ Quyền Dữ Liệu (Data Sovereignty Breach):**
   - Tài liệu mật, tài liệu đề tài quân sự/doanh nghiệp yêu cầu không được gửi ra ngoài máy chủ nội bộ nhưng lại bị tự động gửi lên các dịch vụ điện toán đám mây công cộng (Remote GPU / Cloud Storage).

---

## 2. Quy Tắc Tự Loại Trừ (Self-Match Exclusion Policy)

Hàm `retrieve()` trong `app/search.py` và tác vụ quét `scan_document()` trong `app/tasks.py` áp dụng quy tắc lọc tự động tại tầng cơ sở dữ liệu:

```python
def retrieve(
    query_text: str,
    query_vector,
    corpus_id: str,
    top_k: int = 6,
    fused_k: int = 5,
    exclude_source_keys: list[str] | None = None,
    exclude_document_family: str | None = None,
) -> list[dict]:
```

### Các điều kiện loại trừ:
1. **Loại trừ theo mã nguồn (`exclude_source_keys`):**
   $$\text{s.source\_key} \neq \text{ANY}(exclude\_source\_keys)$$
   Ngăn chặn tài liệu tự so khớp với chính các chunk đã được đánh chỉ mục của bản thân nó trong quá khứ.
2. **Loại trừ theo gia đình tài liệu (`exclude_document_family`):**
   $$\text{s.document\_family} \text{ IS NULL OR } \text{s.document\_family} \neq exclude\_document\_family$$
   Ngăn chặn tài liệu so khớp với các phiên bản nháp, bản sửa đổi bổ sung, hoặc các định dạng chuyển đổi khác của cùng một sinh viên/đề tài.

---

## 3. Phân Cụm Ngăn Rò Rỉ Dữ Liệu (Group-Aware Split)

Thuật toán `split_corpus_by_family()` trong `app/corpus.py` đảm bảo nguyên tắc bất biến:

$$\forall f \in \text{DocumentFamilies}: \quad \text{Members}(f) \subseteq \text{Train} \lor \text{Members}(f) \subseteq \text{Val} \lor \text{Members}(f) \subseteq \text{Test}$$

Tất cả các phiên bản và tài liệu trùng lặp thuộc cùng một cụm `document_family` **bắt buộc phải được phân vào cùng một tập dữ liệu**. Không có bất kỳ thành viên nào của một gia đình xuất hiện ở hai tập khác nhau.

---

## 4. Kiểm Soát Quyền Xử Lý Dữ Liệu (Processing Permissions)

Cột `processing_permission` trong cơ sở dữ liệu và manifest quy định ranh giới xử lý:

- `all` (hoặc `cloud_allowed`): Tài liệu được phép lưu trữ trên Cloudflare R2 và suy luận trên Colab Remote GPU.
- `local_only`: Tài liệu nhạy cảm hoặc có yêu cầu bảo mật cao. Hệ thống kích hoạt kiểm tra tiền xử lý nghiêm ngặt:

```python
def assert_processing_allowed(permission: str, target: Literal["cloud_storage", "remote_gpu"]) -> None:
    if permission.strip().lower() == "local_only" and target in ("cloud_storage", "remote_gpu"):
        raise PermissionError(
            f"Processing operation '{target}' rejected: Document is marked with processing_permission='local_only'."
        )
```

Mọi tác vụ cố gắng đẩy dữ liệu `local_only` lên cloud worker sẽ lập tức bị chặn với ngoại lệ `PermissionError`.

