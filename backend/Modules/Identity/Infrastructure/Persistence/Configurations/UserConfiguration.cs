using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(x => x.NormalizedEmail)
            .HasDatabaseName("ux_user_normalized_email")
            .IsUnique();
        builder.HasIndex(x => x.UserCode)
            .HasDatabaseName("ux_user_user_code")
            .IsUnique();

        builder.ToTable("user", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasColumnType("text");

        builder.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.UserCode)
            .HasMaxLength(20);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.SecurityStamp)
            .IsRequired();

        builder.Property(x => x.ConcurrencyStamp)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.AccessFailedCount)
            .HasDefaultValue(0)
            .IsRequired();
    }
}
