using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;

public sealed record GetPlagiarismReportQuery(Guid FinalReportId, int? VersionNo = null);

public sealed record PlagiarismReportResultDto(
    bool IsReceiptOnly,
    PlagiarismReceiptResponseDto? Receipt,
    PlagiarismReportResponseDto? FullReport);

public sealed class GetPlagiarismReportHandler(
    IFinalReportRepository finalReportRepository,
    IFinalReportPlagiarismScanRepository scanRepository,
    IStudentGroupRepository studentGroupRepository,
    FinalReportReadAccessChecker readAccessChecker,
    ICurrentUser currentUser,
    ITopicAdvisorAssignmentContract advisorAssignmentContract)
{
    public async Task<Result<PlagiarismReportResultDto>> HandleAsync(
        GetPlagiarismReportQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.FinalReportId == Guid.Empty)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        var report = await finalReportRepository.GetByIdAsync(query.FinalReportId, cancellationToken);
        if (report is null)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        var group = await studentGroupRepository.GetByIdAsync(report.GroupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        bool HasRole(string role) => currentUser.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
        var isAdmin = ThesisAuthorization.IsAdministrator(currentUser);
        var isFaculty = HasRole("Faculty") && ThesisAuthorization.CanReviewGates(currentUser);

        var assignment = await advisorAssignmentContract.GetByTopicIdAsync(group.TopicId, cancellationToken);
        var isAssignedAdvisor = currentUser.UserId.HasValue && assignment?.AdvisorId == currentUser.UserId.Value;
        var isCouncil = HasRole("CouncilMember") && !isAdmin && !isFaculty && !isAssignedAdvisor;

        if (isCouncil)
        {
            if (report.Status != FinalReportStatus.UnderEvaluation)
            {
                return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.CouncilNotReady);
            }
        }
        else
        {
            var canRead = await readAccessChecker.CanReadAsync(group, cancellationToken);
            if (!canRead)
            {
                return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.AccessDenied);
            }
        }

        var scan = query.VersionNo.HasValue
            ? await scanRepository.GetByReportAndVersionAsync(query.FinalReportId, query.VersionNo.Value, cancellationToken)
            : await scanRepository.GetLatestByReportIdAsync(query.FinalReportId, cancellationToken);

        if (scan is null)
        {
            return Result.Failure<PlagiarismReportResultDto>(PlagiarismScanErrors.NotFound);
        }

        var isStudentOnly = HasRole("Student") && !isAdmin && !isFaculty && !isAssignedAdvisor && !isCouncil;

        if (isStudentOnly)
        {
            var receipt = new PlagiarismReceiptResponseDto(
                scan.Id,
                report.Id,
                scan.VersionNo,
                scan.SubmissionFile?.FileName ?? "thesis_document",
                scan.SubmissionFile?.ChecksumSha256,
                scan.SubmissionFile?.FileSize ?? 0,
                scan.Status.ToString(),
                GetStatusDescription(scan.Status, scan.ErrorMessage),
                scan.CreatedAt,
                scan.CompletedAt,
                "Báo cáo đối chiếu tương đồng chi tiết được chuyển giao trực tiếp cho Giảng viên hướng dẫn để phục vụ thẩm định học thuật. Sinh viên chỉ nhận biên nhận đối soát tệp đã nộp.");

            return Result.Success(new PlagiarismReportResultDto(true, receipt, null));
        }

        PlagiarismSummaryDto? summary = null;
        var findings = new List<PlagiarismFindingDto>();
        string? disclaimer = null;

        if (scan.Status == PlagiarismScanStatus.Completed && !string.IsNullOrWhiteSpace(scan.RawReportJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(scan.RawReportJson);
                var parsed = PlagiarismReportParser.Parse(doc.RootElement);
                summary = parsed.Summary;
                findings = parsed.Findings;
                disclaimer = parsed.Disclaimer;
            }
            catch
            {
                // Fallback to database summary metrics if json parse fails
                if (scan.SimilarityCoverage.HasValue && scan.MaxRerankerScore.HasValue)
                {
                    summary = new PlagiarismSummaryDto(
                        scan.SimilarityCoverage.Value,
                        scan.MaxRerankerScore.Value,
                        scan.ScannedChunks ?? 0,
                        scan.ScannedCharacters ?? 0,
                        scan.MatchedCharacters ?? 0,
                        scan.CitedFindingsCount ?? 0,
                        scan.UncitedFindingsCount ?? 0,
                        scan.SuspectedFindingsCount);
                }
            }
        }
        else if (scan.SimilarityCoverage.HasValue && scan.MaxRerankerScore.HasValue)
        {
            summary = new PlagiarismSummaryDto(
                scan.SimilarityCoverage.Value,
                scan.MaxRerankerScore.Value,
                scan.ScannedChunks ?? 0,
                scan.ScannedCharacters ?? 0,
                scan.MatchedCharacters ?? 0,
                scan.CitedFindingsCount ?? 0,
                scan.UncitedFindingsCount ?? 0,
                scan.SuspectedFindingsCount);
        }

        var fullReport = new PlagiarismReportResponseDto(
            scan.Id,
            report.Id,
            scan.VersionNo,
            scan.JobId,
            scan.Status.ToString(),
            GetStatusDescription(scan.Status, scan.ErrorMessage),
            scan.CreatedAt,
            scan.CompletedAt,
            summary,
            findings,
            disclaimer ?? "Bằng chứng tương đồng phục vụ tham chiếu thẩm định chuyên môn. Hệ thống không tự động đưa ra phán quyết vi phạm liêm chính học thuật.",
            CanRescan: isAssignedAdvisor || isAdmin);

        return Result.Success(new PlagiarismReportResultDto(false, null, fullReport));
    }

    private static string GetStatusDescription(PlagiarismScanStatus status, string? errorMessage) => status switch
    {
        PlagiarismScanStatus.PendingSubmission =>
            "Tệp đã tiếp nhận thành công. Đang xếp hàng chuyển giao sang dịch vụ AI.",
        PlagiarismScanStatus.Queued =>
            "Dịch vụ AI đã tiếp nhận tệp. Đang chờ worker nhận tác vụ phân tích.",
        PlagiarismScanStatus.Processing =>
            "Đang phân tích cấu trúc tài liệu và trích xuất đặc trưng ngữ nghĩa.",
        PlagiarismScanStatus.WaitingForCompute =>
            "Đang chờ cụm tính toán GPU (Google Colab) sẵn sàng. Bài nộp được bảo lưu an toàn và sẽ tự động tiếp tục khi GPU kết nối lại.",
        PlagiarismScanStatus.ComputeUnavailable =>
            "Cụm tính toán GPU (Google Colab) đang tạm dừng. Hệ thống sẽ tự động quét lại ngay khi máy chủ GPU được kích hoạt lại.",
        PlagiarismScanStatus.Completed =>
            "Hoàn tất đối chiếu tương đồng và đã tạo báo cáo bằng chứng.",
        PlagiarismScanStatus.UnsupportedFormat =>
            "Tệp định dạng Word .doc cũ không được hỗ trợ phân tích AI (chỉ hỗ trợ định dạng .pdf và .docx).",
        PlagiarismScanStatus.Failed =>
            string.IsNullOrWhiteSpace(errorMessage) ? "Xử lý quét tương đồng thất bại." : errorMessage,
        _ => "Trạng thái không xác định."
    };
}
