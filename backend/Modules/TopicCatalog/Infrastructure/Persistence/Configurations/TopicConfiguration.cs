using UGTM.Modules.TopicCatalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.TopicCatalog.Domain.Enums;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Configurations;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasIndex(x => x.BatchId)
            .HasDatabaseName("ix_topic_batch_id");

        builder.HasIndex(x => x.AdvisorId)
            .HasDatabaseName("ix_topic_advisor_id");

        builder.ToTable("topic", "topics", table =>
            table.HasCheckConstraint("ck_topic_student_number", "student_number IS NULL OR student_number BETWEEN 1 AND 2"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TitleVi)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.TitleEn)
            .HasMaxLength(350);

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.Requirements)
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasDefaultValue(TopicStatus.Draft)
            .HasSentinel(TopicStatus.Draft);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

    }
}
