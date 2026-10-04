using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Councils;

namespace UGTM.Persistence.Configurations.Councils;

public class ReviewerAssignmentConfiguration : IEntityTypeConfiguration<ReviewerAssignment>
{
    public void Configure(EntityTypeBuilder<ReviewerAssignment> builder)
    {
        builder.ToTable("reviewer_assignment", Schemas.Councils);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.GroupId)
            .IsUnique();

        builder.Property(x => x.AssignedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<ReviewerAssignment>(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Reviewer)
            .WithMany()
            .HasForeignKey(x => x.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssignedByUser)
            .WithMany()
            .HasForeignKey(x => x.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
