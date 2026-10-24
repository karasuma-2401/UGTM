using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed record TopicRegistrationDetailsDto(
    Guid Id,
    Guid BatchId,
    Guid AdvisorId,
    string TitleVi,
    string Status,
    int StudentNumber);

public sealed class TopicRegistrationStore(
    ThesisManagementDbContext db,
    IKltnBatchReader batchReader) : ITopicRegistrationStore
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TopicLocks = new();
    private static readonly ConcurrentDictionary<Guid, TopicRegistrationDetailsDto> InProcessTestTopics = new();

    public static void SeedInProcessTopic(TopicRegistrationDetailsDto topic)
        => InProcessTestTopics[topic.Id] = topic;

    public static void ClearInProcessTopics()
        => InProcessTestTopics.Clear();

    public async Task<Result<TopicRegistrationResultDto>> RegisterTopicAsync(
        Guid topicId,
        Guid studentAId,
        Guid? studentBId,
        DateTime nowUtc,
        TimeSpan confirmationWindow,
        string confirmationTokenHash,
        CancellationToken cancellationToken = default)
    {
        var sem = TopicLocks.GetOrAdd(topicId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken);
        try
        {
            if (db.Database.IsRelational())
            {
                var strategy = db.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                    var topic = await QueryTopicForUpdateAsync(topicId, transaction.GetDbTransaction(), cancellationToken);
                    if (topic is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotFound);
                    }

                    var regResult = await ExecuteRegistrationLogicAsync(
                        topic,
                        studentAId,
                        studentBId,
                        nowUtc,
                        confirmationWindow,
                        confirmationTokenHash,
                        cancellationToken);

                    if (!regResult.IsSuccess)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return regResult;
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return regResult;
                });
            }
            else
            {
                InProcessTestTopics.TryGetValue(topicId, out var topic);
                if (topic is null)
                {
                    return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotFound);
                }

                var regResult = await ExecuteRegistrationLogicAsync(
                    topic,
                    studentAId,
                    studentBId,
                    nowUtc,
                    confirmationWindow,
                    confirmationTokenHash,
                    cancellationToken);

                if (!regResult.IsSuccess)
                {
                    return regResult;
                }

                await db.SaveChangesAsync(cancellationToken);
                return regResult;
            }
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<TopicRegistrationDetailsDto?> QueryTopicForUpdateAsync(
        Guid topicId,
        System.Data.Common.DbTransaction tx,
        CancellationToken cancellationToken)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, batch_id, advisor_id, title_vi, status, student_number FROM topics.topic WHERE id = @topicId FOR UPDATE";

        var param = cmd.CreateParameter();
        param.ParameterName = "@topicId";
        param.Value = topicId;
        cmd.Parameters.Add(param);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = reader.GetGuid(0);
        var batchId = reader.GetGuid(1);
        var advisorId = reader.GetGuid(2);
        var titleVi = reader.GetString(3);
        var status = reader.GetString(4);
        var studentNumber = reader.IsDBNull(5) ? 1 : reader.GetInt32(5);

        return new TopicRegistrationDetailsDto(id, batchId, advisorId, titleVi, status, studentNumber);
    }

    private async Task<Result<TopicRegistrationResultDto>> ExecuteRegistrationLogicAsync(
        TopicRegistrationDetailsDto topic,
        Guid studentAId,
        Guid? studentBId,
        DateTime nowUtc,
        TimeSpan confirmationWindow,
        string confirmationTokenHash,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(topic.Status, "Published", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotPublished);
        }

        var window = await batchReader.GetRegistrationWindowAsync(topic.BatchId, nowUtc, cancellationToken);
        if (window is null || !window.IsRegistrationStageActive)
        {
            return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.RegistrationWindowClosed);
        }

        var penaltyA = await db.StudentRegistrationPenalties
            .FirstOrDefaultAsync(p => p.StudentId == studentAId, cancellationToken);
        if (penaltyA is not null && penaltyA.IsRegistrationBlocked)
        {
            return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.StudentRegistrationBlocked);
        }

        if (studentBId.HasValue)
        {
            var penaltyB = await db.StudentRegistrationPenalties
                .FirstOrDefaultAsync(p => p.StudentId == studentBId.Value, cancellationToken);
            if (penaltyB is not null && penaltyB.IsRegistrationBlocked)
            {
                return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.PartnerRegistrationBlocked);
            }
        }

        var alreadyRegistered = await db.StudentGroupMembers.AnyAsync(m =>
            (m.StudentId == studentAId || (studentBId.HasValue && m.StudentId == studentBId.Value))
            && m.BatchId == topic.BatchId, cancellationToken);

        if (alreadyRegistered)
        {
            return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.StudentAlreadyRegisteredInBatch);
        }

        var group = await db.StudentGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.TopicId == topic.Id, cancellationToken);

        if (group is not null)
        {
            var expiredMembers = group.Members
                .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Pending
                            && m.ConfirmationExpiresAtUtc.HasValue
                            && m.ConfirmationExpiresAtUtc.Value <= nowUtc)
                .ToList();

            foreach (var expired in expiredMembers)
            {
                group.RemoveMember(expired.StudentId);
                var penalty = await db.StudentRegistrationPenalties
                    .FirstOrDefaultAsync(p => p.StudentId == expired.StudentId, cancellationToken);
                if (penalty is null)
                {
                    penalty = StudentRegistrationPenalty.Create(expired.StudentId);
                    db.StudentRegistrationPenalties.Add(penalty);
                }
                penalty.RecordViolation(nowUtc);
            }
        }

        var occupiedSlots = group?.Members.Count(m =>
            m.ConfirmationStatus == MemberConfirmationStatus.Confirmed
            || (m.ConfirmationStatus == MemberConfirmationStatus.Pending
                && (!m.ConfirmationExpiresAtUtc.HasValue || m.ConfirmationExpiresAtUtc.Value > nowUtc))) ?? 0;

        var requestedSlots = studentBId.HasValue ? 2 : 1;

        if (requestedSlots > topic.StudentNumber)
        {
            return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.GroupSizeExceedsTopicQuota);
        }

        if (occupiedSlots + requestedSlots > topic.StudentNumber)
        {
            return Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicAlreadyTaken);
        }

        if (group is null)
        {
            group = new StudentGroup(Guid.NewGuid(), topic.Id, nowUtc);
            db.StudentGroups.Add(group);
        }

        group.AddConfirmedMember(studentAId, topic.BatchId, nowUtc);

        DateTime? expiresAt = null;
        if (studentBId.HasValue)
        {
            expiresAt = nowUtc.Add(confirmationWindow);
            group.AddPendingMember(
                studentBId.Value,
                topic.BatchId,
                invitedByStudentId: studentAId,
                confirmationTokenHash: confirmationTokenHash,
                joinedAt: nowUtc,
                expiresAtUtc: expiresAt.Value);
        }

        var message = studentBId.HasValue
            ? "Đăng ký đề tài thành công và đang chờ xác nhận từ thành viên cùng nhóm."
            : "Đăng ký đề tài thành công.";

        return Result.Success(new TopicRegistrationResultDto(
            group.Id,
            topic.Id,
            topic.BatchId,
            IsPendingConfirmation: studentBId.HasValue,
            ConfirmationExpiresAtUtc: expiresAt,
            Message: message));
    }

    public async Task<Result<ConfirmRegistrationResultDto>> ConfirmRegistrationAsync(
        string tokenHash,
        Guid? currentStudentId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var member = await db.StudentGroupMembers
            .Include(m => m.Group)
            .FirstOrDefaultAsync(m => m.ConfirmationTokenHash == tokenHash, cancellationToken);

        if (member is null)
        {
            return Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.InvalidConfirmationToken);
        }

        if (currentStudentId.HasValue && currentStudentId.Value != member.StudentId)
        {
            return Result.Failure<ConfirmRegistrationResultDto>(
                Error.Forbidden("TopicRegistration.Forbidden", "Bạn không có quyền xác nhận yêu cầu của người khác."));
        }

        if (member.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.AlreadyConfirmed);
        }

        if (member.IsExpired(nowUtc))
        {
            return Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.ConfirmationExpired);
        }

        member.Confirm(nowUtc);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new ConfirmRegistrationResultDto(
            member.GroupId,
            member.Group.TopicId,
            member.StudentId,
            nowUtc,
            "Xác nhận tham gia nhóm đề tài thành công."));
    }

    public async Task<IReadOnlyList<ExpiredPendingMemberDto>> ExpirePendingRegistrationsAsync(
        DateTime nowUtc,
        int maxViolationsBeforeBlock,
        CancellationToken cancellationToken = default)
    {
        var expiredMembers = await db.StudentGroupMembers
            .Include(m => m.Group)
            .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Pending
                        && m.ConfirmationExpiresAtUtc.HasValue
                        && m.ConfirmationExpiresAtUtc.Value <= nowUtc)
            .ToListAsync(cancellationToken);

        if (expiredMembers.Count == 0)
        {
            return [];
        }

        var results = new List<ExpiredPendingMemberDto>();

        foreach (var member in expiredMembers)
        {
            var studentId = member.StudentId;
            var groupId = member.GroupId;
            var topicId = member.Group.TopicId;

            member.Group.RemoveMember(studentId);

            var penalty = await db.StudentRegistrationPenalties
                .FirstOrDefaultAsync(p => p.StudentId == studentId, cancellationToken);

            if (penalty is null)
            {
                penalty = StudentRegistrationPenalty.Create(studentId);
                db.StudentRegistrationPenalties.Add(penalty);
            }

            penalty.RecordViolation(nowUtc, maxViolationsBeforeBlock);

            results.Add(new ExpiredPendingMemberDto(
                groupId,
                studentId,
                StudentEmail: null,
                StudentFullName: null,
                topicId,
                TopicTitleVi: null,
                penalty.ViolationCount,
                penalty.IsRegistrationBlocked));
        }

        await db.SaveChangesAsync(cancellationToken);
        return results;
    }

    public async Task<StudentRegistrationStatusDto?> GetStudentRegistrationStatusAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var member = await db.StudentGroupMembers
            .Include(m => m.Group)
            .Where(m => m.StudentId == studentId)
            .OrderByDescending(m => m.JoinedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var penalty = await db.StudentRegistrationPenalties
            .FirstOrDefaultAsync(p => p.StudentId == studentId, cancellationToken);

        string? topicTitle = null;
        if (member is not null && db.Database.IsRelational())
        {
            try
            {
                var conn = db.Database.GetDbConnection();
                if (conn.State != ConnectionState.Open)
                {
                    await conn.OpenAsync(cancellationToken);
                }
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT title_vi FROM topics.topic WHERE id = @topicId";
                var p = cmd.CreateParameter();
                p.ParameterName = "@topicId";
                p.Value = member.Group.TopicId;
                cmd.Parameters.Add(p);
                var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                topicTitle = scalar as string;
            }
            catch
            {
                // Ignore title lookup failure
            }
        }

        return new StudentRegistrationStatusDto(
            HasRegistered: member is not null,
            GroupId: member?.GroupId,
            TopicId: member?.Group.TopicId,
            TopicTitleVi: topicTitle,
            ConfirmationStatus: member?.ConfirmationStatus.ToString(),
            ConfirmationExpiresAtUtc: member?.ConfirmationExpiresAtUtc,
            ViolationCount: penalty?.ViolationCount ?? 0,
            IsRegistrationBlocked: penalty?.IsRegistrationBlocked ?? false,
            BlockedAtUtc: penalty?.BlockedAtUtc);
    }
}
