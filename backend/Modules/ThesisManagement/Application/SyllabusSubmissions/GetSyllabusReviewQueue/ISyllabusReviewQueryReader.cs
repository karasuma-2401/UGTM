using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusReviewQueue;

public sealed record SyllabusReviewFilter(
    SyllabusStatus? Status,
    IReadOnlyList<Guid>? BatchTopicIds,
    IReadOnlyList<Guid>? KeywordTopicIds,
    IReadOnlyList<Guid>? KeywordStudentIds);

public sealed record SyllabusReviewRecord(
    Guid SyllabusId,
    Guid GroupId,
    int CurrentVersionNo,
    SyllabusStatus Status,
    StudentGroup? Group,
    SubmissionFile? LatestFile);

public interface ISyllabusReviewQueryReader
{
    Task<PagedResult<SyllabusReviewRecord>> GetPageAsync(
        SyllabusReviewFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
