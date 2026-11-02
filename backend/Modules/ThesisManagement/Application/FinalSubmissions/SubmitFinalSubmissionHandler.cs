using System.Text.Json;
using Microsoft.Extensions.Logging;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Persistence;
using UGTM.Modules.ThesisManagement.Application.Storage;
using UGTM.Modules.ThesisManagement.Contracts.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.FinalSubmissions;

public sealed record SubmitFinalSubmissionCommand(Guid GroupId, uint? ExpectedSubmissionVersion, Guid Key,
    string? AbstractVi, string? AbstractEn, string? KeywordsVi, string? KeywordsEn,
    string PdfName, Stream Pdf, string DocxName, Stream Docx);

public sealed class SubmitFinalSubmissionHandler(IFinalSubmissionStore store, FinalSubmissionAccess access,
    ILockedDefenseProofReader defense, IFinalSubmissionWorkflowTransactionCoordinator coordinator,
    IThesisSubmissionFileValidator validator, IThesisSubmissionObjectKeyFactory keys, IThesisSubmissionObjectStorage storage,
    IFinalSubmissionUploadCleanup cleanup, IThesisManagementAuditOutbox audit, TimeProvider clock,
    ILogger<SubmitFinalSubmissionHandler> logger)
{
    private const string Operation = "FinalSubmission.Submit";
    public async Task<Result<FinalSubmissionWriteResult>> HandleAsync(SubmitFinalSubmissionCommand command, CancellationToken token = default)
    {
        try { return await HandleCoreAsync(command, token); }
        catch (ThesisManagementPersistenceException) { return Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.ConcurrencyConflict); }
    }
    private async Task<Result<FinalSubmissionWriteResult>> HandleCoreAsync(SubmitFinalSubmissionCommand command, CancellationToken token)
    {
        if (access.AuthenticationError is { } authentication) return Result.Failure<FinalSubmissionWriteResult>(authentication);
        if (command.GroupId == Guid.Empty || command.Key == Guid.Empty || command.ExpectedSubmissionVersion == 0)
            return Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.InvalidRequest);
        var context = await access.ContextAsync(command.GroupId, true, false, token);
        if (context.IsFailure) return Result.Failure<FinalSubmissionWriteResult>(context.Error);
        var vi = FinalSubmissionMapping.Normalize(command.AbstractVi, 10000);
        var en = FinalSubmissionMapping.Normalize(command.AbstractEn, 10000);
        var kvi = FinalSubmissionMapping.Normalize(command.KeywordsVi, 1000);
        var ken = FinalSubmissionMapping.Normalize(command.KeywordsEn, 1000);
        if (vi is null || en is null || kvi is null || ken is null)
            return Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.InvalidMetadata);
        ValidatedThesisSubmissionFile pdf;
        ValidatedThesisSubmissionFile docx;
        try
        {
            pdf = await validator.ValidateAsync(command.PdfName, command.Pdf, token);
            docx = await validator.ValidateAsync(command.DocxName, command.Docx, token);
            if (pdf.FileExtension != ".pdf" || docx.FileExtension != ".docx")
                return Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.InvalidFile);
        }
        catch (ThesisSubmissionFileValidationException) { return Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.InvalidFile); }
        var fingerprint = FinalSubmissionMapping.Intent(command.GroupId, command.ExpectedSubmissionVersion, vi, en, kvi, ken, pdf, docx);
        var replay = await coordinator.ExecuteWithPostSaveAsync(async ct =>
        {
            await store.KeyLockAsync(access.ActorId, Operation, command.Key, ct);
            return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult?>>.Abort(await ReplayAsync(command, fingerprint, ct));
        }, (result, _) => Task.FromResult(result), token);
        if (replay.IsFailure) return Result.Failure<FinalSubmissionWriteResult>(replay.Error);
        if (replay.Value is not null) return Result.Success(replay.Value);
        var existing = await store.GetByGroupAsync(command.GroupId, false, token);
        var preflight = await GuardAsync(context.Value, existing, command.ExpectedSubmissionVersion, false, token);
        if (preflight is not null) return Result.Failure<FinalSubmissionWriteResult>(preflight);
        var submissionId = existing?.Id ?? Guid.NewGuid();
        var version = existing?.CurrentPackageId is not null ? checked(existing.CurrentVersionNo + 1) : 1;
        var now = clock.GetUtcNow().UtcDateTime;
        var attempt = new FinalSubmissionUploadAttempt
        {
            Id = Guid.NewGuid(),
            GroupId = command.GroupId,
            ActorId = access.ActorId,
            IdempotencyKey = command.Key,
            Fingerprint = fingerprint,
            PdfObjectKey = keys.Create(SubmissionEntityType.FinalSubmission, submissionId, version, ".pdf"),
            DocxObjectKey = keys.Create(SubmissionEntityType.FinalSubmission, submissionId, version, ".docx"),
            CreatedAtUtc = now,
            LeaseExpiresAtUtc = now.AddMinutes(10)
        };
        FinalSubmission? saved = null;
        FinalSubmissionPackage? package = null;
        SubmissionFile[] files = [];
        FinalSubmissionPackageFile[] links = [];
        try
        {
            await store.CreateAttemptAsync(attempt, token);
            await UploadAsync(attempt.PdfObjectKey, pdf, token);
            await UploadAsync(attempt.DocxObjectKey, docx, token);
            return await coordinator.ExecuteWithPostSaveAsync(async ct =>
            {
                await store.KeyLockAsync(access.ActorId, Operation, command.Key, ct);
                var repeated = await ReplayAsync(command, fingerprint, ct);
                if (repeated.IsFailure) return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Abort(Result.Failure<FinalSubmissionWriteResult>(repeated.Error));
                if (repeated.Value is not null) return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Abort(Result.Success(repeated.Value));
                var fresh = await access.ContextAsync(command.GroupId, true, true, ct);
                if (fresh.IsFailure) return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Abort(Result.Failure<FinalSubmissionWriteResult>(fresh.Error));
                saved = await store.GetByGroupAsync(command.GroupId, true, ct);
                var durable = await store.LockAttemptAsync(attempt.Id, ct);
                var guard = await GuardAsync(fresh.Value, saved, command.ExpectedSubmissionVersion, true, ct);
                if (guard is not null) return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Abort(Result.Failure<FinalSubmissionWriteResult>(guard));
                // The cleanup worker and final commit serialize on this attempt row.
                now = clock.GetUtcNow().UtcDateTime;
                if (fresh.Value.Batch?.WindowOpensAtUtc is not { } finalStart || fresh.Value.Batch.WindowDeadlineAtUtc is not { } finalEnd
                    || now < finalStart || now > finalEnd)
                    return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Abort(Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.WindowClosed));
                if (durable is null || durable.State != "Uploading" || durable.LeaseExpiresAtUtc <= now)
                    return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Abort(Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.ConcurrencyConflict));
                if (saved is null)
                {
                    saved = new FinalSubmission { Id = submissionId, GroupId = command.GroupId };
                    store.Add(saved);
                }
                else if (saved.IsLegacy && saved.LegacySnapshot is null)
                    saved.LegacySnapshot = JsonSerializer.Serialize(new
                    {
                        saved.Status,
                        saved.CurrentVersionNo,
                        saved.AbstractVi,
                        saved.AbstractEn,
                        saved.KeywordsVi,
                        saved.KeywordsEn,
                        saved.AdvisorConfirmedBy,
                        saved.AdvisorConfirmedAt,
                        saved.ArchivedConfirmedBy,
                        saved.ArchivedConfirmedAt,
                        saved.FacultyFeedback
                    });
                package = new FinalSubmissionPackage
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = saved.Id,
                    VersionNo = version,
                    AbstractVi = vi,
                    AbstractEn = en,
                    KeywordsVi = kvi,
                    KeywordsEn = ken,
                    SubmittedBy = access.ActorId,
                    SubmittedAtUtc = now
                };
                files = [CreateFile(saved.Id, version, pdf, attempt.PdfObjectKey, now), CreateFile(saved.Id, version, docx, attempt.DocxObjectKey, now)];
                links = files.Select(f => new FinalSubmissionPackageFile
                {
                    PackageId = package.Id,
                    SubmissionId = saved.Id,
                    VersionNo = version,
                    FileRole = FinalSubmissionMapping.Role(f),
                    SubmissionFileId = f.Id
                }).ToArray();
                package.ManifestSha256 = FinalSubmissionMapping.Manifest(package, files);
                saved.Submit(package);
                store.Add(package, files, links);
                durable.State = "Committed";
                durable.CommittedPackageId = package.Id;
                return FinalSubmissionTransactionResult<Result<FinalSubmissionWriteResult>>.Commit(Result.Success(
                    new FinalSubmissionWriteResult(FinalSubmissionMapping.Dto(saved, new([package], links, files, [])), version == 1 ? 201 : 200)));
            }, (result, ct) =>
            {
                // First SaveChanges has materialized the real PostgreSQL xmin.
                var response = new FinalSubmissionWriteResult(FinalSubmissionMapping.Dto(saved!, new([package!], links, files, [])), result.Value.StatusCode);
                store.Add(new FinalSubmissionIdempotency
                {
                    Id = Guid.NewGuid(),
                    ActorId = access.ActorId,
                    Operation = Operation,
                    IdempotencyKey = command.Key,
                    Fingerprint = fingerprint,
                    ResponseStatus = response.StatusCode,
                    ResponsePayload = JsonSerializer.Serialize(response.Data),
                    CreatedAtUtc = now
                });
                audit.Add(new AuditIntegrationEntry("FinalSubmission", saved!.Id.ToString(), "ThesisManagement.FinalSubmission.Submitted", "Succeeded", null,
                    new { saved.GroupId, saved.CurrentVersionNo, saved.Version, PackageId = package!.Id, package.ManifestSha256, ActorId = access.ActorId }), now);
                return Task.FromResult(Result.Success(response));
            }, token);
        }
        catch (ThesisManagementPersistenceException) { return Result.Failure<FinalSubmissionWriteResult>(FinalSubmissionErrors.ConcurrencyConflict); }
        finally
        {
            using var compensation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await cleanup.CleanAsync(attempt.Id, compensation.Token); }
            catch (Exception) { logger.LogWarning("Archival attempt {AttemptId} will be verified by durable cleanup.", attempt.Id); }
        }
    }

    private async Task<Result<FinalSubmissionWriteResult?>> ReplayAsync(SubmitFinalSubmissionCommand command, string hash, CancellationToken token)
    {
        var own = await access.ContextAsync(command.GroupId, true, false, token);
        if (own.IsFailure) return Result.Failure<FinalSubmissionWriteResult?>(own.Error);
        var receipt = await store.GetReceiptAsync(access.ActorId, Operation, command.Key, token);
        if (receipt is null) return Result.Success<FinalSubmissionWriteResult?>(null);
        if (receipt.Fingerprint != hash) return Result.Failure<FinalSubmissionWriteResult?>(FinalSubmissionErrors.IdempotencyConflict);
        return Result.Success<FinalSubmissionWriteResult?>(new(JsonSerializer.Deserialize<FinalSubmissionDto>(receipt.ResponsePayload)!, receipt.ResponseStatus));
    }
    private async Task<Error?> GuardAsync(ArchivalContext context, FinalSubmission? row, uint? expected, bool locked, CancellationToken token)
    {
        if (row?.Status is FinalSubmissionStatus.AdvisorConfirmed or FinalSubmissionStatus.ArchiveConfirmed) return FinalSubmissionErrors.Locked;
        if (row is null ? expected.HasValue : expected != row.Version) return FinalSubmissionErrors.VersionConflict;
        if (context.Batch?.BatchStatus != "Open") return FinalSubmissionErrors.BatchNotWritable;
        var proof = locked ? await defense.LockAsync(context.Group.Id, token) : await defense.GetAsync(context.Group.Id, token);
        if (proof.Outcome != LockedDefenseOutcome.Succeeded) return FinalSubmissionErrors.DefenseNotLocked;
        if (proof.BatchId != context.Topic.BatchId) return FinalSubmissionErrors.ContextInvalid;
        var now = clock.GetUtcNow().UtcDateTime;
        return context.Batch.WindowOpensAtUtc is not { } start || context.Batch.WindowDeadlineAtUtc is not { } end || now < start || now > end
            ? FinalSubmissionErrors.WindowClosed : null;
    }
    private Task UploadAsync(string key, ValidatedThesisSubmissionFile file, CancellationToken token)
        => UploadStreamAsync(key, file, token);
    private async Task UploadStreamAsync(string key, ValidatedThesisSubmissionFile file, CancellationToken token)
    {
        await using var stream = new MemoryStream(file.Content.ToArray(), writable: false);
        await storage.UploadAsync(new(key, file.ContentType, file.FileSize), stream, token);
    }
    private SubmissionFile CreateFile(Guid submission, int version, ValidatedThesisSubmissionFile file, string key, DateTime now)
        => new()
        {
            Id = Guid.NewGuid(),
            EntityType = SubmissionEntityType.FinalSubmission,
            EntityId = submission,
            VersionNo = version,
            FileName = file.FileName,
            ObjectKey = key,
            ContentType = file.ContentType,
            FileExtension = file.FileExtension,
            FileSize = file.FileSize,
            ChecksumSha256 = file.ChecksumSha256,
            UploadedBy = access.ActorId,
            UploadedAt = now
        };
}
