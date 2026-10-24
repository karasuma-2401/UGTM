using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class StudentGroupMemberConfiguration : IEntityTypeConfiguration<StudentGroupMember>
{
    public void Configure(EntityTypeBuilder<StudentGroupMember> builder)
    {
        builder.ToTable("student_group_member", "thesis");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.StudentId, x.BatchId })
            .HasDatabaseName("ix_student_group_member_student_id_batch_id")
            .IsUnique();

        builder.HasIndex(x => new { x.GroupId, x.StudentId })
            .HasDatabaseName("ix_student_group_member_group_id_student_id")
            .IsUnique();

        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("ix_student_group_member_batch_id");

        builder.HasIndex(x => x.ConfirmationTokenHash)
            .HasDatabaseName("ix_student_group_member_confirmation_token_hash");

        builder.HasIndex(x => new { x.ConfirmationStatus, x.ConfirmationExpiresAtUtc })
            .HasDatabaseName("ix_student_group_member_status_expires");

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        builder.Property(x => x.ConfirmationStatus)
            .HasDefaultValue(MemberConfirmationStatus.Confirmed)
            .HasSentinel(MemberConfirmationStatus.Confirmed)
            .IsRequired();

        builder.Property(x => x.ConfirmationTokenHash)
            .HasMaxLength(128);

        builder.Property(x => x.ConfirmationExpiresAtUtc);
        builder.Property(x => x.ConfirmedAtUtc);
        builder.Property(x => x.InvitedByStudentId);

        builder.HasOne(x => x.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(x => x.GroupId)
            .HasConstraintName("fk_student_group_member_student_group_group_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
