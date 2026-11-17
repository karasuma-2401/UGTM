using System.Web;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.ProgressReportSubmissions.GetProgressReportFileDownload;
using UGTM.Modules.ThesisManagement.Application.ReviewGateSubmissions.GetReviewGateSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Application.SyllabusSubmissions.GetSyllabusSubmissionFileDownload;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;
using UGTM.Modules.ThesisManagement.Infrastructure.Storage;
using UGTM.Modules.TopicCatalog.Contracts.AdvisorAssignments;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ThesisSubmissionDownloadFilenameTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task S3_presigned_download_uri_includes_attachment_and_inline_original_utf8_filename()
    {
        using var s3Client = new AmazonS3Client(
            new BasicAWSCredentials("access", "secret"),
            new AmazonS3Config
            {
                ServiceURL = "http://localhost:9000",
                ForcePathStyle = true,
                AuthenticationRegion = RegionEndpoint.USEast1.SystemName,
            });
        var storage = new S3ThesisSubmissionObjectStorage(
            s3Client,
            Options.Create(new S3StorageOptions
            {
                Endpoint = "http://localhost:9000",
                AccessKey = "access",
                SecretKey = "secret",
                BucketName = "thesis-bucket",
                Region = "us-east-1",
            }));

        const string objectKey = "thesis-submissions/midtermreport/11111111111111111111111111111111/v1/abcdef123456.pdf";

        var attachmentUri = await storage.CreateDownloadUriAsync(
            objectKey,
            "Báo cáo ABC.pdf",
            TimeSpan.FromMinutes(5));
        var inlineUri = await storage.CreateDownloadUriAsync(
            objectKey,
            "Báo cáo ABC.pdf",
            TimeSpan.FromMinutes(5),
            inline: true);

        var attachmentDisposition = HttpUtility.ParseQueryString(attachmentUri.Query)["response-content-disposition"];
        var inlineDisposition = HttpUtility.ParseQueryString(inlineUri.Query)["response-content-disposition"];

        Assert.NotNull(attachmentDisposition);
        Assert.StartsWith("attachment;", attachmentDisposition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("download.pdf", attachmentDisposition);
        Assert.Contains("filename*=", attachmentDisposition);

        Assert.NotNull(inlineDisposition);
        Assert.StartsWith("inline;", inlineDisposition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("download.pdf", inlineDisposition);
        Assert.Contains("filename*=", inlineDisposition);
    }

    [Fact]
    public async Task ReviewGate_and_Syllabus_download_handlers_pass_original_filename_and_inline_flag()
    {
        var group = CreateGroup(out var studentId, out var batchId);
        var syllabus = Syllabus.CreateSubmitted(Guid.NewGuid(), group.Id);
        var fileId = Guid.NewGuid();
        var file = new SubmissionFile
        {
            Id = fileId,
            EntityType = SubmissionEntityType.Syllabus,
            EntityId = syllabus.Id,
            VersionNo = 1,
            FileName = "DeCuong_ABC.pdf",
            ObjectKey = "thesis-submissions/syllabus/key/v1/randomguid1234.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            FileExtension = ".pdf",
            UploadedBy = studentId,
            UploadedAt = NowUtc,
        };

        var storage = new RecordingStorage();
        var groupRepo = new GroupRepo(group);
        var syllabusRepo = new SyllabusRepo(syllabus);
        var midtermRepo = new MidtermRepo();
        var fileRepo = new FileRepo(file);
        var reviewer = new TestCurrentUser(Guid.NewGuid(), [PermissionCodes.ThesisReviewGatesReview]);
        var clock = new FixedClock(NowUtc);

        var gateHandler = new GetReviewGateSubmissionFileDownloadHandler(
            fileRepo, syllabusRepo, midtermRepo, groupRepo, storage, reviewer, clock);
        var syllabusHandler = new GetSyllabusSubmissionFileDownloadHandler(
            fileRepo, syllabusRepo, groupRepo, storage, reviewer, clock);

        var gateDownload = await gateHandler.HandleAsync(new GetReviewGateSubmissionFileDownloadQuery(fileId));
        var gatePreview = await gateHandler.HandleAsync(new GetReviewGateSubmissionFileDownloadQuery(fileId, Inline: true));
        var syllabusDownload = await syllabusHandler.HandleAsync(new GetSyllabusSubmissionFileDownloadQuery(fileId));

        Assert.True(gateDownload.IsSuccess);
        Assert.True(gatePreview.IsSuccess);
        Assert.True(syllabusDownload.IsSuccess);

        Assert.Collection(
            storage.Calls,
            call =>
            {
                Assert.Equal(file.ObjectKey, call.ObjectKey);
                Assert.Equal("DeCuong_ABC.pdf", call.FileName);
                Assert.False(call.Inline);
            },
            call =>
            {
                Assert.Equal(file.ObjectKey, call.ObjectKey);
                Assert.Equal("DeCuong_ABC.pdf", call.FileName);
                Assert.True(call.Inline);
            },
            call =>
            {
                Assert.Equal(file.ObjectKey, call.ObjectKey);
                Assert.Equal("DeCuong_ABC.pdf", call.FileName);
                Assert.False(call.Inline);
            });
    }

    [Fact]
    public async Task ProgressReport_download_handler_sanitizes_and_passes_original_filename()
    {
        var group = CreateGroup(out var studentId, out _);
        var milestone = ProgressMilestone.Create(
            Guid.NewGuid(),
            group.Id,
            "Tuần 4",
            null,
            NowUtc.AddDays(7),
            Guid.NewGuid(),
            NowUtc.AddDays(-1));
        var report = ProgressReport.CreateSubmitted(Guid.NewGuid(), milestone.Id, group.Id, null, NowUtc);
        var fileId = Guid.NewGuid();
        var file = new SubmissionFile
        {
            Id = fileId,
            EntityType = SubmissionEntityType.ProgressReport,
            EntityId = report.Id,
            VersionNo = 1,
            FileName = "folder/sub\\BaoCao_ABC.pdf",
            ObjectKey = "thesis-submissions/progressreport/key/v1/randomguid5678.pdf",
            ContentType = "application/pdf",
            FileSize = 2048,
            FileExtension = ".pdf",
            UploadedBy = studentId,
            UploadedAt = NowUtc,
        };

        var storage = new RecordingStorage();
        var handler = new GetProgressReportFileDownloadHandler(
            new FileRepo(file),
            new ProgressReportRepo(report),
            new MilestoneRepo(milestone),
            new GroupRepo(group),
            new NullAdvisorAssignmentContract(),
            storage,
            new TestCurrentUser(studentId),
            new FixedClock(NowUtc));

        var result = await handler.HandleAsync(new GetProgressReportFileDownloadQuery(fileId));

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(storage.Calls);
        Assert.Equal("BaoCao_ABC.pdf", recorded.FileName);
        Assert.False(recorded.Inline);
    }

    private static StudentGroup CreateGroup(out Guid studentId, out Guid batchId)
    {
        studentId = Guid.NewGuid();
        batchId = Guid.NewGuid();
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), NowUtc.AddDays(-2));
        group.AddMember(studentId, batchId, NowUtc.AddDays(-2));
        return group;
    }

    private sealed class RecordingStorage : IThesisSubmissionObjectStorage
    {
        public List<(string ObjectKey, string? FileName, bool Inline)> Calls { get; } = [];

        public Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Uri> CreateDownloadUriAsync(string objectKey, TimeSpan validFor, CancellationToken cancellationToken = default)
        {
            Calls.Add((objectKey, null, false));
            return Task.FromResult(new Uri($"https://storage.test/{objectKey}"));
        }

        public Task<Uri> CreateDownloadUriAsync(string objectKey, string downloadFileName, TimeSpan validFor, CancellationToken cancellationToken = default)
            => CreateDownloadUriAsync(objectKey, downloadFileName, validFor, false, cancellationToken);

        public Task<Uri> CreateDownloadUriAsync(string objectKey, string downloadFileName, TimeSpan validFor, bool inline, CancellationToken cancellationToken = default)
        {
            Calls.Add((objectKey, downloadFileName, inline));
            return Task.FromResult(new Uri($"https://storage.test/{objectKey}?filename={Uri.EscapeDataString(downloadFileName)}&inline={inline}"));
        }
    }

    private sealed class FileRepo(SubmissionFile file) : ISubmissionFileRepository
    {
        public Task<SubmissionFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == file.Id ? file : null);
        public Task<IReadOnlyList<SubmissionFile>> GetByEntityAsync(SubmissionEntityType entityType, Guid entityId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SubmissionFile>>([file]);
        public void Add(SubmissionFile submissionFile) { }
    }

    private sealed class GroupRepo(StudentGroup group) : IStudentGroupRepository
    {
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == group.Id ? group : null);
        public void Add(StudentGroup g) { }
    }

    private sealed class SyllabusRepo(Syllabus syllabus) : ISyllabusRepository
    {
        public Task<Syllabus?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == syllabus.Id ? syllabus : null);
        public Task<Syllabus?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(groupId == syllabus.GroupId ? syllabus : null);
        public void Add(Syllabus s) { }
    }

    private sealed class MidtermRepo : IMidtermReportRepository
    {
        public Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<MidtermReport?>(null);
        public Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default) => Task.FromResult<MidtermReport?>(null);
        public void Add(MidtermReport midtermReport) { }
    }

    private sealed class ProgressReportRepo(ProgressReport report) : IProgressReportRepository
    {
        public Task<ProgressReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == report.Id ? report : null);
        public Task<ProgressReport?> GetByMilestoneIdAsync(Guid milestoneId, CancellationToken cancellationToken = default)
            => Task.FromResult(milestoneId == report.MilestoneId ? report : null);
        public Task<IReadOnlyList<ProgressReportVersion>> GetVersionsAsync(Guid progressReportId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressReportVersion>>([]);
        public Task<ProgressReportVersion?> GetVersionAsync(Guid progressReportId, int versionNo, CancellationToken cancellationToken = default)
            => Task.FromResult<ProgressReportVersion?>(null);
        public void Add(ProgressReport r) { }
        public void AddVersion(ProgressReportVersion version) { }
    }

    private sealed class MilestoneRepo(ProgressMilestone milestone) : IProgressMilestoneRepository
    {
        public Task<ProgressMilestone?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == milestone.Id ? milestone : null);
        public Task<IReadOnlyList<ProgressMilestone>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProgressMilestone>>([milestone]);
        public void Add(ProgressMilestone m) { }
    }

    private sealed class NullAdvisorAssignmentContract : ITopicAdvisorAssignmentContract
    {
        public Task<TopicAdvisorAssignmentContractDto?> GetByTopicIdAsync(Guid topicId, CancellationToken cancellationToken = default)
            => Task.FromResult<TopicAdvisorAssignmentContractDto?>(null);
    }

    private sealed class TestCurrentUser(Guid userId, IEnumerable<string>? permissions = null) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlySet<string> Permissions { get; } = (permissions ?? []).ToHashSet(StringComparer.Ordinal);
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
