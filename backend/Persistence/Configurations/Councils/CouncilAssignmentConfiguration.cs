using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class CouncilAssignmentConfiguration : IEntityTypeConfiguration<CouncilAssignment>
{
    public void Configure(EntityTypeBuilder<CouncilAssignment> builder)
    {
        builder.ToTable("council_assignment", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.GroupId)
            .IsUnique();

        builder.Property(x => x.OrderNumber)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.HasOne(x => x.Council)
            .WithMany(c => c.Assignments)
            .HasForeignKey(x => x.CouncilId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<CouncilAssignment>(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
