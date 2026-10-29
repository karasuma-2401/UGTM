namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public sealed class AdvisorEvaluationIdempotencyRecordConfiguration
    : IEntityTypeConfiguration<AdvisorEvaluationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<AdvisorEvaluationIdempotencyRecord> builder)
    {
        builder.ToTable("advisor_evaluation_idempotency", "councils");
        builder.HasKey(value => value.Id)
            .HasName("pk_advisor_evaluation_idempotency");
        builder.HasIndex(value => new { value.ActorId, value.Operation, value.IdempotencyKey })
            .HasDatabaseName("ux_advisor_evaluation_idempotency_actor_operation_key")
            .IsUnique();
        builder.Property(value => value.Operation).HasMaxLength(100).IsRequired();
        builder.Property(value => value.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(value => value.ResponsePayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.ResponseStatusCode).IsRequired();
        builder.Property(value => value.CreatedAtUtc).IsRequired();
    }
}
