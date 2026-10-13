namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.HasIndex(x => new { x.BatchId, x.StageIndex })
            .HasDatabaseName("ix_stage_batch_id_stage_index")
            .IsUnique();

        builder.ToTable("stage", "academic", table =>
            table.HasCheckConstraint("ck_stage_index", "stage_index BETWEEN 1 AND 7"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StageIndex)
            .IsRequired();

        builder.Property(x => x.StageName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        builder.Property(x => x.DeadlineDate)
            .IsRequired();

        builder.HasOne(x => x.Batch)
            .WithMany(b => b.Stages)
            .HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_stage_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
