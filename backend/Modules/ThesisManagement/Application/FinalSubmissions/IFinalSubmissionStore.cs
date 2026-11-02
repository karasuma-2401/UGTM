using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public interface IFinalSubmissionStore
{
    Task<StudentGroup?> GetGroupAsync(Guid groupId, bool locked, CancellationToken token);
    Task<FinalSubmission?> GetByGroupAsync(Guid groupId, bool locked, CancellationToken token);
    Task<FinalSubmission?> GetByIdAsync(Guid id, CancellationToken token);
    Task<FinalSubmissionPackageGraph> GetPackagesAsync(Guid submissionId, CancellationToken token);
    Task<IReadOnlyDictionary<Guid, Guid>> GetGroupTopicsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken token);
    Task<(IReadOnlyList<FinalSubmission> Rows, int Count)> ListAsync(IReadOnlyCollection<Guid> topicIds, Guid? groupId,
        string? status, int page, int size, CancellationToken token);
    Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token);
    Task<FinalSubmissionIdempotency?> GetReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token);
    void Add(FinalSubmission submission);
    void Add(FinalSubmissionPackage package, IReadOnlyList<SubmissionFile> files, IReadOnlyList<FinalSubmissionPackageFile> links);
    void Add(FinalSubmissionIdempotency receipt);
    Task CreateAttemptAsync(FinalSubmissionUploadAttempt attempt, CancellationToken token);
    Task<FinalSubmissionUploadAttempt?> LockAttemptAsync(Guid attemptId, CancellationToken token);
}

/// <summary>Uses a fresh owner context for cleanup and uncertain-commit verification.</summary>
public interface IFinalSubmissionUploadCleanup
{
    Task CleanAsync(Guid? attemptId, CancellationToken token = default);
}
