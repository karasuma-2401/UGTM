using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Topics;

namespace UGTM.Persistence.Configurations.Topics;

public class StudentGroupMemberConfiguration : IEntityTypeConfiguration<StudentGroupMember>
{
    public void Configure(EntityTypeBuilder<StudentGroupMember> builder)
    {
        builder.ToTable("student_group_member", Schemas.Topics);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.GroupId, x.StudentId })
            .IsUnique();

        builder.HasIndex(x => new { x.StudentId, x.BatchId })
            .IsUnique();

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
