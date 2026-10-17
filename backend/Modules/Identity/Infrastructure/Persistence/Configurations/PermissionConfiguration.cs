using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Identity.Contracts.Authorization;

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

        builder.HasData(
            new Permission
            {
                Id = 1,
                PermissionCode = PermissionCodes.IdentityUsersRead,
                PermissionName = "Xem danh sách người dùng",
                Module = "Identity"
            },
            new Permission
            {
                Id = 2,
                PermissionCode = PermissionCodes.IdentityUsersManage,
                PermissionName = "Quản lý người dùng",
                Module = "Identity"
            },
            new Permission
            {
                Id = 3,
                PermissionCode = PermissionCodes.IdentityUsersImport,
                PermissionName = "Nhập danh sách người dùng",
                Module = "Identity"
            },
            new Permission
            {
                Id = 4,
                PermissionCode = PermissionCodes.IdentityPermissionsRead,
                PermissionName = "Xem danh mục quyền",
                Module = "Identity"
            },
            new Permission
            {
                Id = 5,
                PermissionCode = PermissionCodes.IdentityRolesManage,
                PermissionName = "Quản lý vai trò và phân quyền",
                Module = "Identity"
            },
            new Permission
            {
                Id = 6,
                PermissionCode = PermissionCodes.IdentitySessionsRevoke,
                PermissionName = "Thu hồi phiên đăng nhập",
                Module = "Identity"
            });
    }
}
