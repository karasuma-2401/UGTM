using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

public sealed class ThesisManagementDbContext(DbContextOptions<ThesisManagementDbContext> options) : ModuleDbContext(options), IThesisUnitOfWork
{
    public override string Schema => "thesis";

    public DbSet<AuditOutboxMessage> AuditOutboxMessages => Set<AuditOutboxMessage>();
    public DbSet<FinalReport> FinalReports => Set<FinalReport>();
    public DbSet<FinalReportPlagiarismScan> FinalReportPlagiarismScans => Set<FinalReportPlagiarismScan>();
    public DbSet<FinalSubmission> FinalSubmissions => Set<FinalSubmission>();
    public DbSet<FinalSubmissionPackage> FinalSubmissionPackages => Set<FinalSubmissionPackage>();
    public DbSet<FinalSubmissionPackageFile> FinalSubmissionPackageFiles => Set<FinalSubmissionPackageFile>();
    public DbSet<FinalSubmissionReview> FinalSubmissionReviews => Set<FinalSubmissionReview>();
    public DbSet<FinalSubmissionIdempotency> FinalSubmissionIdempotencyRecords => Set<FinalSubmissionIdempotency>();
    public DbSet<FinalSubmissionUploadAttempt> FinalSubmissionUploadAttempts => Set<FinalSubmissionUploadAttempt>();
    public DbSet<MidtermReport> MidtermReports => Set<MidtermReport>();
    public DbSet<ProgressMilestone> ProgressMilestones => Set<ProgressMilestone>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<ProgressReportVersion> ProgressReportVersions => Set<ProgressReportVersion>();
    public DbSet<ReviewGateConfiguration> ReviewGateConfigurations => Set<ReviewGateConfiguration>();
    public DbSet<RenameRequest> RenameRequests => Set<RenameRequest>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<StudentGroupMember> StudentGroupMembers => Set<StudentGroupMember>();
    public DbSet<StudentRegistrationPenalty> StudentRegistrationPenalties => Set<StudentRegistrationPenalty>();
    public DbSet<TopicRegistrationEmail> TopicRegistrationEmails => Set<TopicRegistrationEmail>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<SubmissionAttachment> SubmissionAttachments => Set<SubmissionAttachment>();
    public DbSet<SubmissionProductLink> SubmissionProductLinks => Set<SubmissionProductLink>();
    public DbSet<ThesisUploadAttempt> ThesisUploadAttempts => Set<ThesisUploadAttempt>();
    public DbSet<SubmissionReview> SubmissionReviews => Set<SubmissionReview>();
    public DbSet<Syllabus> Syllabuses => Set<Syllabus>();

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ThesisManagementConcurrencyException(ex);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            } postgresException)
        {
            throw new ThesisManagementUniqueConstraintException(postgresException.ConstraintName, ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThesisManagementDbContext).Assembly);
    }
}
