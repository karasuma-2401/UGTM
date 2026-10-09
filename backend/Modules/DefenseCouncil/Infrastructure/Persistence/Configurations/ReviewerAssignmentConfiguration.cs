using UGTM.Modules.DefenseCouncil.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Persistence.Configurations;

public class ReviewerAssignmentConfiguration : IEntityTypeConfiguration<ReviewerAssignment>
{
    public void Configure(EntityTypeBuilder<ReviewerAssignment> builder)
    {
        builder.HasIndex(x => new { x.GroupId, x.ReviewerId })
            .HasDatabaseName("ix_reviewer_assignment_group_id_reviewer_id")
            .IsUnique();

        builder.HasIndex(x => new { x.GroupId, x.AssignmentOrder })
            .HasDatabaseName("ix_reviewer_assignment_group_id_assignment_order")
            .IsUnique();

        builder.HasIndex(x => x.ReviewerId)
            .HasDatabaseName("ix_reviewer_assignment_reviewer_id");

        builder.HasIndex(x => x.AssignedBy)
            .HasDatabaseName("ix_reviewer_assignment_assigned_by");

        builder.ToTable("reviewer_assignment", "councils");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssignmentOrder).HasDefaultValue(1).HasSentinel(1);
        builder.Property(x => x.IsActive).HasDefaultValue(true).HasSentinel(true);

        builder.Property(x => x.AssignedAt)
            .IsRequired();

    }
}
