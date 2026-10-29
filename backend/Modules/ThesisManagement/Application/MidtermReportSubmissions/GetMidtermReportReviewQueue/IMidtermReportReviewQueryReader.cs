using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.MidtermReportSubmissions.GetMidtermReportReviewQueue;

public sealed record MidtermReportReviewFilter(
    MidtermReportStatus? Status,
    IReadOnlyList<Guid>? BatchTopicIds,
    IReadOnlyList<Guid>? KeywordTopicIds,
    IReadOnlyList<Guid>? KeywordStudentIds);

public sealed record MidtermReportReviewRecord(
    Guid MidtermReportId,
    Guid GroupId,
    int CurrentVersionNo,
    MidtermReportStatus Status,
    StudentGroup? Group,
    SubmissionFile? LatestFile);

public interface IMidtermReportReviewQueryReader
{
    Task<PagedResult<MidtermReportReviewRecord>> GetPageAsync(
        MidtermReportReviewFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
