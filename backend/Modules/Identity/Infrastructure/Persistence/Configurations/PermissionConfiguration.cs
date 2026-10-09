using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasIndex(x => x.PermissionCode)
            .HasDatabaseName("ix_permission_permission_code")
            .IsUnique();

        builder.ToTable("permission", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PermissionCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PermissionName)
            .HasMaxLength(150);

        builder.Property(x => x.Module)
            .HasMaxLength(50);
    }
}
