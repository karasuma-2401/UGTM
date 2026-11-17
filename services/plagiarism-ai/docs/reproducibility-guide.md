# Hướng Dẫn Tái Lập Toàn Diện — Reproducibility Guide (CP-06.2)

Tài liệu này cung cấp quy trình từng bước để bất kỳ nhà nghiên cứu, kỹ sư hoặc hội đồng khoa học nào cũng có thể **tái lập 100% kết quả thực nghiệm** của dự án **UGTM-Plagiarism-AI** từ một môi trường máy tính hoàn toàn mới (Clean Environment).

---

## 1. Yêu cầu Hệ thống Tối thiểu (System Prerequisites)

| Thành phần | Yêu cầu cho Kiểm định Tái tạo (Offline/Eval) | Yêu cầu cho Chạy Toàn phần GPU (Full Inference) |
|---|---|---|
| **Hệ điều hành** | Windows 10/11, Ubuntu 22.04+ hoặc macOS | Windows 10/11, Ubuntu 22.04+ (kèm Google Colab GPU) |
| **Python** | Python `3.12` hoặc `3.13` (Khuyến nghị `Python 3.13.15`) | Python `3.12` hoặc `3.13` |
| **RAM** | Tối thiểu 4 GB RAM | Tối thiểu 8 GB RAM |
| **GPU / VRAM** | Không bắt buộc (Chạy thuần CPU) | GPU có tối thiểu 8–16 GB VRAM (Tesla T4, V100, RTX 3060+) |
| **Dung lượng đĩa** | ~500 MB cho code và môi trường virtualenv | ~5 GB (nếu tải cục bộ trọng số BGE-M3 và Reranker) |

---

## 2. Quy trình Thiết lập Môi trường Sạch (Step-by-Step Setup)

### Bước 2.1: Clone kho mã nguồn và khởi tạo môi trường ảo

```bash
git clone https://github.com/karasuma-2401/UGTM-Plagiarism-AI.git
cd UGTM-Plagiarism-AI

# Khởi tạo Python virtualenv sạch
python -m venv .venv

# Kích hoạt virtualenv:
# Trên Windows PowerShell:
.\.venv\Scripts\Activate.ps1
# Trên Linux/macOS:
source .venv/bin/activate
```

### Bước 2.2: Cài đặt đúng lockfile phụ thuộc (Zero Dependency Mismatch)

Để đảm bảo không bị sai lệch phiên bản thư viện:

```bash
# Cài đặt chính xác các gói đã được khóa phiên bản trong lockfile
pip install -r requirements/cpu-dev-win32.lock.txt
```

---

## 3. Kiểm tra Mã băm Đối soát (Integrity Check)

Trước khi tái lập, kiểm tra mã băm SHA-256 của các tệp nòng cốt để đảm bảo dữ liệu không bị sửa đổi ngoài ý muốn:

```bash
# Trên Windows PowerShell:
(Get-FileHash data/benchmark_v1/test.csv).Hash.ToLower()
# Kết quả bắt buộc khớp chính xác:
# 7b8432a8e7f480ad3589c26e9220acac5d9afd7e89b22c914a9ac2cf277a340a

# Trên Linux/macOS:
sha256sum data/benchmark_v1/test.csv
```

---

## 4. Các Phương thức Tái lập Thực nghiệm (Reproduction Workflows)

### Phương thức 1: Tái tạo Tức thì Toàn bộ Bảng Kết quả Đánh giá (Khuyến nghị)
Phương thức này đọc trực tiếp dữ liệu dự đoán của 6 hệ thống trên 120 cặp kiểm thử `data/final_test_predictions/predictions_*.json`, thực hiện tính toán lại toàn bộ metrics 4 tầng và phân bổ Cluster Bootstrap 95% CI (1.000 lần lấy mẫu theo cụm, seed cố định 42):

```bash
python scripts/reproduce_benchmark_tables.py
```

* **Thời gian thực thi:** ~4 giây.
* **Đầu ra:** Bảng Markdown xuất trực tiếp trên màn hình console và được ghi vào tệp [`reports/reproduced_tables.md`](file:///c:/Projects/UGTM-Plagiarism-AI/reports/reproduced_tables.md).
* **Tiêu chí nghiệm thu:** Script tự động đối soát từng float với các báo cáo chính thức; xác nhận `100% Bit-for-bit Parity` (Delta < 1e-4).

---

### Phương thức 2: Tái chạy Toàn phần Luồng Suy luận qua Colab GPU Worker
Nếu bạn muốn trực tiếp cho GPU tính toán lại từ đầu toàn bộ các biểu diễn Dense Embedding và điểm số Cross-Encoder:

1. **Khởi động GPU Worker trên Google Colab:**
   * Mở notebook [`notebooks/colab_gpu_worker.ipynb`](file:///c:/Projects/UGTM-Plagiarism-AI/notebooks/colab_gpu_worker.ipynb) trên Google Colab.
   * Chọn Runtime: `T4 GPU`.
   * Chạy tất cả các cell để kích hoạt FastAPI và Cloudflare Tunnel (`trycloudflare.com`).
   * Sao chép URL công khai (ví dụ: `https://xxxx.trycloudflare.com`).
2. **Cập nhật URL vào tệp `.env` cục bộ:**
   ```env
   INFERENCE_MODE=remote
   REMOTE_GPU_URL=https://xxxx.trycloudflare.com
   ```
3. **Chạy script kiểm định toàn trình:**
   ```bash
   python scripts/run_final_test_evaluation.py
   ```
   * Toàn bộ 120 cặp trên `test.csv` sẽ được gửi lên GPU Tesla T4 để mã hóa và chấm điểm trong ~49.89 giây. Kết quả báo cáo sẽ được cập nhật mới tại [`docs/final-test-benchmark-report.md`](file:///c:/Projects/UGTM-Plagiarism-AI/docs/final-test-benchmark-report.md).

---

### Phương thức 3: Kiểm thử Trên Kho Tài liệu Mẫu Chia sẻ Công khai
Đối với trường hợp thẩm định nhanh mà không sử dụng toàn bộ corpus khóa luận gốc:

* Sử dụng tài liệu mẫu tại thư mục [`data/sample_shareable_corpus/`](file:///c:/Projects/UGTM-Plagiarism-AI/data/sample_shareable_corpus/) gồm 5 tài liệu mô phỏng (.txt) và tệp `corpus_manifest.sample.csv`.
* Chạy thử nghiệm phân tích block và trích xuất chunk:
  ```bash
  python -c "
  from pathlib import Path
  from app.preprocessing import preprocess_document
  text = Path('data/sample_shareable_corpus/suspect_01_verbatim_reuse.txt').read_text(encoding='utf-8')
  chunks, stats = preprocess_document(text, 'suspect_01', 'sample_family', strategy='paragraph_boundary')
  print(f'Thành công! Trích xuất được {len(chunks)} chunks, khối raw: {stats.raw_blocks}')
  "
  ```

---

## 5. Chạy Kiểm Thử Tự Động Toàn Diện (Automated Test Verification)

Hệ thống cung cấp bộ kiểm thử tự động để kiểm chứng tính toàn vẹn của quá trình đóng gói và tái lập:

```bash
# Kiểm tra riêng suite đóng gói tái lập:
pytest tests/test_reproducibility.py -v

# Kiểm tra bộ kiểm thử đánh giá cuối cùng:
pytest tests/test_final_evaluation.py -v

# Chạy toàn bộ các bộ kiểm thử cốt lõi AI:
pytest tests/test_final_evaluation.py tests/test_evaluator.py tests/test_classifier.py tests/test_classifier_calibration.py tests/test_ablation_study.py tests/test_alignment.py tests/test_evidence_workbench.py tests/test_ugtm_contract_and_rbac.py tests/test_observability_and_restore.py
```

