using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportFileDownload;

public sealed class GetFinalReportFileDownloadHandler(
    ISubmissionFileRepository submissionFileRepository,
    IFinalReportRepository finalReportRepository,
    IStudentGroupRepository groupRepository,
    FinalReportReadAccessChecker accessChecker,
    IThesisSubmissionObjectStorage objectStorage,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DownloadUriLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<SubmissionFileDownloadDto>> HandleAsync(
        GetFinalReportFileDownloadQuery query, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.AuthenticationRequired);
        }
        if (query.FileId == Guid.Empty)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.InvalidRequest);
        }

        var file = await submissionFileRepository.GetByIdAsync(query.FileId, cancellationToken);
        if (file is null || file.EntityType != SubmissionEntityType.FinalReport)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.FileNotFound);
        }
        var report = await finalReportRepository.GetByIdAsync(file.EntityId, cancellationToken);
        var group = report is null ? null : await groupRepository.GetByIdAsync(report.GroupId, cancellationToken);
        if (report is null || group is null || file.VersionNo < 1 || file.VersionNo > report.CurrentVersionNo)
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.FileNotFound);
        }
        if (!await accessChecker.CanReadAsync(group, cancellationToken))
        {
            return Result.Failure<SubmissionFileDownloadDto>(FinalReportSubmissionErrors.ReadAccessDenied);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var uri = await objectStorage.CreateDownloadUriAsync(file.ObjectKey, DownloadUriLifetime, cancellationToken);
        return Result.Success(new SubmissionFileDownloadDto(file.Id, uri.AbsoluteUri, nowUtc.Add(DownloadUriLifetime)));
    }
}
