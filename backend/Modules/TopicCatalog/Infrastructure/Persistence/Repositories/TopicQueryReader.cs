using Microsoft.EntityFrameworkCore;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.TopicCatalog.Application.Topics;
using UGTM.Modules.TopicCatalog.Application.Topics.Dtos;
using UGTM.Modules.TopicCatalog.Application.Topics.GetAllTopics;
using UGTM.Modules.TopicCatalog.Application.Topics.GetMyTopics;
using UGTM.Modules.TopicCatalog.Domain.Entities;
using UGTM.Modules.TopicCatalog.Domain.Enums;

namespace UGTM.Modules.TopicCatalog.Infrastructure.Persistence.Repositories;

public sealed class TopicQueryReader(
    TopicCatalogDbContext db,
    IThesisRegistrationContract registrationContract,
    IStudentInfoReader studentInfoReader) : ITopicQueryReader
{
    public async Task<PagedResult<TopicSummaryDto>> GetAllTopicsAsync(
        GetAllTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        var topicsQuery = db.Topics.AsNoTracking();

        if (query.BatchId.HasValue)
        {
            topicsQuery = topicsQuery.Where(t => t.BatchId == query.BatchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            var escapedKeyword = EscapeIlike(keyword);
            topicsQuery = topicsQuery.Where(t =>
                EF.Functions.ILike(t.TitleVi, $"%{escapedKeyword}%", @"\")
                || (t.TitleEn != null && EF.Functions.ILike(t.TitleEn, $"%{escapedKeyword}%", @"\")));
        }

        var totalCount = await topicsQuery.CountAsync(cancellationToken);

        var pagedTopics = await topicsQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new TopicSummaryDto(
                t.Id,
                t.BatchId,
                t.AdvisorId,
                t.TitleVi,
                t.TitleEn,
                t.Description,
                t.Requirements,
                t.Status.ToString(),
                t.StudentNumber,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<TopicSummaryDto>(
            pagedTopics,
            totalCount,
            query.PageNumber,
            query.PageSize);
    }

    public async Task<PagedResult<TopicTrackingSummaryDto>> GetMyTopicsAsync(
        GetMyTopicsQuery query,
        Guid? advisorId,
        bool isPrivileged,
        CancellationToken cancellationToken = default)
    {
        var topicsQuery = db.Topics.AsNoTracking();

        if (advisorId.HasValue)
        {
            topicsQuery = topicsQuery.Where(t => t.AdvisorId == advisorId.Value);
        }

        if (query.BatchId.HasValue)
        {
            topicsQuery = topicsQuery.Where(t => t.BatchId == query.BatchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && !int.TryParse(query.Status.Trim(), out _)
            && Enum.TryParse<TopicStatus>(query.Status.Trim(), ignoreCase: true, out var statusEnum)
            && Enum.IsDefined(statusEnum))
        {
            topicsQuery = topicsQuery.Where(t => t.Status == statusEnum);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            var escapedKeyword = EscapeIlike(keyword);
            topicsQuery = topicsQuery.Where(t =>
                EF.Functions.ILike(t.TitleVi, $"%{escapedKeyword}%", @"\")
                || (t.TitleEn != null && EF.Functions.ILike(t.TitleEn, $"%{escapedKeyword}%", @"\")));
        }

        var filter = query.RegistrationStatus?.Trim();
        var needsRegistrationFilter = !string.IsNullOrWhiteSpace(filter)
            && !string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);

        if (!needsRegistrationFilter)
        {
            var totalCount = await topicsQuery.CountAsync(cancellationToken);

            var pagedTopics = await topicsQuery
                .OrderByDescending(t => t.CreatedAt)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            var topicIds = pagedTopics.Select(t => t.Id).ToList();
            var registrations = await registrationContract.GetRegistrationsByTopicIdsAsync(topicIds, cancellationToken);

            var allStudentIds = registrations.Values
                .SelectMany(r => r.Members.Select(m => m.StudentId))
                .Distinct()
                .ToList();

            var studentsMap = await studentInfoReader.GetStudentsByIdsAsync(allStudentIds, cancellationToken);

            var items = pagedTopics.Select(t =>
            {
                registrations.TryGetValue(t.Id, out var reg);
                return BuildTrackingSummary(t, reg, studentsMap);
            }).ToList();

            return new PagedResult<TopicTrackingSummaryDto>(
                items,
                totalCount,
                query.PageNumber,
                query.PageSize);
        }
        else
        {
            var candidateTopics = await topicsQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var topicIds = candidateTopics.Select(t => t.Id).ToList();
            var registrations = await registrationContract.GetRegistrationsByTopicIdsAsync(topicIds, cancellationToken);

            var filteredTopics = new List<(Topic Topic, TopicRegistrationContractDto? Registration)>();
            foreach (var topic in candidateTopics)
            {
                registrations.TryGetValue(topic.Id, out var reg);
                var regCount = reg?.Members.Count ?? 0;
                var maxSlots = topic.StudentNumber ?? 1;

                var isMatch = filter switch
                {
                    var f when string.Equals(f, "Empty", StringComparison.OrdinalIgnoreCase)
                        => regCount == 0,
                    var f when string.Equals(f, "Registered", StringComparison.OrdinalIgnoreCase)
                        => regCount > 0,
                    var f when string.Equals(f, "Full", StringComparison.OrdinalIgnoreCase)
                        => regCount >= maxSlots,
                    _ => true
                };

                if (isMatch)
                {
                    filteredTopics.Add((topic, reg));
                }
            }

            var totalCount = filteredTopics.Count;
            var pagedPairs = filteredTopics
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            var pagedStudentIds = pagedPairs
                .Where(p => p.Registration != null)
                .SelectMany(p => p.Registration!.Members.Select(m => m.StudentId))
                .Distinct()
                .ToList();

            var studentsMap = await studentInfoReader.GetStudentsByIdsAsync(pagedStudentIds, cancellationToken);

            var items = pagedPairs.Select(p =>
                BuildTrackingSummary(p.Topic, p.Registration, studentsMap)).ToList();

            return new PagedResult<TopicTrackingSummaryDto>(
                items,
                totalCount,
                query.PageNumber,
                query.PageSize);
        }
    }

    public async Task<TopicTrackingDetailDto?> GetTopicTrackingDetailAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var topic = await db.Topics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return null;
        }

        var registration = await registrationContract.GetRegistrationByTopicIdAsync(topicId, cancellationToken);

        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap = new Dictionary<Guid, StudentInfoContractDto>();
        if (registration != null && registration.Members.Count > 0)
        {
            var studentIds = registration.Members.Select(m => m.StudentId).Distinct();
            studentsMap = await studentInfoReader.GetStudentsByIdsAsync(studentIds, cancellationToken);
        }

        return BuildTrackingDetail(topic, registration, studentsMap);
    }

    private static TopicTrackingSummaryDto BuildTrackingSummary(
        Topic topic,
        TopicRegistrationContractDto? reg,
        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap)
    {
        var registeredCount = reg?.Members.Count ?? 0;
        var maxSlots = topic.StudentNumber ?? 1;
        var remainingSlots = Math.Max(0, maxSlots - registeredCount);

        var regStatus = registeredCount switch
        {
            0 => "Empty",
            _ when registeredCount >= maxSlots => "Full",
            _ => "PartiallyRegistered"
        };

        var groupDto = BuildGroupDto(reg, studentsMap);

        return new TopicTrackingSummaryDto(
            topic.Id,
            topic.BatchId,
            topic.AdvisorId,
            topic.TitleVi,
            topic.TitleEn,
            topic.Description,
            topic.Requirements,
            topic.Status.ToString(),
            topic.StudentNumber,
            topic.CreatedAt,
            registeredCount,
            remainingSlots,
            regStatus,
            HasRegistration: registeredCount > 0,
            IsAvailable: remainingSlots > 0,
            Group: groupDto);
    }

    private static TopicTrackingDetailDto BuildTrackingDetail(
        Topic topic,
        TopicRegistrationContractDto? reg,
        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap)
    {
        var registeredCount = reg?.Members.Count ?? 0;
        var maxSlots = topic.StudentNumber ?? 1;
        var remainingSlots = Math.Max(0, maxSlots - registeredCount);

        var regStatus = registeredCount switch
        {
            0 => "Empty",
            _ when registeredCount >= maxSlots => "Full",
            _ => "PartiallyRegistered"
        };

        var groupDto = BuildGroupDto(reg, studentsMap);

        return new TopicTrackingDetailDto(
            topic.Id,
            topic.BatchId,
            topic.AdvisorId,
            topic.TitleVi,
            topic.TitleEn,
            topic.Description,
            topic.Requirements,
            topic.Status.ToString(),
            topic.StudentNumber,
            topic.CreatedAt,
            registeredCount,
            remainingSlots,
            regStatus,
            HasRegistration: registeredCount > 0,
            IsAvailable: remainingSlots > 0,
            Group: groupDto);
    }

    private static TopicGroupDto? BuildGroupDto(
        TopicRegistrationContractDto? reg,
        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap)
    {
        if (reg is null)
        {
            return null;
        }

        var students = reg.Members.Select(m =>
        {
            studentsMap.TryGetValue(m.StudentId, out var info);
            return new TopicStudentDto(
                m.StudentId,
                info?.UserCode,
                info?.FullName ?? $"Sinh viên ({m.StudentId})",
                info?.Email ?? string.Empty,
                info?.PhoneNumber,
                m.JoinedAt);
        }).ToList();

        return new TopicGroupDto(
            reg.GroupId,
            reg.Status,
            reg.CreatedAt,
            students);
    }

    private static string EscapeIlike(string input)
        => input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
}
