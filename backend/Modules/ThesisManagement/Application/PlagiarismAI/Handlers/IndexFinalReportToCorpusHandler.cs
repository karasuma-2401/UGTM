using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed record IndexFinalReportToCorpusCommand(Guid FinalReportId, int? VersionNo = null);

public sealed record IndexFinalReportToCorpusResult(
    Guid FinalReportId,
    int VersionNo,
    string FileName,
    string CorpusName,
    bool Success,
    string Message);

public sealed class IndexFinalReportToCorpusHandler(
    IFinalReportRepository finalReportRepository,
    ISubmissionFileRepository submissionFileRepository,
    IPlagiarismAiClient aiClient,
    IOptions<PlagiarismAiOptions> options,
    ICurrentUser currentUser,
    IThesisSubmissionObjectStorage objectStorage,
    ILogger<IndexFinalReportToCorpusHandler> logger)
{
    public async Task<Result<IndexFinalReportToCorpusResult>> HandleAsync(
        IndexFinalReportToCorpusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FinalReportId == Guid.Empty)
        {
            return Result.Failure<IndexFinalReportToCorpusResult>(PlagiarismScanErrors.NotFound);
        }

        var isAdmin = ThesisAuthorization.IsAdministrator(currentUser);
        bool HasRole(string role) => currentUser.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        var isFaculty = HasRole("Faculty") && ThesisAuthorization.CanReviewGates(currentUser);

        if (!isAdmin && !isFaculty)
        {
            return Result.Failure<IndexFinalReportToCorpusResult>(PlagiarismScanErrors.AccessDenied);
        }

        var report = await finalReportRepository.GetByIdAsync(command.FinalReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<IndexFinalReportToCorpusResult>(PlagiarismScanErrors.NotFound);
        }

        var targetVersion = command.VersionNo ?? report.CurrentVersionNo;
        var files = await submissionFileRepository.GetByEntityAsync(
            SubmissionEntityType.FinalReport, report.Id, cancellationToken);
        var file = files.FirstOrDefault(f => f.VersionNo == targetVersion);

        if (file is null)
        {
            return Result.Failure<IndexFinalReportToCorpusResult>(PlagiarismScanErrors.NotFound);
        }

        if (string.Equals(file.FileExtension, ".doc", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<IndexFinalReportToCorpusResult>(PlagiarismScanErrors.UnsupportedFormat);
        }

        var corpusName = options.Value.DefaultCorpus;
        logger.LogInformation(
            "Indexing final report {ReportId} v{Version} ({FileName}) into AI corpus '{Corpus}'",
            report.Id,
            targetVersion,
            file.FileName,
            corpusName);

        try
        {
            await using var stream = await objectStorage.OpenReadAsync(file.ObjectKey, cancellationToken);
            var indexResult = await aiClient.IndexCorpusDocumentAsync(
                corpusName,
                report.Id.ToString(),
                targetVersion,
                stream,
                file.FileName,
                cancellationToken);

            if (!indexResult.IsSuccess)
            {
                return Result.Failure<IndexFinalReportToCorpusResult>(
                    Error.Failure("PlagiarismScan.CorpusIndexFailed", indexResult.ErrorMessage ?? "Không thể đánh chỉ mục tài liệu vào kho ngữ văn."));
            }

            return Result.Success(new IndexFinalReportToCorpusResult(
                report.Id,
                targetVersion,
                file.FileName,
                corpusName,
                Success: true,
                Message: $"Đã đánh chỉ mục tệp {file.FileName} vào kho ngữ văn '{corpusName}' thành công."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stream/index final report {ReportId} into corpus", report.Id);
            return Result.Failure<IndexFinalReportToCorpusResult>(
                Error.Failure("PlagiarismScan.CorpusIndexFailed", "Không thể kết nối hoặc tải tệp lên kho ngữ văn."));
        }
    }
}
