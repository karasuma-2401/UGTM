using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasIndex(x => x.PermissionId)
            .HasDatabaseName("ix_role_permission_permission_id");

        builder.ToTable("role_permission", "identity");

        builder.HasKey(x => new { x.RoleId, x.PermissionId });

        builder.Property(x => x.AssignedAt)
            .IsRequired();

        builder.HasOne(x => x.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(x => x.RoleId)
            .HasConstraintName("fk_role_permission_role_role_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(x => x.PermissionId)
            .HasConstraintName("fk_role_permission_permission_permission_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
