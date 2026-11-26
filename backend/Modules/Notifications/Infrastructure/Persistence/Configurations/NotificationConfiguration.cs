using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notification", "notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.NotificationType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IsRead)
            .HasDefaultValue(false)
            .HasSentinel(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.SourceModule)
            .HasMaxLength(64)
            .HasDefaultValue("Notifications")
            .IsRequired();

        builder.Property(x => x.DeduplicationKey)
            .HasMaxLength(200);

        builder.Property(x => x.ResourceType)
            .HasMaxLength(64);

        builder.Property(x => x.ActionPath)
            .HasMaxLength(500);

        builder.Property(x => x.Priority)
            .HasMaxLength(20)
            .HasDefaultValue(NotificationPriorities.Normal)
            .IsRequired();

        builder.HasIndex(x => x.RecipientId)
            .HasDatabaseName("ix_notification_recipient_id");

        builder.HasIndex(x => new { x.RecipientId, x.CreatedAt, x.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_notification_recipient_created_id");

        builder.HasIndex(x => new { x.RecipientId, x.CreatedAt })
            .IsDescending(false, true)
            .HasFilter("is_read = false")
            .HasDatabaseName("ix_notification_recipient_unread");

        builder.HasIndex(x => new { x.RecipientId, x.DeduplicationKey })
            .IsUnique()
            .HasFilter("deduplication_key IS NOT NULL")
            .HasDatabaseName("ux_notification_recipient_deduplication_key");
    }
}
