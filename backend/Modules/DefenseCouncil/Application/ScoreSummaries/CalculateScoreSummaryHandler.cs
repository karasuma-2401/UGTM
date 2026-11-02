using System.Text.Json;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Contracts.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Domain.Entities;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;

namespace UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;

public sealed record CalculateScoreSummaryCommand(Guid GroupId, uint? ExpectedSummaryVersion, int? ExpectedActiveConfigVersion, Guid Key);
public sealed class CalculateScoreSummaryHandler(IScoreSummaryStore store, ScoreSummaryAccess access, ScoreSummaryInputLoader inputs,
    ScoreSummaryResolver resolver, IScoreSummaryWorkflowTransactionCoordinator coordinator,
    IDefenseCouncilAuditOutbox audit, TimeProvider clock)
{
    public Task<Error?> AuthorizeAsync(CancellationToken token) => access.AuthorizeAsync(ScoreSummaryAccess.Manage, false, token);
    public async Task<Result<ScoreSummaryDto>> HandleAsync(CalculateScoreSummaryCommand command, CancellationToken token = default)
    {
        if (await AuthorizeAsync(token) is { } denied) return Result.Failure<ScoreSummaryDto>(denied);
        if (command.GroupId == Guid.Empty || command.Key == Guid.Empty || command.ExpectedSummaryVersion == 0 || command.ExpectedActiveConfigVersion <= 0)
            return Result.Failure<ScoreSummaryDto>(ScoreSummaryErrors.InvalidRequest);
        var hash = ScoreSummaryResolver.Hash(new object?[] { ScoreSummaryResolver.N(command.GroupId), command.ExpectedSummaryVersion, command.ExpectedActiveConfigVersion });
        ScoreHistory? saved = null; Guid batchId = default; DateTime now = default;
        static ScoreSummaryTransactionResult<Result<ScoreSummaryDto>> Abort(Error e) => ScoreSummaryTransactionResult<Result<ScoreSummaryDto>>.Abort(Result.Failure<ScoreSummaryDto>(e));
        const string operation = "ScoreSummary.Calculate";
        try
        {
            return await coordinator.ExecuteWithPostSaveAsync(async ct =>
            {
                await store.KeyLockAsync(access.ActorId, operation, command.Key, ct);
                if (await access.AuthorizeAsync(ScoreSummaryAccess.Manage, false, ct) is { } lost) return Abort(lost);
                var receipt = await store.ReceiptAsync(access.ActorId, operation, command.Key, ct);
                if (receipt is not null) return receipt.Fingerprint != hash ? Abort(ScoreSummaryErrors.Conflict("IdempotencyConflict"))
                    : ScoreSummaryTransactionResult<Result<ScoreSummaryDto>>.Abort(Result.Success(JsonSerializer.Deserialize<ScoreSummaryDto>(receipt.ResponsePayload)!));
                var loaded = await inputs.LockManyAsync([command.GroupId], ScoreSummaryAccess.Manage, ct);
                if (loaded.IsFailure) return Abort(loaded.Error);
                if (loaded.Value.Errors.TryGetValue(command.GroupId, out var inputError)) return Abort(inputError);
                var input = loaded.Value.Inputs[command.GroupId];
                var context = input.Context;
                batchId = context.Topic.BatchId;
                var config = input.Config;
                var row = input.Summary;
                if (row?.Status == ScoreSummaryStatus.PublishedToStudent) return Abort(ScoreSummaryErrors.Conflict("AlreadyPublished"));
                if (row is null ? command.ExpectedSummaryVersion.HasValue : row.Version != command.ExpectedSummaryVersion) return Abort(ScoreSummaryErrors.Conflict("VersionConflict"));
                if (command.ExpectedActiveConfigVersion.HasValue && config?.VersionNo != command.ExpectedActiveConfigVersion) return Abort(ScoreSummaryErrors.Conflict("VersionConflict"));
                now = clock.GetUtcNow().UtcDateTime;
                var resolved = resolver.Resolve(context.Group, context.Topic, input.Facts, input.Archive, config, now);
                if (resolved.IsFailure) return Abort(resolved.Error);
                var prior = (await store.HistoriesAsync([command.GroupId], ct)).GetValueOrDefault(command.GroupId);
                if (row is null) { row = new ScoreSummary { Id = Guid.NewGuid(), GroupId = command.GroupId }; store.Add(row); }
                else if (row.IsLegacy && row.LegacySnapshot is null)
                    row.LegacySnapshot = JsonSerializer.Serialize(new
                    {
                        row.AdvisorScore,
                        row.ReviewerScore,
                        row.CouncilScore,
                        row.FinalScore,
                        row.TotalWeight,
                        row.CalculationFormula,
                        row.ConfigVersionNo,
                        row.CalculationConfigSnapshot,
                        row.CalculatedAt,
                        row.Status,
                        row.PublishedAt,
                        row.PublishedBy,
                        Components = prior?.Components.Select(c => new { c.Id, c.ComponentCode, c.SourceKey, c.Score, c.Weight, c.IsIgnoredInCalculation, c.ExclusionReason }).ToArray()
                    });
                var value = resolved.Value;
                var revision = new ScoreSummaryRevision
                {
                    Id = Guid.NewGuid(),
                    SummaryId = row.Id,
                    RevisionNo = checked((prior?.Revisions.Select(r => r.RevisionNo).DefaultIfEmpty().Max() ?? 0) + 1),
                    GroupId = row.GroupId,
                    BatchId = batchId,
                    CalculatedBy = access.ActorId,
                    CalculatedAtUtc = now,
                    ConfigId = config!.Id,
                    ConfigVersionNo = config.VersionNo,
                    ConfigSnapshot = value.ConfigSnapshot,
                    SourceSnapshot = value.SourceSnapshot,
                    InputFingerprint = value.Fingerprint,
                    FinalScoreRaw = value.Raw,
                    FinalScore = value.Final,
                    TotalWeight = value.TotalWeight,
                    CalculationFormula = value.Formula
                };
                var components = value.Components.Select(c => new ScoreSummaryComponent
                {
                    Id = Guid.NewGuid(),
                    SummaryId = row.Id,
                    RevisionId = revision.Id,
                    ComponentCode = c.ComponentCode,
                    ComponentName = c.ComponentName,
                    Source = c.Source,
                    SourceKey = c.Source,
                    Score = c.Score,
                    Weight = c.Weight,
                    IsIgnoredInCalculation = c.IsIgnoredInCalculation,
                    ExclusionReason = c.ExclusionReason,
                    SourceReferences = c.SourceReferences
                }).ToArray();
                row.ApplyCalculation(revision);
                row.AdvisorScore = value.AdvisorScore; row.ReviewerScore = value.ReviewerScore; row.CouncilScore = value.CouncilScore is { } average ? decimal.Round(average, 2, MidpointRounding.AwayFromZero) : null;
                store.Add(revision, components);
                saved = new(row, [revision, .. prior?.Revisions ?? []], [.. prior?.Components ?? [], .. components]);
                return ScoreSummaryTransactionResult<Result<ScoreSummaryDto>>.Commit(Result.Success(ScoreSummaryMapping.Dto(saved, batchId, false)));
            }, (result, ct) =>
            {
                var dto = ScoreSummaryMapping.Dto(saved!, batchId, false);
                store.Add(new ScoreSummaryIdempotency { Id = Guid.NewGuid(), ActorId = access.ActorId, Operation = operation, IdempotencyKey = command.Key, Fingerprint = hash, ResponsePayload = JsonSerializer.Serialize(dto), CreatedAtUtc = now });
                audit.Add(new AuditIntegrationEntry("ScoreSummary", saved!.Summary.Id.ToString(), "DefenseCouncil.ScoreSummary.Calculated", "Succeeded", null,
                    new { saved.Summary.GroupId, saved.Summary.Version, saved.Summary.ActiveRevisionId, saved.Summary.ConfigId, saved.Summary.InputFingerprint, ActorId = access.ActorId }), now);
                return Task.FromResult(Result.Success(dto));
            }, token);
        }
        catch (DefenseCouncilPersistenceException) { return Result.Failure<ScoreSummaryDto>(ScoreSummaryErrors.Conflict("ConcurrencyConflict")); }
        catch (OverflowException) { return Result.Failure<ScoreSummaryDto>(ScoreSummaryErrors.Conflict("ConfigurationInvalid")); }
    }
}

public static class ScoreSummaryMapping
{
    public static ScoreSummaryDto Dto(ScoreHistory h, Guid batch, bool stale)
    {
        var s = h.Summary; var r = h.Revisions.SingleOrDefault(r => r.Id == s.ActiveRevisionId);
        return new(s.Id, s.GroupId, batch, s.Status.ToString(), s.FinalScore, s.TotalWeight, s.CalculationFormula, s.ConfigVersionNo, s.CalculatedAt, s.CalculatedBy, stale, s.Version, s.IsLegacy,
            s.ActiveRevisionId, s.PublishedRevisionId, s.PublishedBy, s.PublishedAt,
            h.Components.Where(c => s.IsLegacy ? c.RevisionId is null : c.RevisionId == s.ActiveRevisionId).OrderBy(c => c.ComponentCode, StringComparer.Ordinal).Select(c => new ScoreComponentDto(c.ComponentCode, c.ComponentName ?? c.ComponentCode, c.Source ?? "Legacy", c.Score, c.Weight, c.IsIgnoredInCalculation, c.ExclusionReason, c.SourceReferences ?? "[]")).ToArray(),
            h.Revisions.Select(r => new ScoreRevisionDto(r.Id, r.RevisionNo, r.ConfigId, r.ConfigVersionNo, r.FinalScore, r.FinalScoreRaw, r.TotalWeight, r.CalculatedBy, r.CalculatedAtUtc, r.InputFingerprint, r.SourceSnapshot, r.ConfigSnapshot)).ToArray(), r?.SourceSnapshot, r?.ConfigSnapshot);
    }
}
