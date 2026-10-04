using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Topics;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Topics;

public class StudentGroupConfiguration : IEntityTypeConfiguration<StudentGroup>
{
    public void Configure(EntityTypeBuilder<StudentGroup> builder)
    {
        builder.ToTable("student_group", Schemas.Topics);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasDefaultValue(StudentGroupStatus.Registered)
            .HasSentinel(StudentGroupStatus.Registered);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Topic)
            .WithMany(t => t.StudentGroups)
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
