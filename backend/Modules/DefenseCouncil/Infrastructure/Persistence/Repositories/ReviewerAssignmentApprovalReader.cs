using Microsoft.EntityFrameworkCore;
using UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;
using UGTM.Modules.DefenseCouncil.Contracts.AdvisorEvaluations;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Repositories;

internal sealed class ReviewerAssignmentApprovalReader(DefenseCouncilDbContext context) : IReviewerAssignmentApprovalReader
{
    public async Task<ReviewerAssignmentEligibilityContractDto?> GetAsync(Guid groupId, bool locked,
        CancellationToken cancellationToken = default)
    {
        if (locked && context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A shared reviewer transaction is required.");
        var evaluation = locked
            ? await context.AdvisorEvaluations.FromSqlInterpolated($"""
                SELECT e.*, e.xmin FROM councils.advisor_evaluation e WHERE e.group_id = {groupId} FOR SHARE
                """).AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            : await context.AdvisorEvaluations.AsNoTracking().SingleOrDefaultAsync(value => value.GroupId == groupId, cancellationToken);
        if (evaluation is null || !evaluation.IsLocked || evaluation.FinalReportId is not { } reportId
            || evaluation.ReportVersionNo is not > 0 || evaluation.SubmissionFileId is not { } fileId
            || evaluation.SubmissionChecksumSha256 is not { Length: 64 } checksum || !checksum.All(Uri.IsHexDigit)
            || evaluation.DecisionAt is not { } approvedAt)
            return null;
        return new(evaluation.Id, evaluation.GroupId, reportId, evaluation.ReportVersionNo.Value, fileId,
            checksum.ToLowerInvariant(), approvedAt);
    }
}
