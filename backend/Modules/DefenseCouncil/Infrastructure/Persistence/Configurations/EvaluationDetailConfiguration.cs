namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class EvaluationDetailConfiguration : IEntityTypeConfiguration<EvaluationDetail>
{
    public void Configure(EntityTypeBuilder<EvaluationDetail> builder)
    {
        builder.HasIndex(x => new { x.CriteriaId, x.EvaluationId, x.StudentId })
            .HasDatabaseName("ix_evaluation_detail_criteria_id_evaluation_id_student_id")
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("ix_evaluation_detail_student_id");

        builder.ToTable("evaluation_detail", "councils");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId)
            .HasColumnName("student_id");

        builder.Property(x => x.Score)
            .HasPrecision(4, 2)
            .IsRequired();

        builder.HasOne(x => x.Criteria)
            .WithMany()
            .HasForeignKey(x => x.CriteriaId)
            .HasConstraintName("fk_evaluation_detail_grading_criterias_criteria_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
