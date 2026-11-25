using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Notifications.Domain.Entities;

namespace UGTM.Modules.Notifications.Infrastructure.Persistence.Configurations;

public sealed class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
{
    public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
    {
        builder.ToTable("device_registration", "notifications");

        builder.HasKey(x => x.Id)
            .HasName("pk_notifications_device_registration");

        builder.Property(x => x.InstallationId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Token)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Platform)
            .HasMaxLength(30)
            .HasDefaultValue("web")
            .IsRequired();

        builder.Property(x => x.UserAgent)
            .HasMaxLength(256);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.LastSeenAt)
            .IsRequired();

        builder.HasIndex(x => x.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_device_registration_token_hash");

        builder.HasIndex(x => x.UserId)
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName("ix_device_registration_active_user");

        builder.HasIndex(x => new { x.UserId, x.InstallationId })
            .HasDatabaseName("ix_device_registration_user_installation");
    }
}
