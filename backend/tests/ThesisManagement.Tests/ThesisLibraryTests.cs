using System.Text.Json;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Application.Library;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Library;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class ThesisLibraryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
    private static ThesisLibraryPublication Publication(string slug = "nghien-cuu-du-lieu", string year = "2025-2026") => new()
    {
        Slug = slug,
        Title = "Nghiên cứu dữ liệu tiếng Việt",
        Abstract = "Ứng dụng học máy trong phân tích dữ liệu.",
        Authors = ["Nguyễn Văn An"],
        Advisor = "Trần Minh",
        Department = "Khoa học Máy tính",
        AcademicYear = year,
        Semester = "Học kỳ 2",
        Keywords = ["Học máy", "Dữ liệu"],
        PublishedAt = Now.AddDays(-1)
    };
    private sealed class Catalog(params ThesisLibraryPublication[] rows) : IThesisLibraryCatalog
    { public IReadOnlyList<ThesisLibraryPublication> Publications => rows; }

    [Fact]
    public void Empty_catalog_is_not_filled_with_unapproved_archive_data()
    {
        var result = ThesisLibrarySearch.Search(new Catalog(), new(), Now);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value.Items); Assert.Equal(0, result.Value.TotalCount);
    }
    [Theory]
    [InlineData("nghien cuu du lieu")]
    [InlineData("NGUYEN van AN")]
    [InlineData("tran minh")]
    [InlineData("học máy")]
    public void Search_supports_vietnamese_with_or_without_diacritics(string query)
    {
        var result = ThesisLibrarySearch.Search(new Catalog(Publication()), new(Q: query), Now);
        Assert.Single(result.Value.Items);
    }
    [Fact]
    public void Filtering_pagination_and_facets_use_only_published_entries()
    {
        var older = Publication("older", "2024-2025");
        var future = Publication("future", "2026-2027"); future.PublishedAt = Now.AddDays(1);
        var catalog = new Catalog(Publication(), older, future);
        var page = ThesisLibrarySearch.Search(catalog, new(Year: "2025-2026", PageSize: 1), Now).Value;
        Assert.Single(page.Items); Assert.Equal(1, page.TotalCount); Assert.Equal(2, page.Years.Count);
        Assert.DoesNotContain("2026-2027", page.Years);
        Assert.Empty(ThesisLibrarySearch.Search(catalog, new(Page: 3, PageSize: 1), Now).Value.Items);
    }
    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 25)]
    [InlineData(int.MaxValue, 12)]
    public void Invalid_pagination_is_rejected(int page, int size)
        => Assert.True(ThesisLibrarySearch.Search(new Catalog(), new(Page: page, PageSize: size), Now).IsFailure);
    [Fact]
    public void Public_metadata_does_not_serialize_private_binding_or_urls()
    {
        var row = Publication(); row.SubmissionId = Guid.NewGuid(); row.PackageVersion = 1; row.ManifestSha256 = new string('a', 64);
        var json = JsonSerializer.Serialize(row.ToPublic(), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain(row.SubmissionId.ToString()!, json); Assert.DoesNotContain("manifest", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("downloadUrl", json); Assert.DoesNotContain("objectKey", json);
    }
    [Fact]
    public void Publication_configuration_fails_closed_on_duplicate_slug_or_partial_document_binding()
    {
        Assert.True(new ThesisLibraryOptions().IsValid());
        Assert.False(new ThesisLibraryOptions { Publications = [Publication(), Publication()] }.IsValid());
        var row = Publication(); row.SubmissionId = Guid.NewGuid();
        Assert.False(row.IsValid()); row.PackageVersion = 1; row.ManifestSha256 = new string('a', 64);
        Assert.True(row.IsValid()); row.AcademicYear = "2025-2027"; Assert.False(row.IsValid());
    }

    [Fact]
    public async Task Anonymous_and_inactive_accounts_cannot_request_document_urls()
    {
        var fixture = new DocumentFixture(); fixture.User.IsAuthenticated = false;
        Assert.Equal("FinalSubmission.AuthenticationRequired", (await fixture.Handler().HandleAsync(fixture.Publication.Slug, default)).Error.Code);
        fixture.User.IsAuthenticated = true; fixture.Authority.Active = false;
        Assert.Equal("FinalSubmission.AccessDenied", (await fixture.Handler().HandleAsync(fixture.Publication.Slug, default)).Error.Code);
        Assert.Equal(0, fixture.Storage.Signs);
    }
    [Fact]
    public async Task Unpublished_or_wrong_version_or_manifest_never_signs_a_url()
    {
        var f = new DocumentFixture();
        Assert.True((await f.Handler().HandleAsync("unlisted", default)).IsFailure);
        f.Publication.PackageVersion = 2;
        Assert.True((await f.Handler().HandleAsync(f.Publication.Slug, default)).IsFailure);
        f.Publication.PackageVersion = 1; f.Publication.ManifestSha256 = new string('b', 64);
        Assert.True((await f.Handler().HandleAsync(f.Publication.Slug, default)).IsFailure);
        f.Publication.ManifestSha256 = new string('a', 64); f.Proofs.Outcome = ArchiveProofOutcome.Legacy;
        Assert.True((await f.Handler().HandleAsync(f.Publication.Slug, default)).IsFailure);
        Assert.Equal(0, f.Storage.Signs);
    }
    [Fact]
    public async Task Active_reader_can_only_download_the_explicitly_bound_verified_pdf_with_short_ttl()
    {
        var f = new DocumentFixture();
        var result = await f.Handler().HandleAsync(f.Publication.Slug, default);
        Assert.True(result.IsSuccess); Assert.Equal(1, f.Storage.Signs);
        Assert.Equal(TimeSpan.FromMinutes(5), f.Storage.Ttl); Assert.Equal(f.File.ObjectKey, f.Storage.Key);
        Assert.Equal(Now.AddMinutes(5), result.Value.ExpiresAt);
    }
    private sealed class DocumentFixture
    {
        public ThesisLibraryPublication Publication { get; } = ThesisLibraryTests.Publication();
        public User User { get; } = new(); public Authority Authority { get; } = new(); public Storage Storage { get; } = new();
        public SubmissionFile File { get; } = new()
        {
            Id = Guid.NewGuid(),
            EntityId = Guid.NewGuid(),
            EntityType = SubmissionEntityType.FinalSubmission,
            VersionNo = 1,
            ContentType = "application/pdf",
            FileSize = 100,
            ChecksumSha256 = new string('c', 64),
            ObjectKey = "private/bound-report.pdf",
            FileName = "report.pdf"
        };
        public Store Store { get; }
        public Proofs Proofs { get; }
        public DocumentFixture()
        {
            var row = new FinalSubmission
            {
                Id = File.EntityId,
                GroupId = Guid.NewGuid(),
                Status = FinalSubmissionStatus.ArchiveConfirmed,
                CurrentVersionNo = 1,
                CurrentPackageId = Guid.NewGuid(),
                IsLegacy = false
            };
            Publication.SubmissionId = row.Id; Publication.PackageVersion = 1; Publication.ManifestSha256 = new string('a', 64);
            Store = new(row, new([], [new() { PackageId = row.CurrentPackageId.Value, SubmissionId = row.Id, SubmissionFileId = File.Id, FileRole = "ThesisPdf" }], [File], []));
            Proofs = new(row, File);
        }
        public ThesisLibraryDocumentHandler Handler() => new(new Catalog(Publication), User, Authority, Store, Proofs, Storage, new Clock());
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class User : ICurrentUser
    {
        public bool IsAuthenticated { get; set; } = true; public Guid? UserId { get; } = Guid.NewGuid(); public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = new HashSet<string>(); public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }
    private sealed class Authority : IFinalSubmissionAuthorityReader
    {
        public bool Active { get; set; } = true;
        public Task<FinalSubmissionAuthorityDto?> GetAsync(Guid id, CancellationToken token) => Task.FromResult<FinalSubmissionAuthorityDto?>(new(id, Active, ["Student"], []));
        public Task<FinalSubmissionAuthorityDto?> LockAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class Proofs(FinalSubmission row, SubmissionFile file) : IFinalSubmissionArchiveProofContract
    {
        public ArchiveProofOutcome Outcome { get; set; } = ArchiveProofOutcome.Succeeded;
        public Task<FinalSubmissionArchiveProofContractDto> GetArchiveProofAsync(Guid groupId, CancellationToken cancellationToken = default)
            => Task.FromResult(new FinalSubmissionArchiveProofContractDto(Outcome, row.GroupId, Guid.NewGuid(), row.Id, 1,
                row.CurrentPackageId, 1, new string('a', 64), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.UtcDateTime,
                Guid.NewGuid(), Now.UtcDateTime, [new(file.Id, row.CurrentPackageId!.Value, 1, "ThesisPdf", "application/pdf", file.FileSize, file.ChecksumSha256!)]));
        public Task<IReadOnlyList<FinalSubmissionArchiveProofContractDto>> GetManyArchiveProofsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FinalSubmissionArchiveProofContractDto> LockArchiveProofForCalculationAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Storage : IThesisSubmissionObjectStorage
    {
        public int Signs { get; private set; }
        public TimeSpan Ttl { get; private set; }
        public string? Key { get; private set; }
        public Task<Uri> CreateDownloadUriAsync(string key, string name, TimeSpan ttl, CancellationToken token = default)
        { Signs++; Key = key; Ttl = ttl; return Task.FromResult(new Uri("https://storage.example.test/signed-report")); }
        public Task<Uri> CreateDownloadUriAsync(string key, TimeSpan ttl, CancellationToken token = default) => throw new NotSupportedException();
        public Task UploadAsync(ThesisSubmissionObjectWriteRequest request, Stream content, CancellationToken token = default) => throw new NotSupportedException();
        public Task DeleteAsync(string key, CancellationToken token = default) => throw new NotSupportedException();
    }
    private sealed class Store(FinalSubmission row, FinalSubmissionPackageGraph graph) : IFinalSubmissionStore
    {
        public Task<FinalSubmission?> GetByIdAsync(Guid id, CancellationToken token) => Task.FromResult<FinalSubmission?>(id == row.Id ? row : null);
        public Task<FinalSubmissionPackageGraph> GetPackagesAsync(Guid id, CancellationToken token) => Task.FromResult(graph);
        public Task<StudentGroup?> GetGroupAsync(Guid id, bool locked, CancellationToken token) => throw new NotSupportedException();
        public Task<FinalSubmission?> GetByGroupAsync(Guid id, bool locked, CancellationToken token) => throw new NotSupportedException();
        public Task<FinalSubmissionPackageGraph> LockCurrentPackageAsync(Guid id, Guid packageId, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, Guid>> GetGroupTopicsAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => throw new NotSupportedException();
        public Task<(IReadOnlyList<FinalSubmission> Rows, int Count)> ListAsync(IReadOnlyCollection<Guid> topics, Guid? id, string? status, int page, int size, CancellationToken token) => throw new NotSupportedException();
        public Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token) => throw new NotSupportedException();
        public Task<FinalSubmissionIdempotency?> GetReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token) => throw new NotSupportedException();
        public void Add(FinalSubmission row) => throw new NotSupportedException();
        public void Add(FinalSubmissionPackage package, IReadOnlyList<SubmissionFile> files, IReadOnlyList<FinalSubmissionPackageFile> links) => throw new NotSupportedException();
        public void Add(FinalSubmissionIdempotency receipt) => throw new NotSupportedException();
        public void Add(FinalSubmissionReview review) => throw new NotSupportedException();
        public Task CreateAttemptAsync(FinalSubmissionUploadAttempt attempt, CancellationToken token) => throw new NotSupportedException();
        public Task<FinalSubmissionUploadAttempt?> LockAttemptAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
    }
}
