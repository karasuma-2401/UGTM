namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class CouncilAssignmentConfiguration : IEntityTypeConfiguration<CouncilAssignment>
{
    public void Configure(EntityTypeBuilder<CouncilAssignment> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_council_assignment_group_id")
            .IsUnique();

        builder.HasIndex(x => x.CouncilId)
            .HasDatabaseName("ix_council_assignment_council_id");

        builder.ToTable("council_assignment", "councils");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.HasOne(x => x.Council)
            .WithMany(c => c.Assignments)
            .HasForeignKey(x => x.CouncilId)
            .HasConstraintName("fk_council_assignment_councils_council_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
