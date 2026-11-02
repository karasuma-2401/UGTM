namespace UGTM.Modules.ThesisManagement.Domain.Entities;

public sealed class FinalSubmissionPackage
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int VersionNo { get; set; }
    public string AbstractVi { get; set; } = null!;
    public string AbstractEn { get; set; } = null!;
    public string KeywordsVi { get; set; } = null!;
    public string KeywordsEn { get; set; } = null!;
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string ManifestSha256 { get; set; } = null!;
}

public sealed class FinalSubmissionPackageFile
{
    public Guid PackageId { get; set; }
    public Guid SubmissionId { get; set; }
    public int VersionNo { get; set; }
    public string FileRole { get; set; } = null!;
    public Guid SubmissionFileId { get; set; }
    public Enums.SubmissionEntityType EntityType { get; set; } = Enums.SubmissionEntityType.FinalSubmission;
}

public sealed class FinalSubmissionReview
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid PackageId { get; set; }
    public int PackageVersionNo { get; set; }
    public string Action { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public Guid ActorId { get; set; }
    public Guid AssignedAdvisorId { get; set; }
    public DateTime DecidedAtUtc { get; set; }
    public string? Reason { get; set; }
    public string ManifestSha256 { get; set; } = null!;
    public string DefenseProofSnapshot { get; set; } = null!;
    public Guid IdempotencyKey { get; set; }
    public string PreviousStatus { get; set; } = null!;
    public string ResultStatus { get; set; } = null!;
}

public sealed class FinalSubmissionIdempotency
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Operation { get; set; } = null!;
    public Guid IdempotencyKey { get; set; }
    public string Fingerprint { get; set; } = null!;
    public int ResponseStatus { get; set; }
    public string ResponsePayload { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class FinalSubmissionUploadAttempt
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public Guid GroupId { get; set; }
    public string Operation { get; set; } = "FinalSubmission.Submit";
    public Guid IdempotencyKey { get; set; }
    public string Fingerprint { get; set; } = null!;
    public string PdfObjectKey { get; set; } = null!;
    public string DocxObjectKey { get; set; } = null!;
    public string State { get; set; } = "Uploading";
    public DateTime LeaseExpiresAtUtc { get; set; }
    public Guid? CommittedPackageId { get; set; }
    public int RetryCount { get; set; }
    public DateTime? RetryAfterUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? LastErrorCode { get; set; }
}
