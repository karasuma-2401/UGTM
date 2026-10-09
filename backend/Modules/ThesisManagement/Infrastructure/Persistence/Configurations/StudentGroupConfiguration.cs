using UGTM.Modules.ThesisManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.HasIndex(x => x.TopicId)
            .HasDatabaseName("ix_student_group_topic_id");

        builder.ToTable("student_group", "thesis");

        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.DomainEvents);
        builder.Navigation(x => x.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Status)
            .HasDefaultValue(StudentGroupStatus.Registered)
            .HasSentinel(StudentGroupStatus.Registered);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

    }
}
