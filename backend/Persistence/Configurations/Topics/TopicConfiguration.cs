using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Topics;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Topics;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topic", Schemas.Topics);

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

        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Advisor)
            .WithMany()
            .HasForeignKey(x => x.AdvisorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
