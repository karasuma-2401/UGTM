using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Persistence.Entities.Thesis;
using UGTM.Persistence.Enums;

namespace UGTM.Persistence.Configurations.Thesis;

public class RenameRequestConfiguration : IEntityTypeConfiguration<RenameRequest>
{
    public void Configure(EntityTypeBuilder<RenameRequest> builder)
    {
        builder.ToTable("rename_request", Schemas.Thesis);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OldTitleVi)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.NewTitleVi)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.OldTitleEn)
            .HasMaxLength(350);

        builder.Property(x => x.NewTitleEn)
            .HasMaxLength(350);

        builder.Property(x => x.Reason)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasDefaultValue(RenameRequestStatus.Created)
            .HasSentinel(RenameRequestStatus.Created);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Group)
            .WithMany()
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
