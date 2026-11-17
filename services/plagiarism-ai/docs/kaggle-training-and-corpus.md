# Kaggle training và xử lý corpus đối sánh

## Quy tắc dữ liệu trước khi bắt đầu

Ảnh chụp danh sách báo cáo có tên/mã số sinh viên. Xem toàn bộ PDF/DOCX và mọi đoạn trích từ chúng là dữ liệu nội bộ. Chỉ sử dụng chúng khi Khoa/Thư viện/chủ sở hữu đã cho phép đúng các mục đích: lập chỉ mục, lưu text chunks/embeddings, hiển thị evidence cho giảng viên, thời hạn lưu và vị trí xử lý dữ liệu.

| Loại dữ liệu | Vị trí được khuyến nghị | Không được làm mặc định |
|---|---|---|
| PDF/DOCX khóa luận đầy đủ | Kho nội bộ được kiểm soát, Docker volume/server của nhóm | Upload lên Kaggle, GitHub, chat hoặc kho public |
| `pairs.csv` có đoạn trích | Kaggle Dataset **Private** chỉ khi quyền dùng dữ liệu cho cloud đã rõ | Chia sẻ public hoặc mời cộng tác viên không được ủy quyền |
| Code train + notebook | Repo và Kaggle Dataset Private | Kèm `.env`, corpus, artifact hoặc mapping định danh |
| `.joblib` + manifest | `artifacts/` nội bộ, hash đã kiểm tra | Commit Git hoặc dùng artifact không rõ nguồn |

Kaggle tách quyền của Notebook và Dataset; cả hai phải được để Private và mời riêng đúng cộng tác viên. Tham khảo [Kaggle Notebooks](https://www.kaggle.com/docs/notebooks) và [Kaggle Datasets](https://www.kaggle.com/docs/datasets).

## Phần A — Chuẩn bị corpus đối sánh nội bộ

1. Giữ bản gốc trong một thư mục bảo mật ngoài Git, ví dụ `D:\UGTM-secure\approved-theses\`. Không đổi tên hay sửa bản gốc.
2. Tạo một staging directory và manifest có tên ẩn danh. Script chỉ copy những PDF/DOCX có text layer hợp lệ, không xóa file gốc và không ghi tên file gốc vào manifest.

   ```powershell
   .\.venv\Scripts\python.exe -m scripts.prepare_corpus_manifest `
     --input-dir D:\UGTM-secure\approved-theses-1 `
     --input-dir D:\UGTM-secure\approved-theses-2 `
     --staging-dir .\corpus_staging `
     --manifest-out .\data\corpus_manifest.csv `
     --corpus-id uit-library-v1 `
     --version 1
   ```

   Mỗi source có key ổn định dạng `kltn-<16-hex-of-sha256>`. Các file `ready` được copy thành tên ẩn danh trong `corpus_staging/`; `needs_ocr`, `needs_review` và `duplicate` không được upload.

3. Kiểm tra `data/corpus_manifest.csv` trước. Đặc biệt xử lý PDF scan (`needs_ocr`), file quá 20 MB, file lỗi và trùng hash. Không sửa `source_key` sau khi đã index.
4. Dựng database/Redis/API/worker trên hạ tầng được phép. Thử **một** file với `--dry-run --limit 1`, sau đó index thật và polling:

   ```powershell
   $env:AI_API_KEY = '<API-key-của-môi-trường-nội-bộ>'
   .\.venv\Scripts\python.exe -m scripts.submit_corpus `
     --manifest .\data\corpus_manifest.csv `
     --staging-dir .\corpus_staging `
     --limit 1 `
     --dry-run

   .\.venv\Scripts\python.exe -m scripts.submit_corpus `
     --manifest .\data\corpus_manifest.csv `
     --staging-dir .\corpus_staging `
     --limit 1 `
     --poll
   ```

5. Khi source thử nghiệm hoàn tất và report scan mẫu hợp lý, bỏ `--limit 1` để index phần còn lại. Worker hiện cần GPU cho BGE-M3/reranker; không chạy indexing thật trên máy không có GPU.

`corpus_staging/` và `data/` đã được ignore bởi Git và Docker build context. Vẫn cần phân quyền filesystem phù hợp vì đây là dữ liệu nội bộ.

## Phần B — Tạo dữ liệu train

`training_data/pairs.csv` phải có năm cột:

```csv
pair_id,group_id,label,query_text,source_text
P001,source-family-a,1,"Đoạn nộp có tái sử dụng nội dung.","Đoạn nguồn đã được xác nhận."
N001,source-family-b,0,"Đoạn cùng chủ đề nhưng độc lập.","Đoạn từ nguồn khác."
```

- `label=1`: tái sử dụng nội dung trong case kiểm thử có kiểm soát; không đồng nghĩa kết luận đạo văn.
- `label=0`: độc lập; ưu tiên hard-negative cùng ngành/cùng thuật ngữ.
- `group_id`: toàn bộ biến thể của cùng gia đình nguồn phải nằm trong đúng một train/validation/test split.
- Bắt đầu từ 500–1.000 cặp đã được người kiểm tra xem xét. Không tạo label từ cosine score.

Nếu quyền dữ liệu chưa cho phép đưa đoạn trích lên Kaggle, chỉ dùng đoạn do nhóm tự tạo/được phép cho môi trường cloud; chưa bật classifier trên corpus thật.

## Phần C — Tạo Dataset code cho Kaggle

Script dưới đây tạo code-only bundle; nó không copy corpus, `training_data/`, `.env` hay artifacts:

```powershell
.\.venv\Scripts\python.exe -m scripts.export_kaggle_training_bundle `
  --output .\kaggle_bundle
```

Trên Kaggle, tạo **hai Dataset Private**:

1. `ugtm-plagiarism-training-code`: upload toàn bộ nội dung `kaggle_bundle/`.
2. `ugtm-plagiarism-training-pairs`: chỉ upload `pairs.csv` đã được phê duyệt.

Sau đó tạo một Notebook Private, upload [kaggle_train_classifier.ipynb](../kaggle/kaggle_train_classifier.ipynb), thêm hai Dataset trên ở phần **Input**, và đổi hai đường dẫn `CODE_DATASET_DIR`/`PAIRS_DATASET_DIR` nếu slug khác. Trong Settings, bật **Accelerator → GPU**; nếu cần tải package/model lần đầu, bật Internet trong lúc chạy và lưu notebook version sau khi hoàn tất.

## Phần D — Nhận artifact về nội bộ

Notebook tạo hai file ở `/kaggle/working/ugtm_classifier_artifacts/`:

- `suspicious_passage_classifier.joblib`
- `suspicious_passage_classifier.manifest.json`

Tải cả hai file, đối chiếu SHA-256 mà notebook in ra và đặt chúng vào `artifacts/` của hạ tầng nội bộ. Chỉ sau khi review metrics test holdout, cập nhật `.env`:

```dotenv
CLASSIFIER_ENABLED=true
CLASSIFIER_PATH=/artifacts/suspicious_passage_classifier.joblib
```

Restart worker. Thiếu artifact, sai feature schema hoặc base model mismatch phải làm job failed; không tự động fallback để tránh che giấu cấu hình sai.
