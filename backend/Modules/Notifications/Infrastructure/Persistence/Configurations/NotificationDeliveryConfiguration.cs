using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_delivery", "notifications");

        builder.HasKey(x => x.Id)
            .HasName("pk_notifications_notification_delivery");

        builder.Property(x => x.Channel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Destination)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(30)
            .HasDefaultValue(NotificationDeliveryStatuses.Pending)
            .IsRequired();

        builder.Property(x => x.AttemptCount)
            .HasDefaultValue(0);

        builder.Property(x => x.NextAttemptAt)
            .IsRequired();

        builder.Property(x => x.LastErrorCode)
            .HasMaxLength(100);

        builder.Property(x => x.LastErrorMessage)
            .HasColumnType("text");

        builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Notification)
            .WithMany()
            .HasForeignKey(x => x.NotificationId)
            .HasConstraintName("fk_notification_delivery_notification_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.NotificationId, x.Channel, x.Destination })
            .IsUnique()
            .HasDatabaseName("ux_notification_delivery_unique_target");

        builder.HasIndex(x => x.NextAttemptAt)
            .HasFilter("status IN ('Pending', 'RetryScheduled')")
            .HasDatabaseName("ix_notification_delivery_pending");
    }
}
