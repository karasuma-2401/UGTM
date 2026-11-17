from app.features import FEATURE_NAMES, build_features, feature_vector


def test_feature_shape_and_bounds():
    features = build_features("mô hình học sâu", "mạng nơ-ron học sâu", 0.74, 0.81)
    assert list(features.keys()) == FEATURE_NAMES
    assert len(feature_vector(features)) == 5
    assert 0 <= features["token_jaccard"] <= 1
    assert 0 <= features["char_trigram_dice"] <= 1
    assert 0 <= features["length_ratio"] <= 1


def test_identical_text_is_high_lexical():
    f = build_features("ABC là phương pháp X", "ABC là phương pháp X", 1, 1)
    assert f["char_trigram_dice"] == 1
    assert f["token_jaccard"] == 1

