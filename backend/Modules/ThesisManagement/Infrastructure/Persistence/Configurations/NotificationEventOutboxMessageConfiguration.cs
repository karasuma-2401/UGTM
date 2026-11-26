using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class NotificationEventOutboxMessageConfiguration
    : IEntityTypeConfiguration<NotificationEventOutboxMessage>
{
    public void Configure(EntityTypeBuilder<NotificationEventOutboxMessage> builder)
    {
        builder.ToTable("notification_event_outbox", "thesis");
        builder.HasKey(x => x.Id)
            .HasName("pk_thesis_notification_event_outbox");
        builder.Property(x => x.EventId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadVersion).HasDefaultValue(1).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.AttemptCount).HasDefaultValue(0);
        builder.Property(x => x.NextAttemptAt).IsRequired();
        builder.Property(x => x.LastErrorCode).HasMaxLength(100);

        builder.HasIndex(x => x.EventId)
            .IsUnique()
            .HasDatabaseName("ux_thesis_notification_event_outbox_event_id");

        builder.HasIndex(x => x.NextAttemptAt)
            .HasFilter("processed_at IS NULL")
            .HasDatabaseName("ix_thesis_notification_event_outbox_pending");
    }
}
