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

        var seedTimestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            // Admin (Role ID = 1)
            new RolePermission { RoleId = 1, PermissionId = 1, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 1, PermissionId = 2, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 1, PermissionId = 3, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 1, PermissionId = 4, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 1, PermissionId = 5, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 1, PermissionId = 6, AssignedAt = seedTimestamp },

            // Faculty (Role ID = 2)
            new RolePermission { RoleId = 2, PermissionId = 1, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 2, PermissionId = 3, AssignedAt = seedTimestamp },

            // AcademicAffairs (Role ID = 3)
            new RolePermission { RoleId = 3, PermissionId = 1, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 3, PermissionId = 2, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 3, PermissionId = 3, AssignedAt = seedTimestamp },
            new RolePermission { RoleId = 3, PermissionId = 4, AssignedAt = seedTimestamp }
        );
    }
}
