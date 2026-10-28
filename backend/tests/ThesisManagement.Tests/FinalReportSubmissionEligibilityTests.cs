using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class FinalReportSubmissionEligibilityTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid PartnerId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Any_confirmed_student_can_pass_prerequisites_without_mutating_the_group(
        bool isPartner, bool alreadySubmitted)
    {
        var state = alreadySubmitted ? StudentGroupStatus.FinalReportSubmitted : StudentGroupStatus.InProgress;
        var group = CreateGroup(state);
        var fixtures = new Fixtures(group);
        var studentId = isPartner ? PartnerId : StudentId;
        using var cancellation = new CancellationTokenSource();

        var result = await fixtures.Checker(new User(studentId, Roles: new HashSet<string> { "student", "Lecturer" }))
            .CheckAsync(group.Id, cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Same(group, result.Value.Group);
        Assert.Equal(studentId, result.Value.StudentId);
        Assert.Equal(BatchId, result.Value.BatchId);
        Assert.Equal(Now, result.Value.SubmittedAtUtc);
        Assert.Equal(state, group.Status);
        Assert.Equal(MidtermReportStatus.Approved, fixtures.Midterms.Report!.Status);
        Assert.Equal(BatchId, fixtures.Batches.RequestedBatchId);
        Assert.Equal(Now, fixtures.Batches.RequestedTimeUtc);
        Assert.Equal(cancellation.Token, fixtures.Batches.RequestedCancellation);
        Assert.Equal(cancellation.Token, fixtures.Groups.RequestedCancellation);
        Assert.Equal(cancellation.Token, fixtures.Midterms.RequestedCancellation);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Authentication_and_a_user_identifier_are_required(bool authenticated, bool hasUserId)
    {
        var fixtures = new Fixtures(CreateGroup());
        var user = new User(hasUserId ? StudentId : null, authenticated);

        var result = await fixtures.Checker(user).CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.AuthenticationRequired, result.Error);
        Assert.Equal(0, fixtures.Groups.ReadCount);
    }

    [Fact]
    public async Task An_empty_user_identifier_cannot_authorize_a_submission()
    {
        var fixtures = new Fixtures(CreateGroup());

        var result = await fixtures.Checker(new User(Guid.Empty)).CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.AuthenticationRequired, result.Error);
        Assert.Equal(0, fixtures.Groups.ReadCount);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Faculty")]
    [InlineData("Lecturer")]
    [InlineData("AcademicAffairs")]
    [InlineData("")]
    public async Task Membership_or_an_administrative_role_does_not_replace_the_student_role(string role)
    {
        var fixtures = new Fixtures(CreateGroup());

        var result = await fixtures.Checker(new User(StudentId, Roles: new HashSet<string> { role }))
            .CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.AccessDenied, result.Error);
        Assert.Equal(0, fixtures.Groups.ReadCount);
    }

    [Fact]
    public async Task A_student_cannot_submit_for_another_group()
    {
        var fixtures = new Fixtures(CreateGroup());

        var result = await fixtures.Checker(new User(Guid.NewGuid())).CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.AccessDenied, result.Error);
        Assert.Equal(0, fixtures.Midterms.ReadCount);
    }

    [Fact]
    public async Task A_pending_member_cannot_submit_even_when_the_group_has_passed_midterm()
    {
        var fixtures = new Fixtures(CreateGroup(pendingPartner: true));

        var result = await fixtures.Checker(new User(PartnerId)).CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.AccessDenied, result.Error);
        Assert.Equal(0, fixtures.Midterms.ReadCount);
    }

    [Fact]
    public async Task An_empty_group_identifier_is_invalid()
    {
        var fixtures = new Fixtures(CreateGroup());

        var result = await fixtures.Checker().CheckAsync(Guid.Empty);

        Assert.Equal(FinalReportSubmissionErrors.InvalidRequest, result.Error);
        Assert.Equal(0, fixtures.Groups.ReadCount);
    }

    [Fact]
    public async Task A_missing_group_is_reported()
    {
        var fixtures = new Fixtures(CreateGroup());

        var result = await fixtures.Checker().CheckAsync(Guid.NewGuid());

        Assert.Equal(FinalReportSubmissionErrors.GroupNotFound, result.Error);
    }

    [Theory]
    [InlineData(StudentGroupStatus.Registered)]
    [InlineData(StudentGroupStatus.SyllabusSubmitted)]
    [InlineData(StudentGroupStatus.SyllabusRevisionRequired)]
    [InlineData(StudentGroupStatus.MidtermSubmitted)]
    [InlineData(StudentGroupStatus.Stopped)]
    public async Task Groups_outside_the_final_submission_workflow_are_blocked(StudentGroupStatus status)
    {
        var fixtures = new Fixtures(CreateGroup(status));

        var result = await fixtures.Checker().CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.InvalidGroupState, result.Error);
        Assert.Equal(status, fixtures.Group.Status);
        Assert.Equal(0, fixtures.Midterms.ReadCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(MidtermReportStatus.Submitted)]
    [InlineData(MidtermReportStatus.NeedRevision)]
    [InlineData(MidtermReportStatus.Rejected)]
    public async Task In_progress_alone_does_not_prove_midterm_approval(MidtermReportStatus? status)
    {
        var fixtures = new Fixtures(CreateGroup());
        fixtures.Midterms.Report = status.HasValue ? CreateMidterm(fixtures.Group.Id, status.Value) : null;

        var result = await fixtures.Checker().CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.MidtermNotApproved, result.Error);
        Assert.Null(fixtures.Batches.RequestedBatchId);
    }

    [Fact]
    public async Task A_missing_batch_is_reported()
    {
        var fixtures = new Fixtures(CreateGroup());
        fixtures.Batches.Window = null;

        var result = await fixtures.Checker().CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.BatchNotFound, result.Error);
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Closed", true)]
    [InlineData("Archived", true)]
    [InlineData("Open", false)]
    public async Task A_submission_requires_both_an_open_batch_and_an_open_window(string batchStatus, bool openWindow)
    {
        var fixtures = new Fixtures(CreateGroup());
        fixtures.Batches.Window = fixtures.Batches.Window! with
        {
            BatchStatus = batchStatus,
            IsSubmissionWindowOpen = openWindow
        };

        var result = await fixtures.Checker().CheckAsync(fixtures.Group.Id);

        Assert.Equal(FinalReportSubmissionErrors.OutsideAllowedWindow, result.Error);
    }

    private static StudentGroup CreateGroup(
        StudentGroupStatus status = StudentGroupStatus.InProgress, bool pendingPartner = false)
    {
        var group = new StudentGroup(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-80));
        group.AddConfirmedMember(StudentId, BatchId, Now.AddDays(-80));
        if (pendingPartner)
        {
            group.AddPendingMember(PartnerId, BatchId, StudentId, "confirmation-hash", Now.AddDays(-80), Now.AddDays(1));
        }
        else
        {
            group.AddConfirmedMember(PartnerId, BatchId, Now.AddDays(-80));
        }

        if (status == StudentGroupStatus.Registered) return group;
        Assert.True(group.TrySubmitSyllabus());
        if (status == StudentGroupStatus.SyllabusSubmitted) return group;
        if (status == StudentGroupStatus.SyllabusRevisionRequired)
        {
            Assert.True(group.TryOpenSyllabusRevision());
            return group;
        }
        Assert.True(group.TryApproveSyllabus());
        Assert.True(group.TrySubmitMidtermReport());
        if (status == StudentGroupStatus.MidtermSubmitted) return group;
        Assert.True(group.TryApproveMidtermReport());
        if (status == StudentGroupStatus.Stopped) Assert.True(group.TryStop());
        if (status == StudentGroupStatus.FinalReportSubmitted) Assert.True(group.TrySubmitFinalReport());
        return group;
    }

    private static MidtermReport CreateMidterm(Guid groupId, MidtermReportStatus status)
    {
        var report = MidtermReport.CreateSubmitted(Guid.NewGuid(), groupId);
        if (status != MidtermReportStatus.Submitted)
        {
            report.RecordReview(Enum.Parse<ReviewDecision>(status.ToString()), "Midterm feedback", Guid.NewGuid(), Now.AddDays(-1));
        }
        return report;
    }

    private sealed class Fixtures(StudentGroup group)
    {
        public StudentGroup Group { get; } = group;
        public GroupRepository Groups { get; } = new(group);
        public MidtermRepository Midterms { get; } = new(CreateMidterm(group.Id, MidtermReportStatus.Approved));
        public BatchReader Batches { get; } = new();

        public FinalReportSubmissionEligibilityChecker Checker(ICurrentUser? user = null)
            => new(Groups, Midterms, Batches, user ?? new User(StudentId), new FixedTimeProvider());
    }

    private sealed record User(Guid? UserId, bool IsAuthenticated = true, IReadOnlySet<string>? Roles = null) : ICurrentUser
    {
        public Guid? SessionId => null;
        public IReadOnlySet<string> Roles { get; } = Roles ?? new HashSet<string> { "Student" };
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class GroupRepository(StudentGroup group) : IStudentGroupRepository
    {
        public int ReadCount { get; private set; }
        public CancellationToken RequestedCancellation { get; private set; }
        public Task<StudentGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            RequestedCancellation = cancellationToken;
            return Task.FromResult(id == group.Id ? group : null);
        }
        public void Add(StudentGroup value) => throw new NotSupportedException();
    }

    private sealed class MidtermRepository(MidtermReport report) : IMidtermReportRepository
    {
        public MidtermReport? Report { get; set; } = report;
        public int ReadCount { get; private set; }
        public CancellationToken RequestedCancellation { get; private set; }
        public Task<MidtermReport?> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            RequestedCancellation = cancellationToken;
            return Task.FromResult(Report?.GroupId == groupId ? Report : null);
        }
        public Task<MidtermReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public void Add(MidtermReport value) => throw new NotSupportedException();
    }

    private sealed class BatchReader : IKltnBatchReader
    {
        public BatchFinalReportSubmissionWindowContractDto? Window { get; set; } = new(
            BatchId, "Open", true, Now.AddDays(-1), Now.AddDays(1));
        public Guid? RequestedBatchId { get; private set; }
        public DateTime? RequestedTimeUtc { get; private set; }
        public CancellationToken RequestedCancellation { get; private set; }
        public Task<BatchFinalReportSubmissionWindowContractDto?> GetFinalReportSubmissionWindowAsync(
            Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            RequestedBatchId = batchId;
            RequestedTimeUtc = nowUtc;
            RequestedCancellation = cancellationToken;
            return Task.FromResult(Window);
        }
        public Task<bool> ExistsAsync(Guid batchId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BatchRegistrationWindowContractDto?> GetRegistrationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BatchTopicPreparationWindowContractDto?> GetTopicPreparationWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BatchRenameRequestWindowContractDto?> GetRenameRequestWindowAsync(Guid batchId, DateTime nowUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
