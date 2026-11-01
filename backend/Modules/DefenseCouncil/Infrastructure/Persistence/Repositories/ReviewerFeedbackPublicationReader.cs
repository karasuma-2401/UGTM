using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerEvaluations;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerFeedbackPublicationReader(DefenseCouncilDbContext context) : IReviewerFeedbackPublicationReader
{
    public async Task<ReviewerFeedbackPublication> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var sources = await context.ReviewerAssignments.AsNoTracking().Where(value => value.GroupId == groupId && value.IsActive)
            .OrderBy(value => value.Id).Select(value => new
            {
                value.IsLegacy,
                value.AdvisorEvaluationId,
                value.FinalReportId,
                value.ReportVersionNo,
                value.SubmissionFileId,
                value.SubmissionChecksumSha256,
                IsPublished = value.Evaluation != null && !value.Evaluation.IsLegacy && value.Evaluation.Status == ReviewerEvaluationStatus.Submitted
                    && value.Evaluation.IsVisibleToStudent,
                Comment = value.Evaluation != null && !value.Evaluation.IsLegacy && value.Evaluation.Status == ReviewerEvaluationStatus.Submitted
                    && value.Evaluation.IsVisibleToStudent ? value.Evaluation.Comment : null,
                ApprovalMatches = context.AdvisorEvaluations.Any(approval => approval.Id == value.AdvisorEvaluationId && approval.GroupId == groupId
                    && !approval.IsLegacy && approval.AllowsReviewerAssignment == true && approval.DecisionAt != null
                    && approval.FinalReportId == value.FinalReportId && approval.ReportVersionNo == value.ReportVersionNo
                    && approval.SubmissionFileId == value.SubmissionFileId && approval.SubmissionChecksumSha256 != null
                    && value.SubmissionChecksumSha256 != null && approval.SubmissionChecksumSha256.ToLower() == value.SubmissionChecksumSha256.ToLower())
            }).Take(2).ToListAsync(cancellationToken);
        if (sources.Count > 1) return new(ReviewerFeedbackPublicationOutcome.ContextInvalid);
        var source = sources.SingleOrDefault();
        if (source is null || source.IsLegacy || !source.IsPublished) return new(ReviewerFeedbackPublicationOutcome.None);
        if (!source.ApprovalMatches || source.AdvisorEvaluationId is null || source.AdvisorEvaluationId == Guid.Empty
            || source.FinalReportId is null || source.FinalReportId == Guid.Empty || source.ReportVersionNo is not > 0
            || source.SubmissionFileId is null || source.SubmissionFileId == Guid.Empty
            || source.SubmissionChecksumSha256 is not { Length: 64 } checksum || !checksum.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(source.Comment)) return new(ReviewerFeedbackPublicationOutcome.ContextInvalid);
        return new(ReviewerFeedbackPublicationOutcome.Published, source.Comment,
            new ReviewerFeedbackReportSnapshotContractDto(groupId, source.FinalReportId.Value, source.ReportVersionNo.Value,
                source.SubmissionFileId.Value, checksum.ToLowerInvariant()));
    }
}
