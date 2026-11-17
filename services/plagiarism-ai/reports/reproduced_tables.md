# Reproduced Benchmark Evaluation Tables (CP-06.2)

- **Verification Timestamp:** 2026-11-01 04:51:28 UTC
- **Dataset Split:** `data/benchmark_v1/test.csv` (120 pairs, 19 document families)
- **Split SHA-256:** `7b8432a8e7f480ad3589c26e9220acac5d9afd7e89b22c914a9ac2cf277a340a` (Verified identical to split_manifest.json)
- **Bootstrap Parameters:** Resampling Cluster: `query_family_id`, N = 1000, Seed = 42, Level = 95%
- **Parity Status:** 100% Bit-for-bit Parity with Official Reports

---

## 1. Overall Benchmark Comparison (Reproduced)

| System / Baseline | Description | Source Recall@50 | Span IoU | Pair Precision | Pair Recall | Pair F1 [95% CI] | PR-AUC | False Alarms / doc |
|---|---|---|---|---|---|---|---|---|
| **B0** (MVP v2) | 90/20 window, hard prefilter | 0.7778 | 0.7531 | 0.9104 | 0.7531 | **0.8243** `[0.6990, 0.8889]` | 0.9488 | 0.1538 |
| **B1** (Lexical Only) | Trigram Dice + Jaccard (>= 0.65) | 0.8148 | 0.5432 | 1.0000 | 0.5432 | **0.7040** `[0.5825, 0.7714]` | 0.9357 | 0.0000 |
| **B2** (Dense Only) | BGE-M3 Cosine (>= 0.68) | 1.0000 | 0.9506 | 1.0000 | 0.9506 | **0.9747** `[0.9500, 0.9940]` | 1.0000 | 0.0000 |
| **B3** (Hybrid RRF) | Lexical + Dense (No Reranker) | 1.0000 | 0.9630 | 1.0000 | 0.9630 | **0.9811** `[0.9595, 1.0000]` | 1.0000 | 0.0000 |
| **B4** (Hybrid + Reranker) | BGE-M3 + BGE-Reranker-v2-m3 | 1.0000 | 0.9753 | 1.0000 | 0.9753 | **0.9875** `[0.9689, 1.0000]` | 1.0000 | 0.0000 |
| **RC1** (Proposed) | Block parser + Paragraph + Reranker + Platt + Spans | **1.0000** | **0.9031** | **1.0000** | **0.9753** | **0.9875** `[0.9689, 1.0000]` | **1.0000** | **0.0000** |

---

## 2. RC1 Slice Breakdown (Reproduced)

| Evaluation Slice | Support | TP | FP | FN | Precision | Recall | F1 Score |
|---|---|---|---|---|---|---|---|
| `cross_lingual_vi_en` | 15 | 15 | 0 | 0 | 1.0000 | 1.0000 | **1.0000** |
| `hard_negatives` | 27 | 0 | 0 | 0 | 0.0000 | 0.0000 | **0.0000** |
| `multi_source` | 12 | 12 | 0 | 0 | 1.0000 | 1.0000 | **1.0000** |
| `out_of_corpus` | 12 | 0 | 0 | 0 | 0.0000 | 0.0000 | **0.0000** |
| `paraphrase` | 21 | 21 | 0 | 0 | 1.0000 | 1.0000 | **1.0000** |
| `summarization` | 15 | 13 | 0 | 2 | 1.0000 | 0.8667 | **0.9286** |
| `verbatim_copy` | 18 | 18 | 0 | 0 | 1.0000 | 1.0000 | **1.0000** |
