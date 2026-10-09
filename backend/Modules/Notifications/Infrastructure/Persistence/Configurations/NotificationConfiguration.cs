using UGTM.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasIndex(x => x.RecipientId)
            .HasDatabaseName("ix_notification_recipient_id");

        builder.ToTable("notification", "notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.NotificationType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IsRead)
            .HasDefaultValue(false)
            .HasSentinel(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

    }
}
