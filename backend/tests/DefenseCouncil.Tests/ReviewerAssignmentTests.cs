using System.Text.Json;
using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ReviewerAssignmentTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);
    private const string Checksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Creation_binds_the_approved_snapshot_without_cancellation_or_legacy_metadata()
    {
        var assignment = Create(checksum: "  " + Checksum.ToUpperInvariant() + "  ");

        Assert.True(assignment.IsActive);
        Assert.False(assignment.IsLegacy);
        Assert.Equal(1, assignment.AssignmentOrder);
        Assert.Equal(2, assignment.ReportVersionNo);
        Assert.NotNull(assignment.AdvisorEvaluationId);
        Assert.NotNull(assignment.FinalReportId);
        Assert.NotNull(assignment.SubmissionFileId);
        Assert.NotEqual(assignment.ReviewerId, assignment.AdvisorIdAtAssignment);
        Assert.Equal(Checksum, assignment.SubmissionChecksumSha256);
        Assert.Equal(Now, assignment.AssignedAt);
        Assert.Null(assignment.CancelledBy);
        Assert.Null(assignment.CancelledAt);
        Assert.Null(assignment.CancellationReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Creation_rejects_each_empty_identifier(int emptyIdentifier)
        => Assert.Throws<ArgumentException>(() => Create(emptyIdentifier: emptyIdentifier));

    [Fact]
    public void A_reviewer_cannot_be_the_advisor_even_when_the_assigner_is_someone_else()
    {
        var advisor = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => ReviewerAssignment.Create(
            Guid.NewGuid(), Guid.NewGuid(), advisor, advisor, Guid.NewGuid(), Guid.NewGuid(),
            1, Guid.NewGuid(), Checksum, Guid.NewGuid(), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Creation_requires_a_positive_report_version(int version)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Create(version: version));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-checksum")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Creation_requires_a_valid_sha256_binding(string checksum)
        => Assert.Throws<ArgumentException>(() => Create(checksum: checksum));

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Creation_requires_server_utc_time(DateTimeKind kind)
        => Assert.Throws<ArgumentException>(() => Create(assignedAt: DateTime.SpecifyKind(Now, kind)));

    [Fact]
    public void Cancellation_preserves_the_original_assigner_and_snapshot_and_cannot_be_overwritten()
    {
        var assignment = Create();
        var assigner = assignment.AssignedBy;
        var reportId = assignment.FinalReportId;
        var actor = Guid.NewGuid();

        Assert.True(assignment.TryCancel(actor, Now.AddMinutes(1), "  Điều chỉnh phân công.  ", false));
        Assert.False(assignment.IsActive);
        Assert.Equal(actor, assignment.CancelledBy);
        Assert.Equal(Now.AddMinutes(1), assignment.CancelledAt);
        Assert.Equal("Điều chỉnh phân công.", assignment.CancellationReason);
        Assert.Equal(assigner, assignment.AssignedBy);
        Assert.Equal(Now, assignment.AssignedAt);
        Assert.Equal(reportId, assignment.FinalReportId);
        Assert.Equal(Checksum, assignment.SubmissionChecksumSha256);
        Assert.False(assignment.TryCancel(Guid.NewGuid(), Now.AddMinutes(2), "Không được ghi đè.", false));
        Assert.Equal(actor, assignment.CancelledBy);
        Assert.Equal("Điều chỉnh phân công.", assignment.CancellationReason);
    }

    [Fact]
    public void Any_existing_evaluation_prevents_cancellation_without_mutation()
    {
        var assignment = Create();

        Assert.False(assignment.TryCancel(Guid.NewGuid(), Now.AddMinutes(1), "Không được hủy.", true));
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.CancelledAt);
        Assert.Null(assignment.CancelledBy);
        Assert.Null(assignment.CancellationReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Cancellation_requires_a_reason_without_partial_mutation(string reason)
    {
        var assignment = Create();
        Assert.Throws<ArgumentException>(() => assignment.TryCancel(Guid.NewGuid(), Now, reason, false));
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.CancelledBy);
    }

    [Fact]
    public void Cancellation_rejects_invalid_actor_time_and_excessive_reason_before_mutating()
    {
        var assignment = Create();
        Assert.Throws<ArgumentException>(() => assignment.TryCancel(Guid.Empty, Now, "Lý do", false));
        Assert.Throws<ArgumentException>(() => assignment.TryCancel(Guid.NewGuid(),
            DateTime.SpecifyKind(Now, DateTimeKind.Unspecified), "Lý do", false));
        Assert.Throws<ArgumentOutOfRangeException>(() => assignment.TryCancel(Guid.NewGuid(),
            Now.AddTicks(-1), "Lý do", false));
        Assert.Throws<ArgumentOutOfRangeException>(() => assignment.TryCancel(Guid.NewGuid(),
            Now, new string('a', 1_001), false));
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.CancelledBy);
        Assert.Null(assignment.CancelledAt);
    }

    [Fact]
    public void The_reason_limit_counts_unicode_characters_as_postgresql_does()
    {
        var assignment = Create();
        var reason = string.Concat(Enumerable.Repeat("😀", 1_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => assignment.TryCancel(Guid.NewGuid(), Now, reason + "a", false));
        Assert.True(assignment.TryCancel(Guid.NewGuid(), Now, reason, false));
        Assert.Equal(reason, assignment.CancellationReason);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public void Receipts_preserve_the_success_status_and_original_json(int status)
    {
        var receipt = CreateReceipt(status);
        const string payload = "{\"assignmentVersion\":42,\"isActive\":true}";
        receipt.SetResponsePayload(payload);
        Assert.Equal(status, receipt.ResponseStatusCode);
        Assert.Equal(payload, receipt.ResponsePayloadJson);
        Assert.Equal(Checksum, receipt.RequestFingerprint);
        Assert.Equal(Now, receipt.CreatedAtUtc);
    }

    [Theory]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(409)]
    [InlineData(500)]
    public void Receipts_cannot_store_non_contract_statuses(int status)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateReceipt(status));

    [Fact]
    public void Receipts_reject_invalid_payloads_without_changing_the_saved_response()
    {
        var receipt = CreateReceipt(201);
        receipt.SetResponsePayload("{\"id\":1}");
        Assert.ThrowsAny<JsonException>(() => receipt.SetResponsePayload("not-json"));
        Assert.Throws<ArgumentException>(() => receipt.SetResponsePayload("[]"));
        Assert.Throws<ArgumentException>(() => receipt.SetResponsePayload("null"));
        Assert.Equal("{\"id\":1}", receipt.ResponsePayloadJson);
    }

    [Fact]
    public void Receipts_require_identifiers_operation_fingerprint_and_utc_time()
    {
        Assert.Throws<ArgumentException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.Empty, Guid.NewGuid(), "Create", Guid.NewGuid(), Checksum, 201, Now));
        Assert.Throws<ArgumentException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.NewGuid(), Guid.Empty, "Create", Guid.NewGuid(), Checksum, 201, Now));
        Assert.Throws<ArgumentException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Create", Guid.Empty, Checksum, 201, Now));
        Assert.Throws<ArgumentException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), " ", Guid.NewGuid(), Checksum, 201, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), new string('x', 101), Guid.NewGuid(), Checksum, 201, Now));
        Assert.Throws<ArgumentException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Create", Guid.NewGuid(), "invalid", 201, Now));
        Assert.Throws<ArgumentException>(() => ReviewerAssignmentIdempotencyRecord.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Create", Guid.NewGuid(), Checksum, 201,
            DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    private static ReviewerAssignment Create(int emptyIdentifier = -1, int version = 2,
        string checksum = Checksum, DateTime? assignedAt = null)
    {
        var ids = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        if (emptyIdentifier >= 0) ids[emptyIdentifier] = Guid.Empty;
        return ReviewerAssignment.Create(ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], version,
            ids[6], checksum, ids[7], assignedAt ?? Now);
    }

    private static ReviewerAssignmentIdempotencyRecord CreateReceipt(int status)
        => ReviewerAssignmentIdempotencyRecord.Create(Guid.NewGuid(), Guid.NewGuid(),
            "ReviewerAssignment.Create", Guid.NewGuid(), Checksum.ToUpperInvariant(), status, Now);
}
