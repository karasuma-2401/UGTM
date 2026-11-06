using System.Security.Cryptography;
using System.Text;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Tests;

public sealed class FakeTopicRegistrationStore : ITopicRegistrationStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, (Guid BatchId, Guid AdvisorId, int StudentNumber, string Status)> _topics = [];
    private readonly List<StudentGroup> _groups = [];
    private readonly Dictionary<Guid, StudentRegistrationPenalty> _penalties = [];
    private string? _lastRawToken;

    public bool IsRegistrationWindowOpen { get; set; } = true;
    public bool IsTopicPreparationStageActive { get; set; } = true;

    public void SeedTopic(Guid topicId, Guid batchId, int studentNumber, string status, Guid? advisorId = null)
    {
        _topics[topicId] = (batchId, advisorId ?? Guid.Empty, studentNumber, status);
    }

    public void SetPenalty(Guid studentId, int violationCount, bool isBlocked)
    {
        var p = StudentRegistrationPenalty.Create(studentId);
        for (var i = 0; i < violationCount; i++)
        {
            p.RecordViolation(DateTime.UtcNow);
        }
        if (isBlocked && !p.IsRegistrationBlocked)
        {
            p.RecordViolation(DateTime.UtcNow, maxViolations: violationCount);
        }
        _penalties[studentId] = p;
    }

    public string? GetLastGeneratedRawToken() => _lastRawToken;

    public IReadOnlyList<StudentGroupMember> GetGroupMembers(Guid groupId)
        => _groups.FirstOrDefault(g => g.Id == groupId)?.Members.ToList() ?? [];

    public Task<Result<TopicRegistrationResultDto>> RegisterTopicAsync(
        Guid topicId,
        Guid studentAId,
        Guid? studentBId,
        DateTime nowUtc,
        TimeSpan confirmationWindow,
        string confirmationTokenHash,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_topics.TryGetValue(topicId, out var topic))
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotFound));
            }

            if (!string.Equals(topic.Status, "Published", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotPublished));
            }

            if (!IsRegistrationWindowOpen)
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.RegistrationWindowClosed));
            }

            if (_penalties.TryGetValue(studentAId, out var penA) && penA.IsRegistrationBlocked)
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.StudentRegistrationBlocked));
            }

            if (studentBId.HasValue && _penalties.TryGetValue(studentBId.Value, out var penB) && penB.IsRegistrationBlocked)
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.PartnerRegistrationBlocked));
            }

            // Check already registered in batch
            var alreadyRegistered = _groups.SelectMany(g => g.Members).Any(m =>
                (m.StudentId == studentAId || (studentBId.HasValue && m.StudentId == studentBId.Value))
                && m.BatchId == topic.BatchId);

            if (alreadyRegistered)
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.StudentAlreadyRegisteredInBatch));
            }

            var group = _groups.FirstOrDefault(g => g.TopicId == topicId);
            if (group is not null)
            {
                // Clean up expired pending members
                var expired = group.Members
                    .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Pending && m.ConfirmationExpiresAtUtc <= nowUtc)
                    .ToList();
                foreach (var exp in expired)
                {
                    group.RemoveMember(exp.StudentId);
                    if (!_penalties.TryGetValue(exp.StudentId, out var p))
                    {
                        p = StudentRegistrationPenalty.Create(exp.StudentId);
                        _penalties[exp.StudentId] = p;
                    }
                    p.RecordViolation(nowUtc);
                }
            }

            var occupiedSlots = group?.Members.Count(m =>
                m.ConfirmationStatus == MemberConfirmationStatus.Confirmed
                || (m.ConfirmationStatus == MemberConfirmationStatus.Pending && m.ConfirmationExpiresAtUtc > nowUtc)) ?? 0;

            var requestedSlots = studentBId.HasValue ? 2 : 1;
            if (requestedSlots > topic.StudentNumber)
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.GroupSizeExceedsTopicQuota));
            }

            if (occupiedSlots + requestedSlots > topic.StudentNumber)
            {
                return Task.FromResult(Result.Failure<TopicRegistrationResultDto>(TopicRegistrationErrors.TopicAlreadyTaken));
            }

            if (group is null)
            {
                group = new StudentGroup(Guid.NewGuid(), topicId, nowUtc);
                _groups.Add(group);
            }

            group.AddConfirmedMember(studentAId, topic.BatchId, nowUtc);

            DateTime? expiresAt = null;
            if (studentBId.HasValue)
            {
                expiresAt = nowUtc.Add(confirmationWindow);
                _lastRawToken = "test_raw_token_" + Guid.NewGuid().ToString("N");
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_lastRawToken))).ToLowerInvariant();
                group.AddPendingMember(studentBId.Value, topic.BatchId, studentAId, hash, nowUtc, expiresAt.Value);
            }

            return Task.FromResult(Result.Success(new TopicRegistrationResultDto(
                group.Id,
                topicId,
                topic.BatchId,
                studentBId.HasValue,
                expiresAt,
                "Success")));
        }
    }

    public Task<Result<ConfirmRegistrationResultDto>> ConfirmRegistrationAsync(
        string tokenHash,
        Guid? currentStudentId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var member = _groups.SelectMany(g => g.Members)
                .FirstOrDefault(m => m.ConfirmationTokenHash == tokenHash);

            if (member is null)
            {
                return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.InvalidConfirmationToken));
            }

            if (currentStudentId.HasValue && currentStudentId.Value != member.StudentId)
            {
                return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(
                    Error.Forbidden("TopicRegistration.Forbidden", "Forbidden")));
            }

            if (member.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
            {
                return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.AlreadyConfirmed));
            }

            if (member.IsExpired(nowUtc))
            {
                return Task.FromResult(Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.ConfirmationExpired));
            }

            member.Confirm(nowUtc);
            return Task.FromResult(Result.Success(new ConfirmRegistrationResultDto(
                member.GroupId,
                member.Group.TopicId,
                member.StudentId,
                nowUtc,
                "Success")));
        }
    }

    public Task<IReadOnlyList<ExpiredPendingMemberDto>> ExpirePendingRegistrationsAsync(
        DateTime nowUtc,
        int maxViolationsBeforeBlock,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var expiredMembers = _groups.SelectMany(g => g.Members)
                .Where(m => m.ConfirmationStatus == MemberConfirmationStatus.Pending
                            && m.ConfirmationExpiresAtUtc <= nowUtc)
                .ToList();

            var results = new List<ExpiredPendingMemberDto>();
            foreach (var m in expiredMembers)
            {
                m.Group.RemoveMember(m.StudentId);
                if (!_penalties.TryGetValue(m.StudentId, out var pen))
                {
                    pen = StudentRegistrationPenalty.Create(m.StudentId);
                    _penalties[m.StudentId] = pen;
                }
                pen.RecordViolation(nowUtc, maxViolationsBeforeBlock);
                results.Add(new ExpiredPendingMemberDto(
                    m.GroupId,
                    m.StudentId,
                    null,
                    null,
                    m.Group.TopicId,
                    null,
                    pen.ViolationCount,
                    pen.IsRegistrationBlocked));
            }
            return Task.FromResult<IReadOnlyList<ExpiredPendingMemberDto>>(results);
        }
    }

    public Task<StudentRegistrationStatusDto?> GetStudentRegistrationStatusAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var member = _groups.SelectMany(g => g.Members)
                .OrderByDescending(m => m.JoinedAt)
                .FirstOrDefault(m => m.StudentId == studentId);

            _penalties.TryGetValue(studentId, out var penalty);

            return Task.FromResult<StudentRegistrationStatusDto?>(new StudentRegistrationStatusDto(
                HasRegistered: member is not null,
                GroupId: member?.GroupId,
                TopicId: member?.Group.TopicId,
                TopicTitleVi: null,
                ConfirmationStatus: member?.ConfirmationStatus.ToString(),
                ConfirmationExpiresAtUtc: member?.ConfirmationExpiresAtUtc,
                ViolationCount: penalty?.ViolationCount ?? 0,
                IsRegistrationBlocked: penalty?.IsRegistrationBlocked ?? false,
                BlockedAtUtc: penalty?.BlockedAtUtc));
        }
    }

    public Task<Result<AdvisorTopicRegistrationResultDto>> AssignTopicStudentsByAdvisorAsync(
        Guid topicId,
        Guid advisorId,
        bool isAdmin,
        IReadOnlyList<Guid> studentIds,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_topics.TryGetValue(topicId, out var topic))
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotFound));
            }

            if (!isAdmin && topic.AdvisorId != Guid.Empty && topic.AdvisorId != advisorId)
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.NotTopicAdvisor));
            }

            var isAllowedStatus = string.Equals(topic.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(topic.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(topic.Status, "NeedRevision", StringComparison.OrdinalIgnoreCase)
                || string.Equals(topic.Status, "Approved", StringComparison.OrdinalIgnoreCase);

            if (!isAllowedStatus)
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.InvalidTopicStatusForAdvisorAssign));
            }

            if (!IsTopicPreparationStageActive)
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.TopicPreparationWindowClosed));
            }

            if (studentIds.Count == 0)
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.EmptyStudentList));
            }

            if (studentIds.Distinct().Count() != studentIds.Count)
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.DuplicateStudentInGroup));
            }

            if (studentIds.Count > topic.StudentNumber)
            {
                return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.GroupSizeExceedsTopicQuota));
            }

            foreach (var sId in studentIds)
            {
                if (_penalties.TryGetValue(sId, out var pen) && pen.IsRegistrationBlocked)
                {
                    return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.StudentRegistrationBlocked));
                }

                var inOtherGroup = _groups.Any(g => g.TopicId != topicId
                                                    && g.Members.Any(m => m.StudentId == sId && m.BatchId == topic.BatchId));
                if (inOtherGroup)
                {
                    return Task.FromResult(Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.StudentAlreadyRegisteredInBatch));
                }
            }

            var group = _groups.FirstOrDefault(g => g.TopicId == topicId);
            if (group is null)
            {
                group = new StudentGroup(Guid.NewGuid(), topicId, nowUtc);
                _groups.Add(group);
            }
            else
            {
                var existingMembers = group.Members.ToList();
                foreach (var member in existingMembers)
                {
                    if (!studentIds.Contains(member.StudentId))
                    {
                        group.RemoveMember(member.StudentId);
                    }
                }
            }

            var currentMemberIds = group.Members.Select(m => m.StudentId).ToHashSet();
            foreach (var studentId in studentIds)
            {
                if (!currentMemberIds.Contains(studentId))
                {
                    group.AddConfirmedMember(studentId, topic.BatchId, nowUtc);
                }
            }

            return Task.FromResult(Result.Success(new AdvisorTopicRegistrationResultDto(
                group.Id,
                topicId,
                topic.BatchId,
                studentIds,
                "Đăng ký nhóm sinh viên cho đề tài thành công.")));
        }
    }

    public Task<Result> UnassignTopicStudentsByAdvisorAsync(
        Guid topicId,
        Guid advisorId,
        bool isAdmin,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_topics.TryGetValue(topicId, out var topic))
            {
                return Task.FromResult(Result.Failure(TopicRegistrationErrors.TopicNotFound));
            }

            if (!isAdmin && topic.AdvisorId != Guid.Empty && topic.AdvisorId != advisorId)
            {
                return Task.FromResult(Result.Failure(TopicRegistrationErrors.NotTopicAdvisor));
            }

            var isAllowedStatus = string.Equals(topic.Status, "Draft", StringComparison.OrdinalIgnoreCase)
                || string.Equals(topic.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(topic.Status, "NeedRevision", StringComparison.OrdinalIgnoreCase)
                || string.Equals(topic.Status, "Approved", StringComparison.OrdinalIgnoreCase);

            if (!isAllowedStatus)
            {
                return Task.FromResult(Result.Failure(TopicRegistrationErrors.InvalidTopicStatusForAdvisorAssign));
            }

            if (!IsTopicPreparationStageActive)
            {
                return Task.FromResult(Result.Failure(TopicRegistrationErrors.TopicPreparationWindowClosed));
            }

            var group = _groups.FirstOrDefault(g => g.TopicId == topicId);
            if (group is null || group.Members.Count == 0)
            {
                return Task.FromResult(Result.Failure(TopicRegistrationErrors.RegistrationNotFound));
            }

            var members = group.Members.ToList();
            foreach (var m in members)
            {
                group.RemoveMember(m.StudentId);
            }
            _groups.Remove(group);

            return Task.FromResult(Result.Success());
        }
    }

}
