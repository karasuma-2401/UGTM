using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class CouncilMemberConfiguration : IEntityTypeConfiguration<CouncilMember>
{
    public void Configure(EntityTypeBuilder<CouncilMember> builder)
    {
        builder.ToTable("council_member", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.CouncilId, x.UserId })
            .IsUnique();

        builder.Property(x => x.RoleInCouncil)
            .IsRequired();

        builder.HasOne(x => x.Council)
            .WithMany(c => c.Members)
            .HasForeignKey(x => x.CouncilId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
