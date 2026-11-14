namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class SubmissionAttachmentConfiguration : IEntityTypeConfiguration<SubmissionAttachment>
{
    public void Configure(EntityTypeBuilder<SubmissionAttachment> builder)
    {
        builder.ToTable("submission_attachment", "thesis", table =>
            table.HasCheckConstraint("ck_submission_attachment_version_no", "version_no > 0"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.VersionNo, x.DisplayOrder })
            .HasDatabaseName("ix_submission_attachment_entity_version");

        builder.HasIndex(x => x.UploadedBy)
            .HasDatabaseName("ix_submission_attachment_uploaded_by");

        builder.Property(x => x.EntityType)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.VersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ObjectKey)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ChecksumSha256)
            .HasMaxLength(64);

        builder.Property(x => x.FileSize)
            .IsRequired();

        builder.Property(x => x.FileExtension)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.UploadedBy)
            .IsRequired();

        builder.Property(x => x.UploadedAt)
            .IsRequired();
    }
}
