using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public sealed record ConfirmFinalSubmissionCommand(Guid SubmissionId, FinalSubmissionReviewAction Action,
    uint ExpectedSubmissionVersion, int ExpectedPackageVersion, Guid Key, string? Reason = null);

public sealed class ConfirmFinalSubmissionHandler(IFinalSubmissionStore store, FinalSubmissionAccess access,
    ILockedDefenseProofReader defense, IFinalSubmissionWorkflowTransactionCoordinator coordinator,
    IThesisManagementAuditOutbox audit, TimeProvider clock,
    IThesisNotificationOutbox? notificationOutbox = null)
{
    private const string Operation = "FinalSubmission.Confirm";
    public async Task<bool> CanReviewAsync(CancellationToken token = default)
    {
        var actor = await access.ActorAsync(false, token);
        return FinalSubmissionAccess.HasRole(actor, "Lecturer") || FinalSubmissionAccess.CanManage(actor);
    }

    public async Task<Result<FinalSubmissionConfirmationDto>> HandleAsync(ConfirmFinalSubmissionCommand command, CancellationToken token = default)
    {
        if (access.AuthenticationError is { } authentication) return Fail(authentication);
        if (!await CanReviewAsync(token)) return Fail(FinalSubmissionErrors.AccessDenied);
        if (command.SubmissionId == Guid.Empty || command.Key == Guid.Empty || command.ExpectedSubmissionVersion == 0
            || command.ExpectedPackageVersion < 1 || !Enum.IsDefined(command.Action)) return Fail(FinalSubmissionErrors.InvalidRequest);
        var reason = command.Action == FinalSubmissionReviewAction.RequestRevision ? FinalSubmissionMapping.Normalize(command.Reason, 1000) : null;
        if (command.Action == FinalSubmissionReviewAction.RequestRevision ? reason is null : command.Reason is not null)
            return Fail(FinalSubmissionErrors.InvalidRequest);
        var hash = FinalSubmissionMapping.Hash(new { command.SubmissionId, Action = command.Action.ToString(), command.ExpectedSubmissionVersion, command.ExpectedPackageVersion, Reason = reason });
        FinalSubmission? saved = null;
        string? manifest = null;
        DateTime now = default;
        ArchivalContext? committedContext = null;
        string? committedStage = null;
        try
        {
            return await coordinator.ExecuteWithPostSaveAsync(async ct =>
            {
                await store.KeyLockAsync(access.ActorId, Operation, command.Key, ct);
                // Recheck current ownership after waiting on the key, including successful replays.
                var known = await store.GetByIdAsync(command.SubmissionId, ct);
                if (known is null) return Abort(FinalSubmissionErrors.NotFound);
                var context = await access.ReviewContextAsync(known.GroupId, false, ct);
                if (context.IsFailure) return Abort(context.Error);
                if (!Authorized(context.Value, command.Action)) return Abort(FinalSubmissionErrors.AccessDenied);
                var receipt = await store.GetReceiptAsync(access.ActorId, Operation, command.Key, ct);
                if (receipt is not null)
                    return receipt.Fingerprint != hash ? Abort(FinalSubmissionErrors.IdempotencyConflict)
                        : FinalSubmissionTransactionResult<Result<FinalSubmissionConfirmationDto>>.Abort(Result.Success(JsonSerializer.Deserialize<FinalSubmissionConfirmationDto>(receipt.ResponsePayload)!));
                context = await access.ReviewContextAsync(known.GroupId, true, ct);
                if (context.IsFailure) return Abort(context.Error);
                if (!Authorized(context.Value, command.Action)) return Abort(FinalSubmissionErrors.AccessDenied);
                saved = await store.GetByGroupAsync(known.GroupId, true, ct);
                if (saved is null || saved.Id != command.SubmissionId) return Abort(FinalSubmissionErrors.NotFound);
                if (saved.Status == FinalSubmissionStatus.ArchiveConfirmed) return Abort(FinalSubmissionErrors.Locked);
                if (saved.IsLegacy || saved.CurrentPackageId is null) return Abort(FinalSubmissionErrors.Legacy);
                if (saved.Version != command.ExpectedSubmissionVersion || saved.CurrentVersionNo != command.ExpectedPackageVersion)
                    return Abort(FinalSubmissionErrors.VersionConflict);
                if (!ValidState(saved.Status, command.Action, context.Value)) return Abort(FinalSubmissionErrors.InvalidState);
                if (context.Value.Batch?.BatchStatus is not ("Open" or "Closed")) return Abort(FinalSubmissionErrors.BatchNotWritable);
                var graph = await store.LockCurrentPackageAsync(saved.Id, saved.CurrentPackageId.Value, ct);
                if (!FinalSubmissionPackageValidation.TryValidate(saved, graph, context.Value.Topic.BatchId, context.Value.Topic.TopicId, out var verified))
                    return Abort(FinalSubmissionErrors.ContextInvalid);
                var proof = await defense.LockAsync(saved.GroupId, ct);
                manifest = verified!.Package.ManifestSha256;
                now = clock.GetUtcNow().UtcDateTime;
                if (now < saved.SubmittedAtUtc || now < saved.AdvisorConfirmedAt) return Abort(FinalSubmissionErrors.ContextInvalid);
                var snapshot = new FinalSubmissionDefenseSnapshot(context.Value.Topic.TopicId, context.Value.Topic.BatchId, context.Value.Topic.AdvisorId, proof);
                // Revision is the recovery path when the previously reviewed context has changed.
                // Only confirmations require a currently valid Locked proof.
                if (command.Action != FinalSubmissionReviewAction.RequestRevision)
                {
                    if (proof.Outcome != LockedDefenseOutcome.Succeeded) return Abort(FinalSubmissionErrors.DefenseNotLocked);
                    if (!snapshot.IsValid(saved.GroupId, context.Value.Topic.BatchId, context.Value.Topic.TopicId, context.Value.Topic.AdvisorId, now))
                        return Abort(FinalSubmissionErrors.ContextInvalid);
                    if (command.Action == FinalSubmissionReviewAction.FacultyConfirm && snapshot != verified!.AdvisorSnapshot)
                        return Abort(FinalSubmissionErrors.ContextInvalid);
                }
                var previous = saved.Status;
                var reviewStage = Stage(command.Action, previous, context.Value);
                saved.RecordReview(command.Action, access.ActorId, now, reason);
                store.Add(new FinalSubmissionReview
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = saved.Id,
                    PackageId = verified!.Package.Id,
                    PackageVersionNo = saved.CurrentVersionNo,
                    Action = command.Action.ToString(),
                    Stage = reviewStage,
                    ActorId = access.ActorId,
                    AssignedAdvisorId = context.Value.Topic.AdvisorId,
                    DecidedAtUtc = now,
                    Reason = reason,
                    ManifestSha256 = verified.Package.ManifestSha256,
                    DefenseProofSnapshot = JsonSerializer.Serialize(snapshot),
                    IdempotencyKey = command.Key,
                    PreviousStatus = previous.ToString(),
                    ResultStatus = saved.Status.ToString()
                });
                committedContext = context.Value;
                committedStage = reviewStage;
                return FinalSubmissionTransactionResult<Result<FinalSubmissionConfirmationDto>>.Commit(Result.Success(Dto(saved, now)));
            }, (result, ct) =>
            {
                var response = Dto(saved!, now);
                store.Add(new FinalSubmissionIdempotency
                {
                    Id = Guid.NewGuid(),
                    ActorId = access.ActorId,
                    Operation = Operation,
                    IdempotencyKey = command.Key,
                    Fingerprint = hash,
                    ResponseStatus = 200,
                    ResponsePayload = JsonSerializer.Serialize(response),
                    CreatedAtUtc = now
                });
                var auditAction = command.Action switch
                {
                    FinalSubmissionReviewAction.AdvisorConfirm => "AdvisorConfirmed",
                    FinalSubmissionReviewAction.FacultyConfirm => "ArchiveConfirmed",
                    _ => "RevisionRequested"
                };
                audit.Add(new AuditIntegrationEntry("FinalSubmission", saved!.Id.ToString(), $"ThesisManagement.FinalSubmission.{auditAction}", "Succeeded", null,
                    new { saved.GroupId, saved.CurrentVersionNo, saved.Version, saved.CurrentPackageId, ManifestSha256 = manifest, ActorId = access.ActorId, HasReason = reason is not null }), now);

                if (notificationOutbox is not null && committedContext is not null)
                {
                    EnqueueConfirmationNotifications(
                        notificationOutbox,
                        committedContext,
                        saved,
                        command.Action,
                        committedStage ?? "Faculty",
                        reason,
                        access.ActorId,
                        now);
                }

                return Task.FromResult(Result.Success(response));
            }, token);
        }
        catch (ThesisManagementPersistenceException) { return Fail(FinalSubmissionErrors.ConcurrencyConflict); }
    }
    private bool Authorized(ArchivalContext c, FinalSubmissionReviewAction action)
        => FinalSubmissionAccess.HasRole(c.Actor, "Admin") || action switch
        {
            FinalSubmissionReviewAction.AdvisorConfirm => OwnAdvisor(c),
            FinalSubmissionReviewAction.FacultyConfirm => FinalSubmissionAccess.CanManage(c.Actor),
            FinalSubmissionReviewAction.RequestRevision => OwnAdvisor(c) || FinalSubmissionAccess.CanManage(c.Actor),
            _ => false
        };
    private bool OwnAdvisor(ArchivalContext c) => c.Topic.AdvisorId == access.ActorId && FinalSubmissionAccess.HasRole(c.Actor, "Lecturer");
    private bool ValidState(FinalSubmissionStatus state, FinalSubmissionReviewAction action, ArchivalContext c)
        => action switch
        {
            FinalSubmissionReviewAction.AdvisorConfirm => state == FinalSubmissionStatus.Submitted,
            FinalSubmissionReviewAction.FacultyConfirm => state == FinalSubmissionStatus.AdvisorConfirmed,
            FinalSubmissionReviewAction.RequestRevision => state == FinalSubmissionStatus.AdvisorConfirmed
                || (state == FinalSubmissionStatus.Submitted && (OwnAdvisor(c) || FinalSubmissionAccess.HasRole(c.Actor, "Admin"))),
            _ => false
        };
    private string Stage(FinalSubmissionReviewAction action, FinalSubmissionStatus state, ArchivalContext c)
        => action switch
        {
            FinalSubmissionReviewAction.AdvisorConfirm => "Advisor",
            FinalSubmissionReviewAction.FacultyConfirm => "Faculty",
            _ => FinalSubmissionAccess.HasRole(c.Actor, "Admin") ? state == FinalSubmissionStatus.Submitted ? "Advisor" : "Faculty"
                : OwnAdvisor(c) ? "Advisor" : "Faculty"
        };
    private static FinalSubmissionConfirmationDto Dto(FinalSubmission s, DateTime now)
        => new(s.Id, s.GroupId, s.Status.ToString(), s.CurrentVersionNo, s.Version, s.AdvisorConfirmedBy, s.AdvisorConfirmedAt,
            s.ArchivedConfirmedBy, s.ArchivedConfirmedAt, s.FacultyFeedback, now);
    private static Result<FinalSubmissionConfirmationDto> Fail(Error e) => Result.Failure<FinalSubmissionConfirmationDto>(e);
    private static FinalSubmissionTransactionResult<Result<FinalSubmissionConfirmationDto>> Abort(Error e)
        => FinalSubmissionTransactionResult<Result<FinalSubmissionConfirmationDto>>.Abort(Fail(e));

    private static void EnqueueConfirmationNotifications(
        IThesisNotificationOutbox notificationOutbox,
        ArchivalContext context,
        FinalSubmission submission,
        FinalSubmissionReviewAction action,
        string reviewStage,
        string? reason,
        Guid actorId,
        DateTime occurredAtUtc)
    {
        var confirmedStudents = ThesisNotificationFormatting.GetConfirmedStudentIds(context.Group);
        var shortTopic = ThesisNotificationFormatting.Shorten(context.Topic.TitleVi, 60);
        var versionLabel = $"v{submission.CurrentVersionNo}";

        if (action == FinalSubmissionReviewAction.AdvisorConfirm)
        {
            notificationOutbox.Enqueue(
                new NotificationOutboxEntry(
                    NotificationType: "FINAL_SUBMISSION_ADVISOR_CONFIRMED",
                    Title: $"Hồ sơ lưu chiểu {versionLabel} chờ Khoa duyệt",
                    Content: $"GVHD đã xác nhận gói lưu chiểu đề tài \"{shortTopic}\". Khoa vui lòng kiểm tra chốt lưu chiểu.",
                    RecipientRoles: ["Faculty"],
                    RecipientPermissions: [PermissionCodes.ThesisFinalSubmissionsManage],
                    ExcludeUserIds: [actorId],
                    ResourceType: "FinalSubmission",
                    ResourceId: submission.Id,
                    AcademicBatchId: context.Topic.BatchId,
                    ActionPath: "/thesis/review/archival",
                    Priority: "Normal",
                    EnqueuePush: true,
                    EnqueueEmail: false),
                occurredAtUtc);

            if (confirmedStudents.Count > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "FINAL_SUBMISSION_ADVISOR_CONFIRMED",
                        Title: "GVHD đã xác nhận hồ sơ lưu chiểu",
                        Content: "GVHD đã xác nhận gói lưu chiểu của nhóm, hồ sơ đang chờ Khoa kiểm tra hoàn tất.",
                        RecipientUserIds: confirmedStudents,
                        ResourceType: "FinalSubmission",
                        ResourceId: submission.Id,
                        AcademicBatchId: context.Topic.BatchId,
                        ActionPath: "/thesis/archival",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAtUtc);
            }
        }
        else if (action == FinalSubmissionReviewAction.FacultyConfirm)
        {
            if (confirmedStudents.Count > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "FINAL_SUBMISSION_ARCHIVED",
                        Title: "Hoàn tất xác nhận Hồ sơ lưu chiểu KLTN",
                        Content: "Hồ sơ lưu chiểu KLTN của nhóm đã được Khoa xác nhận hoàn tất chính thức.",
                        RecipientUserIds: confirmedStudents,
                        ResourceType: "FinalSubmission",
                        ResourceId: submission.Id,
                        AcademicBatchId: context.Topic.BatchId,
                        ActionPath: "/thesis/archival",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAtUtc);
            }

            if (context.Topic.AdvisorId != Guid.Empty)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "FINAL_SUBMISSION_ARCHIVED",
                        Title: "Nhóm hướng dẫn đã hoàn tất lưu chiểu",
                        Content: $"Khoa đã chốt xác nhận hoàn tất lưu chiểu cho đề tài \"{shortTopic}\".",
                        RecipientUserIds: [context.Topic.AdvisorId],
                        ResourceType: "FinalSubmission",
                        ResourceId: submission.Id,
                        AcademicBatchId: context.Topic.BatchId,
                        ActionPath: "/advisor/archival",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAtUtc);
            }
        }
        else if (action == FinalSubmissionReviewAction.RequestRevision)
        {
            var reviewerLabel = string.Equals(reviewStage, "Advisor", StringComparison.OrdinalIgnoreCase)
                ? "GVHD"
                : "Khoa";

            if (confirmedStudents.Count > 0)
            {
                var revisionMetadata = new List<NotificationEmailMetaItem>
                {
                    new("Người yêu cầu", reviewerLabel),
                    new("Phiên bản", versionLabel)
                };
                if (!string.IsNullOrWhiteSpace(context.Topic.TitleVi))
                {
                    revisionMetadata.Add(new NotificationEmailMetaItem("Đề tài", context.Topic.TitleVi.Trim()));
                }
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    revisionMetadata.Add(new NotificationEmailMetaItem("Lý do / Yêu cầu", reason.Trim()));
                }

                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "FINAL_SUBMISSION_REVISION_REQUIRED",
                        Title: "Yêu cầu chỉnh sửa Hồ sơ lưu chiểu",
                        Content: $"{reviewerLabel} yêu cầu cập nhật lại gói lưu chiểu. Lý do: {ThesisNotificationFormatting.Shorten(reason, 75)}",
                        RecipientUserIds: confirmedStudents,
                        ResourceType: "FinalSubmission",
                        ResourceId: submission.Id,
                        AcademicBatchId: context.Topic.BatchId,
                        ActionPath: "/thesis/archival",
                        Priority: "High",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Yêu cầu chỉnh sửa Hồ sơ lưu chiểu ({versionLabel})",
                        EmailCategoryLabel: "Hồ sơ lưu chiểu",
                        EmailCtaLabel: "Cập nhật hồ sơ lưu chiểu",
                        EmailNotice: "Vui lòng chỉnh sửa và nộp lại gói lưu chiểu trước khi kết thúc đợt.",
                        EmailMetadata: revisionMetadata),
                    occurredAtUtc);
            }

            if (!string.Equals(reviewStage, "Advisor", StringComparison.OrdinalIgnoreCase)
                && context.Topic.AdvisorId != Guid.Empty)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "FINAL_SUBMISSION_REVISION_REQUIRED",
                        Title: "Khoa yêu cầu nhóm sửa lại lưu chiểu",
                        Content: $"Khoa yêu cầu nhóm đề tài \"{shortTopic}\" chỉnh sửa gói lưu chiểu.",
                        RecipientUserIds: [context.Topic.AdvisorId],
                        ResourceType: "FinalSubmission",
                        ResourceId: submission.Id,
                        AcademicBatchId: context.Topic.BatchId,
                        ActionPath: "/advisor/archival",
                        Priority: "Normal",
                        EnqueuePush: true,
                        EnqueueEmail: false),
                    occurredAtUtc);
            }
        }
    }
}
