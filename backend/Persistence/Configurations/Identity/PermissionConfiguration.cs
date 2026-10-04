using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Identity;

namespace UGTM.Persistence.Configurations.Identity;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permission", Schemas.Identity);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PermissionCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.PermissionCode)
            .IsUnique();

        builder.Property(x => x.PermissionName)
            .HasMaxLength(150);

        builder.Property(x => x.Module)
            .HasMaxLength(50);
    }
}
