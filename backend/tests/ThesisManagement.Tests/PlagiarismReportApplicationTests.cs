using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;
using UGTM.Modules.DefenseCouncil.Contracts.Councils;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportHistory;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Dtos;
using UGTM.Modules.ThesisManagement.Application.PlagiarismAI.Handlers;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class PlagiarismReportApplicationTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid TopicId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid AdvisorId = Guid.NewGuid();
    private static readonly Guid CouncilMemberId = Guid.NewGuid();

    private const string SampleReportJson = """
    {
      "schema_version": "v3",
      "summary": {
        "similarity_coverage": 0.185,
        "max_reranker_score": 0.82,
        "scanned_chunks": 12,
        "scanned_characters": 15000,
        "matched_characters": 2775,
        "cited_findings": 1,
        "uncited_findings": 0,
        "suspected_findings": 0
      },
      "findings": [
        {
          "passage_group_id": "pg-001",
          "submission_chunk_no": 1,
          "submission_page": 2,
          "submission_start": 100,
          "submission_end": 2875,
          "submission_text": "Sample submission text snippet.",
          "source_key": "corpus_thesis_2025_01.pdf",
          "source_version": 1,
          "source_filename": "corpus_thesis_2025_01.pdf",
          "source_page": 5,
          "source_text": "Sample source text snippet that matched.",
          "reranker_score": 0.82,
          "classifier_score": 0.91,
          "citation_status": "Cited",
          "explanation": "Properly cited in bibliography",
          "potential_misattribution": false,
          "review_required": false
        }
      ],
      "disclaimer": "Automated similarity verification."
    }
    """;

    [Fact]
    public void PlagiarismReportParser_Parses_Valid_SchemaV3_Json()
    {
        using var doc = JsonDocument.Parse(SampleReportJson);
        var (summary, findings, disclaimer) = PlagiarismReportParser.Parse(doc.RootElement);

        Assert.NotNull(summary);
        Assert.Equal(0.185, summary.SimilarityCoverage, 3);
        Assert.Equal(0.82, summary.MaxRerankerScore, 2);
        Assert.Equal(15000, summary.ScannedCharacters);
        Assert.Equal(2775, summary.MatchedCharacters);
        Assert.Equal(1, summary.CitedFindings);
        Assert.Equal(0, summary.UncitedFindings);

        Assert.NotNull(findings);
        Assert.Single(findings);
        Assert.Equal("pg-001", findings[0].PassageGroupId);
        Assert.Equal("corpus_thesis_2025_01.pdf", findings[0].SourceFilename);
        Assert.Equal(0.82, findings[0].RerankerScore!.Value, 2);
        Assert.Equal("Sample source text snippet that matched.", findings[0].SourceText);
        Assert.Equal("Sample submission text snippet.", findings[0].SubmissionText);
        Assert.Equal("Automated similarity verification.", disclaimer);
    }

    [Fact]
    public void PlagiarismReportParser_Handles_Empty_Object_Safely()
    {
        using var doc = JsonDocument.Parse("{}");
        var (summary, findings, disclaimer) = PlagiarismReportParser.Parse(doc.RootElement);

        Assert.Null(summary);
        Assert.Empty(findings);
        Assert.Null(disclaimer);
    }

    [Fact]
    public async Task Student_Can_Only_Access_Masked_Receipt_Without_Findings()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var handler = CreateGetReportHandler(report, scan, StudentUser(StudentId));

        var result = await handler.HandleAsync(new GetPlagiarismReportQuery(report.Id));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsReceiptOnly);
        Assert.NotNull(result.Value.Receipt);
        Assert.Null(result.Value.FullReport);

        var receipt = result.Value.Receipt;
        Assert.Equal(report.Id, receipt.FinalReportId);
        Assert.Equal(PlagiarismScanStatus.Completed.ToString(), receipt.Status);
        // Student receipt must NEVER expose findings or source passages
    }

    [Fact]
    public async Task Advisor_Accesses_Full_Report_With_Findings()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var handler = CreateGetReportHandler(report, scan, AdvisorUser(AdvisorId));

        var result = await handler.HandleAsync(new GetPlagiarismReportQuery(report.Id));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsReceiptOnly);
        Assert.Null(result.Value.Receipt);
        Assert.NotNull(result.Value.FullReport);

        var fullReport = result.Value.FullReport;
        Assert.Equal(report.Id, fullReport.FinalReportId);
        Assert.Equal(PlagiarismScanStatus.Completed.ToString(), fullReport.Status);
        Assert.NotNull(fullReport.Summary);
        Assert.Equal(0.185, fullReport.Summary.SimilarityCoverage, 3);
        Assert.Single(fullReport.Findings);
        Assert.Equal("corpus_thesis_2025_01.pdf", fullReport.Findings[0].SourceFilename);
    }

    [Fact]
    public async Task Council_Member_Can_Only_Access_Full_Report_When_UnderEvaluation()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        // Report status is Submitted initially, not UnderEvaluation
        var handler = CreateGetReportHandler(report, scan, CouncilMemberUser(CouncilMemberId));

        var rejectedResult = await handler.HandleAsync(new GetPlagiarismReportQuery(report.Id));
        Assert.False(rejectedResult.IsSuccess);
        Assert.Equal(PlagiarismScanErrors.CouncilNotReady.Code, rejectedResult.Error.Code);

        // Advance report to UnderEvaluation
        Assert.True(report.TryBeginEvaluation());
        var allowedResult = await handler.HandleAsync(new GetPlagiarismReportQuery(report.Id));
        Assert.True(allowedResult.IsSuccess);
        Assert.NotNull(allowedResult.Value.FullReport);
        var unassigned = CreateGetReportHandler(report, scan, CouncilMemberUser(Guid.NewGuid()));
        var denied = await unassigned.HandleAsync(new GetPlagiarismReportQuery(report.Id));
        Assert.False(denied.IsSuccess);
        Assert.Equal(PlagiarismScanErrors.AccessDenied.Code, denied.Error.Code);
    }

    [Fact]
    public void FinalReportPlagiarismScan_State_Transitions_Work_As_Expected()
    {
        var scanId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        var scan = FinalReportPlagiarismScan.Create(scanId, reportId, fileId, 1, Now);
        Assert.Equal(PlagiarismScanStatus.PendingSubmission, scan.Status);
        Assert.Equal(0, scan.AttemptCount);

        // Mark queued
        scan.MarkQueued(jobId, Now.AddSeconds(5));
        Assert.Equal(PlagiarismScanStatus.Queued, scan.Status);
        Assert.Equal(jobId, scan.JobId);

        // Mark processing
        scan.MarkProcessing(Now.AddSeconds(10));
        Assert.Equal(PlagiarismScanStatus.Processing, scan.Status);

        // Mark waiting for compute
        var nextCheck = Now.AddSeconds(90);
        scan.MarkWaitingForCompute(Now.AddSeconds(30), nextCheck, "Waiting for Colab");
        Assert.Equal(PlagiarismScanStatus.WaitingForCompute, scan.Status);
        Assert.Equal(nextCheck, scan.NextCheckAfterUtc);

        // Mark compute unavailable (Colab exhausted)
        scan.MarkComputeUnavailable(Now.AddMinutes(5), "GPU_UNAVAILABLE_EXHAUSTED", "Colab GPU offline");
        Assert.Equal(PlagiarismScanStatus.ComputeUnavailable, scan.Status);
        Assert.Equal("GPU_UNAVAILABLE_EXHAUSTED", scan.ErrorCode);

        // Reset for retry
        scan.ResetForRetry(Now.AddMinutes(10));
        Assert.Equal(PlagiarismScanStatus.PendingSubmission, scan.Status);
        Assert.Null(scan.JobId);
        Assert.Null(scan.ErrorCode);

        // Complete
        scan.MarkCompleted(0.185, 0.82, 12, 15000, 2775, 1, 0, 0, SampleReportJson, Now.AddMinutes(12));
        Assert.Equal(PlagiarismScanStatus.Completed, scan.Status);
        Assert.Equal(0.185, scan.SimilarityCoverage);
        Assert.Equal(0.82, scan.MaxRerankerScore);
        Assert.Equal(SampleReportJson, scan.RawReportJson);
        scan.ResetForRetry(Now.AddMinutes(13));
        Assert.Null(scan.RawReportJson);
        Assert.Null(scan.SimilarityCoverage);
        Assert.Null(scan.CompletedAt);
    }

    [Fact]
    public async Task RetryPlagiarismScanHandler_Rejects_Student_With_AccessDenied()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var reportRepo = new FakeFinalReportRepository(report);
        var scanRepo = new FakeScanRepository(scan);
        var group = new StudentGroup(GroupId, TopicId, Now);
        group.AddConfirmedMember(StudentId, Guid.NewGuid(), Now);
        var groupRepo = new FakeStudentGroupRepository(group);
        var advisorContract = new FakeAdvisorAssignmentContract(TopicId, AdvisorId);
        var getReportHandler = CreateGetReportHandler(report, scan, StudentUser(StudentId));

        var handler = new RetryPlagiarismScanHandler(
            reportRepo,
            scanRepo,
            groupRepo,
            StudentUser(StudentId),
            advisorContract,
            null!,
            getReportHandler);

        var result = await handler.HandleAsync(new RetryPlagiarismScanCommand(report.Id));
        Assert.False(result.IsSuccess);
        Assert.Equal(PlagiarismScanErrors.AccessDenied.Code, result.Error.Code);
    }

    [Fact]
    public async Task RetryPlagiarismScanHandler_Rejects_When_Scan_Is_Already_Processing()
    {
        var report = FinalReport.CreateSubmitted(Guid.NewGuid(), GroupId, FinalReportVersionType.InitialFinal, Now);
        var scan = FinalReportPlagiarismScan.Create(Guid.NewGuid(), report.Id, Guid.NewGuid(), 1, Now);
        scan.MarkProcessing(Now);

        var reportRepo = new FakeFinalReportRepository(report);
        var scanRepo = new FakeScanRepository(scan);
        var group = new StudentGroup(GroupId, TopicId, Now);
        group.AddConfirmedMember(StudentId, Guid.NewGuid(), Now);
        var groupRepo = new FakeStudentGroupRepository(group);
        var advisorContract = new FakeAdvisorAssignmentContract(TopicId, AdvisorId);
        var getReportHandler = CreateGetReportHandler(report, scan, AdvisorUser(AdvisorId));

        var handler = new RetryPlagiarismScanHandler(
            reportRepo,
            scanRepo,
            groupRepo,
            AdvisorUser(AdvisorId),
            advisorContract,
            null!,
            getReportHandler);

        var result = await handler.HandleAsync(new RetryPlagiarismScanCommand(report.Id));
        Assert.False(result.IsSuccess);
        Assert.Equal(PlagiarismScanErrors.RescanNotAllowed.Code, result.Error.Code);
    }

    [Fact]
    public async Task ResumePendingPlagiarismScansHandler_Rejects_Student_And_Advisor()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var scanRepo = new FakeScanRepository(scan);

        var studentHandler = new ResumePendingPlagiarismScansHandler(
            scanRepo,
            StudentUser(StudentId),
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResumePendingPlagiarismScansHandler>.Instance);

        var studentResult = await studentHandler.HandleAsync(new ResumePendingPlagiarismScansCommand());
        Assert.False(studentResult.IsSuccess);
        Assert.Equal("PlagiarismScan.ResumeNotAllowed", studentResult.Error.Code);

        var advisorHandler = new ResumePendingPlagiarismScansHandler(
            scanRepo,
            AdvisorUser(AdvisorId),
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ResumePendingPlagiarismScansHandler>.Instance);

        var advisorResult = await advisorHandler.HandleAsync(new ResumePendingPlagiarismScansCommand());
        Assert.False(advisorResult.IsSuccess);
        Assert.Equal("PlagiarismScan.ResumeNotAllowed", advisorResult.Error.Code);
    }

    [Fact]
    public async Task GetFinalReportHistoryHandler_Attaches_PlagiarismStatus_And_Masks_Score_For_Student()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var file = new SubmissionFile
        {
            Id = scan.SubmissionFileId,
            EntityType = SubmissionEntityType.FinalReport,
            EntityId = report.Id,
            VersionNo = 1,
            FileName = "thesis.pdf",
            ObjectKey = "key-1",
            ContentType = "application/pdf",
            ChecksumSha256 = "abc",
            FileSize = 1024,
            FileExtension = ".pdf",
            UploadedBy = StudentId,
            UploadedAt = Now
        };

        var reportRepo = new FakeFinalReportRepository(report);
        var scanRepo = new FakeScanRepository(scan);
        var fileRepo = new FakeSubmissionFileRepository(file);
        var group = new StudentGroup(GroupId, TopicId, Now);
        group.AddConfirmedMember(StudentId, Guid.NewGuid(), Now);
        var groupRepo = new FakeStudentGroupRepository(group);
        var advisorContract = new FakeAdvisorAssignmentContract(TopicId, AdvisorId);
        var accessChecker = new FinalReportReadAccessChecker(StudentUser(StudentId), advisorContract);

        var handler = new GetFinalReportHistoryHandler(
            groupRepo,
            reportRepo,
            fileRepo,
            scanRepo,
            accessChecker,
            StudentUser(StudentId),
            advisorContract);

        var result = await handler.HandleAsync(new GetFinalReportHistoryQuery(GroupId));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Versions);
        var version = result.Value.Versions[0];
        Assert.Equal(PlagiarismScanStatus.Completed.ToString(), version.PlagiarismStatus);
        Assert.Null(version.SimilarityCoverage); // Strictly masked for students
    }

    [Fact]
    public async Task GetFinalReportHistoryHandler_Exposes_SimilarityScore_For_Advisor()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var file = new SubmissionFile
        {
            Id = scan.SubmissionFileId,
            EntityType = SubmissionEntityType.FinalReport,
            EntityId = report.Id,
            VersionNo = 1,
            FileName = "thesis.pdf",
            ObjectKey = "key-1",
            ContentType = "application/pdf",
            ChecksumSha256 = "abc",
            FileSize = 1024,
            FileExtension = ".pdf",
            UploadedBy = StudentId,
            UploadedAt = Now
        };

        var reportRepo = new FakeFinalReportRepository(report);
        var scanRepo = new FakeScanRepository(scan);
        var fileRepo = new FakeSubmissionFileRepository(file);
        var group = new StudentGroup(GroupId, TopicId, Now);
        group.AddConfirmedMember(StudentId, Guid.NewGuid(), Now);
        var groupRepo = new FakeStudentGroupRepository(group);
        var advisorContract = new FakeAdvisorAssignmentContract(TopicId, AdvisorId);
        var accessChecker = new FinalReportReadAccessChecker(AdvisorUser(AdvisorId), advisorContract);

        var handler = new GetFinalReportHistoryHandler(
            groupRepo,
            reportRepo,
            fileRepo,
            scanRepo,
            accessChecker,
            AdvisorUser(AdvisorId),
            advisorContract);

        var result = await handler.HandleAsync(new GetFinalReportHistoryQuery(GroupId));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Versions);
        var version = result.Value.Versions[0];
        Assert.Equal(PlagiarismScanStatus.Completed.ToString(), version.PlagiarismStatus);
        Assert.Equal(0.185, version.SimilarityCoverage); // Available for advisor
    }

    [Fact]
    public async Task IndexFinalReportToCorpusHandler_Rejects_Student_With_AccessDenied()
    {
        var (report, scan) = CreateCompletedReportWithScan();
        var reportRepo = new FakeFinalReportRepository(report);
        var fileRepo = new FakeSubmissionFileRepository(null);

        var handler = new IndexFinalReportToCorpusHandler(
            reportRepo,
            fileRepo,
            null!,
            Microsoft.Extensions.Options.Options.Create(new PlagiarismAiOptions()),
            StudentUser(StudentId),
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<IndexFinalReportToCorpusHandler>.Instance);

        var result = await handler.HandleAsync(new IndexFinalReportToCorpusCommand(report.Id));
        Assert.False(result.IsSuccess);
        Assert.Equal(PlagiarismScanErrors.AccessDenied.Code, result.Error.Code);
    }

    private static (FinalReport report, FinalReportPlagiarismScan scan) CreateCompletedReportWithScan()
    {
        var report = FinalReport.CreateSubmitted(Guid.NewGuid(), GroupId, FinalReportVersionType.InitialFinal, Now);
        var scan = FinalReportPlagiarismScan.Create(Guid.NewGuid(), report.Id, Guid.NewGuid(), 1, Now);
        scan.MarkCompleted(0.185, 0.82, 12, 15000, 2775, 1, 0, 0, SampleReportJson, Now.AddMinutes(2));
        return (report, scan);
    }

    private static GetPlagiarismReportHandler CreateGetReportHandler(
        FinalReport report,
        FinalReportPlagiarismScan scan,
        ICurrentUser currentUser)
    {
        var reportRepo = new FakeFinalReportRepository(report);
        var scanRepo = new FakeScanRepository(scan);
        var group = new StudentGroup(GroupId, TopicId, Now);
        group.AddConfirmedMember(StudentId, Guid.NewGuid(), Now);
        var groupRepo = new FakeStudentGroupRepository(group);
        var advisorContract = new FakeAdvisorAssignmentContract(TopicId, AdvisorId);
        var accessChecker = new FinalReportReadAccessChecker(currentUser, advisorContract);

        return new GetPlagiarismReportHandler(
            reportRepo,
            scanRepo,
            groupRepo,
            accessChecker,
            currentUser,
            advisorContract,
            new FakeCouncilReportAccess());
    }

    private sealed class FakeCouncilReportAccess : ICouncilReportAccess
    {
        public Task<bool> IsAssignedAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(groupId == GroupId && userId == CouncilMemberId);
    }

    private static ICurrentUser StudentUser(Guid studentId) =>
        new FakeCurrentUser(studentId, ["Student"]);

    private static ICurrentUser AdvisorUser(Guid advisorId) =>
        new FakeCurrentUser(advisorId, ["Advisor"]);

    private static ICurrentUser CouncilMemberUser(Guid memberId) =>
        new FakeCurrentUser(memberId, ["CouncilMember"]);

    private sealed class FakeCurrentUser(Guid userId, string[] roles) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId { get; } = userId;
        public Guid? SessionId => Guid.NewGuid();
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>(roles);
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FakeFinalReportRepository(FinalReport report) : IFinalReportRepository
    {
        public Task<FinalReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(report.Id == id ? report : null);

        public Task<FinalReport?> GetByGroupAndVersionTypeAsync(
            Guid groupId,
            FinalReportVersionType versionType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(report.GroupId == groupId && report.VersionType == versionType ? report : null);

        public void Add(FinalReport finalReport) { }
        public void TrackGroupConcurrency(FinalReport finalReport, StudentGroup group) { }
    }

    private sealed class FakeScanRepository(FinalReportPlagiarismScan scan) : IFinalReportPlagiarismScanRepository
    {
        public Task<FinalReportPlagiarismScan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(scan.Id == id ? scan : null);

        public Task<FinalReportPlagiarismScan?> GetByReportAndVersionAsync(
            Guid finalReportId,
            int versionNo,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(scan.FinalReportId == finalReportId && scan.VersionNo == versionNo ? scan : null);

        public Task<FinalReportPlagiarismScan?> GetLatestByReportIdAsync(
            Guid finalReportId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(scan.FinalReportId == finalReportId ? scan : null);

        public Task<IReadOnlyList<FinalReportPlagiarismScan>> GetByReportIdAsync(
            Guid finalReportId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinalReportPlagiarismScan>>(scan.FinalReportId == finalReportId ? [scan] : []);

        public Task<FinalReportPlagiarismScan?> GetByJobIdAsync(
            Guid jobId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(scan.JobId == jobId ? scan : null);

        public Task<IReadOnlyList<FinalReportPlagiarismScan>> GetActiveOrPendingScansAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinalReportPlagiarismScan>>([scan]);

        public Task<IReadOnlyList<FinalReportPlagiarismScan>> GetResumableScansAsync(
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinalReportPlagiarismScan>>([scan]);

        public void Add(FinalReportPlagiarismScan scanRecord) { }
        public void Update(FinalReportPlagiarismScan scanRecord) { }
    }

    private sealed class FakeStudentGroupRepository(StudentGroup group) : IStudentGroupRepository
    {
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(group.Id == id ? group : null);

        public void Add(StudentGroup studentGroup) { }
    }

    private sealed class FakeAdvisorAssignmentContract(Guid topicId, Guid advisorId) : ITopicAdvisorAssignmentContract
    {
        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(
            Guid topicQueryId,
            CancellationToken cancellationToken = default)
        {
            if (topicQueryId == topicId)
            {
                return Task.FromResult<TopicAdvisorAssignmentContractDto?>(
                    new TopicAdvisorAssignmentContractDto(topicId, advisorId));
            }

            return Task.FromResult<TopicAdvisorAssignmentContractDto?>(null);
        }
    }

    private sealed class FakeSubmissionFileRepository(SubmissionFile? file) : ISubmissionFileRepository
    {
        public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(file?.Id == id ? file : null);

        public Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(
            SubmissionEntityType entityType,
            Guid entityId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SubmissionFile>>(file is not null ? [file] : []);

        public void Add(SubmissionFile submissionFile) { }
    }
}
