using UGTM.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasIndex(x => x.RoleName)
            .HasDatabaseName("ix_role_role_name")
            .IsUnique();

        builder.ToTable("role", "identity");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.RoleName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(255);

        builder.HasData(Enum.GetValues<SystemRoleName>().Select(role => new Role
        {
            Id = (int)role + 1,
            RoleName = role.ToString(),
            Description = role.ToString()
        }));
    }
}
