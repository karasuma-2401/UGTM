using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Academic;

namespace UGTM.Persistence.Configurations.Academic;

public class RoleScoreWeightConfiguration : IEntityTypeConfiguration<RoleScoreWeight>
{
    public void Configure(EntityTypeBuilder<RoleScoreWeight> builder)
    {
        builder.ToTable("role_score_weight", Schemas.Academic);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.ConfigId, x.RoleId })
            .IsUnique();

        builder.Property(x => x.Weight)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasOne(x => x.Config)
            .WithMany(c => c.RoleScoreWeights)
            .HasForeignKey(x => x.ConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
