namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class ProgressMilestoneConfiguration : IEntityTypeConfiguration<ProgressMilestone>
{
    public void Configure(EntityTypeBuilder<ProgressMilestone> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_progress_milestone_group_id");

        builder.HasIndex(x => x.CreatedBy)
            .HasDatabaseName("ix_progress_milestone_created_by");

        builder.ToTable("progress_milestone", "thesis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.Deadline)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .HasConstraintName("fk_progress_milestone_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
