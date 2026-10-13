using UGTM.BuildingBlocks.Infrastructure.Persistence;
using UGTM.Modules.ThesisManagement.Application.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

public sealed class ThesisManagementDbContext(DbContextOptions<ThesisManagementDbContext> options) : ModuleDbContext(options), IThesisUnitOfWork
{
    public override string Schema => "thesis";

    public DbSet<FinalReport> FinalReports => Set<FinalReport>();
    public DbSet<FinalSubmission> FinalSubmissions => Set<FinalSubmission>();
    public DbSet<MidtermReport> MidtermReports => Set<MidtermReport>();
    public DbSet<ProgressMilestone> ProgressMilestones => Set<ProgressMilestone>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<RenameRequest> RenameRequests => Set<RenameRequest>();
    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<StudentGroupMember> StudentGroupMembers => Set<StudentGroupMember>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<Syllabus> Syllabuses => Set<Syllabus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThesisManagementDbContext).Assembly);
    }
}
