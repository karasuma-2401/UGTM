using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasIndex(x => x.RoleId)
            .HasDatabaseName("ix_user_role_role_id");

        builder.ToTable("user_role", "identity");

        builder.HasKey(x => new { x.UserId, x.RoleId });

        builder.Property(x => x.AssignedAt)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("fk_user_role_user_user_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(x => x.RoleId)
            .HasConstraintName("fk_user_role_role_role_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
