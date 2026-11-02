namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Configurations;

public sealed class FinalSubmissionPackageConfiguration : IEntityTypeConfiguration<FinalSubmissionPackage>
{
    public void Configure(EntityTypeBuilder<FinalSubmissionPackage> b)
    {
        b.ToTable("final_submission_package", "thesis", t =>
        {
            t.HasCheckConstraint("ck_archival_package", "version_no > 0 AND manifest_sha256 ~ '^[0-9a-f]{64}$' AND char_length(btrim(abstract_vi)) BETWEEN 1 AND 10000 AND char_length(btrim(abstract_en)) BETWEEN 1 AND 10000 AND char_length(btrim(keywords_vi)) BETWEEN 1 AND 1000 AND char_length(btrim(keywords_en)) BETWEEN 1 AND 1000");
        });
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.SubmissionId, x.VersionNo }).IsUnique();
        b.HasAlternateKey(x => new { x.Id, x.SubmissionId, x.VersionNo }).HasName("ak_archival_package_binding");
        b.Property(x => x.AbstractVi).HasColumnType("text");
        b.Property(x => x.AbstractEn).HasColumnType("text");
        b.Property(x => x.KeywordsVi).HasMaxLength(1000);
        b.Property(x => x.KeywordsEn).HasMaxLength(1000);
        b.Property(x => x.ManifestSha256).HasMaxLength(64);
        b.HasOne<FinalSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinalSubmissionPackageFileConfiguration : IEntityTypeConfiguration<FinalSubmissionPackageFile>
{
    public void Configure(EntityTypeBuilder<FinalSubmissionPackageFile> b)
    {
        b.ToTable("final_submission_package_file", "thesis", t =>
            t.HasCheckConstraint("ck_archival_file_role", "file_role IN ('ThesisPdf','ThesisDocx') AND entity_type = 'FinalSubmission'"));
        b.HasKey(x => new { x.PackageId, x.FileRole });
        b.Property(x => x.FileRole).HasMaxLength(20);
        b.HasIndex(x => x.SubmissionFileId).IsUnique();
        b.HasOne<FinalSubmissionPackage>().WithMany().HasForeignKey(x => new { x.PackageId, x.SubmissionId, x.VersionNo })
            .HasPrincipalKey(x => new { x.Id, x.SubmissionId, x.VersionNo }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SubmissionFile>().WithMany().HasForeignKey(x => new { x.SubmissionFileId, x.EntityType, x.SubmissionId, x.VersionNo })
            .HasPrincipalKey(x => new { x.Id, x.EntityType, x.EntityId, x.VersionNo }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinalSubmissionReviewConfiguration : IEntityTypeConfiguration<FinalSubmissionReview>
{
    public void Configure(EntityTypeBuilder<FinalSubmissionReview> b)
    {
        b.ToTable("final_submission_review", "thesis", t =>
        {
            t.HasCheckConstraint("ck_archival_review", "action IN ('AdvisorConfirm','FacultyConfirm','RequestRevision') AND stage IN ('Advisor','Faculty') AND manifest_sha256 ~ '^[0-9a-f]{64}$' AND ((action = 'RequestRevision' AND char_length(btrim(reason)) BETWEEN 1 AND 1000) OR (action <> 'RequestRevision' AND reason IS NULL))");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(30);
        b.Property(x => x.Stage).HasMaxLength(10);
        b.Property(x => x.Reason).HasColumnType("text");
        b.Property(x => x.ManifestSha256).HasMaxLength(64);
        b.Property(x => x.PreviousStatus).HasMaxLength(50);
        b.Property(x => x.ResultStatus).HasMaxLength(50);
        b.Property(x => x.DefenseProofSnapshot).HasColumnType("jsonb");
        b.HasIndex(x => new { x.SubmissionId, x.PackageVersionNo, x.DecidedAtUtc, x.Id });
        b.HasOne<FinalSubmissionPackage>().WithMany().HasForeignKey(x => new { x.PackageId, x.SubmissionId, x.PackageVersionNo })
            .HasPrincipalKey(x => new { x.Id, x.SubmissionId, x.VersionNo }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FinalSubmissionIdempotencyConfiguration : IEntityTypeConfiguration<FinalSubmissionIdempotency>
{
    public void Configure(EntityTypeBuilder<FinalSubmissionIdempotency> b)
    {
        b.ToTable("final_submission_idempotency", "thesis", t =>
            t.HasCheckConstraint("ck_archival_receipt", "operation IN ('FinalSubmission.Submit','FinalSubmission.Confirm') AND fingerprint ~ '^[0-9a-f]{64}$' AND response_status IN (200,201) AND jsonb_typeof(response_payload) = 'object'"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ActorId, x.Operation, x.IdempotencyKey }).IsUnique();
        b.Property(x => x.Operation).HasMaxLength(64);
        b.Property(x => x.Fingerprint).HasMaxLength(64);
        b.Property(x => x.ResponsePayload).HasColumnType("jsonb");
    }
}

public sealed class FinalSubmissionUploadAttemptConfiguration : IEntityTypeConfiguration<FinalSubmissionUploadAttempt>
{
    public void Configure(EntityTypeBuilder<FinalSubmissionUploadAttempt> b)
    {
        b.ToTable("final_submission_upload_attempt", "thesis", t =>
            t.HasCheckConstraint("ck_archival_attempt", "state IN ('Uploading','Committed','CleanupPending','Cleaning','Cleaned') AND retry_count >= 0 AND ((state = 'Committed' AND committed_package_id IS NOT NULL) OR (state <> 'Committed' AND committed_package_id IS NULL))"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasMaxLength(64);
        b.Property(x => x.Fingerprint).HasMaxLength(64);
        b.Property(x => x.PdfObjectKey).HasMaxLength(1000);
        b.Property(x => x.DocxObjectKey).HasMaxLength(1000);
        b.Property(x => x.State).HasMaxLength(20);
        b.Property(x => x.LastErrorCode).HasMaxLength(100);
        b.HasIndex(x => x.PdfObjectKey).IsUnique();
        b.HasIndex(x => x.DocxObjectKey).IsUnique();
        b.HasIndex(x => new { x.State, x.RetryAfterUtc, x.LeaseExpiresAtUtc });
        b.HasOne<StudentGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FinalSubmissionPackage>().WithMany().HasForeignKey(x => x.CommittedPackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
