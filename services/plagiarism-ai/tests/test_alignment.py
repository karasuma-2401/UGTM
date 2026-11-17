from app.alignment import group_adjacent_findings


def test_adjacent_passages_are_grouped():
    rows = [
        {
            "source_key": "a",
            "source_version": 1,
            "submission_chunk_no": 3,
            "source_chunk_no": 7,
        },
        {
            "source_key": "a",
            "source_version": 1,
            "submission_chunk_no": 4,
            "source_chunk_no": 8,
        },
        {
            "source_key": "a",
            "source_version": 1,
            "submission_chunk_no": 8,
            "source_chunk_no": 18,
        },
    ]
    group_adjacent_findings(rows)
    assert rows[0]["passage_group_id"] == rows[1]["passage_group_id"]
    assert rows[0]["passage_group_id"] != rows[2]["passage_group_id"]


def test_different_source_versions_are_not_grouped():
    rows = [
        {
            "source_key": "a",
            "source_version": 1,
            "submission_chunk_no": 3,
            "source_chunk_no": 7,
        },
        {
            "source_key": "a",
            "source_version": 2,
            "submission_chunk_no": 4,
            "source_chunk_no": 8,
        },
    ]

    group_adjacent_findings(rows)

    assert rows[0]["passage_group_id"] != rows[1]["passage_group_id"]

