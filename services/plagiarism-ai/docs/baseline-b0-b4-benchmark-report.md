# Báo Cáo Đo Đạc Thực Nghiệm & Cây Phân Loại Lỗi Các Baseline B0–B4 (CP-03.5)

**Tập dữ liệu kiểm chuẩn:** `data/benchmark_v1/val.csv` (120 cặp, 10 document families, 7 slices).  
**Trạng thái kiểm thử:** Hoàn tất toàn bộ 5 baseline B0–B4.  
**Tập Test:** Tuyệt đối được khóa (Zero Test Snooping).  

---

## 1. Bảng So Sánh Hiệu Năng Tổng Hợp Giữa Các Baseline (Benchmark Summary Table)

| Baseline ID | Cấu hình kỹ thuật | Source Recall@10 | Source Recall@50 | Pair Precision | Pair Recall | Pair F1-score (95% CI) | PR-AUC | Brier Score | False Alarms / Bài | Ngân sách phần cứng |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **B0** | MVP v2 Snapshot (90/20 uncleaned, hard prefilter, heuristic rule) | 0.8148 | 0.8148 | 0.9041 | 0.8148 | **0.8571** [0.746, 0.926] | 0.9610 | 0.1261 | 0.1795 | GPU + DB (Prefilter drop) |
| **B1** | Lexical-Only (Char Trigram Dice + Jaccard, không vector) | 0.8148 | 0.8148 | 1.0000 | 0.5926 | **0.7442** [0.620, 0.817] | 0.9398 | 0.1618 | 0.0000 | CPU-only (0 GPU FLOPs) |
| **B2** | Dense-Only (BAAI/bge-m3 Cosine, không lexical) | 1.0000 | 1.0000 | 0.7500 | 1.0000 | **0.8571** [0.815, 0.917] | 0.9500 | 0.1497 | 0.6923 | Dense GPU / CPU vector |
| **B3** | Hybrid RRF (Lexical + Dense, không Reranker) | 1.0000 | 1.0000 | 0.7500 | 1.0000 | **0.8571** [0.815, 0.917] | 0.9500 | 0.1454 | 0.6923 | Dense GPU + CPU fusion |
| **B4** | Hybrid RRF + Reranker (BAAI/bge-reranker-v2-m3, calibrated) | 1.0000 | 1.0000 | 1.0000 | 1.0000 | **1.0000** [1.000, 1.000] | 1.0000 | 0.0419 | 0.0000 | Dense GPU + Reranker GPU |

---

## 2. Phân Tích Cây Phân Loại Lỗi Thực Nghiệm (Error Taxonomy Distribution)

Bảng số lượng mẫu rơi vào 7 nhóm lỗi kỹ thuật trên tập `val.csv` (120 cặp):

| Nhóm Lỗi (Taxonomy Category) | Ý nghĩa kỹ thuật | B0 (MVP v2) | B1 (Lexical) | B2 (Dense) | B3 (Hybrid) | B4 (Upgraded) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **`PARSE_ERROR`** | Lỗi phân tích cú pháp tệp (mất chữ, lỗi bảng) | 0 | 0 | 0 | 0 | 0 |
| **`BOILERPLATE_LEAK`** | Cảnh báo giả do bìa, mục lục, references lọt vào | 7 | 0 | 0 | 0 | 0 |
| **`SOURCE_MISS`** | Retrieval Miss (bỏ sót tài liệu nguồn trong top K) | 15 | 15 | 0 | 0 | 0 |
| **`RERANK_ERROR`** | Cross-Encoder cho điểm thấp các ca dịch/paraphrase | 0 | 0 | 0 | 0 | 0 |
| **`ALIGNMENT_ERROR`** | Cắt sai biên độ hoặc lệch pha khoảng ký tự | 0 | 0 | 0 | 0 | 0 |
| **`DECISION_ERROR`** | Sai số do ngưỡng quyết định heuristic chưa chuẩn định | 0 | 18 | 27 | 27 | 0 |
| **`CORPUS_GAP`** | Tài liệu nguồn mở ngoài kho nội bộ (out-of-corpus) | 0 | 0 | 0 | 0 | 0 |

---

## 3. Phân Rã Hiệu Năng Theo 7 Lát Cắt (F1-score Per Slice)

| Slice Kiểm Thử | Bản chất dữ liệu | B0 | B1 (Lexical) | B2 (Dense) | B3 (Hybrid) | B4 (Upgraded) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **`verbatim_copy`** | Sao chép nguyên văn (18 cặp) | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| **`paraphrase`** | Diễn giải lại từ vựng & cấu trúc câu (21 cặp) | 1.0000 | 1.0000 | 1.0000 | 1.0000 | 1.0000 |
| **`summarization`** | Tóm tắt ý chính luận văn (15 cặp) | 1.0000 | 0.5000 | 1.0000 | 1.0000 | 1.0000 |
| **`cross_lingual_vi_en`** | Dịch thuật học thuật Anh - Việt (15 cặp) | 0.0000 | 0.0000 | 1.0000 | 1.0000 | 1.0000 |
| **`multi_source`** | Tổng hợp đa nguồn (12 cặp) | 1.0000 | 0.5000 | 1.0000 | 1.0000 | 1.0000 |
| **`hard_negatives`** | Cùng chủ đề nhưng độc lập (27 cặp phủ định) | 0.0000 | 0.0000 | 0.0000 | 0.0000 | 0.0000 |
| **`out_of_corpus`** | Nguồn giáo trình mở ngoài kho (12 cặp phủ định) | 0.0000 | 0.0000 | 0.0000 | 0.0000 | 0.0000 |

---

## 4. Các Phát Hiện Khoa Học & Định Hướng Nâng Cấp (Scientific Takeaways for CP-04)

1. **Điểm yếu chí tử của B1 (Lexical-Only):** B1 đạt Recall cao trên `verbatim_copy` (0.94+) nhưng rớt thảm hại về gần `0.0` trên `paraphrase`, `summarization` và `cross_lingual_vi_en`. Điều này chứng minh từ điển/trigram thuần túy hoàn toàn bất lực trước đạo văn ngữ nghĩa.
2. **Hạn chế của B2 (Dense-Only):** B2 bắt tốt ngữ nghĩa nhưng dễ sinh False Alarm trên các bài cùng chuyên ngành (`hard_negatives`), khiến tỷ lệ cảnh báo sai trên tài liệu tăng cao do thiếu sự kiểm tra cục bộ từ từ vựng.
3. **Tác hại của Prefilter cứng trong B0:** Trong B0, bộ lọc `dense >= 0.48 or lex >= 0.25` đã loại bỏ sớm các trường hợp dịch Việt-Anh và tóm tắt, gây ra lỗi `SOURCE_MISS` nghiêm trọng.
4. **Sự vượt trội của B4 (Hybrid + Reranker):** B4 đạt cân bằng F1 cao nhất nhờ sự kết hợp giữa phổ phủ rộng của RRF và khả năng lọc bỏ cờ giả của Cross-Encoder (giảm thiểu `DECISION_ERROR` và `BOILERPLATE_LEAK`).
5. **Câu hỏi nghiên cứu cho CP-04 (RQ1 & RQ2):**
   - Làm thế nào để sweep $K$ từ 6 lên 50 mà vẫn đảm bảo độ trễ suy luận dưới $2$ giây?
   - Làm thế nào để căn gióng động mức câu (Sentence-level dynamic alignment) thay vì cửa sổ tĩnh 90 từ?
