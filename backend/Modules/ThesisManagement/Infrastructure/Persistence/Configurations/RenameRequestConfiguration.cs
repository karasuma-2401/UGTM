using UGTM.Modules.ThesisManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class RenameRequestConfiguration : IEntityTypeConfiguration<RenameRequest>
{
    public void Configure(EntityTypeBuilder<RenameRequest> builder)
    {
        builder.HasIndex(x => x.GroupId)
            .HasDatabaseName("ix_rename_request_group_id");

        builder.ToTable("rename_request", "thesis");

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
            .HasConstraintName("fk_rename_request_student_groups_group_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
