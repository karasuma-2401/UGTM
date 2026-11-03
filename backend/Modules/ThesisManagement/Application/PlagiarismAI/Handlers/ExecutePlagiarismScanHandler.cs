using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed class ExecutePlagiarismScanHandler(
    IPlagiarismAiClient plagiarismAiClient,
    IThesisSubmissionObjectStorage objectStorage,
    IThesisUnitOfWork unitOfWork,
    IOptions<PlagiarismAiOptions> options,
    ILogger<ExecutePlagiarismScanHandler> logger)
{
    private readonly PlagiarismAiOptions _options = options.Value;

    public async Task<bool> ExecuteAsync(FinalReportPlagiarismScan scan, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        try
        {
            if (scan.SubmissionFile is null)
            {
                logger.LogWarning("Plagiarism scan {ScanId} does not have SubmissionFile loaded", scan.Id);
                return false;
            }

            // 1. Format check: legacy .doc is not supported by AI
            if (string.Equals(scan.SubmissionFile.FileExtension, ".doc", StringComparison.OrdinalIgnoreCase))
            {
                scan.MarkUnsupportedFormat(
                    "Tệp định dạng Word .doc cũ không được hỗ trợ phân tích AI (chỉ chấp nhận .pdf và .docx).",
                    nowUtc);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return true;
            }

            // 2. If PendingSubmission: submit to AI service
            if (scan.Status == PlagiarismScanStatus.PendingSubmission)
            {
                return await HandlePendingSubmissionAsync(scan, nowUtc, cancellationToken);
            }

            // 3. If Queued, Processing, WaitingForCompute: poll status from AI service
            if (scan.Status is PlagiarismScanStatus.Queued
                or PlagiarismScanStatus.Processing
                or PlagiarismScanStatus.WaitingForCompute)
            {
                return await HandleActivePollAsync(scan, nowUtc, cancellationToken);
            }

            // 4. If ComputeUnavailable: auto-check if AI is back and Colab is alive
            if (scan.Status == PlagiarismScanStatus.ComputeUnavailable)
            {
                var isHealthy = await plagiarismAiClient.CheckHealthAsync(cancellationToken);
                if (isHealthy)
                {
                    logger.LogInformation("AI service is healthy; auto-rescuing scan {ScanId} from ComputeUnavailable", scan.Id);
                    scan.ResetForRetry(nowUtc);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    return await HandlePendingSubmissionAsync(scan, nowUtc, cancellationToken);
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error executing plagiarism scan {ScanId}", scan.Id);
            return false;
        }
    }

    private async Task<bool> HandlePendingSubmissionAsync(
        FinalReportPlagiarismScan scan,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Dispatching final report {ReportId} v{Version} to AI service", scan.FinalReportId, scan.VersionNo);

        await using var stream = await objectStorage.OpenReadAsync(scan.SubmissionFile.ObjectKey, cancellationToken);
        var corpusId = _options.DefaultCorpus;

        var result = await plagiarismAiClient.SubmitScanJobAsync(
            corpusId,
            scan.FinalReportId,
            scan.VersionNo,
            stream,
            scan.SubmissionFile.FileName,
            cancellationToken);

        if (result.IsSuccess)
        {
            scan.MarkQueued(result.JobId, nowUtc);
            logger.LogInformation("Scan job accepted by AI service with JobId: {JobId}", result.JobId);
        }
        else if (result.ErrorCode == "UNSUPPORTED_FILE_FORMAT")
        {
            scan.MarkUnsupportedFormat(result.ErrorMessage ?? "Định dạng không được hỗ trợ", nowUtc);
        }
        else if (result.ErrorCode == "AI_SERVICE_UNAVAILABLE")
        {
            // Backoff retry when service is temporarily down
            var nextCheck = nowUtc.AddSeconds(Math.Min(30 * Math.Pow(2, Math.Min(scan.AttemptCount, 5)), 600));
            scan.RecordPollAttempt(nowUtc, nextCheck);
            scan.MarkWaitingForCompute(nowUtc, nextCheck, "Dịch vụ AI đang tạm ngoại tuyến. Tự động gửi lại khi dịch vụ sẵn sàng.");
        }
        else
        {
            scan.MarkFailed(result.ErrorCode ?? "SUBMISSION_FAILED", result.ErrorMessage ?? "Lỗi gửi quét", nowUtc);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result.IsSuccess;
    }

    private async Task<bool> HandleActivePollAsync(
        FinalReportPlagiarismScan scan,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (!scan.JobId.HasValue)
        {
            scan.ResetForRetry(nowUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return false;
        }

        var statusDto = await plagiarismAiClient.GetJobStatusAsync(scan.JobId.Value, cancellationToken);
        if (statusDto is null)
        {
            // Failed to reach AI service: apply adaptive backoff
            var backoffSeconds = Math.Min(30 * Math.Pow(2, Math.Min(scan.AttemptCount, 5)), 600);
            scan.RecordPollAttempt(nowUtc, nowUtc.AddSeconds(backoffSeconds));
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return false;
        }

        var status = statusDto.Status.ToLowerInvariant();
        if (status == "processing")
        {
            scan.MarkProcessing(nowUtc);
            scan.RecordPollAttempt(nowUtc, nowUtc.AddSeconds(15));
        }
        else if (status == "waiting_for_compute")
        {
            // Colab GPU is offline: adaptive backoff between 30s and 10 minutes
            var backoff = Math.Min(30 * Math.Pow(2, Math.Min(statusDto.Attempt, 6)), 600);
            var nextCheck = statusDto.RetryAfter ?? nowUtc.AddSeconds(backoff);
            scan.MarkWaitingForCompute(nowUtc, nextCheck, "Đang chờ tài nguyên tính toán GPU (Google Colab)...");
            scan.RecordPollAttempt(nowUtc, nextCheck);
        }
        else if (status == "failed")
        {
            if (string.Equals(statusDto.ErrorCode, "GPU_UNAVAILABLE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(statusDto.ErrorCode, "COMPUTE_UNAVAILABLE_TIMEOUT", StringComparison.OrdinalIgnoreCase))
            {
                // CRUCIAL: Do not fail the student's submission when Colab times out.
                // Mark as ComputeUnavailable so it can be resumed when Colab is back online!
                scan.MarkComputeUnavailable(
                    nowUtc,
                    statusDto.ErrorCode,
                    "Cụm tính toán GPU (Google Colab) đang tạm dừng. Hệ thống sẽ tự động quét lại khi GPU sẵn sàng.");
            }
            else
            {
                scan.MarkFailed(statusDto.ErrorCode ?? "SCAN_FAILED", "Quá trình phân tích tài liệu thất bại.", nowUtc);
            }
        }
        else if (status == "completed")
        {
            var rawJson = await plagiarismAiClient.GetReportRawJsonAsync(scan.JobId.Value, schema: 3, ct: cancellationToken);
            if (rawJson.HasValue)
            {
                var parsed = PlagiarismReportParser.Parse(rawJson.Value);
                var summary = parsed.Summary;

                scan.MarkCompleted(
                    similarityCoverage: summary?.SimilarityCoverage ?? 0,
                    maxRerankerScore: summary?.MaxRerankerScore ?? 0,
                    scannedChunks: summary?.ScannedChunks,
                    scannedCharacters: summary?.ScannedCharacters,
                    matchedCharacters: summary?.MatchedCharacters,
                    citedFindings: summary?.CitedFindings,
                    uncitedFindings: summary?.UncitedFindings,
                    suspectedFindings: summary?.SuspectedFindings,
                    reportJson: rawJson.Value.GetRawText(),
                    nowUtc: nowUtc);

                logger.LogInformation(
                    "Scan completed for Report {ReportId} v{Version}: Coverage={Coverage}%, MaxScore={Score}",
                    scan.FinalReportId,
                    scan.VersionNo,
                    summary?.SimilarityCoverage,
                    summary?.MaxRerankerScore);
            }
            else
            {
                // Status completed but report not ready yet
                scan.RecordPollAttempt(nowUtc, nowUtc.AddSeconds(10));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return scan.Status == PlagiarismScanStatus.Completed;
    }
}
