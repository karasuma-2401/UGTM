using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IntegrationEventId);

        builder.Property(x => x.ActorUserId);

        builder.Property(x => x.ActorSessionId);

        builder.Property(x => x.TargetType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.TargetId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Outcome)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ReasonCode)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(x => x.RequestId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MetadataJson)
            .HasColumnType("jsonb")
            .IsRequired(false);

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.HasIndex(x => x.ActorUserId)
            .HasDatabaseName("ix_audit_log_actor_user_id");

        builder.HasIndex(x => x.ActorSessionId)
            .HasDatabaseName("ix_audit_log_actor_session_id");

        builder.HasIndex(x => x.Timestamp)
            .HasDatabaseName("ix_audit_log_timestamp");

        builder.HasIndex(x => x.RequestId)
            .HasDatabaseName("ix_audit_log_request_id");

        builder.HasIndex(x => x.IntegrationEventId)
            .IsUnique()
            .HasFilter("integration_event_id IS NOT NULL")
            .HasDatabaseName("ux_audit_log_integration_event_id");
    }
}
