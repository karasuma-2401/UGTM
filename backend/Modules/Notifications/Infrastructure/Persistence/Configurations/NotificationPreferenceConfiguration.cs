using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preference", "notifications");

        builder.HasKey(x => x.Id)
            .HasName("pk_notifications_notification_preference");

        builder.Property(x => x.Category)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Category, x.Channel })
            .IsUnique()
            .HasDatabaseName("ux_notification_preference_user_category_channel");
    }
}
