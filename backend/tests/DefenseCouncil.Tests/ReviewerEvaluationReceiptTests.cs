using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Tests;

public sealed class ReviewerEvaluationReceiptTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public void Receipt_keeps_actor_operation_key_status_and_final_version_json(int status)
    {
        var actor = Guid.NewGuid(); var key = Guid.NewGuid();
        var receipt = ReviewerEvaluationIdempotencyRecord.Create(Guid.NewGuid(), actor, "ReviewerEvaluation.Save", key,
            new string('A', 64), status, DateTime.UtcNow);
        receipt.SetResponsePayload("{\"evaluationVersion\":123,\"score\":null}");
        Assert.Equal(actor, receipt.ActorId); Assert.Equal(key, receipt.IdempotencyKey);
        Assert.Equal(status, receipt.ResponseStatusCode); Assert.Equal(new string('a', 64), receipt.RequestFingerprint);
        Assert.Contains("123", receipt.ResponsePayloadJson);
        Assert.Throws<ArgumentException>(() => receipt.SetResponsePayload("[]"));
        Assert.Contains("123", receipt.ResponsePayloadJson);
    }

    [Fact]
    public void Invalid_scope_fingerprint_status_and_time_cannot_create_success_receipts()
    {
        var id = Guid.NewGuid(); var actor = Guid.NewGuid(); var key = Guid.NewGuid(); var at = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() => ReviewerEvaluationIdempotencyRecord.Create(id, Guid.Empty, "ReviewerEvaluation.Save", key, new string('a', 64), 201, at));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluationIdempotencyRecord.Create(id, actor, "ReviewerEvaluation.Save", Guid.Empty, new string('a', 64), 201, at));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewerEvaluationIdempotencyRecord.Create(id, actor, " Save ", key, new string('a', 64), 201, at));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluationIdempotencyRecord.Create(id, actor, "ReviewerEvaluation.Save", key, new string('z', 64), 201, at));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReviewerEvaluationIdempotencyRecord.Create(id, actor, "ReviewerEvaluation.Save", key, new string('a', 64), 409, at));
        Assert.Throws<ArgumentException>(() => ReviewerEvaluationIdempotencyRecord.Create(id, actor, "ReviewerEvaluation.Save", key, new string('a', 64), 201, DateTime.SpecifyKind(at, DateTimeKind.Unspecified)));
    }
}
