using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public enum ReviewerEvaluationAction { SaveDraft, Submit }
public sealed record SaveReviewerEvaluationCommand(Guid AssignmentId, ReviewerEvaluationAction Action,
    uint ExpectedAssignmentVersion, uint? ExpectedEvaluationVersion, decimal? Score, string? Comment,
    DefenseApprovalStatus? DefenseApproval, Guid IdempotencyKey);
public sealed record SaveReviewerEvaluationResponse(ReviewerEvaluationDto Evaluation, int StatusCode);

public static class ReviewerEvaluationInput
{
    public static Result<SaveReviewerEvaluationCommand> Normalize(SaveReviewerEvaluationCommand command)
    {
        if (command.AssignmentId == Guid.Empty || command.IdempotencyKey == Guid.Empty
            || command.ExpectedAssignmentVersion == 0 || command.ExpectedEvaluationVersion == 0
            || !Enum.IsDefined(command.Action) || (command.DefenseApproval.HasValue && !Enum.IsDefined(command.DefenseApproval.Value)))
            return Result.Failure<SaveReviewerEvaluationCommand>(ReviewerEvaluationErrors.InvalidRequest);
        if (command.Score is < 0m or > 10m) return Result.Failure<SaveReviewerEvaluationCommand>(ReviewerEvaluationErrors.InvalidScore);
        (decimal? Score, string? Comment, DefenseApprovalStatus? DefenseApproval) content;
        try { content = ReviewerEvaluation.NormalizeContent(command.Score, command.Comment, command.DefenseApproval, false); }
        catch (ArgumentException) { return Result.Failure<SaveReviewerEvaluationCommand>(ReviewerEvaluationErrors.InvalidComment); }
        if (command.Action == ReviewerEvaluationAction.Submit && (content.Score is null || content.Comment is null || content.DefenseApproval is null))
            return Result.Failure<SaveReviewerEvaluationCommand>(ReviewerEvaluationErrors.IncompleteEvaluation);
        return Result.Success(command with { Score = content.Score, Comment = content.Comment, DefenseApproval = content.DefenseApproval });
    }

    public static string Fingerprint(SaveReviewerEvaluationCommand normalized)
    {
        var json = JsonSerializer.Serialize(new object?[] { normalized.AssignmentId.ToString("N"), normalized.Action.ToString(),
            normalized.ExpectedAssignmentVersion, normalized.ExpectedEvaluationVersion,
            normalized.Score?.ToString("F2", CultureInfo.InvariantCulture), normalized.Comment, normalized.DefenseApproval?.ToString() });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}
