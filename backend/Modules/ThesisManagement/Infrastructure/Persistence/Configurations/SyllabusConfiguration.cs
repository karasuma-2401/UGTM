using UGTM.Modules.ThesisManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class SyllabusConfiguration : IEntityTypeConfiguration<Syllabus>
{
    public void Configure(EntityTypeBuilder<Syllabus> builder)
    {
        builder.HasIndex(x => x.ReviewedBy)
            .HasDatabaseName("ix_syllabus_reviewed_by");

        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_syllabus_group_id")
            .IsUnique();

        builder.ToTable("syllabus", "thesis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CurrentVersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.Status)
            .HasDefaultValue(SyllabusStatus.Submitted)
            .HasSentinel(SyllabusStatus.Submitted);

        builder.Property(x => x.FacultyFeedback)
            .HasColumnType("text");

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<Syllabus>(x => x.GroupId)
            .HasConstraintName("fk_syllabus_student_group_group_id")
            .OnDelete(DeleteBehavior.Cascade);

    }
}
