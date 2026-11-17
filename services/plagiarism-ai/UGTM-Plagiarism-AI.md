# UGTM-Plagiarism-AI — Hướng dẫn cập nhật source code: Hybrid AI Pipeline + Custom Classifier (v2)

> **Mục đích (QUYẾT ĐỊNH MỚI — 19/10/2026):** Triển khai **kết hợp cả hai hướng**: (1) **Hybrid Lexical + Semantic Retrieval → Reranking → Passage Alignment → Evidence Report**, (2) **Custom Classifier do nhóm tự huấn luyện** để chấm điểm *ứng viên tái sử dụng nội dung* dựa trên nhiều tín hiệu. Model này là phần tăng cường; **không tự kết luận sinh viên đạo văn**. Đây là hướng **tính năng sản phẩm** của đồ án SE310, không phải dự án nghiên cứu khoa học độc lập.
>
> **Quy ước áp dụng tài liệu:** Đây là **bản v2 tích hợp**, thay thế hướng chỉ có pretrained model ở bản cũ. Các khối mã bên dưới là source mẫu để tạo/cập nhật **repo AI riêng**, không phải PR đã áp dụng lên GitHub. **Giai đoạn A** chạy pipeline cơ bản; **Giai đoạn B** có dữ liệu gán nhãn thì train classifier và bật bằng feature flag. Dùng cùng một hợp đồng API với .NET trong cả hai giai đoạn.
>
> **Giả định kỹ thuật (có thể thay đổi):** Tài liệu giả định có GPU NVIDIA/PyTorch CUDA khi chạy BGE-M3 và reranker; **hiện nhóm CHƯA có GPU được cấp**, vì vậy chưa được coi GPU là hạ tầng đang sẵn sàng. Nhóm có thể phát triển parser, API, Postgres, feature logic, classifier nhỏ và unit tests trên CPU; GPU là mục tiêu cho inference nặng. Nhóm tạo **repo Python riêng**; hệ thống UGTM tiếp tục sử dụng **ASP.NET Core (.NET 10), React 19, PostgreSQL**, kiến trúc Modular Monolith/DDD; dự định xin phép thư viện UIT sử dụng khoảng **20–50 khóa luận** làm nguồn đối chiếu.
>
> **Tình trạng kiểm chứng 19/10/2026:** Kết nối GitHub tới `karasuma-2401/UGTM` vẫn trả về 404, nên **chưa review được mã nguồn thực tế**. Các tên class/module tích hợp .NET trong tài liệu là **đề xuất**, không phải tên hiện có của repo. Những đặc điểm .NET/DDD/PDF-DOCX căn cứ vào báo cáo phân tích thiết kế của nhóm. Code bên dưới là **reference implementation hướng MVP**, chưa được chạy end-to-end trên GPU, Docker hay repo của nhóm; cần kiểm thử tại môi trường thật trước khi dùng chính thức.

## 0. Quyết định kỹ thuật và phạm vi

### Chốt kiến trúc

- **Repo 1** — `karasuma-2401/UGTM`: giao diện, đăng nhập, phân quyền, quy trình nộp báo cáo, giữ file gốc, liên kết kết quả AI với bản nộp, cho GVHD/GVPB xem báo cáo.
- **Repo 2** — `UGTM-Plagiarism-AI`: dịch vụ Python **FastAPI**, **Celery worker** (GPU khi được cấp), trích xuất PDF/DOCX, tìm kiếm lexical + semantic, reranking, phân nhóm đoạn và trả bằng chứng; có **custom classifier versioned** tùy chọn.
- **Database:** PostgreSQL có `pgvector`, dùng schema `integrity`; bản MVP có thể sử dụng **database riêng** `ugtm_ai` để tránh phụ thuộc bảng nghiệp vụ của UGTM. Không chia sẻ Entity Framework DbContext hoặc domain entity qua hai dịch vụ.
- **Queue:** Redis + Celery, không chạy AI trực tiếp trong HTTP request của sinh viên.
- **Base models (không train lại):** `BAAI/bge-m3` (**dense 1024 chiều**) và `BAAI/bge-reranker-v2-m3` để encode + rerank; dự kiến inference GPU.
- **Model nhóm tự training (giai đoạn B):** `scikit-learn` Logistic Regression trên **5 tín hiệu ổn định**, huấn luyện CPU sau khi đã dùng model nền trích đặc trưng và tự gán nhãn dữ liệu. **Chưa cần fine-tune Transformer.** Có `CLASSIFIER_ENABLED` để bật/tắt; thiếu/mismatch artifact thì fail rõ ràng, không lặng lẽ đổi chiến lược.
- **Luồng quyết định:** pipeline trả các *candidate pair*. Classifier chỉ xếp hạng/cắm cờ `review_required`, không quyết định kỷ luật; một số rule an toàn phục vụ demo có thể hỗ trợ cảnh báo sao chép gần nguyên văn.
- **Lexical:** PostgreSQL `pg_trgm` trên các đoạn văn, đủ cho corpus nhỏ. Sau này mới cân nhắc BM25/Elasticsearch/OpenSearch.
- **Quyền:** sinh viên **không** gọi AI trực tiếp; ASP.NET Core gọi API nội bộ có xác thực service-to-service. Báo cáo là gợi ý để giảng viên kiểm tra.

### MVP phải có

1. Admin/Khoa nhập khóa luận nguồn `.pdf`/`.docx` vào corpus `uit-library-v1` qua API nội bộ.
2. Tách văn bản thành chunks, gắn `source_id`, phiên bản, trang, khoảng ký tự; tạo embedding và lưu pgvector.
3. Sinh viên nộp bài trên UGTM; .NET tạo job quét và trả trạng thái `queued` ngay.
4. Worker thực hiện lexical search + dense vector search + RRF + reranker.
5. Báo cáo gồm **đoạn trong bài nộp**, **đoạn nguồn**, tài liệu, trang, điểm lexical/dense/reranker, cờ `review_required`.
6. GVHD/GVPB có màn hình xem và nhận xét; AI không tự cấm nộp, không tự đánh rớt KLTN.
7. Khi có tập cặp đoạn **đã được người kiểm tra gán nhãn**: chạy `training/train_classifier.py`, xuất `artifacts/suspicious_passage_classifier.joblib` và manifest, chạy lại test rồi bật classifier bằng cấu hình.
8. Report phải có `detector_mode`, `classifier_version` (hoặc `null`), `decision_source` và các tín hiệu; tính năng **Passage Alignment** nhóm các hit liền kề để giảng viên xem dễ hơn.

### Những thứ **không** tuyên bố đã có trong code MVP

- OCR PDF scan; PDF nhiều cột/bảng/hình ảnh phức tạp; đọc mã nguồn và biểu đồ; phát hiện đạo văn ý tưởng.
- Phân tích trích dẫn đáng tin cậy ở cấp câu; phân loại “nội dung AI tạo ra”; tìm kiếm trên toàn Internet. **Trong v2 chỉ ghi nhận metadata/trích dẫn nếu có, chưa triển khai citation-aware filtering tự động.**
- Highlight theo **tọa độ PDF**; code chỉ có **offset trên văn bản đã trích xuất** và `page_number` PDF. DOCX **không** có trang ổn định.
- Độ chính xác hay ngưỡng “đạo văn 30%”; muốn có cần dữ liệu gán nhãn và hiệu chỉnh.
- Vận hành production: object storage, retry/dead-letter, metrics, migration/backup đầy đủ và bảo vệ dữ liệu nâng cao được liệt kê ở cuối tài liệu.

## 1. Kiến trúc tổng thể

```text
                 REPOSITORY: UGTM (.NET + React)
  Sinh viên --> React --> ASP.NET Core --> UGTM file storage
                            |
                  SubmissionAccepted event
                            |
                       IntegrityAdapter
                            | multipart + X-API-Key
                            v
              REPOSITORY: UGTM-Plagiarism-AI
  +-------------------------------------------------------+
  |  FastAPI (chỉ điều phối, không load AI model)         |
  |    POST corpus/documents --> job index                 |
  |    POST scans -----------> job scan                    |
  |    GET jobs/{id}, reports/{id}                         |
  +---------------------------+---------------------------+
                              |
                          Celery / Redis
                              |
                              v
  +-------------------------------------------------------+
  | GPU Worker (concurrency=1 lúc đầu)                    |
  | extract -> chunk -> BGE-M3 -> lexical/vector retrieve |
  |                         -> RRF -> BGE reranker        |
  |                         -> evidence/report            |
  +---------------------------+---------------------------+
                              |
                PostgreSQL + pgvector + pg_trgm
                  sources / chunks / jobs
```

**Ba pipeline độc lập, có thứ tự phụ thuộc:**

- **Indexing (ít chạy):** khóa luận nguồn → text extraction → chunking → embedding → lưu vector + metadata → bật `is_active` cho phiên bản mới.
- **Scanning (mỗi bản nộp):** báo cáo mới → extraction → chunking → embedding → dense & lexical retrieve → RRF → rerank → tính 5 features → **classifier nếu bật / heuristic nếu tắt** → passage grouping → báo cáo.
- **Offline training (không chạy lúc upload):** thu thập các cặp đoạn đã có nhãn `reused=1` hoặc `independent=0` → tạo 5 features bằng **chính hàm inference** → chia train/validation/test theo nhóm nguồn → huấn luyện classifier nhỏ trên CPU → chọn ngưỡng từ validation → kiểm thử trên test → xuất artifact và manifest. Khi không có đủ nhãn, giữ `CLASSIFIER_ENABLED=false`.

## 2. Cách hoạt động của các thuật toán

**Chunking:** MVP tách theo từng trang PDF, dùng cửa sổ **90 từ trắng / chồng lấn 20 từ** (đây là *word windows*, **không phải** 90 model tokens). `max_length=512` trong BGE-M3 để giảm VRAM. Tăng chất lượng bằng cách tách theo đoạn/mục và tokenizer thực ở giai đoạn sau.

**Dense similarity:** BGE-M3 sinh vector; Postgres tìm nearest neighbors với cosine distance (`<=>`). `dense_score = 1 - cosine_distance`.

**Lexical similarity:** `pg_trgm.similarity(content, query)` giúp bắt sao chép hoặc thay đổi nhỏ; không đủ bắt paraphrase sâu.

**Fusion:** Reciprocal Rank Fusion (RRF) theo thứ hạng, không cộng trực tiếp hai thang điểm vốn không tương đương:

\[
\mathrm{RRF}(d)=\sum_{r\in\{\mathrm{dense,lexical}\}}\frac{1}{60+\mathrm{rank}_r(d)}.
\]

**Reranking:** `bge-reranker-v2-m3` chấm từng cặp (đoạn nộp, đoạn nguồn). `normalize=True` trả về score sau sigmoid **không phải xác suất đạo văn**. Chỉ rerank danh sách ngắn để giảm chi phí GPU.

**Cờ cần rà soát khi chưa bật classifier:** ví dụ MVP dùng `lexical >= 0.65` **hoặc** `(dense >= 0.68 và reranker >= 0.67)`. Đây **chỉ là ngưỡng khởi tạo để demo**, không phải ngưỡng khoa học hay quy chế UIT: phải điều chỉnh sau khi xem mẫu thật. Không tự động kết luận vi phạm.

**Passage Alignment (v2):** sau khi có cặp đã gắn cờ, nhóm các hit trên cùng một nguồn khi thứ tự `submission_chunk_no` và `source_chunk_no` liên tiếp (và ngữ cảnh trang hợp lý). Trả `passage_group_id` để FE gộp hiển thị. Đây là **nhóm đoạn có tương đồng**, không phải bằng chứng độc lập của hành vi đạo văn.

**Classifier (v2):** 5 feature `semantic_cosine`, `reranker_score`, `char_trigram_dice`, `token_jaccard`, `length_ratio`. Hai feature cuối và `char_trigram_dice` tính tại Python **giống hệt offline và online**; `semantic_cosine` lấy từ pgvector cosine distance (sau khi truy xuất candidates); `reranker_score` lấy từ model. `review_required` sử dụng `classifier_score >= threshold_from_manifest` hoặc rule gần nguyên văn. `classifier_score` là **điểm của bộ phân loại đã train trên dữ liệu nhóm** chứ không phải xác suất đạo văn thực tế.

**Không tính phần trăm đạo văn trong MVP.** Nếu thêm “tỷ lệ vùng văn bản trùng với kho”, phải gộp khoảng offset (tránh đếm double do chunk chồng lấn), xác định mẫu số là phần văn bản đủ điều kiện và giải thích phụ thuộc vào tập tài liệu đã index.

## 3. Tạo repo riêng

Tên gợi ý: `UGTM-Plagiarism-AI` (private repository nếu dùng dữ liệu trường).

```text
UGTM-Plagiarism-AI/
├── app/
│   ├── __init__.py
│   ├── config.py
│   ├── db.py
│   ├── documents.py
│   ├── models.py
│   ├── search.py
│   ├── features.py                # feature dùng chung train/inference
│   ├── classifier.py              # artifact + inference + validation
│   ├── alignment.py               # gộp cặp đoạn liền kề
│   ├── celery_app.py
│   ├── tasks.py
│   └── main.py
├── sql/
│   └── 001_init.sql
├── training/
│   └── train_classifier.py       # training offline theo source groups
├── training_data/                # KHÔNG commit, chứa pairs.csv có nhãn
├── artifacts/                    # KHÔNG commit, model + manifest đã kiểm thử
├── tests/
│   ├── test_documents.py
│   ├── test_features.py
│   └── test_alignment.py
├── requirements.txt
├── Dockerfile
├── compose.yaml
├── .env.example
├── .gitignore
└── README.md
```

**Lưu ý:** Tất cả nội dung các file mẫu kể trên đều được ghi **trong chính tài liệu này**. Khi lập repo, tạo đúng đường dẫn và copy code từ các khối tương ứng. Không commit PDF/DOCX khóa luận, model weights, `.env`, secrets, job output hoặc vector data lên GitHub.

### `.gitignore`

```gitignore
.env
.venv/
__pycache__/
.pytest_cache/
*.pyc
models/
artifacts/
training_data/
training_runs/
*.joblib
data/
*.pdf
*.docx
*.sql.gz
*.log
```

`*.pdf` và `*.docx` ngăn commit nhầm dữ liệu thật; nếu muốn lưu fixtures test công khai, phải tạo ngoại lệ chính xác cho thư mục fixtures đã được phép sử dụng.

## 4. Cài đặt Docker/GPU

**Yêu cầu cho giai đoạn inference GPU (chưa cần ở giai đoạn phát triển CPU):** Linux server hoặc WSL2 có NVIDIA driver, Docker Engine/Compose Plugin và NVIDIA Container Toolkit; `nvidia-smi` hoạt động; `docker compose ...` có thể phân bổ GPU. Kiểm thử riêng GPU trước:

```bash
nvidia-smi
# Sau khi dựng worker:
docker compose exec worker python -c "import torch; print(torch.cuda.is_available()); print(torch.cuda.get_device_name(0))"
```

### `requirements.txt`

```txt
fastapi>=0.115,<1
uvicorn[standard]>=0.30,<1
python-multipart>=0.0.12,<1
pydantic-settings>=2.5,<3
psycopg[binary]>=3.2,<4
celery[redis]>=5.4,<6
PyMuPDF>=1.24,<2
python-docx>=1.1,<2
FlagEmbedding>=1.3,<2
numpy>=1.26,<3
scikit-learn>=1.5,<2
joblib>=1.4,<2
pytest>=8,<10
```

> PyTorch/CUDA có sẵn từ image nền. Các khoảng phiên bản là xuất phát điểm; **sau khi chạy thành công cần lock phiên bản/sha image** để tái lập được môi trường. Cập nhật phiên bản thư viện có thể làm thay đổi API hoặc phụ thuộc (`FlagEmbedding`/`transformers`).

### `Dockerfile`

```dockerfile
FROM pytorch/pytorch:2.5.1-cuda12.4-cudnn9-runtime
WORKDIR /service
ENV PYTHONUNBUFFERED=1 PYTHONDONTWRITEBYTECODE=1
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 && rm -rf /var/lib/apt/lists/*
COPY requirements.txt .
RUN python -m pip install --no-cache-dir -r requirements.txt
COPY app ./app
COPY training ./training
COPY tests ./tests
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

### `.env.example`

```dotenv
POSTGRES_PASSWORD=replace-with-strong-password
AI_API_KEY=replace-with-long-random-private-key
MAX_UPLOAD_MB=20
CLASSIFIER_ENABLED=false
CLASSIFIER_PATH=/artifacts/suspicious_passage_classifier.joblib
```

Sau đó chạy `cp .env.example .env` và sửa bằng secret thật. Với việc triển khai máy chủ, cấp `.env` qua secret manager thay vì git.

### `compose.yaml`

```yaml
services:
  db:
    image: pgvector/pgvector:pg17
    environment:
      POSTGRES_DB: ugtm_ai
      POSTGRES_USER: ugtm_ai
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
      - ./sql/001_init.sql:/docker-entrypoint-initdb.d/001_init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ugtm_ai -d ugtm_ai"]
      interval: 5s
      retries: 20

  redis:
    image: redis:7-alpine
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      retries: 20

  api:
    build: .
    command: uvicorn app.main:app --host 0.0.0.0 --port 8000
    ports:
      - "127.0.0.1:8000:8000"
    environment: &ai_env
      POSTGRES_DSN: postgresql://ugtm_ai:${POSTGRES_PASSWORD}@db:5432/ugtm_ai
      REDIS_URL: redis://redis:6379/0
      API_KEY: ${AI_API_KEY}
      MAX_UPLOAD_MB: ${MAX_UPLOAD_MB:-20}
      DATA_DIR: /data
      HF_HOME: /models
      CLASSIFIER_ENABLED: ${CLASSIFIER_ENABLED:-false}
      CLASSIFIER_PATH: ${CLASSIFIER_PATH:-/artifacts/suspicious_passage_classifier.joblib}
    volumes:
      - uploads:/data
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_healthy

  worker:
    build: .
    command: celery -A app.celery_app.celery_app worker --loglevel=INFO --concurrency=1 --pool=solo
    environment: *ai_env
    volumes:
      - uploads:/data
      - models:/models
      - ./artifacts:/artifacts:ro
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_healthy
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1
              capabilities: [gpu]

volumes:
  pgdata:
  uploads:
  models:
```

**Vận hành:** `api` chỉ CPU; `worker` độc quyền một GPU để tránh hai worker cùng tải hai model gây OOM. Vì Redis/DB không có `ports`, chúng không được expose ra host. Với UGTM chạy trên cùng Docker network, gọi tên service `http://api:8000`; nếu .NET chạy ngoài Docker, dùng endpoint internal có TLS/reverse proxy, không mở public trực tiếp port 8000.

**Lưu ý Postgres init script:** `docker-entrypoint-initdb.d` chỉ tự chạy khi volume `pgdata` **mới hoàn toàn**. Nếu database đã tồn tại, chạy migration SQL thủ công; không xóa volume có dữ liệu thật chỉ để cập nhật schema.

## 5. Database — `sql/001_init.sql`

```sql
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE SCHEMA IF NOT EXISTS integrity;

CREATE TABLE IF NOT EXISTS integrity.jobs (
    id UUID PRIMARY KEY,
    job_type TEXT NOT NULL CHECK (job_type IN ('index','scan')),
    corpus_id TEXT NOT NULL,
    document_key TEXT NOT NULL,
    version INTEGER NOT NULL CHECK (version > 0),
    filename TEXT NOT NULL,
    file_path TEXT NOT NULL,
    file_sha256 TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'queued'
        CHECK (status IN ('queued','processing','completed','failed')),
    report JSONB,
    error_code TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE(job_type, corpus_id, document_key, version)
);

CREATE TABLE IF NOT EXISTS integrity.sources (
    id BIGSERIAL PRIMARY KEY,
    corpus_id TEXT NOT NULL,
    source_key TEXT NOT NULL,
    version INTEGER NOT NULL CHECK (version > 0),
    filename TEXT NOT NULL,
    file_sha256 TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT FALSE,
    indexed_at TIMESTAMPTZ,
    UNIQUE(corpus_id, source_key, version)
);

CREATE TABLE IF NOT EXISTS integrity.chunks (
    id BIGSERIAL PRIMARY KEY,
    source_id BIGINT NOT NULL REFERENCES integrity.sources(id) ON DELETE CASCADE,
    chunk_no INTEGER NOT NULL,
    page_number INTEGER,
    start_offset INTEGER NOT NULL,
    end_offset INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding VECTOR(1024) NOT NULL,
    UNIQUE(source_id, chunk_no)
);

CREATE INDEX IF NOT EXISTS idx_source_active
  ON integrity.sources (corpus_id, is_active);
CREATE INDEX IF NOT EXISTS idx_chunks_source ON integrity.chunks(source_id);
CREATE INDEX IF NOT EXISTS idx_chunks_trgm
  ON integrity.chunks USING GIN (content gin_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_jobs_created
  ON integrity.jobs (created_at DESC);

-- Chỉ tạo HNSW sau khi đo truy vấn chính xác thấy chậm.
-- CREATE INDEX idx_chunks_hnsw ON integrity.chunks
--   USING hnsw (embedding vector_cosine_ops);
```

**Giải thích:** `sources` và `chunks` giữ **chỉ các nguồn được thư viện cấp quyền**; mỗi nguồn có version và `is_active`. Khi version mới index thành công mới tắt version cũ trong **cùng một transaction**. Dữ liệu report lưu trong `jobs.report`. Với kho 20–50 tài liệu, truy vấn vector exact trước là lựa chọn hợp lý; HNSW là approximate và có thể ảnh hưởng Recall.

## 6. Code Python — cấu hình và database

### `app/__init__.py`

Tạo file rỗng.

### `app/config.py`

```python
from pathlib import Path
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(extra="ignore")
    postgres_dsn: str
    redis_url: str
    api_key: str
    max_upload_mb: int = 20
    data_dir: Path = Path("/data")
    embedding_model: str = "BAAI/bge-m3"
    reranker_model: str = "BAAI/bge-reranker-v2-m3"
    classifier_enabled: bool = False
    classifier_path: Path = Path("/artifacts/suspicious_passage_classifier.joblib")


settings = Settings()
```

### `app/db.py`

```python
import psycopg
from psycopg.rows import dict_row
from app.config import settings


def connect():
    return psycopg.connect(settings.postgres_dsn, row_factory=dict_row)


def set_status(job_id: str, status: str, error_code: str | None = None):
    with connect() as con:
        con.execute("""
            UPDATE integrity.jobs
            SET status = %s, error_code = %s, updated_at = now()
            WHERE id = %s::uuid
        """, (status, error_code, job_id))


def get_job(job_id: str):
    with connect() as con:
        return con.execute("""
            SELECT * FROM integrity.jobs WHERE id = %s::uuid
        """, (job_id,)).fetchone()
```

## 7. Code Python — trích xuất và chia đoạn

### `app/documents.py`

```python
import re
from dataclasses import dataclass
from pathlib import Path

import pymupdf
from docx import Document


@dataclass
class TextPage:
    page_number: int | None
    text: str


@dataclass
class Chunk:
    chunk_no: int
    page_number: int | None
    start_offset: int
    end_offset: int
    text: str


def normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def extract_pages(path: str) -> list[TextPage]:
    file = Path(path)
    if file.suffix.lower() == ".pdf":
        with pymupdf.open(str(file)) as pdf:
            if len(pdf) > 250:
                raise ValueError("TOO_MANY_PAGES")
            return [
                TextPage(i + 1, normalize(page.get_text("text", sort=True)))
                for i, page in enumerate(pdf)
            ]
    if file.suffix.lower() == ".docx":
        doc = Document(str(file))
        # DOCX không có số trang cố định. MVP chỉ lấy paragraphs.
        paragraphs = [p.text for p in doc.paragraphs if p.text.strip()]
        if sum(len(p) for p in paragraphs) > 2_000_000:
            raise ValueError("DOCUMENT_TOO_LONG")
        return [TextPage(None, normalize("\n".join(paragraphs)))]
    raise ValueError("Only PDF and DOCX are supported")


def make_chunks(pages: list[TextPage], size: int = 90,
                overlap: int = 20, min_words: int = 8) -> list[Chunk]:
    if not (0 <= overlap < size):
        raise ValueError("overlap must be smaller than size")
    result: list[Chunk] = []
    chunk_no = 0
    for page in pages:
        # Offset tính trên văn bản đã normalize, không phải PDF bounding box.
        spans = list(re.finditer(r"\S+", page.text))
        for first in range(0, len(spans), size - overlap):
            last = min(first + size, len(spans))
            if last - first < min_words:
                break
            start = spans[first].start()
            end = spans[last - 1].end()
            result.append(Chunk(
                chunk_no=chunk_no,
                page_number=page.page_number,
                start_offset=start,
                end_offset=end,
                text=page.text[start:end],
            ))
            chunk_no += 1
            if last == len(spans):
                break
    return result
```

**Giới hạn extraction:** PDF scan chỉ chứa ảnh sẽ trả về văn bản rỗng hoặc không đầy đủ; cần báo `EMPTY_TEXT` và yêu cầu file có text layer. DOCX có bảng, hình, footnote phức tạp cần mở rộng parser. Muốn highlight theo vị trí PDF phải lưu `bbox` ở bước `page.get_text('dict')`, đối chiếu token-to-box và xử lý dòng/cột/ligature; *không* được dùng `start_offset` của normalized text làm tọa độ PDF.

## 8. Code Python — chạy model trên GPU

### `app/models.py`

```python
from functools import lru_cache

import numpy as np
import torch
from FlagEmbedding import BGEM3FlagModel, FlagReranker
from app.config import settings


def assert_gpu():
    if not torch.cuda.is_available():
        raise RuntimeError("GPU CUDA is required by this configuration")


@lru_cache(maxsize=1)
def embedder():
    assert_gpu()
    return BGEM3FlagModel(
        settings.embedding_model,
        use_fp16=True,
        devices=["cuda:0"],
    )


@lru_cache(maxsize=1)
def reranker():
    assert_gpu()
    return FlagReranker(
        settings.reranker_model,
        use_fp16=True,
        devices=["cuda:0"],
    )


def encode_texts(texts: list[str], batch_size: int = 8) -> np.ndarray:
    if not texts:
        return np.empty((0, 1024), dtype=np.float32)
    data = embedder().encode(
        texts,
        batch_size=batch_size,
        max_length=512,
        return_dense=True,
        return_sparse=False,
        return_colbert_vecs=False,
    )
    return np.asarray(data["dense_vecs"], dtype=np.float32)


def rerank_pairs(pairs: list[list[str]], batch_size: int = 8) -> list[float]:
    if not pairs:
        return []
    scores = reranker().compute_score(
        pairs, batch_size=batch_size, normalize=True,
        max_length=512,
    )
    if isinstance(scores, (float, int)):
        return [float(scores)]
    return [float(v) for v in scores]
```

**GPU (khi nhóm có GPU):** Model tải một lần khi worker gọi đầu tiên. Có thể mất vài phút trong lần đầu (download trọng số). Nếu VRAM không đủ, giảm `batch_size` 8 → 4 → 2, giảm số cặp rerank, hoặc chia embedding/rerank thành hai worker khác GPU. Sau khi ổn định cần lưu model snapshot revision và cache weights. GPU NVIDIA không đồng nghĩa chắc chắn có đủ VRAM cho cả hai model — phải đo bằng `nvidia-smi`.

## 9. Code Python — truy tìm ứng viên và hợp nhất RRF

### `app/search.py`

```python
import json
from app.db import connect


def vector_literal(vector) -> str:
    # Cast ::vector ở SQL; không dựng câu SQL bằng nối chuỗi.
    return json.dumps([float(x) for x in vector], separators=(",", ":"))


def retrieve(query_text: str, query_vector, corpus_id: str,
             top_k: int = 6, fused_k: int = 5) -> list[dict]:
    vector = vector_literal(query_vector)
    with connect() as con:
        dense = con.execute("""
            SELECT c.id, c.content, c.page_number,
                   c.start_offset, c.end_offset,
                   s.source_key, s.version, s.filename,
                   1 - (c.embedding <=> %s::vector) AS dense_score
            FROM integrity.chunks c
            JOIN integrity.sources s ON s.id = c.source_id
            WHERE s.corpus_id = %s AND s.is_active = TRUE
            ORDER BY c.embedding <=> %s::vector
            LIMIT %s
        """, (vector, corpus_id, vector, top_k)).fetchall()

        lexical = con.execute("""
            SELECT c.id, c.content, c.page_number,
                   c.start_offset, c.end_offset,
                   s.source_key, s.version, s.filename,
                   similarity(c.content, %s) AS lexical_score
            FROM integrity.chunks c
            JOIN integrity.sources s ON s.id = c.source_id
            WHERE s.corpus_id = %s AND s.is_active = TRUE
            ORDER BY similarity(c.content, %s) DESC
            LIMIT %s
        """, (query_text, corpus_id, query_text, top_k)).fetchall()

    merged: dict[int, dict] = {}
    for branch, rows in (("dense", dense), ("lexical", lexical)):
        for rank, row in enumerate(rows, start=1):
            item = merged.setdefault(row["id"], {
                "chunk_id": row["id"],
                "source_key": row["source_key"],
                "source_version": row["version"],
                "source_filename": row["filename"],
                "source_page": row["page_number"],
                "source_start": row["start_offset"],
                "source_end": row["end_offset"],
                "source_text": row["content"],
                "rrf_score": 0.0,
                "dense_score": None,
                "lexical_score": None,
            })
            item["rrf_score"] += 1.0 / (60 + rank)
            score_key = branch + "_score"
            item[score_key] = float(row[score_key])
    # Chốt score từ CẢ HAI phương pháp trên TỪNG ứng viên, không giữ NULL
    # chỉ vì candidate được tìm thấy bởi một nhánh. Điều này quan trọng
    # để vector feature lúc train và inference cùng một định nghĩa.
    selected = sorted(
        merged.values(), key=lambda x: x["rrf_score"], reverse=True
    )[:fused_k]
    if not selected:
        return []
    ids = [int(x["chunk_id"]) for x in selected]
    with connect() as con:
        scores = con.execute("""
            SELECT id,
                   1 - (embedding <=> %s::vector) AS dense_score,
                   similarity(content, %s) AS lexical_score,
                   chunk_no
            FROM integrity.chunks
            WHERE id = ANY(%s::bigint[])
        """, (vector, query_text, ids)).fetchall()
    score_by_id = {row["id"]: row for row in scores}
    for item in selected:
        row = score_by_id[item["chunk_id"]]
        item["dense_score"] = float(row["dense_score"])
        item["lexical_score"] = float(row["lexical_score"])
        item["source_chunk_no"] = int(row["chunk_no"])
    return selected
```

**Hiệu năng MVP:** đoạn trên thực hiện **hai câu truy vấn cho mỗi query chunk** (dense + trigram), dễ hiểu nhưng có thể tốn thời gian nếu quét hàng trăm chunk. Với corpus 20–50 khóa luận, hãy đo trước. Nếu chậm, tối ưu bằng batch retrieval, giới hạn những chương cần kiểm tra, cache candidates và tạo index phù hợp. Không nên tuyên bố API xử lý “trong vài giây” khi chưa benchmark.

## 9A. Code Python — Feature Engineering chung cho train và inference (MỚI V2)

### `app/features.py`

**Không lấy `pg_trgm.similarity` làm feature training**, vì offline dataset chưa chắc ở trong Postgres. Lấy điểm pg_trgm cho retrieval và UI; dùng hàm Python deterministic cho đặc trưng để bảo đảm `train == inference`.

```python
import re
from collections import Counter

FEATURE_SCHEMA = "candidate-features-v1"
FEATURE_NAMES = [
    "semantic_cosine",
    "reranker_score",
    "char_trigram_dice",
    "token_jaccard",
    "length_ratio",
]


def tokens(text: str) -> list[str]:
    return re.findall(r"\w+", text.casefold(), flags=re.UNICODE)


def char_trigrams(text: str) -> Counter:
    text = " ".join(tokens(text))
    if len(text) < 3:
        return Counter([text]) if text else Counter()
    return Counter(text[i:i + 3] for i in range(len(text) - 2))


def dice_similarity(a: str, b: str) -> float:
    x, y = char_trigrams(a), char_trigrams(b)
    denom = sum(x.values()) + sum(y.values())
    return (2 * sum((x & y).values()) / denom) if denom else 0.0


def build_features(query_text: str, source_text: str,
                   semantic_cosine: float, reranker_score: float) -> dict:
    a, b = set(tokens(query_text)), set(tokens(source_text))
    ratio = min(len(tokens(query_text)), len(tokens(source_text))) / max(
        len(tokens(query_text)), len(tokens(source_text)), 1
    )
    return {
        "semantic_cosine": float(semantic_cosine),
        "reranker_score": float(reranker_score),
        "char_trigram_dice": dice_similarity(query_text, source_text),
        "token_jaccard": len(a & b) / len(a | b) if a or b else 0.0,
        "length_ratio": float(ratio),
    }


def feature_vector(features: dict) -> list[float]:
    return [float(features[name]) for name in FEATURE_NAMES]
```

**Feature chưa đưa vào classifier v1:** `citation_present`, `section_similarity`, `passage_continuity`. Chúng rất hữu ích trong tương lai nhưng cần parser và nhãn chất lượng. **Không tự thêm feature lúc inference nếu artifact train không biết feature đó.**

## 9B. Code Python — Load model nhóm train + feature flag (MỚI V2)

### `app/classifier.py`

```python
import json
from functools import lru_cache
from pathlib import Path

import joblib
from app.config import settings
from app.features import FEATURE_NAMES, FEATURE_SCHEMA, feature_vector


@lru_cache(maxsize=1)
def load_classifier():
    # Chỉ dùng artifact do pipeline nội bộ tạo và đã được kiểm chứng.
    # joblib/pickle KHÔNG an toàn với file không đáng tin cậy.
    path = Path(settings.classifier_path)
    manifest_path = path.with_suffix(".manifest.json")
    if not path.is_file() or not manifest_path.is_file():
        raise RuntimeError("CLASSIFIER_ARTIFACT_MISSING")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if (manifest.get("feature_schema") != FEATURE_SCHEMA or
        manifest.get("feature_names") != FEATURE_NAMES):
        raise RuntimeError("CLASSIFIER_FEATURE_SCHEMA_MISMATCH")
    if (manifest.get("embedding_model") != settings.embedding_model or
        manifest.get("reranker_model") != settings.reranker_model):
        raise RuntimeError("CLASSIFIER_BASE_MODEL_MISMATCH")
    model = joblib.load(path)
    return model, manifest


def score_candidate(features: dict):
    if not settings.classifier_enabled:
        return None
    model, manifest = load_classifier()
    # predict_proba là điểm mô hình trên dữ liệu huấn luyện, KHÔNG phải
    # xác suất vi phạm quy chế hay đạo văn trong thực tế.
    score = float(model.predict_proba([feature_vector(features)])[0, 1])
    return {
        "score": score,
        "threshold": float(manifest["threshold"]),
        "version": str(manifest["model_version"]),
    }
```

**Feature flag:** mặc định `CLASSIFIER_ENABLED=false`. Với Docker Compose bind mount `./artifacts`, tạo thư mục bằng `mkdir -p artifacts training_data` trước khi chạy để tránh Docker tự tạo thư mục do root sở hữu. Sau khi train và kiểm thử: đưa cả `.joblib` và `.manifest.json` lên volume read-only cho worker; đặt `CLASSIFIER_ENABLED=true`; **restart worker** (model được cache). Nếu bật mà không có artifact hoặc schema không khớp, job phải lỗi rõ ràng — không tự động chuyển về heuristic mà che giấu lỗi.

## 9C. Code Python — Gộp findings liền kề (Passage Alignment, MỚI V2)

### `app/alignment.py`

```python
from collections import defaultdict


def group_adjacent_findings(findings: list[dict]) -> list[dict]:
    # Cùng source + cùng version, và chunk của 2 tài liệu cùng tiến tới.
    # Với chunk overlap, group chỉ là tiện ích hiển thị, chưa hợp nhất
    # chính xác khoảng ký tự toàn văn giữa các trang.
    by_source = defaultdict(list)
    for f in findings:
        by_source[(f["source_key"], f["source_version"])].append(f)

    next_id = 1
    for group in by_source.values():
        group.sort(key=lambda x: (x["submission_chunk_no"],
                                  x["source_chunk_no"]))
        previous = None
        for f in group:
            contiguous = (
                previous is not None
                and 0 <= f["submission_chunk_no"] - previous["submission_chunk_no"] <= 1
                and 0 <= f["source_chunk_no"] - previous["source_chunk_no"] <= 1
            )
            if contiguous:
                f["passage_group_id"] = previous["passage_group_id"]
            else:
                f["passage_group_id"] = f"PG-{next_id:04d}"
                next_id += 1
            previous = f
    return findings
```

> **Giới hạn:** Với cùng một query chunk có nhiều nguồn/candidate, hoặc những đoạn bị đảo vị trí mạnh, rule liền kề này chưa thay thế được thuật toán sequence alignment tối ưu. Để tránh group nhầm khi dữ liệu lớn, sau MVP có thể dùng dynamic programming và chặn khoảng trang/offset.

## 10. Code Python — Celery tasks indexing/scanning

### `app/celery_app.py`

```python
from celery import Celery
from app.config import settings

celery_app = Celery(
    "ugtm_plagiarism_ai",
    broker=settings.redis_url,
    backend=settings.redis_url,
    include=["app.tasks"],
)
```

### `app/tasks.py`

```python
import logging
from psycopg.types.json import Jsonb
from app.celery_app import celery_app
from app.config import settings
from app.db import connect, get_job, set_status
from app.documents import extract_pages, make_chunks
from app.models import encode_texts, rerank_pairs
from app.search import retrieve, vector_literal
from app.features import build_features
from app.classifier import score_candidate
from app.alignment import group_adjacent_findings

logger = logging.getLogger(__name__)


def parse_job(job):
    chunks = make_chunks(extract_pages(job["file_path"]))
    if not chunks:
        raise ValueError("EMPTY_TEXT")
    return chunks


@celery_app.task(name="app.tasks.index_source")
def index_source(job_id: str):
    set_status(job_id, "processing")
    try:
        job = get_job(job_id)
        chunks = parse_job(job)
        embeddings = encode_texts([x.text for x in chunks])
        with connect() as con:
            row = con.execute("""
                INSERT INTO integrity.sources
                  (corpus_id, source_key, version, filename, file_sha256)
                VALUES (%s,%s,%s,%s,%s)
                ON CONFLICT (corpus_id, source_key, version)
                DO UPDATE SET filename = EXCLUDED.filename,
                              file_sha256 = EXCLUDED.file_sha256
                RETURNING id
            """, (job["corpus_id"], job["document_key"], job["version"],
                  job["filename"], job["file_sha256"])).fetchone()
            source_id = row["id"]
            con.execute("DELETE FROM integrity.chunks WHERE source_id = %s",
                        (source_id,))
            for chunk, vector in zip(chunks, embeddings):
                con.execute("""
                    INSERT INTO integrity.chunks
                    (source_id, chunk_no, page_number, start_offset,
                     end_offset, content, embedding)
                    VALUES (%s,%s,%s,%s,%s,%s,%s::vector)
                """, (source_id, chunk.chunk_no, chunk.page_number,
                      chunk.start_offset, chunk.end_offset,
                      chunk.text, vector_literal(vector)))
            # Chỉ chuyển phiên bản active khi indexing hoàn tất.
            con.execute("""
                UPDATE integrity.sources SET is_active = FALSE
                WHERE corpus_id = %s AND source_key = %s AND id <> %s
            """, (job["corpus_id"], job["document_key"], source_id))
            con.execute("""
                UPDATE integrity.sources
                SET is_active = TRUE, indexed_at = now()
                WHERE id = %s
            """, (source_id,))
            con.execute("""
                UPDATE integrity.jobs
                SET status = 'completed', updated_at = now(),
                    report = %s
                WHERE id = %s::uuid
            """, (Jsonb({"indexed_chunks": len(chunks),
                           "source_id": job["document_key"]}), job_id))
    except Exception:
        logger.exception("Index job failed: %s", job_id)
        set_status(job_id, "failed", "INDEX_ERROR")


@celery_app.task(name="app.tasks.scan_document")
def scan_document(job_id: str):
    set_status(job_id, "processing")
    try:
        job = get_job(job_id)
        with connect() as con:
            count = con.execute("""
                SELECT count(*) AS n FROM integrity.sources
                WHERE corpus_id = %s AND is_active = TRUE
            """, (job["corpus_id"],)).fetchone()["n"]
        if count == 0:
            raise ValueError("CORPUS_EMPTY")

        query_chunks = parse_job(job)
        vectors = encode_texts([x.text for x in query_chunks])
        all_findings: list[dict] = []
        for query_chunk, vector in zip(query_chunks, vectors):
            candidates = retrieve(query_chunk.text, vector, job["corpus_id"])
            if not candidates:
                continue
            pairs = [[query_chunk.text, item["source_text"]]
                     for item in candidates]
            scores = rerank_pairs(pairs)
            for item, score in zip(candidates, scores):
                # Mọi candidate có dense_score/lexical_score đầy đủ sau retrieve().
                lexical = float(item["lexical_score"])
                dense = float(item["dense_score"])
                features = build_features(
                    query_chunk.text, item["source_text"], dense, float(score)
                )
                result = score_candidate(features)  # None khi feature flag OFF
                if result is None:
                    # Chỉ là heuristic demo; phải hiệu chỉnh với dữ liệu có nhãn.
                    review = lexical >= 0.65 or (dense >= 0.68 and score >= 0.67)
                    decision_source = "heuristic"
                else:
                    # Near-exact override giúp không bỏ qua copy gần nguyên văn.
                    review = (result["score"] >= result["threshold"]
                              or lexical >= 0.92)
                    decision_source = (
                        "near_exact_override" if lexical >= 0.92
                        else "custom_classifier"
                    )
                if not review:
                    continue
                all_findings.append({
                    "submission_chunk_no": query_chunk.chunk_no,
                    "submission_page": query_chunk.page_number,
                    "submission_start": query_chunk.start_offset,
                    "submission_end": query_chunk.end_offset,
                    "submission_text": query_chunk.text,
                    **item,
                    "reranker_score": round(float(score), 4),
                    "classifier_score": (
                        round(result["score"], 4) if result else None
                    ),
                    "decision_source": decision_source,
                    "feature_values": features,
                    "review_required": True,
                })

        # Sắp xếp, giữ một vài bằng chứng có điểm tốt nhất trên mỗi chunk.
        all_findings.sort(key=lambda f: f["reranker_score"], reverse=True)
        findings = group_adjacent_findings(all_findings[:200])
        # Các ID nhóm passage chỉ dành cho UI, không phải kết luận đạo văn.
        if settings.classifier_enabled:
            from app.classifier import load_classifier
            _, classifier_manifest = load_classifier()
            classifier_version = classifier_manifest["model_version"]
        else:
            classifier_version = None
        report = {
            "schema_version": 2,
            "job_id": job_id,
            "corpus_id": job["corpus_id"],
            "submission_id": job["document_key"],
            "submission_version": job["version"],
            "model": {
                "embedding": settings.embedding_model,
                "reranker": settings.reranker_model,
                "classifier_version": classifier_version,
                "detector_mode": ("classifier" if settings.classifier_enabled
                                  else "heuristic"),
            },
            "summary": {
                "scanned_chunks": len(query_chunks),
                "flagged_pairs": len(all_findings),
                "findings_returned": len(findings),
                "matched_sources": len({f["source_key"] for f in all_findings}),
                "passage_groups": len({f["passage_group_id"] for f in findings}),
            },
            "findings": findings,
            "disclaimer": (
                "Similarity evidence only; teachers must verify sources, "
                "citations and context. No automatic plagiarism verdict."
            ),
        }
        with connect() as con:
            con.execute("""
                UPDATE integrity.jobs
                SET status='completed', report=%s, updated_at=now()
                WHERE id=%s::uuid
            """, (Jsonb(report), job_id))
    except Exception:
        logger.exception("Scan job failed: %s", job_id)
        set_status(job_id, "failed", "SCAN_ERROR")
```

**Ghi chú kỹ thuật:** `flagged_pairs` là số cặp đoạn ứng viên được gắn cờ, **không phải số đoạn duy nhất**; do chunk chồng lấn, có thể nhiều cặp trỏ tới cùng một đoạn hoặc cùng nguồn. Bản UI có thể nhóm theo trang/nguồn để đỡ rối. Lỗi hiện được trả bằng mã chung; worker ghi log riêng. Nếu muốn hiển thị `EMPTY_TEXT`/`CORPUS_EMPTY` cho Admin, phân loại exception cụ thể trước khi lưu `error_code`. MVP trên chỉ đảm bảo state từ `queued` → `processing` → `completed/failed`, chưa có retry scheduler và recovery của job khi worker chết.

## 11. Code Python — HTTP API, upload và kiểm soát truy cập

### `app/main.py`

```python
import hashlib
import re
import secrets
import uuid
import zipfile
from pathlib import Path

from fastapi import (
    Depends, FastAPI, File, Form, Header,
    HTTPException, UploadFile,
)
from app.config import settings
from app.db import connect, get_job
from app.tasks import index_source, scan_document

app = FastAPI(title="UGTM Plagiarism AI", version="0.1.0")
SAFE_KEY = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$")


def require_key(x_api_key: str | None = Header(None, alias="X-API-Key")):
    if not x_api_key or not secrets.compare_digest(x_api_key, settings.api_key):
        raise HTTPException(status_code=401, detail="Unauthorized")


def validate_metadata(corpus_id: str, document_key: str, version: int):
    if (not SAFE_KEY.fullmatch(corpus_id)
        or not SAFE_KEY.fullmatch(document_key)
        or version < 1):
        raise HTTPException(status_code=422, detail="Invalid metadata")


async def save_upload(file: UploadFile) -> tuple[str, str, str]:
    filename = Path(file.filename or "unnamed").name[:180]
    ext = Path(filename).suffix.lower()
    if ext not in {".pdf", ".docx"}:
        raise HTTPException(status_code=415, detail="Only PDF/DOCX allowed")
    upload_dir = settings.data_dir / "uploads"
    upload_dir.mkdir(parents=True, exist_ok=True)
    path = upload_dir / f"{uuid.uuid4().hex}{ext}"
    digest = hashlib.sha256()
    count = 0
    try:
        with path.open("wb") as dest:
            while data := await file.read(1024 * 1024):
                count += len(data)
                if count > settings.max_upload_mb * 1024 * 1024:
                    raise HTTPException(status_code=413, detail="File too large")
                digest.update(data)
                dest.write(data)
        if count == 0:
            raise HTTPException(status_code=400, detail="Empty file")
        if ext == ".pdf":
            with path.open("rb") as src:
                if not src.read(5).startswith(b"%PDF-"):
                    raise HTTPException(status_code=415, detail="Invalid PDF")
        else:
            if not zipfile.is_zipfile(path):
                raise HTTPException(status_code=415, detail="Invalid DOCX")
            with zipfile.ZipFile(path) as archive:
                if "word/document.xml" not in archive.namelist():
                    raise HTTPException(status_code=415, detail="Invalid DOCX")
                # Giới hạn tổng dung lượng giải nén, chống ZIP bomb cơ bản.
                if sum(f.file_size for f in archive.infolist()) > 100 * 1024 * 1024:
                    raise HTTPException(status_code=413, detail="Expanded DOCX too large")
        return str(path), digest.hexdigest(), filename
    except Exception:
        path.unlink(missing_ok=True)
        raise
    finally:
        await file.close()


def insert_job(job_type: str, corpus_id: str, document_key: str,
               version: int, filename: str, path: str, sha: str):
    job_id = str(uuid.uuid4())
    with connect() as con:
        created = con.execute("""
            INSERT INTO integrity.jobs
              (id, job_type, corpus_id, document_key, version,
               filename, file_path, file_sha256)
            VALUES (%s::uuid,%s,%s,%s,%s,%s,%s,%s)
            ON CONFLICT (job_type, corpus_id, document_key, version)
            DO NOTHING RETURNING id
        """, (job_id, job_type, corpus_id, document_key,
              version, filename, path, sha)).fetchone()
        if created:
            return job_id, True
        existing = con.execute("""
            SELECT id, file_sha256 FROM integrity.jobs
            WHERE job_type=%s AND corpus_id=%s AND document_key=%s
              AND version=%s
        """, (job_type, corpus_id, document_key, version)).fetchone()
        if existing["file_sha256"] != sha:
            raise HTTPException(status_code=409,
                                detail="Version already exists with different content")
        return str(existing["id"]), False


@app.get("/healthz")
def healthz():
    # Readiness thực tế cần kiểm tra thêm Redis, worker, database.
    return {"status": "ok"}


@app.post("/api/v1/corpus/documents", status_code=202,
          dependencies=[Depends(require_key)])
async def add_source(
    corpus_id: str = Form(...),
    source_id: str = Form(...),
    version: int = Form(1),
    file: UploadFile = File(...),
):
    validate_metadata(corpus_id, source_id, version)
    path, sha, filename = await save_upload(file)
    try:
        job_id, created = insert_job(
            "index", corpus_id, source_id, version, filename, path, sha
        )
    except Exception:
        Path(path).unlink(missing_ok=True)
        raise
    if created:
        index_source.delay(job_id)
    else:
        Path(path).unlink(missing_ok=True)
    return {"job_id": job_id, "status": "queued" if created else "existing"}


@app.post("/api/v1/scans", status_code=202,
          dependencies=[Depends(require_key)])
async def start_scan(
    corpus_id: str = Form(...),
    submission_id: str = Form(...),
    version: int = Form(1),
    file: UploadFile = File(...),
):
    validate_metadata(corpus_id, submission_id, version)
    path, sha, filename = await save_upload(file)
    try:
        job_id, created = insert_job(
            "scan", corpus_id, submission_id, version, filename, path, sha
        )
    except Exception:
        Path(path).unlink(missing_ok=True)
        raise
    if created:
        scan_document.delay(job_id)
    else:
        Path(path).unlink(missing_ok=True)
    return {"job_id": job_id, "status": "queued" if created else "existing"}


@app.get("/api/v1/jobs/{job_id}", dependencies=[Depends(require_key)])
def job_status(job_id: uuid.UUID):
    job = get_job(str(job_id))
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    return {
        "job_id": str(job["id"]),
        "type": job["job_type"],
        "status": job["status"],
        "error_code": job["error_code"],
        "updated_at": job["updated_at"],
    }


@app.get("/api/v1/reports/{job_id}", dependencies=[Depends(require_key)])
def report(job_id: uuid.UUID):
    job = get_job(str(job_id))
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    if job["status"] != "completed":
        raise HTTPException(status_code=409, detail="Job not completed")
    return job["report"] or {}
```

### API contract

| HTTP | Endpoint | Form/body | Kết quả |
|---|---|---|---|
| `POST` | `/api/v1/corpus/documents` | `corpus_id`, `source_id`, `version`, `file` | `202` + `job_id` |
| `POST` | `/api/v1/scans` | `corpus_id`, `submission_id`, `version`, `file` | `202` + `job_id` |
| `GET` | `/api/v1/jobs/{job_id}` | Path UUID | `queued/processing/completed/failed` |
| `GET` | `/api/v1/reports/{job_id}` | Path UUID | Báo cáo JSON hoặc `409` |
| `GET` | `/healthz` | — | Liveness của API |

Tất cả endpoint trừ `/healthz` cần header **`X-API-Key`**. `source_id` và `submission_id` là ID nghiệp vụ **do .NET quản lý**, không phải tên file. `version` là phiên bản nộp. Một bộ `(job_type, corpus_id, document_id, version)` được coi là idempotent: nếu upload lại đúng nội dung sẽ trả `job_id` cũ; nếu cùng ID/version nhưng nội dung khác sẽ bị `409`. Muốn quét lại sau khi thay đổi model cần thiết kế `run_version`/`model_version` hoặc endpoint reprocess riêng — không âm thầm ghi đè báo cáo cũ.

**Cảnh báo:** Nếu Celery/Redis bị lỗi ngay lúc `.delay()`, job có thể kẹt `queued` dù API đã lưu thành công. Bản production phải dùng transactional outbox hoặc bộ quét job `queued` quá hạn để đẩy lại vào queue. MVP cần có cách Admin thủ công retry/restart; không xóa bài sinh viên khi AI lỗi.

## 12. Response JSON mẫu cho giao diện .NET/React

```json
{
  "schema_version": 2,
  "job_id": "d13bff7f-9d84-40cf-88a9-ae70fa92d41c",
  "corpus_id": "uit-library-v1",
  "submission_id": "thesis-2026-001",
  "submission_version": 1,
  "model": {
    "embedding": "BAAI/bge-m3",
    "reranker": "BAAI/bge-reranker-v2-m3",
    "classifier_version": "candidate-lr-v1",
    "detector_mode": "classifier"
  },
  "summary": {
    "scanned_chunks": 35,
    "flagged_pairs": 6,
    "findings_returned": 6,
    "matched_sources": 2,
    "passage_groups": 4
  },
  "findings": [
    {
      "submission_page": 12,
      "submission_start": 100,
      "submission_end": 430,
      "submission_text": "Đoạn trong báo cáo mới...",
      "chunk_id": 91,
      "source_key": "kltn-2024-025",
      "source_version": 1,
      "source_filename": "kltn-2024-025.pdf",
      "source_page": 18,
      "source_start": 240,
      "source_end": 580,
      "source_text": "Đoạn nguồn được tìm thấy...",
      "rrf_score": 0.0323,
      "dense_score": 0.84,
      "lexical_score": 0.31,
      "reranker_score": 0.91,
      "classifier_score": 0.76,
      "decision_source": "custom_classifier",
      "submission_chunk_no": 13,
      "source_chunk_no": 20,
      "passage_group_id": "PG-0001",
      "feature_values": {
        "semantic_cosine": 0.84,
        "reranker_score": 0.91,
        "char_trigram_dice": 0.41,
        "token_jaccard": 0.36,
        "length_ratio": 0.97
      },
      "review_required": true
    }
  ],
  "disclaimer": "Similarity evidence only; teachers must verify sources, citations and context. No automatic plagiarism verdict."
}
```

**Lưu ý:** Đây là **JSON minh họa**, không phải kết quả benchmark. Trong **bản v2**, `retrieve()` đã tính lại `dense_score`/`lexical_score` cho mọi ứng viên được chọn nên hai trường này không còn `null`. `classifier_score` cũng không phải xác suất đạo văn. Reranker score không biểu diễn xác suất vi phạm. **Không** dùng `matched_sources` như số nguồn đạo văn đã chứng minh.

## 13. Tích hợp với ASP.NET Core UGTM

### Ranh giới trách nhiệm

- Chỉ **UGTM** biết người nộp, đề tài, GVHD, GVPB, trạng thái nghiệp vụ, quyền xem report.
- AI **không** nhận JWT sinh viên, MSSV, điểm chấm hoặc thông tin cá nhân không cần thiết; chỉ nhận `submission_id`/`corpus_id`/`version` và nội dung file.
- UGTM lưu: `AIJobId`, `SubmissionId`, `Version`, `Status`, `RequestedAt`, `CompletedAt`, `ModelVersion` tùy quy ước hiện có. AI lưu corpus, chunks, evidence/report. Tránh thiết lập foreign key xuyên hai database.
- Khi phiên bản báo cáo thay đổi, tạo scan mới theo `version`. Kết quả của phiên bản cũ chỉ là lịch sử, không bị coi là nguồn độc lập của sinh viên khác.
- Sinh viên upload thành công **kể cả khi AI đang chạy hoặc AI bị lỗi**. GVHD/GVPB xem báo cáo khi đã hoàn tất, tuân thủ phạm vi được phân công (RBAC + policy nghiệp vụ).

### Ví dụ adapter `PlagiarismAiClient.cs` (tham khảo, chỉnh namespace theo repo thật)

```csharp
using System.Net.Http.Headers;
using System.Text.Json;

public sealed class PlagiarismAiClient(HttpClient client)
{
    public async Task<string> StartScanAsync(
        string submissionId,
        int version,
        string corpusId,
        Stream documentStream,
        string originalFilename,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(corpusId), "corpus_id");
        form.Add(new StringContent(submissionId), "submission_id");
        form.Add(new StringContent(version.ToString()), "version");

        using var file = new StreamContent(documentStream);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            originalFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
              ? "application/pdf"
              : "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        );
        form.Add(file, "file", Path.GetFileName(originalFilename));

        using var response = await client.PostAsync(
            "/api/v1/scans", form, ct);
        response.EnsureSuccessStatusCode();
        using var result = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));
        return result.RootElement.GetProperty("job_id").GetString()!;
    }

    public async Task<JsonDocument> GetJobAsync(
        string jobId, CancellationToken ct = default)
    {
        using var response = await client.GetAsync(
            $"/api/v1/jobs/{jobId}", ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));
    }

    public async Task<JsonDocument> GetReportAsync(
        string jobId, CancellationToken ct = default)
    {
        using var response = await client.GetAsync(
            $"/api/v1/reports/{jobId}", ct);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(ct));
    }
}
```

**Dependency injection** trong host UGTM (chỉnh theo `Program.cs`/composition root thực tế):

```csharp
builder.Services.AddHttpClient<PlagiarismAiClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["PlagiarismAI:BaseUrl"]!);
    client.DefaultRequestHeaders.Add(
        "X-API-Key", config["PlagiarismAI:ApiKey"]!);
    client.Timeout = TimeSpan.FromMinutes(2); // upload HTTP, không chờ AI infer
});
```

```json
{
  "PlagiarismAI": {
    "BaseUrl": "http://localhost:8000",
    "ApiKey": "USE_ENV_OR_SECRET_MANAGER_IN_REAL_DEPLOYMENT"
  }
}
```

Không commit `ApiKey` thực vào `appsettings.json`; override qua biến môi trường hoặc secret manager. Nếu .NET chạy **trong cùng Docker network** thì đổi URL thành `http://api:8000`. Nếu khác host, dùng HTTPS reverse proxy/mTLS và ràng buộc firewall/IP allowlist.

### Luồng nghiệp vụ trong .NET (pseudo-implementation)

```text
1. Sinh viên upload report.pdf lên UGTM.
2. .NET xác thực RBAC/Policy; lưu file vào UGTM storage;
   ghi nhận SubmissionVersion + DB transaction COMMIT.
3. Phát sự kiện nội bộ ReportSubmitted(submissionId, version, fileKey).
4. Background handler của UGTM đọc file từ storage -> gọi
   PlagiarismAiClient.StartScanAsync(...) -> lưu AIJobId.
5. Background scheduler của UGTM GET /jobs/{id}; cập nhật status.
6. Khi completed, tải GET /reports/{id}; lưu snapshot hoặc ID tham chiếu.
7. GVHD/GVPB được phân công GET /ugtm/reports/{submissionId}/integrity.
   Backend UGTM thực hiện authorize, rồi trả dữ liệu cho React.
8. Nếu AI Failed: cho phép retry sau; không rollback file nộp KLTN.
```

**Vị trí tích hợp được khuyên dùng:** bước nộp **báo cáo cuối kỳ** (tuần 11–18), trước quá trình nhận xét/chuyển phản biện. Có thể mở rộng sang đề cương và báo cáo giữa kỳ khi MVP ổn định. Trong báo cáo phân tích của nhóm, thẩm định KLTN và quyền truy cập gắn với GVHD/GVPB/Khoa theo nhiệm vụ; tính năng mới phải tuân thủ đúng phạm vi đó.

### Mẫu UI tối thiểu (React)

```text
[Chi tiết khóa luận]
  File: BaoCaoCuoiKy_v3.pdf
  AI Integrity: Đang kiểm tra ...

  Sau khi hoàn tất:
  • Đoạn cần rà soát: 6 cặp
  • Tài liệu nguồn liên quan: 2

  [Đoạn báo cáo: Trang 12]    [Nguồn: KLTN 2024/025, Trang 18]
  "..."                      "..."
  Lexical: 0.31  |  Semantic: 0.84  |  Reranker: 0.91
  [Cần GV kiểm tra trích dẫn và bối cảnh]
```

Hiển thị **số cặp cần rà soát** và bằng chứng thay vì một “tỷ lệ đạo văn” duy nhất. Không hiện các tài liệu nguồn chưa được phép cho sinh viên đọc; có thể chỉ để giảng viên xem toàn văn đối chiếu.

## 14. Chạy MVP từ đầu đến cuối

### Bước 1 — Dựng môi trường

```bash
cp .env.example .env
# Sửa POSTGRES_PASSWORD và AI_API_KEY trong .env
docker compose build
docker compose up -d

docker compose ps
docker compose logs -f worker
```

Lần đầu Celery worker tải model từ Hugging Face, cần Internet và dung lượng ổ đĩa cho cache. Tại thời điểm khởi động, không bắt buộc API phải đợi worker tải model để trả `/healthz`.

### Bước 2 — Test GPU trong container

```bash
docker compose exec worker python -c \
 'import torch; print("CUDA:", torch.cuda.is_available()); print(torch.cuda.get_device_name(0))'
```

### Bước 3 — Index một file nguồn được phép sử dụng

```bash
export AI_API_KEY='the-same-value-in-.env'

curl -X POST http://127.0.0.1:8000/api/v1/corpus/documents \
  -H "X-API-Key: $AI_API_KEY" \
  -F "corpus_id=uit-library-v1" \
  -F "source_id=kltn-demo-001" \
  -F "version=1" \
  -F "file=@./samples/source.pdf"
```

Response ví dụ:

```json
{"job_id": "84d7ef55-4b99-4a63-88cf-28e4a493deae", "status": "queued"}
```

**Quan trọng:** Kiểm tra job index đã `completed` trước khi gọi scan, nếu không corpus sẽ bị coi là chưa sẵn sàng.

### Bước 4 — Kiểm tra job

```bash
curl -H "X-API-Key: $AI_API_KEY" \
  http://127.0.0.1:8000/api/v1/jobs/84d7ef55-4b99-4a63-88cf-28e4a493deae
```

### Bước 5 — Scan báo cáo thử

```bash
curl -X POST http://127.0.0.1:8000/api/v1/scans \
  -H "X-API-Key: $AI_API_KEY" \
  -F "corpus_id=uit-library-v1" \
  -F "submission_id=thesis-demo-2026-001" \
  -F "version=1" \
  -F "file=@./samples/submission.pdf"
```

Dùng `job_id` trả về để `GET /api/v1/jobs/{job_id}` rồi `GET /api/v1/reports/{job_id}` khi hoàn tất.

**Test âm tính:** tạo tài liệu mới có cùng chủ đề nhưng diễn giải phương pháp/kết quả khác với tài liệu nguồn; kiểm tra số cảnh báo giả. **Test dương tính:** dùng một phần nội dung từ tài liệu nguồn mà nhóm có quyền kiểm thử rồi thử sửa câu chữ; bảo đảm corpus đã index trước.

## 15. Kiểm thử tự động căn bản

### `tests/test_documents.py`

```python
from app.documents import TextPage, make_chunks, normalize


def test_normalize():
    assert normalize("Xin  chào\nUIT\t!") == "Xin chào UIT !"


def test_chunk_offsets_are_valid():
    text = " ".join(f"word{i}" for i in range(250))
    result = make_chunks([TextPage(3, text)], size=90, overlap=20)
    assert len(result) >= 3
    assert all(c.page_number == 3 for c in result)
    assert all(text[c.start_offset:c.end_offset] == c.text for c in result)
    assert result[1].start_offset < result[0].end_offset
```

```bash
docker compose exec api pytest -q tests
```

**Chú ý:** `Dockerfile` MVP chỉ copy thư mục `app`, không copy `tests`. Để chạy lệnh pytest trên, sửa Dockerfile thành `COPY tests ./tests` **trong môi trường dev** hoặc mount `tests` vào container. Không nhất thiết đóng gói test vào image production.

### `tests/test_features.py` (MỚI V2)

```python
from app.features import build_features, feature_vector, FEATURE_NAMES


def test_feature_shape_and_bounds():
    features = build_features("mô hình học sâu", "mạng nơ-ron học sâu", .74, .81)
    assert list(features.keys()) == FEATURE_NAMES
    assert len(feature_vector(features)) == 5
    assert 0 <= features["token_jaccard"] <= 1
    assert 0 <= features["char_trigram_dice"] <= 1
    assert 0 <= features["length_ratio"] <= 1


def test_identical_text_is_high_lexical():
    f = build_features("ABC là phương pháp X", "ABC là phương pháp X", 1, 1)
    assert f["char_trigram_dice"] == 1
    assert f["token_jaccard"] == 1
```

### `tests/test_alignment.py` (MỚI V2)

```python
from app.alignment import group_adjacent_findings


def test_adjacent_passages_are_grouped():
    rows = [
        {"source_key": "a", "source_version": 1,
         "submission_chunk_no": 3, "source_chunk_no": 7},
        {"source_key": "a", "source_version": 1,
         "submission_chunk_no": 4, "source_chunk_no": 8},
        {"source_key": "a", "source_version": 1,
         "submission_chunk_no": 8, "source_chunk_no": 18},
    ]
    group_adjacent_findings(rows)
    assert rows[0]["passage_group_id"] == rows[1]["passage_group_id"]
    assert rows[0]["passage_group_id"] != rows[2]["passage_group_id"]
```

**Bài test hợp đồng classifier phải bổ sung khi bật training:** nếu thiếu manifest, feature schema sai, `CLASSIFIER_ENABLED=true` phải lỗi với mã rõ ràng; cùng input/features sau restart phải cho cùng inference score; dữ liệu test hoàn toàn tách khỏi group nguồn train.

### Checklist kiểm thử tích hợp (phải làm khi kết nối UGTM)

| Tình huống | Mong đợi |
|---|---|
| PDF/DOCX hợp lệ, dưới 20 MB | Tạo job `queued` |
| File `.exe` đổi tên `.pdf`, hoặc PDF header sai | Từ chối `415` |
| File vượt 20 MB | Từ chối `413`, xóa file tạm |
| Request không có `X-API-Key` | `401` |
| Trùng `source_id + version`, cùng bytes | Idempotent, không index lại |
| Trùng ID/version, bytes khác | `409` |
| Corpus chưa index | Job scan `failed` (`SCAN_ERROR` trong MVP) |
| PDF scan không có text | Job `failed` (`INDEX_ERROR` hoặc `SCAN_ERROR`) |
| 2 PDF giống nhau theo cùng nguồn | Có candidates, GV xem được source/page |
| Hai tài liệu cùng lĩnh vực, không sao chép | Theo dõi false positives |
| API worker chết trong lúc chạy | Được phát hiện và có quy trình vận hành/retry thủ công |
| Sinh viên nộp bài khi AI lỗi | UGTM vẫn lưu bài thành công |
| Người ngoài nhóm/GV không được phân công | UGTM chặn xem report theo policy |
| Nộp version mới | Tạo job mới, không ghi đè report version cũ |

## 15A. Training classifier do nhóm tự xây (MỚI V2 — chỉ triển khai SAU khi pipeline chạy)

### A. Định nghĩa nhãn đúng để tránh khẳng định quá mức

- `label=1`: cặp đoạn **được xác nhận có tái sử dụng nội dung từ cùng nguồn** trong một tình huống kiểm thử có kiểm soát (copy, paraphrase, dịch thuật). **Không** có nghĩa "đạo văn", vì việc trích dẫn/quy định/ngoại lệ do giảng viên xem xét.
- `label=0`: cặp đoạn **độc lập**; cần nhiều hard negatives (cùng ngành/cùng thuật ngữ/cùng định nghĩa nhưng nội dung khác), cả trường hợp có trích dẫn hợp lệ.
- `group_id`: ID của **gia đình tài liệu nguồn** để **tất cả biến thể của cùng nguồn nằm cùng một tập** train hoặc validation hoặc test. Không chia từng hàng ngẫu nhiên.

**Khởi đầu gợi ý:** 500–1.000 cặp được kiểm tra nhãn bởi thành viên nhóm; không có bảo đảm đủ cho production. Trong thời gian chờ 20–50 KLTN, tạo cặp từ tài liệu do nhóm sở hữu/quyền sử dụng và bản demo giả lập. Tránh train và đánh giá trên những biến thể gần trùng nhau bị phân tán sang train/test. Chỉ sử dụng KLTN UIT theo giấy phép thư viện; không đưa các văn bản này lên dịch vụ gán nhãn công khai nếu chưa được phép.

### B. Format dữ liệu `training_data/pairs.csv`

```csv
pair_id,group_id,label,query_text,source_text
P001,source_demo_001,1,"Hệ thống áp dụng CNN để phân loại hình ảnh.","Nghiên cứu sử dụng mạng tích chập nhận diện ảnh."
N001,source_demo_002,0,"Mô hình CNN phân loại ảnh X-quang.","Mạng CNN được dùng phân loại ảnh vệ tinh."
```

**Hai hàng trên chỉ minh họa cấu trúc CSV, không đủ để train.** Đoạn cùng chủ đề phải được người kiểm tra xác nhận nhãn, không gán nhãn `1` chỉ dựa vào cosine. Không commit `training_data/` vào repo.

### C. `training/train_classifier.py` — tạo features trên GPU, train classifier nhỏ trên CPU

```python
import argparse
import csv
import json
from datetime import datetime, timezone
from pathlib import Path

import joblib
import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (precision_score, recall_score, f1_score,
                             confusion_matrix)
from sklearn.model_selection import GroupShuffleSplit
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from app.features import (FEATURE_NAMES, FEATURE_SCHEMA,
                          build_features, feature_vector)
from app.models import encode_texts, rerank_pairs


def load_pairs(path: Path):
    with path.open(newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    for row in rows:
        if row.get("label") not in {"0", "1"}:
            raise ValueError(f"Invalid label for pair {row.get('pair_id')}")
        if not all(row.get(k) for k in ("pair_id", "group_id",
                                       "query_text", "source_text")):
            raise ValueError(f"Missing columns for pair {row}")
    if len({r["group_id"] for r in rows}) < 6:
        raise ValueError("Need more independent document groups")
    return rows


def build_dataset(rows, batch_size=8):
    X = []
    for start in range(0, len(rows), batch_size):
        batch = rows[start:start + batch_size]
        queries = [r["query_text"] for r in batch]
        sources = [r["source_text"] for r in batch]
        qvecs = encode_texts(queries, batch_size=batch_size)
        svecs = encode_texts(sources, batch_size=batch_size)
        rerank = rerank_pairs([[q, s] for q, s in zip(queries, sources)],
                              batch_size=batch_size)
        for i, row in enumerate(batch):
            denom = float(np.linalg.norm(qvecs[i]) * np.linalg.norm(svecs[i]))
            cosine = float(np.dot(qvecs[i], svecs[i]) / denom) if denom else 0.0
            features = build_features(row["query_text"], row["source_text"],
                                      cosine, float(rerank[i]))
            X.append(feature_vector(features))
    return np.asarray(X, dtype=np.float64)


def require_both_classes(y, name):
    if len(set(int(v) for v in y)) != 2:
        raise ValueError(f"{name} needs both positive and negative labels; "
                         "add more independent source groups")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", default="training_data/pairs.csv")
    parser.add_argument("--output", default="artifacts/suspicious_passage_classifier.joblib")
    args = parser.parse_args()

    rows = load_pairs(Path(args.input))
    X = build_dataset(rows)
    y = np.asarray([int(r["label"]) for r in rows])
    groups = np.asarray([r["group_id"] for r in rows])

    # Hold-out TEST không đụng vào trong quá trình chọn threshold.
    split1 = GroupShuffleSplit(n_splits=1, test_size=0.20, random_state=310)
    trainval_idx, test_idx = next(split1.split(X, y, groups))
    split2 = GroupShuffleSplit(n_splits=1, test_size=0.25, random_state=311)
    inner_train, inner_val = next(split2.split(
        X[trainval_idx], y[trainval_idx], groups[trainval_idx]
    ))
    train_idx = trainval_idx[inner_train]
    val_idx = trainval_idx[inner_val]
    for name, ids in [("train", train_idx), ("validation", val_idx),
                      ("test", test_idx)]:
        require_both_classes(y[ids], name)
    assert not (set(groups[train_idx]) & set(groups[val_idx]))
    assert not (set(groups[train_idx]) & set(groups[test_idx]))
    assert not (set(groups[val_idx]) & set(groups[test_idx]))

    clf = Pipeline([
        ("scale", StandardScaler()),
        ("lr", LogisticRegression(max_iter=3000, class_weight="balanced",
                                  random_state=310)),
    ])
    clf.fit(X[train_idx], y[train_idx])
    val_scores = clf.predict_proba(X[val_idx])[:, 1]
    candidates = np.arange(0.25, 0.86, 0.05)
    threshold = float(max(candidates, key=lambda t:
        f1_score(y[val_idx], val_scores >= t, zero_division=0)))

    test_scores = clf.predict_proba(X[test_idx])[:, 1]
    predicted = (test_scores >= threshold).astype(int)
    metrics = {
        "precision": float(precision_score(y[test_idx], predicted,
                                           zero_division=0)),
        "recall": float(recall_score(y[test_idx], predicted,
                                     zero_division=0)),
        "f1": float(f1_score(y[test_idx], predicted, zero_division=0)),
        "confusion_matrix": confusion_matrix(y[test_idx], predicted).tolist(),
    }
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(clf, out)
    manifest = {
        "model_version": "candidate-lr-v1",
        "feature_schema": FEATURE_SCHEMA,
        "feature_names": FEATURE_NAMES,
        "embedding_model": "BAAI/bge-m3",
        "reranker_model": "BAAI/bge-reranker-v2-m3",
        "threshold": threshold,
        "created_at": datetime.now(timezone.utc).isoformat(),
        "dataset_size": len(rows),
        "group_counts": {
            "train": len(set(groups[train_idx])),
            "validation": len(set(groups[val_idx])),
            "test": len(set(groups[test_idx])),
        },
        "test_metrics": metrics,
        "meaning": "candidate content reuse score; not a plagiarism verdict",
    }
    out.with_suffix(".manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(json.dumps({"threshold": threshold, **metrics}, indent=2))


if __name__ == "__main__":
    main()
```

**Quan trọng:** Code trên minh họa một cách phân chia theo nhóm nguồn, nhưng với ít nhóm rất dễ có tập validation/test chỉ chứa một class và chương trình sẽ dừng. Đó là **biện pháp bảo vệ đánh giá**, không phải lỗi ngẫu nhiên nên bỏ qua. Nên tăng số nguồn đa dạng và cân bằng nhãn trong từng nhóm. Với 20–50 KLTN, chất lượng annotation quyết định tính hữu ích của classifier.

**Vì sao training dùng GPU lẫn CPU?** BGE-M3/reranker trích feature trên GPU (inference), còn `LogisticRegression.fit()` là phần model **nhóm tự training trên CPU** trong thời gian ngắn. Nếu chưa có GPU, hoàn tất pipeline tổ chức dữ liệu và unit tests trước, hoặc tạm dùng cấu hình mô hình nhẹ hơn có khả năng chạy CPU để thử luồng; không sử dụng features từ model A để đánh giá như features của model B.

### D. Cách chạy training

```bash
# Trong virtualenv có requirements và CUDA tương thích:
# Chạy tại root repo, sau khi chuẩn bị pairs.csv với nhãn đã kiểm tra.
python -m training.train_classifier \
  --input training_data/pairs.csv \
  --output artifacts/suspicious_passage_classifier.joblib

# Kiểm tra hai artifact xuất hiện:
ls artifacts/suspicious_passage_classifier.joblib \
   artifacts/suspicious_passage_classifier.manifest.json

# Sau khi kiểm thử và được chấp thuận, chỉnh .env:
# CLASSIFIER_ENABLED=true
# CLASSIFIER_PATH=/artifacts/suspicious_passage_classifier.joblib
# Sau đó restart docker compose worker
```

`training_data/`, `artifacts/` không commit vào GitHub. Có thể giữ artifact được ký/hash trên kho nội bộ; track `model_version` và `feature_schema` trong mỗi report để tránh không thể tái lập kết quả khi thay model.

### E. Các chỉ tiêu trước khi cho phép bật classifier

- Test theo **group** độc lập với train/validation; phải có đủ nhãn dương/âm.
- So sánh với heuristic và pipeline **không classifier** trên **cùng tập test**; cải thiện một chỉ số không được đánh đổi bằng false-positive quá cao.
- Kiểm tra case sao chép nguyên văn, paraphrase, Việt–Anh, hard negatives, phần lý thuyết chung; thử version mới không làm mất khả năng tìm nguồn (retrieval Recall@K).
- Không dùng kết quả `test` để chọn threshold hay sửa lại mô hình. Nếu muốn lặp lại thử nghiệm, giữ một holdout khác chưa sử dụng.
- Bật feature flag sau khi kiểm thử kết quả và thời gian inference; có rollback `CLASSIFIER_ENABLED=false` tức thì mà không thay đổi API.

## 16. Dữ liệu 20–50 khóa luận từ thư viện UIT

### Cần xin thư viện những gì?

1. **Quyền sử dụng cụ thể:** được phép xử lý tự động, lưu text chunks/embeddings, giữ file bao lâu, ai được xem toàn văn/đoạn trích và có được triển khai trên GPU thuê ngoài không.
2. **Format:** ưu tiên PDF có text layer hoặc DOCX. Nếu được cấp PDF scan, lên kế hoạch OCR sau; không đưa vào MVP mà không kiểm tra.
3. **Metadata:** mã nội bộ đã khử định danh, tên tài liệu, năm, lĩnh vực, tình trạng công bố, phiên bản, quyền truy cập. Không đưa MSSV hoặc danh sách điểm vào AI khi không cần.
4. **Phạm vi:** 20–50 tài liệu đủ để xây **demo đối chiếu nội bộ**, nhưng không được tuyên bố bao phủ mọi tài liệu UIT hoặc Internet.
5. **Quy trình thu hồi:** nếu thư viện rút quyền sử dụng một tài liệu, cần endpoint xóa/deactivate nguồn và chunks/vector, đồng thời xử lý snapshot báo cáo cũ theo chính sách.

### Cách chuẩn bị trước khi có dữ liệu

- Dùng 5–10 tài liệu do nhóm tự tạo hoặc có giấy phép cho phép lập chỉ mục.
- **Hai bộ dữ liệu khác nhau:** `corpus` 20–50 KLTN để **truy tìm nguồn**; `training_data/pairs.csv` là **cặp đoạn có nhãn** để train classifier. Không tự suy luận rằng "50 tài liệu = 50 mẫu train".
- Tạo 3 nhóm test: `EXACT_COPY`, `PARAPHRASE`, `UNRELATED_SAME_TOPIC` và một số cặp Việt–Anh.
- Giữ bảng test đơn giản (`test_id, source_id, submission_id, expected_source, note`) để demo với thầy.
- Chỉ số cần quan sát: nguồn nào được tìm thấy trong top-K, vị trí đoạn, cảnh báo giả, thời gian index một PDF và scan một PDF.
- Khi dữ liệu thư viện đến, chạy `POST /corpus/documents` cho từng tài liệu, chờ job `completed`. Không cần train lại BGE-M3.

**Đạo văn ≠ tương đồng:** việc trích dẫn đúng, quy chuẩn biểu mẫu, cùng kiến thức nền và hai đề tài có chủ đề gần nhau có thể làm điểm cao. Báo cáo phải ghi rõ chỉ hỗ trợ kiểm tra bằng chứng.

## 17. Bảo mật, chất lượng và vận hành trước khi đưa lên máy chủ thật

### Tối thiểu trước demo

- GitHub repo AI **private**; `.gitignore` đầy đủ cho `.env`, `data/`, `models/`, `*.pdf`, `*.docx`, database dumps, logs nhạy cảm.
- API không public; backend UGTM là cửa kiểm soát quyền duy nhất đối với sinh viên và giảng viên. Secret qua env, không để browser chứa `X-API-Key`.
- File upload giới hạn size, extension và signature, validate DOCX zip; chạy container không root và quét malware nếu hạ tầng có điều kiện.
- Log lỗi AI nhưng tránh log toàn văn tài liệu hay nội dung file; không gửi text khóa luận sang dịch vụ LLM bên ngoài khi chưa có phép.
- Mỗi scan phải gắn với **phiên bản báo cáo** và **corpus/model version** để có thể giải thích kết quả.
- Đo `time_to_index`, `time_to_scan`, VRAM tối đa, số chunks, số cặp rerank, worker queue depth; thử nghiệm với PDF dài thực tế.

### Nâng cấp sau MVP, ưu tiên từ cao đến thấp

1. **Khả dụng:** transactional outbox + retry/backoff + timeout + dead-letter/queued recovery; trạng thái `cancelled` nếu sinh viên thay thế file khi job chưa chạy.
2. **Chất lượng đầu vào:** parsing theo heading/paragraph, loại mục tài liệu tham khảo/biểu mẫu đặc thù, OCR PDF scan có kiểm soát chất lượng.
3. **Kết quả:** nhóm các hit chồng lấn, hỗ trợ teacher feedback `confirmed/rejected/needs_review`, tổng hợp phạm vi text được bao phủ, chưa gọi đó là % đạo văn.
4. **Citation-aware:** parser trích dẫn/tài liệu tham khảo và xác định trích dẫn liên quan đến vị trí matched passage. Không giả định “có cite => không đạo văn”.
5. **Hiệu năng:** batch vector retrieval, HNSW nếu lớn, model batch và async worker autoscaling theo VRAM.
6. **Lưu trữ:** object storage riêng; retention/delete theo chính sách; encrypt at rest; audit log cho thao tác xem/xóa nguồn.
7. **Quan sát:** OpenTelemetry metrics/log/trace; kiểm thử hợp đồng API từ .NET; CI/CD build image cố định phiên bản.
8. **Bảo mật người dùng:** policy theo submission/assignment; kiểm tra vai trò giảng viên ở .NET; tách quyền Admin của thư viện; kiểm tra giới hạn corpus để tránh lộ tài liệu giữa khoa/đợt.

## 18. Lộ trình triển khai đề xuất cho nhóm

| Sprint | Công việc | Tiêu chí hoàn thành |
|---|---|---|
| 1 | Khởi tạo repo, Docker, CUDA, Postgres, FastAPI, Celery | `/healthz`, worker thấy GPU, schema tạo được |
| 2 | Parser + chunking + BGE-M3 indexing | Index được 5 PDF/DOCX mẫu, có chunks/vectors |
| 3 | Dense search + pg_trgm + RRF + reranker | Test copy/paraphrase tìm được nguồn và trả JSON evidence |
| 4 | .NET integration + React view + quyền | Nộp bài UGTM → AI queued → completed → giảng viên xem |
| 5 | Passage alignment + quy chuẩn report + dữ liệu thư viện | Nhóm findings liền kề; nhập 20–50 KLTN được phép, đo lỗi giả |
| 6 (giai đoạn B) | Chuẩn bị pairs có nhãn và feature engineering | `pairs.csv`, bộ test theo group, train/test tách nguồn |
| 7 (giai đoạn B) | Train CPU classifier + validate + feature flag | Xuất `.joblib` + manifest, test không leakage, có rollback |
| 8 (nếu đủ thời gian) | Teacher feedback + so sánh trước/sau classifier | Demo thêm model nhóm tự train và phân tích trường hợp fail |

**Tiêu chí báo cáo với thầy:** Phân biệt **"pretrained retrieval/reranker"** và **"custom trained classifier"**. Thể hiện artifact, feature pipeline nhất quán, số lượng mẫu tự gán nhãn, phân chia theo nguồn, metrics so với heuristic (kể cả nếu model không tốt hơn). Không trình bày đây là “AI tự kết luận đạo văn”. Hãy demo ba case: **copy nguyên văn**, **diễn đạt lại**, **cùng chủ đề nhưng độc lập**; trình bày rõ vì sao lexical và semantic bổ sung nhau, chứng minh trace được nguồn và giảng viên có quyền đánh giá.

## 19. Rủi ro và quyết định cần chốt khi bắt tay vào code

| Vấn đề | Phương án mặc định | Khi nào cần đổi |
|---|---|---|
| Repo UGTM chưa review được | Module tích hợp .NET chỉ là adapter mẫu | Khi cấp quyền repo, map vào Events/Contracts thật |
| Dữ liệu thư viện chưa có | Corpus tài liệu được phép + bộ test mô phỏng | Khi thư viện gửi tài liệu và cấp quyền |
| GPU loại/VRAM chưa rõ | Một worker, fp16, batch=8 | CUDA OOM → batch=2/4 hoặc tách model |
| Quét PDF dài | Background job | Tối ưu sau khi benchmark thật |
| PDF scan | Báo lỗi `EMPTY_TEXT` | Khi bổ sung OCR |
| Có nhiều phiên bản cùng nguồn | Chỉ latest indexed là active | Cần quản trị version cụ thể/audit |
| Có AI score cao | Chỉ `review_required`, GV thẩm định | Khi có quy chế/nhãn dữ liệu để hiệu chỉnh |
| Chưa có classifier được đánh giá | `CLASSIFIER_ENABLED=false`, pipeline heuristic vẫn hoạt động | Khi có đủ nhãn và artifact qua test |
| Classifier model/feature drift | Ghi `feature_schema`, `embedding_model`, `reranker_model`, `model_version` | Khi model thay đổi phải regenerate features và huấn luyện lại |
| Quyền xem khóa luận nguồn | Chỉ giảng viên được cấp quyền ở UGTM | Theo thỏa thuận thư viện |
| Schema/vector model đổi | Re-index corpus theo model/version | Khi cập nhật embedding model |

## 19A. Kế hoạch cập nhật repo UGTM thực tế (C# + React; chỉ là mapping đề xuất)

**KHÔNG sửa trực tiếp code trong repository do chưa đọc được source (GitHub 404).** Trước khi tạo PR, hãy tìm trong UGTM các điểm nối tương ứng (không tự suy diễn tên file/class):

1. **Submission finalized** trong module Thesis/Documents: sau khi transaction lưu báo cáo thành công, phát integration event kiểu `ThesisReportSubmitted` với `submission_id`, `version`, `file_reference`, `corpus_id`. Dùng outbox nếu đã có; không tạo request AI trong transaction upload.
2. **Integrity.Application handler**: gọi `POST /api/v1/scans` qua typed `HttpClient` có `X-API-Key`; nhận `job_id`; lưu bảng trong schema `integrity` phía UGTM: `submission_id`, `version`, `ai_job_id`, `status`, `detector_mode`, `classifier_version`, `report_snapshot`, `updated_at`.
3. **Background reconciliation**: định kỳ hỏi `GET /api/v1/jobs/{id}`. Nếu `completed`, lấy `GET /api/v1/reports/{id}`. Nếu `failed`, log mã lỗi và cho Admin retry theo chính sách; bài nộp vẫn hợp lệ.
4. **Authorization**: endpoint UI `GET /api/theses/{id}/similarity-report?version=...` áp dụng RBAC + Policy-based Authorization; chỉ đúng GVHD/GVPB/Khoa được cấp quyền xem đoạn nguồn, không lộ report qua URL public hay JWT browser đến FastAPI.
5. **React**: hiển thị `Queued/Processing/Completed/Failed`; nhóm theo `passage_group_id`, hiển thị bản nộp cạnh bản nguồn (nếu có quyền), `decision_source`, các score và `classifier_version` dưới mục kỹ thuật; có thao tác GV `Needs review / Reviewed: accepted / Reviewed: rejected` lưu trong UGTM.
6. **Bổ sung kiểm thử contract**: `POST` cùng `submission_id/version/bytes` là idempotent, thay version có report riêng; AI fail/timeout không làm thất bại upload; quyền GV khác không xem được report; bật/tắt classifier không phá vỡ JSON contract.

**Quy tắc đổi phiên bản:** `submission_version` là phiên bản báo cáo; `classifier_version` là phiên bản model do nhóm train; `corpus_revision` là snapshot tài liệu nguồn. Chúng không thay thế nhau. Nếu cần quét lại cùng báo cáo với model mới, nên thêm `scan_run`/`analysis_version` hoặc `rescan` có quyền; không ghi đè job cũ bằng cách dùng lại `submission_version`.

### Hợp đồng dữ liệu cần giữ ổn định

```json
{
  "schema_version": 2,
  "submission_id": "thesis-demo-001",
  "submission_version": 1,
  "model": {
    "embedding": "BAAI/bge-m3",
    "reranker": "BAAI/bge-reranker-v2-m3",
    "detector_mode": "classifier",
    "classifier_version": "candidate-lr-v1"
  },
  "summary": {
    "scanned_chunks": 42,
    "findings_returned": 8,
    "matched_sources": 3,
    "passage_groups": 4
  },
  "findings": [{
    "source_key": "source_demo_01",
    "source_page": 9,
    "submission_page": 12,
    "passage_group_id": "PG-0001",
    "dense_score": 0.78,
    "lexical_score": 0.47,
    "reranker_score": 0.83,
    "classifier_score": 0.74,
    "decision_source": "custom_classifier",
    "review_required": true
  }],
  "disclaimer": "Similarity evidence only; not a plagiarism verdict."
}
```

**Mã JSON là minh họa, không phải benchmark.** `schema_version:2` là hợp đồng API v2 của code trong tài liệu; khi triển khai, .NET và React phải dùng cùng schema. Cùng một hợp đồng JSON sử dụng `classifier_version: null` khi feature flag OFF.

## 19B. Definition of Done (MVP + giai đoạn B)

| Hạng mục | Kiểm thử/tiêu chí chấp nhận |
|---|---|
| File + API | PDF/DOCX hợp lệ nhận `202`; file sai/oversize bị từ chối; job trả trạng thái rõ |
| Indexing | Nguồn đã có vector; re-index cùng source/version không nhân đôi; nguồn chưa active không được tìm |
| Retrieval | Test copy chính xác và paraphrase tìm đúng `source_key` trong top-K trên corpus demo |
| Passage grouping | Hai hit liên tiếp theo source/query cùng group, hit xa khác group |
| Classifier OFF | Scan vẫn hoàn thành, `detector_mode=heuristic`, `classifier_score=null` |
| Feature parity | Cùng cặp đầu vào tính feature y hệt lúc train và inference; 5 feature tên/độ dài chính xác |
| Classifier ON | File `.joblib` + manifest hợp lệ; trả score/version; thiếu artifact hoặc schema mismatch fail, không tự tắt |
| Evaluation | Có bảng so sánh heuristic vs classifier (precision, recall, F1, false positives) trên cùng held-out groups |
| Privacy | Không commit nguồn thật vào GitHub, không public AI API, học liệu chỉ được lưu/index trong phạm vi phép |
| UGTM integration | Upload vẫn thành công nếu AI lỗi; FE chỉ gọi .NET; GV đúng quyền xem evidence |

## 20. Nguồn kỹ thuật tham khảo

Các nguồn dưới đây là **tham khảo kỹ thuật bên ngoài**, không phải tính năng đã được xác thực trong repo UGTM:

- BAAI BGE-M3 model card và ví dụ `BGEM3FlagModel`: <https://huggingface.co/BAAI/bge-m3>
- BGE Reranker v2 M3 và ví dụ `FlagReranker`: <https://huggingface.co/BAAI/bge-reranker-v2-m3>
- FlagEmbedding inference examples: <https://github.com/FlagOpen/FlagEmbedding/tree/master/examples/inference>
- pgvector (cosine `<=>`, exact search, HNSW, hybrid): <https://github.com/pgvector/pgvector>
- PostgreSQL pg_trgm: <https://www.postgresql.org/docs/current/pgtrgm.html>
- FastAPI upload: <https://fastapi.tiangolo.com/tutorial/request-files/>
- Celery user guide: <https://docs.celeryq.dev/en/stable/userguide/index.html>
- Docker Compose GPU: <https://docs.docker.com/compose/how-tos/gpu-support/>
- scikit-learn LogisticRegression: <https://scikit-learn.org/stable/modules/generated/sklearn.linear_model.LogisticRegression.html>
- scikit-learn GroupShuffleSplit: <https://scikit-learn.org/stable/modules/generated/sklearn.model_selection.GroupShuffleSplit.html>
- UGTM (đường dẫn do nhóm cung cấp, chưa mở được qua kết nối hiện tại): <https://github.com/karasuma-2401/UGTM>

---

### Kết luận

**Bắt đầu bằng repo `UGTM-Plagiarism-AI` gồm 4 endpoint nội bộ, 2 background tasks, PostgreSQL+pgvector và pipeline Hybrid Search + Reranking + Passage Alignment.** GPU có thể bổ sung khi nhóm xin/thuê được; tập trung code giao diện và nghiệp vụ độc lập trước. **Tiếp theo mới train classifier trên cặp đoạn có nhãn, xuất model/manifest, bật bằng feature flag và kiểm thử trước/sau.** Sau khi demo độc lập ổn định, kết nối vào **luồng nộp báo cáo cuối kỳ** của UGTM qua background event handler. Tiếp nhận corpus 20–50 khóa luận từ thư viện bằng pipeline indexing riêng, giữ nguyên cơ chế bảo mật/quyền xem của hệ thống trường. Ưu tiên chức năng **tìm nguồn và giải thích bằng chứng**, không tạo “tỷ lệ đạo văn” thiếu cơ sở.
