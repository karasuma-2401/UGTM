# Báo cáo Kiểm thử Smoke Test Neon PostgreSQL (`CP-01.2`)

**Cập nhật:** 30/10/2026  
**Checkpoint:** `CP-01.2 — Chuẩn bị Neon và migration`  
**Trạng thái kiểm thử:** **PASSED (100% Đạt)**

---

## 1. Thông tin Kết nối & Môi trường Neon Dev (Không lộ Secret)

- **Hạ tầng:** Neon Serverless PostgreSQL (Region: AWS Singapore `ap-southeast-1`).
- **Phiên bản PostgreSQL:** `PostgreSQL 18.6`.
- **Chế độ bảo mật TLS:** Bắt buộc (`sslmode=require`).
- **Cấu hình hai luồng DSN:**
  - **Runtime Connection (Pooled):** `postgresql://***:***@ep-damp-sea-b3yjz9pa-pooler.c-4.ap-southeast-1.aws.neon.tech/neondb` (Dành cho API và CPU Worker xử lý truy vấn thường nhật).
  - **Migration & Backup Connection (Direct):** `postgresql://***:***@ep-damp-sea-b3yjz9pa.c-4.ap-southeast-1.aws.neon.tech/neondb` (Dành cho chạy DDL migration, backup và bảo trì).

---

## 2. Kết quả Quản lý Migration (`app/migrations.py` + `sql/`)

- **Bảng theo dõi phiên bản:** `integrity.schema_migrations` (`version`, `applied_at`, `checksum_sha256`).
- **Migration `001_init` (`sql/001_init.sql`):**
  - Trạng thái: **Đã áp dụng thành công (APPLIED)**.
  - Tính năng idempotent: Khi chạy lại, hệ thống kiểm tra mã băm SHA-256 và tự động bỏ qua (`skipped: ['001_init']`) mà không sinh lỗi trùng lặp.

### Extensions đã kích hoạt:
| Extension | Phiên bản thực tế trên Neon | Mục đích sử dụng |
|---|---|---|
| `vector` | `0.8.6` | Lưu trữ vector nhúng 1024 chiều (`BAAI/bge-m3`) và toán tử khoảng cách cosine `<=>`. |
| `pg_trgm` | `1.6` | Trích xuất n-gram 3 ký tự, hàm tính độ tương đồng `similarity()` và GIN index `gin_trgm_ops`. |

### Bảng dữ liệu đã khởi tạo trong schema `integrity`:
- `integrity.jobs` (quản lý trạng thái quét, idempotency theo hash và báo cáo JSONB)
- `integrity.sources` (danh mục tài liệu nguồn trong kho)
- `integrity.chunks` (đoạn văn bản và vector nhúng)
- `integrity.schema_migrations` (sổ đăng ký migration)

---

## 3. Kết quả Smoke Test Truy vấn Kết hợp (RRF Dense + Trigram)

Kiểm thử được thực hiện bằng đúng hàm truy vấn `retrieve()` trong `app/search.py` với tài liệu mẫu tổng hợp (2 chunk 1024-d):

```json
{
  "search_and_retrieval": {
    "status": "passed",
    "candidates_found": 2,
    "top_dense_score": 0.513866,
    "top_lexical_score": 0.495238,
    "top_rrf_score": 0.032787
  }
}
```
- **Xác nhận:**
  1. Toán tử cosine distance `1 - (c.embedding <=> %s::vector)` hoạt động mượt mà trên Neon.
  2. Hàm `similarity(c.content, %s)` và GIN index hoạt động chính xác.
  3. Thuật toán xếp hạng Reciprocal Rank Fusion (RRF) kết hợp điểm thành công.
  4. Toàn bộ dữ liệu synthetic test được dọn dẹp sạch sẽ sau khi test xong (`Cleaned up synthetic test corpus`).

---

## 4. Phân loại Lỗi Kết nối (`classify_db_exception`)

Kiểm thử cố ý cung cấp mật khẩu sai trên host Neon thật:
- Ngoại lệ bắt được: `psycopg.errors.InvalidPassword` / `OperationalError`
- Mã lỗi phân loại chuẩn hóa: **`DB_AUTH_ERROR`** (khớp `expected_code`).
- Bảo vệ an toàn: Toàn bộ thông báo lỗi và log hệ thống đều được bọc qua `mask_dsn()`, tuyệt đối không in mật khẩu ra log hoặc console.

