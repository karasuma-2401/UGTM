namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public sealed class ReviewerEvaluationIdempotencyRecordConfiguration
    : IEntityTypeConfiguration<ReviewerEvaluationIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<ReviewerEvaluationIdempotencyRecord> builder)
    {
        builder.ToTable("reviewer_evaluation_idempotency", "councils", table =>
        {
            table.HasCheckConstraint("ck_reviewer_evaluation_idempotency_status", "response_status_code IN (200, 201)");
            table.HasCheckConstraint("ck_reviewer_evaluation_idempotency_fingerprint",
                "request_fingerprint ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_reviewer_evaluation_idempotency_operation",
                "char_length(btrim(operation)) BETWEEN 1 AND 100 AND operation = btrim(operation)");
        });
        builder.HasKey(value => value.Id).HasName("pk_reviewer_evaluation_idempotency");
        builder.HasIndex(value => new { value.ActorId, value.Operation, value.IdempotencyKey })
            .HasDatabaseName("ux_reviewer_evaluation_idempotency_actor_operation_key")
            .IsUnique();
        builder.Property(value => value.Operation).HasMaxLength(100).IsRequired();
        builder.Property(value => value.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(value => value.ResponsePayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.ResponseStatusCode).IsRequired();
        builder.Property(value => value.CreatedAtUtc).IsRequired();
    }
}
