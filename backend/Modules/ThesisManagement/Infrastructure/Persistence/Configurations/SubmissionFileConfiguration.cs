namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public class SubmissionFileConfiguration : IEntityTypeConfiguration<SubmissionFile>
{
    public void Configure(EntityTypeBuilder<SubmissionFile> builder)
    {
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.VersionNo })
            .HasDatabaseName("ix_submission_file_entity_type_entity_id_version_no");

        builder.HasIndex(x => x.UploadedBy)
            .HasDatabaseName("ix_submission_file_uploaded_by");

        builder.ToTable("submission_file", "thesis");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityType)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.VersionNo)
            .HasDefaultValue(1)
            .HasSentinel(1);

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FileUrl)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.FileSize)
            .IsRequired();

        builder.Property(x => x.FileExtension)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.UploadedAt)
            .IsRequired();

    }
}
