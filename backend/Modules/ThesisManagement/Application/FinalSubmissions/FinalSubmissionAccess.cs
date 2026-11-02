using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.TopicCatalog.Contracts.ReviewerAssignments;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public sealed record ArchivalContext(StudentGroup Group, ReviewerAssignmentTopicContractDto Topic,
    FinalSubmissionBatchContractDto? Batch, FinalSubmissionAuthorityDto Actor);

public sealed class FinalSubmissionAccess(IFinalSubmissionStore store, ICurrentUser user,
    IFinalSubmissionAuthorityReader authority, IReviewerAssignmentTopicReader topics, IFinalSubmissionBatchReader batches)
{
    public Guid ActorId => user.UserId ?? Guid.Empty;
    public Error? AuthenticationError => !user.IsAuthenticated || ActorId == Guid.Empty ? FinalSubmissionErrors.AuthenticationRequired : null;
    public async Task<FinalSubmissionAuthorityDto?> ActorAsync(bool locked, CancellationToken token)
        => AuthenticationError is not null ? null : locked ? await authority.LockAsync(ActorId, token) : await authority.GetAsync(ActorId, token);
    public static bool HasRole(FinalSubmissionAuthorityDto? actor, string role)
        => actor is { IsActive: true } && actor.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public static bool IsManager(FinalSubmissionAuthorityDto? actor)
        => HasRole(actor, "Admin") || ((HasRole(actor, "Faculty") || HasRole(actor, "AcademicAffairs"))
            && actor!.Permissions.Contains(PermissionCodes.ThesisFinalSubmissionsRead));
    public static bool CanManage(FinalSubmissionAuthorityDto? actor)
        => HasRole(actor, "Admin") || (HasRole(actor, "Faculty") && actor!.Permissions.Contains(PermissionCodes.ThesisFinalSubmissionsManage));
    public async Task<Result<ArchivalContext>> ContextAsync(Guid groupId, bool studentOnly, bool locked, CancellationToken token)
        => await ContextCoreAsync(groupId, studentOnly, false, locked, token);
    public Task<Result<ArchivalContext>> ReviewContextAsync(Guid groupId, bool locked, CancellationToken token)
        => ContextCoreAsync(groupId, false, true, locked, token);
    private async Task<Result<ArchivalContext>> ContextCoreAsync(Guid groupId, bool studentOnly, bool review, bool locked, CancellationToken token)
    {
        if (AuthenticationError is { } authError) return Result.Failure<ArchivalContext>(authError);
        if (groupId == Guid.Empty) return Result.Failure<ArchivalContext>(FinalSubmissionErrors.InvalidRequest);
        var group = await store.GetGroupAsync(groupId, locked, token);
        if (group is null) return Result.Failure<ArchivalContext>(FinalSubmissionErrors.AccessDenied);
        // Root/membership -> topic -> batch -> authority, shared across all consumers.
        var topic = locked ? await topics.LockAsync(group.TopicId, token) : await topics.GetAsync(group.TopicId, token);
        var batch = topic is null ? null : locked ? await batches.LockAsync(topic.BatchId, token) : await batches.GetAsync(topic.BatchId, token);
        var actor = await ActorAsync(locked, token);
        var ownStudent = HasRole(actor, "Student") && group.Members.Any(m => m.StudentId == ActorId && m.ConfirmationStatus == MemberConfirmationStatus.Confirmed);
        var advisor = topic is not null && topic.AdvisorId == ActorId && HasRole(actor, "Lecturer");
        if (review ? !(advisor || CanManage(actor)) : studentOnly ? !ownStudent : !(ownStudent || advisor || IsManager(actor)))
            return Result.Failure<ArchivalContext>(FinalSubmissionErrors.AccessDenied);
        var batchIds = group.Members.Select(m => m.BatchId).Distinct().ToArray();
        if (topic is null || batchIds.Length != 1 || batchIds[0] != topic.BatchId)
            return Result.Failure<ArchivalContext>(FinalSubmissionErrors.ContextInvalid);
        return Result.Success(new ArchivalContext(group, topic, batch, actor!));
    }
}
