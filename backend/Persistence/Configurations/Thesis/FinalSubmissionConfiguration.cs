using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Thesis;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Thesis;

public class FinalSubmissionConfiguration : IEntityTypeConfiguration<FinalSubmission>
{
    public void Configure(EntityTypeBuilder<FinalSubmission> builder)
    {
        builder.ToTable("final_submission", Schemas.Thesis);

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.GroupId)
            .IsUnique();

        builder.Property(x => x.Status)
            .HasDefaultValue(FinalSubmissionStatus.NotSubmitted)
            .HasSentinel(FinalSubmissionStatus.NotSubmitted);

        builder.HasOne(x => x.Group)
            .WithOne()
            .HasForeignKey<FinalSubmission>(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ArchivedConfirmedByUser)
            .WithMany()
            .HasForeignKey(x => x.ArchivedConfirmedBy)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
