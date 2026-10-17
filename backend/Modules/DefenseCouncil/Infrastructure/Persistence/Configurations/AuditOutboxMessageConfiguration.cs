namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public sealed class AuditOutboxMessageConfiguration
    : IEntityTypeConfiguration<AuditOutboxMessage>
{
    public void Configure(EntityTypeBuilder<AuditOutboxMessage> builder)
    {
        builder.ToTable("audit_outbox_message", "councils");
        builder.HasKey(x => x.Id)
            .HasName("pk_councils_audit_outbox_message");
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt })
            .HasDatabaseName("ix_councils_audit_outbox_pending");
    }
}
