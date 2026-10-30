using UGTM.Modules.AcademicPeriods.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.ReviewerAssignments;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public sealed class ReviewerAssignmentWorkflowContract(
    IReviewerAssignmentThesisReader thesisReader,
    IReviewerAssignmentTopicReader topicReader,
    IReviewerAssignmentBatchReader batchReader,
    IReviewerCandidateReader candidateReader,
    IReviewerAssignmentApprovalReader approvalReader,
    IReviewerAssignmentRepository assignments) : IReviewerAssignmentWorkflowContract
{
    public async Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(Guid groupId, Guid reviewerId,
        CancellationToken cancellationToken = default)
        => await LockCreationAsync(groupId, reviewerId, null, null, cancellationToken);

    public async Task<ReviewerAssignmentEligibilityResultContractDto> LockForCreationAsync(Guid groupId, Guid reviewerId,
        Guid expectedAdvisorEvaluationId, int expectedReportVersionNo, CancellationToken cancellationToken = default)
        => await LockCreationAsync(groupId, reviewerId, expectedAdvisorEvaluationId, expectedReportVersionNo, cancellationToken);

    private async Task<ReviewerAssignmentEligibilityResultContractDto> LockCreationAsync(Guid groupId, Guid reviewerId,
        Guid? expectedAdvisorEvaluationId, int? expectedReportVersionNo, CancellationToken cancellationToken)
    {
        var result = await ReadBindingAsync(groupId, reviewerId, true, true, cancellationToken,
            expectedAdvisorEvaluationId, expectedReportVersionNo);
        if (result.Outcome != ReviewerAssignmentEligibilityOutcome.Succeeded) return result;
        return await assignments.GetActiveByGroupAsync(groupId, cancellationToken) is not null
            ? new(ReviewerAssignmentEligibilityOutcome.AlreadyAssigned) : result;
    }

    public async Task<ReviewerAssignmentReportAccessContractDto?> GetActiveReportAccessAsync(Guid reviewerId, Guid fileId,
        CancellationToken cancellationToken = default)
    {
        if (reviewerId == Guid.Empty || fileId == Guid.Empty) return null;
        foreach (var assignment in await assignments.GetActiveByReviewerAndFileAsync(reviewerId, fileId, cancellationToken))
        {
            var result = await ReadBindingAsync(assignment.GroupId, reviewerId, false, false, cancellationToken);
            if (result.Binding is { } binding && Matches(assignment, binding))
                return new(assignment.Id, assignment.Version, reviewerId, binding);
        }
        return null;
    }

    public async Task<ReviewerAssignmentReportAccessContractDto?> LockForEvaluationAsync(Guid assignmentId, Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await assignments.GetByIdAsync(assignmentId, cancellationToken);
        if (snapshot is null || snapshot.ReviewerId != reviewerId || !snapshot.IsActive || snapshot.IsLegacy) return null;
        var result = await ReadBindingAsync(snapshot.GroupId, reviewerId, true, false, cancellationToken);
        if (result.Binding is not { } binding) return null;
        var locked = await assignments.LockByIdAsync(assignmentId, cancellationToken);
        return locked is not null && locked.ReviewerId == reviewerId && Matches(locked, binding)
            ? new(locked.Id, locked.Version, reviewerId, binding) : null;
    }

    private async Task<ReviewerAssignmentEligibilityResultContractDto> ReadBindingAsync(Guid groupId, Guid reviewerId,
        bool locked, bool requireOpen, CancellationToken token,
        Guid? expectedAdvisorEvaluationId = null, int? expectedReportVersionNo = null)
    {
        var thesis = locked ? await thesisReader.LockAsync(groupId, token) : await thesisReader.GetAsync(groupId, token);
        if (thesis is null) return new(ReviewerAssignmentEligibilityOutcome.GroupNotFound);
        var topic = locked ? await topicReader.LockAsync(thesis.TopicId, token) : await topicReader.GetAsync(thesis.TopicId, token);
        if (topic is null || thesis.BatchId is null || topic.BatchId != thesis.BatchId || topic.AdvisorId == Guid.Empty)
            return new(ReviewerAssignmentEligibilityOutcome.ContextInvalid);
        var batch = locked ? await batchReader.LockAsync(topic.BatchId, token) : await batchReader.GetAsync(topic.BatchId, token);
        if (batch is null) return new(ReviewerAssignmentEligibilityOutcome.ContextInvalid);
        var approval = await approvalReader.GetAsync(groupId, locked, token);
        if (approval is null || !thesis.IsReady || !thesis.IsUnderEvaluation
            || approval.GroupId != thesis.GroupId || approval.FinalReportId != thesis.FinalReportId
            || approval.ReportVersionNo != thesis.ReportVersionNo || approval.SubmissionFileId != thesis.SubmissionFileId
            || !string.Equals(approval.SubmissionChecksumSha256, thesis.SubmissionChecksumSha256, StringComparison.OrdinalIgnoreCase))
            return new(ReviewerAssignmentEligibilityOutcome.NotEligible);
        if (requireOpen && !batch.IsOpen) return new(ReviewerAssignmentEligibilityOutcome.BatchNotOpen);
        if ((expectedAdvisorEvaluationId.HasValue && expectedAdvisorEvaluationId != approval.AdvisorEvaluationId)
            || (expectedReportVersionNo.HasValue && expectedReportVersionNo != approval.ReportVersionNo))
            return new(ReviewerAssignmentEligibilityOutcome.BindingConflict);
        var reviewer = locked ? await candidateReader.LockAsync(reviewerId, token) : await candidateReader.GetAsync(reviewerId, token);
        if (reviewer is null) return new(ReviewerAssignmentEligibilityOutcome.ReviewerNotFound);
        if (!reviewer.IsActive || !reviewer.IsLecturer) return new(ReviewerAssignmentEligibilityOutcome.ReviewerNotEligible);
        if (reviewer.UserId == topic.AdvisorId) return new(ReviewerAssignmentEligibilityOutcome.AdvisorConflict);
        return new(ReviewerAssignmentEligibilityOutcome.Succeeded, new(thesis.GroupId, topic.TopicId, topic.BatchId,
            topic.AdvisorId, approval.AdvisorEvaluationId, approval.FinalReportId, approval.ReportVersionNo,
            approval.SubmissionFileId, approval.SubmissionChecksumSha256, approval.ApprovedAtUtc));
    }

    private static bool Matches(ReviewerAssignment assignment, ReviewerAssignmentBindingContractDto binding)
        => assignment.IsActive && !assignment.IsLegacy && assignment.GroupId == binding.GroupId
           && assignment.AdvisorEvaluationId == binding.AdvisorEvaluationId && assignment.FinalReportId == binding.FinalReportId
           && assignment.ReportVersionNo == binding.ReportVersionNo && assignment.SubmissionFileId == binding.SubmissionFileId
           && string.Equals(assignment.SubmissionChecksumSha256, binding.SubmissionChecksumSha256, StringComparison.OrdinalIgnoreCase);
}
