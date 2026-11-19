import pytest

from app.documents import TextPage, make_chunks, normalize


def test_normalize():
    assert normalize("Xin  chào\nUIT\t!") == "Xin chào UIT !"


def test_chunk_offsets_are_valid():
    text = " ".join(f"word{i}" for i in range(250))
    result = make_chunks([TextPage(3, text)], size=90, overlap=20)

    assert len(result) >= 3
    assert all(chunk.page_number == 3 for chunk in result)
    assert all(
        text[chunk.start_offset : chunk.end_offset] == chunk.text
        for chunk in result
    )
    assert result[1].start_offset < result[0].end_offset


def test_overlapping_tail_is_emitted_when_long_enough():
    text = " ".join(f"word{i}" for i in range(95))
    result = make_chunks(
        [TextPage(None, text)],
        size=90,
        overlap=20,
        min_words=8,
    )

    assert len(result) == 2
    assert len(result[-1].text.split()) == 25


def test_short_tail_is_not_emitted():
    text = " ".join(f"word{i}" for i in range(75))
    result = make_chunks(
        [TextPage(None, text)],
        size=70,
        overlap=0,
        min_words=8,
    )

    assert len(result) == 1


@pytest.mark.parametrize(
    ("size", "overlap"),
    [(0, 0), (10, 10), (10, 11), (10, -1)],
)
def test_invalid_chunk_window(size: int, overlap: int):
    with pytest.raises(ValueError, match="overlap must be smaller than size"):
        make_chunks([TextPage(1, "some valid words in a test page")], size, overlap)

