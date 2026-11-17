namespace UGTM.Modules.AcademicPeriods.Infrastructure.Persistence.Configurations;

public sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("stage", "academic", table =>
        {
            table.HasCheckConstraint("ck_stage_order_index", "order_index BETWEEN 1 AND 7");
            table.HasCheckConstraint("ck_stage_dates", "end_date >= start_date");
        });

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.BatchId, x.OrderIndex })
            .HasDatabaseName("ix_stage_batch_id_order_index")
            .IsUnique();
        builder.HasIndex(x => new { x.BatchId, x.StageCode })
            .HasDatabaseName("ix_stage_batch_id_stage_code")
            .IsUnique();

        builder.Property(x => x.OrderIndex).IsRequired();
        builder.Property(x => x.StageCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StageName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.StartDate).IsRequired();
        builder.Property(x => x.EndDate).IsRequired();
        builder.Property(x => x.DeadlineAtUtc).IsRequired();
        builder.Property(x => x.IsOverridden).HasDefaultValue(false);

        builder.HasOne(x => x.Batch)
            .WithMany(x => x.Stages)
            .HasForeignKey(x => x.BatchId)
            .HasConstraintName("fk_stage_kltn_batch_batch_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
