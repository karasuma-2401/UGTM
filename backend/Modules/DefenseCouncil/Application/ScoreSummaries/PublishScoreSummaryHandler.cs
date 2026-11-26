using System.Text.Json;
using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Notifications;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Notifications.Contracts.Notifications;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed record ScorePublicationSelection(Guid GroupId, uint ExpectedSummaryVersion);
public sealed record PublishScoreSummaryCommand(Guid BatchId, IReadOnlyList<ScorePublicationSelection> Selections, Guid Key);
public sealed record ScorePublicationDto(Guid BatchId, int PublishedCount, DateTime PublishedAt, IReadOnlyList<Guid> PublishedGroupIds);
public sealed record ScorePublicationFailure(Guid GroupId, string Code);
public sealed record ScorePublicationResult(ScorePublicationDto? Data, Error? Error, IReadOnlyList<ScorePublicationFailure>? Failures = null);

public sealed class PublishScoreSummaryHandler(IScoreSummaryStore store, ScoreSummaryAccess access, ScoreSummaryInputLoader inputs,
    ScoreSummaryResolver resolver, IScoreSummaryWorkflowTransactionCoordinator coordinator, IDefenseCouncilAuditOutbox audit, TimeProvider clock,
    ICacheService? cache = null,
    IDefenseCouncilNotificationOutbox? notificationOutbox = null)
{
    public Task<Error?> AuthorizeAsync(CancellationToken token) => access.AuthorizeAsync(ScoreSummaryAccess.Publish, false, token);

    public async Task<ScorePublicationResult> HandleAsync(PublishScoreSummaryCommand command, CancellationToken token = default)
    {
        if (await AuthorizeAsync(token) is { } denied) return new(null, denied);
        if (command.BatchId == Guid.Empty || command.Key == Guid.Empty || command.Selections is null || command.Selections.Count is < 1 or > 100
            || command.Selections.Any(s => s.GroupId == Guid.Empty || s.ExpectedSummaryVersion == 0)
            || command.Selections.Select(s => s.GroupId).Distinct().Count() != command.Selections.Count) return new(null, ScoreSummaryErrors.InvalidRequest);
        var selections = command.Selections.OrderBy(s => s.GroupId).ToArray();
        var fingerprint = ScoreSummaryResolver.Hash(new object[] { ScoreSummaryResolver.N(command.BatchId), selections.Select(s => new object[] { ScoreSummaryResolver.N(s.GroupId), s.ExpectedSummaryVersion }).ToArray() });
        const string operation = "ScoreSummary.Publish";
        DateTime now = default;
        ScoreContext[] publishedContexts = [];
        static ScoreSummaryTransactionResult<ScorePublicationResult> Abort(Error error, IReadOnlyList<ScorePublicationFailure>? failures = null)
            => ScoreSummaryTransactionResult<ScorePublicationResult>.Abort(new(null, error, failures));
        try
        {
            var outcome = await coordinator.ExecuteWithPostSaveAsync(async ct =>
            {
                await store.KeyLockAsync(access.ActorId, operation, command.Key, ct);
                if (await AuthorizeAsync(ct) is { } lost) return Abort(lost);
                var receipt = await store.ReceiptAsync(access.ActorId, operation, command.Key, ct);
                if (receipt is not null) return receipt.Fingerprint != fingerprint ? Abort(ScoreSummaryErrors.Conflict("IdempotencyConflict"))
                    : ScoreSummaryTransactionResult<ScorePublicationResult>.Abort(new(JsonSerializer.Deserialize<ScorePublicationDto>(receipt.ResponsePayload)!, null));
                var loaded = await inputs.LockManyAsync(selections.Select(s => s.GroupId).ToArray(), ScoreSummaryAccess.Publish, ct);
                if (loaded.IsFailure) return Abort(loaded.Error);
                now = clock.GetUtcNow().UtcDateTime;
                var failures = new List<ScorePublicationFailure>();
                foreach (var selection in selections)
                {
                    if (loaded.Value.Errors.TryGetValue(selection.GroupId, out var inputError)) { failures.Add(new(selection.GroupId, inputError.Code)); continue; }
                    var input = loaded.Value.Inputs[selection.GroupId]; var row = input.Summary;
                    string? code = input.Context.Topic.BatchId != command.BatchId ? "ScoreSummary.BatchMismatch"
                        : row is null ? "ScoreSummary.NotFound"
                        : row.Status == ScoreSummaryStatus.PublishedToStudent ? "ScoreSummary.AlreadyPublished"
                        : row.Version != selection.ExpectedSummaryVersion ? "ScoreSummary.VersionConflict"
                        : row.IsLegacy || row.Status != ScoreSummaryStatus.Calculated || row.ActiveRevisionId is null ? "ScoreSummary.NotCalculated" : null;
                    if (code is null)
                    {
                        var resolved = resolver.Resolve(input.Context.Group, input.Context.Topic, input.Facts, input.Archive, input.Config, now);
                        if (resolved.IsFailure) code = resolved.Error.Code;
                        else if (resolved.Value.Fingerprint != row!.InputFingerprint) code = "ScoreSummary.Stale";
                        else if (row.CalculatedAt > now) code = "ScoreSummary.NotEligible";
                    }
                    if (code is not null) failures.Add(new(selection.GroupId, code));
                }
                if (failures.Count != 0) return Abort(ScoreSummaryErrors.Conflict("PublicationRejected"), failures);
                foreach (var selection in selections) loaded.Value.Inputs[selection.GroupId].Summary!.Publish(access.ActorId, now);
                publishedContexts = selections.Select(s => loaded.Value.Inputs[s.GroupId].Context).ToArray();
                var dto = new ScorePublicationDto(command.BatchId, selections.Length, now, selections.Select(s => s.GroupId).ToArray());
                return ScoreSummaryTransactionResult<ScorePublicationResult>.Commit(new(dto, null));
            }, (result, ct) =>
            {
                var dto = result.Data!;
                store.Add(new ScoreSummaryIdempotency { Id = Guid.NewGuid(), ActorId = access.ActorId, Operation = operation, IdempotencyKey = command.Key, Fingerprint = fingerprint, ResponsePayload = JsonSerializer.Serialize(dto), CreatedAtUtc = now });
                audit.Add(new AuditIntegrationEntry("ScoreSummary", command.BatchId.ToString(), "DefenseCouncil.ScoreSummary.Published", "Succeeded", null,
                    new { command.BatchId, ActorId = access.ActorId, Selections = selections.Select(s => new { s.GroupId, s.ExpectedSummaryVersion }).ToArray() }), now);

                if (notificationOutbox is not null)
                {
                    foreach (var ctx in publishedContexts)
                    {
                        var studentIds = ctx.Group.StudentIds?
                            .Where(id => id != Guid.Empty)
                            .Distinct()
                            .ToArray() ?? [];
                        if (studentIds.Length == 0)
                        {
                            continue;
                        }

                        var shortTopic = DefenseCouncilNotificationFormatting.Shorten(ctx.Topic.TitleVi, 60);
                        notificationOutbox.Enqueue(
                            new NotificationOutboxEntry(
                                NotificationType: "SCORE_SUMMARY_PUBLISHED",
                                Title: "Đã công bố điểm tổng kết KLTN chính thức",
                                Content: $"Điểm tổng kết KLTN của nhóm đề tài \"{shortTopic}\" đã được công bố chính thức.",
                                RecipientUserIds: studentIds,
                                ResourceType: "ScoreSummary",
                                ResourceId: ctx.Group.GroupId,
                                AcademicBatchId: command.BatchId,
                                ActionPath: "/councils/results",
                                Priority: "Critical",
                                EnqueuePush: true,
                                EnqueueEmail: true,
                                EmailSubject: $"[UGTM] Công bố điểm tổng kết KLTN: {shortTopic}",
                                EmailCategoryLabel: "Công bố kết quả",
                                EmailCtaLabel: "Xem bảng điểm tổng kết",
                                EmailNotice: "Sinh viên có thể đăng nhập hệ thống để tra cứu chi tiết điểm thành phần.",
                                EmailMetadata:
                                [
                                    new("Đề tài", ctx.Topic.TitleVi.Trim()),
                                    new("Trạng thái", "Đã công bố chính thức")
                                ]),
                            now);
                    }
                }

                return Task.FromResult(result);
            }, token);

            if (outcome.Error is null && outcome.Data is not null)
            {
                await DefenseCouncilCacheKeys.InvalidateBatchAsync(cache, command.BatchId, token);
            }

            return outcome;
        }
        catch (DefenseCouncilPersistenceException) { return new(null, ScoreSummaryErrors.Conflict("ConcurrencyConflict")); }
    }
}
