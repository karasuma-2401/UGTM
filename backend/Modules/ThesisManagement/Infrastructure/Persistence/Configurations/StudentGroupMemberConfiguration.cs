using UGTM.Modules.ThesisManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class StudentGroupMemberConfiguration : IEntityTypeConfiguration<StudentGroupMember>
{
    public void Configure(EntityTypeBuilder<StudentGroupMember> builder)
    {
        builder.HasIndex(x => new { x.StudentId, x.BatchId })
            .HasDatabaseName("ix_student_group_member_student_id_batch_id")
            .IsUnique();

        builder.HasIndex(x => new { x.GroupId, x.StudentId })
            .HasDatabaseName("ix_student_group_member_group_id_student_id")
            .IsUnique();

        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("ix_student_group_member_batch_id");

        builder.ToTable("student_group_member", "thesis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(x => x.GroupId)
            .HasConstraintName("fk_student_group_member_student_group_group_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
