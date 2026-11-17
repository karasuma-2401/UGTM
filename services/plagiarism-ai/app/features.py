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
    return Counter(text[i : i + 3] for i in range(len(text) - 2))


def dice_similarity(a: str, b: str) -> float:
    x, y = char_trigrams(a), char_trigrams(b)
    denom = sum(x.values()) + sum(y.values())
    return (2 * sum((x & y).values()) / denom) if denom else 0.0


def build_features(
    query_text: str,
    source_text: str,
    semantic_cosine: float,
    reranker_score: float,
) -> dict:
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

