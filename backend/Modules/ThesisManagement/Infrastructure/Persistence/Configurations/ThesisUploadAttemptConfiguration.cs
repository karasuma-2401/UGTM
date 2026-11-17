using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class ThesisUploadAttemptConfiguration : IEntityTypeConfiguration<ThesisUploadAttempt>
{
    public void Configure(EntityTypeBuilder<ThesisUploadAttempt> builder)
    {
        builder.ToTable("submission_upload_attempt", "thesis", table =>
            table.HasCheckConstraint("ck_submission_upload_attempt", "state IN ('Uploading','Committed','CleanupPending','Cleaning','Cleaned') AND retry_count >= 0"));
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.ObjectKey).HasMaxLength(1024).IsRequired();
        builder.Property(attempt => attempt.State).HasMaxLength(24).IsRequired();
        builder.Property(attempt => attempt.LastErrorCode).HasMaxLength(100);
        builder.HasIndex(attempt => attempt.ObjectKey).IsUnique();
        builder.HasIndex(attempt => new { attempt.State, attempt.LeaseExpiresAtUtc });
    }
}
