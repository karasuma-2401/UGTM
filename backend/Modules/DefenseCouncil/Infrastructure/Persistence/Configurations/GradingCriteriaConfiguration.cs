namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class GradingCriteriaConfiguration : IEntityTypeConfiguration<GradingCriteria>
{
    public void Configure(EntityTypeBuilder<GradingCriteria> builder)
    {
        builder.HasIndex(x => new { x.BatchId, x.CriteriaCode })
            .HasDatabaseName("ix_grading_criteria_batch_id_criteria_code")
            .IsUnique();

        builder.ToTable("grading_criteria", "councils");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CriteriaCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CriteriaName)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.MaxScore)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.Property(x => x.OrderIndex)
            .HasDefaultValue(1)
            .HasSentinel(1);

    }
}
