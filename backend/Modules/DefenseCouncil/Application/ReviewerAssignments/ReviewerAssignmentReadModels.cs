using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Contracts.ReviewerAssignments;

namespace UGTM.Modules.DefenseCouncil.Application.ReviewerAssignments;

public sealed record ReviewerAssignmentListItemDto : ReviewerAssignmentDto
{
    public ReviewerAssignmentListItemDto(ReviewerAssignmentDto assignment, bool hasEvaluation, bool canCancel) : base(assignment)
    { HasEvaluation = hasEvaluation; CanCancel = canCancel; }
    public bool HasEvaluation { get; init; }
    public bool CanCancel { get; init; }
}

public sealed record ReviewerAssignmentEligibilityItemDto(Guid GroupId, Guid TopicId, Guid BatchId,
    string TopicTitleVi, string? TopicTitleEn, Guid AdvisorId, string? AdvisorFullName,
    Guid AdvisorEvaluationId, Guid FinalReportId, Guid SubmissionFileId, int ReportVersionNo,
    string SubmissionChecksumSha256, DateTime ApprovedAtUtc, bool HasActiveAssignment, Guid? ActiveAssignmentId, bool CanAssign);

public sealed record ReviewerAssignmentTaskDto(Guid Id, Guid GroupId, Guid TopicId, Guid BatchId,
    string TopicTitleVi, string? TopicTitleEn, DateTime AssignedAtUtc, uint AssignmentVersion, bool IsLegacy,
    Guid? FinalReportId, int? ReportVersionNo, Guid? SubmissionFileId,
    ReviewerAssignmentReportFileContractDto? ReportFile, bool CanDownloadReport, string? AccessBlockCode);

public sealed record ReviewerAssignmentListQuery(Guid? BatchId = null, Guid? GroupId = null, Guid? ReviewerId = null,
    bool? IsActive = null, bool IncludeHistory = false, string? Keyword = null, int PageNumber = 1, int PageSize = 20);
public sealed record ReviewerAssignmentEligibilityQuery(Guid BatchId, bool IncludeAssigned = false,
    string? Keyword = null, int PageNumber = 1, int PageSize = 20);
public sealed record ReviewerAssignmentCandidateQuery(Guid GroupId, string? Keyword = null, int PageNumber = 1, int PageSize = 20);
public sealed record ReviewerAssignmentTaskQuery(Guid? BatchId = null, string? Keyword = null, int PageNumber = 1, int PageSize = 20);

public interface IReviewerAssignmentReadRepository
{
    Task<PagedResult<ReviewerAssignmentListItemDto>> ListAsync(ReviewerAssignmentListQuery query, bool canManage, CancellationToken token);
    Task<PagedResult<ReviewerAssignmentEligibilityItemDto>> EligibilityAsync(ReviewerAssignmentEligibilityQuery query, bool canManage, CancellationToken token);
    Task<PagedResult<ReviewerAssignmentTaskDto>> TasksAsync(Guid reviewerId, ReviewerAssignmentTaskQuery query, CancellationToken token);
}
