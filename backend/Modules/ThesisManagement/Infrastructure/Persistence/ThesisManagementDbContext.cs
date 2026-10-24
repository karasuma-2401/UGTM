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
    public DbSet<FinalSubmission> FinalSubmissions => Set<FinalSubmission>();
    public DbSet<MidtermReport> MidtermReports => Set<MidtermReport>();
    public DbSet<ProgressMilestone> ProgressMilestones => Set<ProgressMilestone>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<ProgressReportVersion> ProgressReportVersions => Set<ProgressReportVersion>();
    public DbSet<ReviewGateConfiguration> ReviewGateConfigurations => Set<ReviewGateConfiguration>();
    public DbSet<RenameRequest> RenameRequests => Set<RenameRequest>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<StudentGroupMember> StudentGroupMembers => Set<StudentGroupMember>();
    public DbSet<StudentRegistrationPenalty> StudentRegistrationPenalties => Set<StudentRegistrationPenalty>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
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
