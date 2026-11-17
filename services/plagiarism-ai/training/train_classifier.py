import argparse
import csv
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path

import joblib
import numpy as np
import sklearn
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import (
    confusion_matrix,
    f1_score,
    precision_score,
    recall_score,
)
from sklearn.model_selection import GroupShuffleSplit
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from app.features import (
    FEATURE_NAMES,
    FEATURE_SCHEMA,
    build_features,
    feature_vector,
)
from app.models import encode_texts, rerank_pairs


def load_pairs(path: Path):
    with path.open(newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    for row in rows:
        if row.get("label") not in {"0", "1"}:
            raise ValueError(f"Invalid label for pair {row.get('pair_id')}")
        if not all(
            row.get(k)
            for k in ("pair_id", "group_id", "query_text", "source_text")
        ):
            raise ValueError(f"Missing columns for pair {row}")
    if len({r["group_id"] for r in rows}) < 6:
        raise ValueError("Need more independent document groups")
    return rows


def build_dataset(rows, batch_size=8):
    X = []
    for start in range(0, len(rows), batch_size):
        batch = rows[start : start + batch_size]
        queries = [r["query_text"] for r in batch]
        sources = [r["source_text"] for r in batch]
        qvecs = encode_texts(queries, batch_size=batch_size)
        svecs = encode_texts(sources, batch_size=batch_size)
        rerank = rerank_pairs(
            [[q, s] for q, s in zip(queries, sources)],
            batch_size=batch_size,
        )
        for i, row in enumerate(batch):
            denom = float(np.linalg.norm(qvecs[i]) * np.linalg.norm(svecs[i]))
            cosine = (
                float(np.dot(qvecs[i], svecs[i]) / denom) if denom else 0.0
            )
            features = build_features(
                row["query_text"],
                row["source_text"],
                cosine,
                float(rerank[i]),
            )
            X.append(feature_vector(features))
    return np.asarray(X, dtype=np.float64)


def require_both_classes(y, name):
    if len(set(int(v) for v in y)) != 2:
        raise ValueError(
            f"{name} needs both positive and negative labels; "
            "add more independent source groups"
        )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", default="training_data/pairs.csv")
    parser.add_argument(
        "--output", default="artifacts/suspicious_passage_classifier.joblib"
    )
    args = parser.parse_args()

    rows = load_pairs(Path(args.input))
    X = build_dataset(rows)
    y = np.asarray([int(r["label"]) for r in rows])
    groups = np.asarray([r["group_id"] for r in rows])

    # Hold-out TEST không đụng vào trong quá trình chọn threshold.
    split1 = GroupShuffleSplit(n_splits=1, test_size=0.20, random_state=310)
    trainval_idx, test_idx = next(split1.split(X, y, groups))
    split2 = GroupShuffleSplit(n_splits=1, test_size=0.25, random_state=311)
    inner_train, inner_val = next(
        split2.split(X[trainval_idx], y[trainval_idx], groups[trainval_idx])
    )
    train_idx = trainval_idx[inner_train]
    val_idx = trainval_idx[inner_val]
    for name, ids in [
        ("train", train_idx),
        ("validation", val_idx),
        ("test", test_idx),
    ]:
        require_both_classes(y[ids], name)
    assert not (set(groups[train_idx]) & set(groups[val_idx]))
    assert not (set(groups[train_idx]) & set(groups[test_idx]))
    assert not (set(groups[val_idx]) & set(groups[test_idx]))

    clf = Pipeline(
        [
            ("scale", StandardScaler()),
            (
                "lr",
                LogisticRegression(
                    max_iter=3000,
                    class_weight="balanced",
                    random_state=310,
                ),
            ),
        ]
    )
    clf.fit(X[train_idx], y[train_idx])
    val_scores = clf.predict_proba(X[val_idx])[:, 1]
    candidates = np.arange(0.25, 0.86, 0.05)
    threshold = float(
        max(
            candidates,
            key=lambda t: f1_score(
                y[val_idx], val_scores >= t, zero_division=0
            ),
        )
    )

    test_scores = clf.predict_proba(X[test_idx])[:, 1]
    predicted = (test_scores >= threshold).astype(int)
    metrics = {
        "precision": float(
            precision_score(y[test_idx], predicted, zero_division=0)
        ),
        "recall": float(recall_score(y[test_idx], predicted, zero_division=0)),
        "f1": float(f1_score(y[test_idx], predicted, zero_division=0)),
        "confusion_matrix": confusion_matrix(y[test_idx], predicted).tolist(),
    }
    out = Path(args.output)
    out.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(clf, out)
    artifact_sha256 = hashlib.sha256(out.read_bytes()).hexdigest()
    manifest = {
        "model_version": "candidate-lr-v1",
        "feature_schema": FEATURE_SCHEMA,
        "feature_names": FEATURE_NAMES,
        "embedding_model": "BAAI/bge-m3",
        "reranker_model": "BAAI/bge-reranker-v2-m3",
        "artifact_sha256": artifact_sha256,
        "sklearn_version": sklearn.__version__,
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

