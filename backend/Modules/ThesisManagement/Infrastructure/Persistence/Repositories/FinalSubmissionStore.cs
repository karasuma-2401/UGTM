using Microsoft.EntityFrameworkCore;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class FinalSubmissionStore(ThesisManagementDbContext db) : IFinalSubmissionStore
{
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Shared archival transaction required.");
    }
    public async Task<StudentGroup?> GetGroupAsync(Guid id, bool locked, CancellationToken token)
    {
        if (locked) RequireTransaction();
        var group = locked
            ? await db.StudentGroups.FromSqlInterpolated($"SELECT g.*, g.xmin FROM thesis.student_group g WHERE g.id={id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(token)
            : await db.StudentGroups.AsNoTracking().SingleOrDefaultAsync(g => g.Id == id, token);
        if (group is null) return null;
        if (locked)
        {
            await db.StudentGroupMembers.FromSqlInterpolated($"SELECT m.* FROM thesis.student_group_member m WHERE m.group_id={id} ORDER BY m.student_id FOR SHARE").AsNoTracking().ToListAsync(token);
            var reports = await db.FinalReports.FromSqlInterpolated($"SELECT r.*, r.xmin FROM thesis.final_report r WHERE r.group_id={id} ORDER BY r.id FOR SHARE").AsNoTracking().ToListAsync(token);
            foreach (var report in reports)
                await db.SubmissionFiles.FromSqlInterpolated($"SELECT f.* FROM thesis.submission_file f WHERE f.entity_type='FinalReport' AND f.entity_id={report.Id} ORDER BY f.id FOR SHARE").AsNoTracking().ToListAsync(token);
        }
        return await db.StudentGroups.AsNoTracking().Include(g => g.Members).SingleAsync(g => g.Id == id, token);
    }
    public Task<FinalSubmission?> GetByGroupAsync(Guid id, bool locked, CancellationToken token)
    {
        if (!locked) return db.FinalSubmissions.AsNoTracking().SingleOrDefaultAsync(s => s.GroupId == id, token);
        RequireTransaction();
        return db.FinalSubmissions.FromSqlInterpolated($"SELECT s.*, s.xmin FROM thesis.final_submission s WHERE s.group_id={id} FOR UPDATE").SingleOrDefaultAsync(token);
    }
    public Task<FinalSubmission?> GetByIdAsync(Guid id, CancellationToken token)
        => db.FinalSubmissions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, token);
    public async Task<IReadOnlyDictionary<Guid, Guid>> GetGroupTopicsAsync(IReadOnlyCollection<Guid> ids, CancellationToken token)
        => await db.StudentGroups.AsNoTracking().Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.TopicId, token);
    public async Task<FinalSubmissionPackageGraph> GetPackagesAsync(Guid id, CancellationToken token)
    {
        var packages = await db.FinalSubmissionPackages.AsNoTracking().Where(p => p.SubmissionId == id).OrderByDescending(p => p.VersionNo).ToListAsync(token);
        var links = await db.FinalSubmissionPackageFiles.AsNoTracking().Where(p => p.SubmissionId == id).ToListAsync(token);
        var fileIds = links.Select(l => l.SubmissionFileId).ToArray();
        var files = await db.SubmissionFiles.AsNoTracking().Where(f => fileIds.Contains(f.Id)).ToListAsync(token);
        var reviews = await db.FinalSubmissionReviews.AsNoTracking().Where(r => r.SubmissionId == id).OrderBy(r => r.DecidedAtUtc).ThenBy(r => r.Id).ToListAsync(token);
        return new(packages, links, files, reviews);
    }
    public async Task<FinalSubmissionPackageGraph> LockCurrentPackageAsync(Guid id, Guid packageId, CancellationToken token)
    {
        RequireTransaction();
        var packages = await db.FinalSubmissionPackages.FromSqlInterpolated($"SELECT p.* FROM thesis.final_submission_package p WHERE p.submission_id={id} AND p.id={packageId} FOR SHARE").AsNoTracking().ToListAsync(token);
        var links = await db.FinalSubmissionPackageFiles.FromSqlInterpolated($"SELECT l.* FROM thesis.final_submission_package_file l WHERE l.submission_id={id} AND l.package_id={packageId} ORDER BY l.file_role FOR SHARE").AsNoTracking().ToListAsync(token);
        var files = await db.SubmissionFiles.FromSqlInterpolated($"""
            SELECT f.* FROM thesis.submission_file f JOIN thesis.final_submission_package_file l ON l.submission_file_id=f.id
            WHERE l.submission_id={id} AND l.package_id={packageId} ORDER BY f.id FOR SHARE OF f
            """).AsNoTracking().ToListAsync(token);
        var reviews = await db.FinalSubmissionReviews.FromSqlInterpolated($"SELECT r.* FROM thesis.final_submission_review r WHERE r.submission_id={id} AND r.package_id={packageId} ORDER BY r.id FOR SHARE").AsNoTracking().ToListAsync(token);
        return new(packages, links, files, reviews);
    }
    public async Task<(IReadOnlyList<FinalSubmission> Rows, int Count)> ListAsync(IReadOnlyCollection<Guid> topicIds, Guid? groupId,
        string? status, int page, int size, CancellationToken token)
    {
        var query = db.FinalSubmissions.AsNoTracking().Where(s => topicIds.Contains(s.Group.TopicId));
        if (groupId.HasValue) query = query.Where(s => s.GroupId == groupId.Value);
        if (status is not null) { var value = Enum.Parse<FinalSubmissionStatus>(status); query = query.Where(s => s.Status == value); }
        var count = await query.CountAsync(token);
        var skip = ((long)page - 1) * size;
        var rows = skip >= count ? [] : await query.OrderByDescending(s => s.SubmittedAtUtc).ThenBy(s => s.Id).Skip((int)skip).Take(size).ToListAsync(token);
        return (rows, count);
    }
    public async Task KeyLockAsync(Guid actor, string operation, Guid key, CancellationToken token)
    {
        RequireTransaction();
        var scope = $"{actor:N}:{operation}:{key:N}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({scope},0))", token);
    }
    public Task<FinalSubmissionIdempotency?> GetReceiptAsync(Guid actor, string operation, Guid key, CancellationToken token)
        => db.FinalSubmissionIdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == actor && r.Operation == operation && r.IdempotencyKey == key, token);
    public void Add(FinalSubmission submission) => db.FinalSubmissions.Add(submission);
    public void Add(FinalSubmissionPackage package, IReadOnlyList<SubmissionFile> files, IReadOnlyList<FinalSubmissionPackageFile> links)
    {
        db.FinalSubmissionPackages.Add(package);
        db.SubmissionFiles.AddRange(files);
        db.FinalSubmissionPackageFiles.AddRange(links);
    }
    public void Add(FinalSubmissionIdempotency receipt) => db.FinalSubmissionIdempotencyRecords.Add(receipt);
    public void Add(FinalSubmissionReview review) => db.FinalSubmissionReviews.Add(review);
    public async Task CreateAttemptAsync(FinalSubmissionUploadAttempt attempt, CancellationToken token)
    {
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Attempt creation requires a clean context.");
        db.FinalSubmissionUploadAttempts.Add(attempt);
        await db.SaveChangesAsync(token);
        db.ChangeTracker.Clear();
    }
    public Task<FinalSubmissionUploadAttempt?> LockAttemptAsync(Guid attemptId, CancellationToken token)
    {
        RequireTransaction();
        return db.FinalSubmissionUploadAttempts.FromSqlInterpolated($"SELECT a.* FROM thesis.final_submission_upload_attempt a WHERE a.id={attemptId} FOR UPDATE").SingleOrDefaultAsync(token);
    }
}
