using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.AcademicPeriods.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;

public sealed class ReviewerDefenseEligibilityContract(IReviewerAssignmentThesisReader thesis,
    IReviewerAssignmentTopicReader topics, IReviewerAssignmentBatchReader batches, IReviewerAssignmentApprovalReader approvals,
    IReviewerAssignmentRepository assignments, IReviewerEvaluationRepository evaluations, IReviewerDefenseRuleReader rules)
    : IReviewerDefenseEligibilityContract
{
    public Task<ReviewerDefenseEligibilityContractDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
        => ReadAsync(groupId, false, cancellationToken);
    public Task<ReviewerDefenseEligibilityContractDto?> LockForSchedulingAsync(Guid groupId, CancellationToken cancellationToken = default)
        => ReadAsync(groupId, true, cancellationToken);

    private async Task<ReviewerDefenseEligibilityContractDto?> ReadAsync(Guid groupId, bool locked, CancellationToken token)
    {
        // LockAsync requires the caller's coordinator transaction, including when the group is missing.
        var group = locked ? await thesis.LockAsync(groupId, token) : await thesis.GetAsync(groupId, token);
        if (group is null) return null;
        var result = new ReviewerDefenseEligibilityContractDto(groupId, group.BatchId == Guid.Empty ? null : group.BatchId, null, null, null, null, null, null, null, false, null);
        var topic = locked ? await topics.LockAsync(group.TopicId, token) : await topics.GetAsync(group.TopicId, token);
        if (group.TopicId == Guid.Empty || !group.BatchId.HasValue || group.BatchId == Guid.Empty
            || topic is null || topic.BatchId != group.BatchId || topic.AdvisorId == Guid.Empty)
            return Block(result, ReviewerDefenseEligibilityReason.ContextInvalid);
        var batch = locked ? await batches.LockAsync(topic.BatchId, token) : await batches.GetAsync(topic.BatchId, token);
        var batchStatus = batch is null ? null : await rules.GetBatchStatusAsync(topic.BatchId, token);
        if (batchStatus is not ("Draft" or "Open" or "Closed" or "Archived"))
            return Block(result, ReviewerDefenseEligibilityReason.ContextInvalid);
        result = result with { BatchStatus = batchStatus };
        if (!group.IsReady || !group.IsUnderEvaluation) return Block(result, ReviewerDefenseEligibilityReason.GroupNotReady);

        // Order matches the writer protocol; approval stays locked before assignment/evaluation.
        var approval = await approvals.GetAsync(groupId, locked, token);
        var assignment = await assignments.GetActiveByGroupAsync(groupId, token);
        if (locked && assignment is not null) assignment = await assignments.LockByIdAsync(assignment.Id, token);
        if (assignment is null || !assignment.IsActive || assignment.GroupId != groupId)
            return Block(result, ReviewerDefenseEligibilityReason.AssignmentMissing);
        result = result with { AssignmentId = assignment.Id };
        if (assignment.IsLegacy) return Block(result, ReviewerDefenseEligibilityReason.LegacyAssignment);
        if (assignment.ReviewerId == topic.AdvisorId) return Block(result, ReviewerDefenseEligibilityReason.AdvisorConflict);
        if (approval is null || approval.GroupId != groupId || approval.AdvisorEvaluationId != assignment.AdvisorEvaluationId
            || group.FinalReportId != assignment.FinalReportId || group.ReportVersionNo != assignment.ReportVersionNo
            || group.SubmissionFileId != assignment.SubmissionFileId || approval.FinalReportId != assignment.FinalReportId
            || approval.ReportVersionNo != assignment.ReportVersionNo || approval.SubmissionFileId != assignment.SubmissionFileId
            || !string.Equals(group.SubmissionChecksumSha256, assignment.SubmissionChecksumSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(approval.SubmissionChecksumSha256, assignment.SubmissionChecksumSha256, StringComparison.OrdinalIgnoreCase))
            return Block(result, ReviewerDefenseEligibilityReason.BindingInvalid);

        var evaluation = locked ? await evaluations.LockByAssignmentAsync(assignment.Id, token) : await evaluations.GetByAssignmentAsync(assignment.Id, token);
        if (evaluation is null) return Block(result, ReviewerDefenseEligibilityReason.EvaluationMissing);
        result = result with { EvaluationId = evaluation.Id, EvaluationVersion = evaluation.Version };
        if (evaluation.IsLegacy) return Block(result, ReviewerDefenseEligibilityReason.LegacyEvaluation);
        if (evaluation.Status == ReviewerEvaluationStatus.Draft) return Block(result, ReviewerDefenseEligibilityReason.EvaluationNotSubmitted);
        if (!IsValidSubmission(evaluation)) return Block(result, ReviewerDefenseEligibilityReason.ContextInvalid);
        if (evaluation.IsIgnoredInCalculation) return Block(result, ReviewerDefenseEligibilityReason.EvaluationExcluded);

        ReviewerDefenseRuleContractDto? rule;
        try { rule = locked ? await rules.LockActiveAsync(topic.BatchId, token) : await rules.GetActiveAsync(topic.BatchId, token); }
        catch (ReviewerDefenseRuleConcurrencyException error) { throw new DefenseCouncilConcurrencyException(error); }
        if (rule is null || rule.BatchId != topic.BatchId) return Block(result, ReviewerDefenseEligibilityReason.ContextInvalid);
        var resolution = ReviewerDefenseRuleResolver.Resolve(rule);
        result = result with
        {
            BatchStatus = rule.BatchStatus,
            ScoreConfigurationId = rule.ConfigurationId,
            ScoreConfigurationVersionNo = rule.ConfigurationVersionNo,
            MinimumReviewerScore = resolution.MinimumScore
        };
        if (resolution.Outcome != ReviewerDefenseRuleOutcome.Succeeded)
            return Block(result, resolution.Outcome switch
            {
                ReviewerDefenseRuleOutcome.ConfigurationMissing => ReviewerDefenseEligibilityReason.ConfigurationMissing,
                ReviewerDefenseRuleOutcome.ConfigurationAmbiguous => ReviewerDefenseEligibilityReason.ConfigurationAmbiguous,
                _ => ReviewerDefenseEligibilityReason.ConfigurationInvalid
            });
        if (evaluation.DefenseApproval != DefenseApprovalStatus.Approved) return Block(result, ReviewerDefenseEligibilityReason.DefenseRejected);
        if (evaluation.Score < resolution.MinimumScore) return Block(result, ReviewerDefenseEligibilityReason.ScoreBelowMinimum);
        return result with { CanProceed = true };
    }

    private static ReviewerDefenseEligibilityContractDto Block(ReviewerDefenseEligibilityContractDto result, ReviewerDefenseEligibilityReason reason)
        => result with { CanProceed = false, ReasonCode = reason };
    private static bool IsValidSubmission(ReviewerEvaluation value)
    {
        if (value.Status != ReviewerEvaluationStatus.Submitted || !value.IsVisibleToStudent || value.Version == 0
            || value.CreatedBy is null || value.CreatedBy == Guid.Empty || value.CreatedAtUtc is null || value.CreatedAtUtc > value.EvaluatedAt
            || value.EvaluatedBy is null || value.EvaluatedBy == Guid.Empty || value.SubmittedBy != value.EvaluatedBy
            || value.SubmittedAtUtc != value.EvaluatedAt) return false;
        try
        {
            var content = ReviewerEvaluation.NormalizeContent(value.Score, value.Comment, value.DefenseApproval, true);
            return content.Score == value.Score && content.Comment == value.Comment;
        }
        catch (ArgumentException) { return false; }
    }
}
