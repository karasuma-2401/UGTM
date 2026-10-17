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

public sealed class TopicQueryReader : ITopicQueryReader
{
    private readonly TopicCatalogDbContext? _db;
    private readonly IReadOnlyList<Topic>? _inMemoryTopics;
    private readonly IThesisRegistrationContract _registrationContract;
    private readonly IStudentInfoReader _studentInfoReader;

    public TopicQueryReader(
        TopicCatalogDbContext db,
        IThesisRegistrationContract registrationContract,
        IStudentInfoReader studentInfoReader)
    {
        _db = db;
        _registrationContract = registrationContract;
        _studentInfoReader = studentInfoReader;
    }

    private TopicQueryReader(
        IReadOnlyList<Topic> inMemoryTopics,
        IThesisRegistrationContract registrationContract,
        IStudentInfoReader studentInfoReader)
    {
        _inMemoryTopics = inMemoryTopics;
        _registrationContract = registrationContract;
        _studentInfoReader = studentInfoReader;
    }

    public static TopicQueryReader CreateInMemory(
        IEnumerable<Topic> topics,
        IThesisRegistrationContract registrationContract,
        IStudentInfoReader studentInfoReader)
        => new(topics.ToList(), registrationContract, studentInfoReader);

    public async Task<PagedResult<TopicSummaryDto>> GetAllTopicsAsync(
        GetAllTopicsQuery query,
        CancellationToken cancellationToken = default)
    {
        var topicsQuery = QueryTopics();

        if (query.BatchId.HasValue)
        {
            topicsQuery = topicsQuery.Where(t => t.BatchId == query.BatchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            topicsQuery = ApplyKeywordFilter(topicsQuery, query.Keyword);
        }

        var totalCount = await CountSafeAsync(topicsQuery, cancellationToken);

        var pagedQuery = topicsQuery
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
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
                t.CreatedAt));

        var pagedTopics = await ToListSafeAsync(pagedQuery, cancellationToken);

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
        var topicsQuery = QueryTopics();

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
            topicsQuery = ApplyKeywordFilter(topicsQuery, query.Keyword);
        }

        var filter = query.RegistrationStatus?.Trim();
        var needsRegistrationFilter = !string.IsNullOrWhiteSpace(filter)
            && !string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase);

        if (!needsRegistrationFilter)
        {
            var totalCount = await CountSafeAsync(topicsQuery, cancellationToken);

            var pagedQuery = topicsQuery
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize);

            var pagedTopics = await ToListSafeAsync(pagedQuery, cancellationToken);

            var topicIds = pagedTopics.Select(t => t.Id).ToList();
            var registrations = await _registrationContract.GetRegistrationsByTopicIdsAsync(topicIds, cancellationToken);

            var allStudentIds = registrations.Values
                .SelectMany(r => r.Members.Select(m => m.StudentId))
                .Distinct()
                .ToList();

            var studentsMap = await _studentInfoReader.GetStudentsByIdsAsync(allStudentIds, cancellationToken);

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
            var candidateQuery = topicsQuery
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Id);

            var candidateTopics = await ToListSafeAsync(candidateQuery, cancellationToken);

            var topicIds = candidateTopics.Select(t => t.Id).ToList();
            var registrations = await _registrationContract.GetRegistrationsByTopicIdsAsync(topicIds, cancellationToken);

            var filteredTopics = new List<(Topic Topic, TopicRegistrationContractDto? Registration)>();
            foreach (var topic in candidateTopics)
            {
                registrations.TryGetValue(topic.Id, out var reg);
                var (_, _, regStatus, _, _) = ComputeRegistrationMetrics(topic.StudentNumber, reg?.Members.Count ?? 0);

                if (string.Equals(regStatus, filter, StringComparison.OrdinalIgnoreCase))
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

            var studentsMap = await _studentInfoReader.GetStudentsByIdsAsync(pagedStudentIds, cancellationToken);

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
        Topic? topic;
        if (_inMemoryTopics is not null)
        {
            topic = _inMemoryTopics.FirstOrDefault(t => t.Id == topicId);
        }
        else
        {
            topic = _db!.Topics.Local.FirstOrDefault(t => t.Id == topicId)
                ?? await _db.Topics
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == topicId, cancellationToken);
        }

        if (topic is null)
        {
            return null;
        }

        var registration = await _registrationContract.GetRegistrationByTopicIdAsync(topicId, cancellationToken);

        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap = new Dictionary<Guid, StudentInfoContractDto>();
        if (registration != null && registration.Members.Count > 0)
        {
            var studentIds = registration.Members.Select(m => m.StudentId).Distinct();
            studentsMap = await _studentInfoReader.GetStudentsByIdsAsync(studentIds, cancellationToken);
        }

        return BuildTrackingDetail(topic, registration, studentsMap);
    }

    public static (int RegisteredCount, int RemainingSlots, string RegistrationStatus, bool HasRegistration, bool IsAvailable) ComputeRegistrationMetrics(
        int? maxStudentNumber,
        int registeredStudentsCount)
    {
        var registeredCount = Math.Max(0, registeredStudentsCount);
        var maxSlots = Math.Max(1, maxStudentNumber ?? 1);
        var remainingSlots = Math.Max(0, maxSlots - registeredCount);

        var regStatus = registeredCount switch
        {
            0 => "Empty",
            _ when registeredCount >= maxSlots => "Full",
            _ => "Registered"
        };

        return (
            RegisteredCount: registeredCount,
            RemainingSlots: remainingSlots,
            RegistrationStatus: regStatus,
            HasRegistration: registeredCount > 0,
            IsAvailable: remainingSlots > 0);
    }

    public static TopicTrackingSummaryDto BuildTrackingSummary(
        Topic topic,
        TopicRegistrationContractDto? reg,
        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap)
    {
        var (registeredCount, remainingSlots, regStatus, hasRegistration, isAvailable) =
            ComputeRegistrationMetrics(topic.StudentNumber, reg?.Members.Count ?? 0);

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
            HasRegistration: hasRegistration,
            IsAvailable: isAvailable,
            Group: groupDto);
    }

    public static TopicTrackingDetailDto BuildTrackingDetail(
        Topic topic,
        TopicRegistrationContractDto? reg,
        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap)
    {
        var (registeredCount, remainingSlots, regStatus, hasRegistration, isAvailable) =
            ComputeRegistrationMetrics(topic.StudentNumber, reg?.Members.Count ?? 0);

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
            HasRegistration: hasRegistration,
            IsAvailable: isAvailable,
            Group: groupDto);
    }

    private static TopicGroupDto? BuildGroupDto(
        TopicRegistrationContractDto? reg,
        IReadOnlyDictionary<Guid, StudentInfoContractDto> studentsMap)
    {
        if (reg is null || reg.Members.Count == 0)
        {
            return null;
        }

        var students = reg.Members
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.StudentId)
            .Select(m =>
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

    private IQueryable<Topic> QueryTopics()
        => _inMemoryTopics is not null
            ? _inMemoryTopics.AsQueryable()
            : _db!.Topics.AsNoTracking();

    private static Task<int> CountSafeAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        => query is IAsyncEnumerable<T>
            ? query.CountAsync(cancellationToken)
            : Task.FromResult(query.Count());

    private static Task<List<T>> ToListSafeAsync<T>(IQueryable<T> query, CancellationToken cancellationToken)
        => query is IAsyncEnumerable<T>
            ? query.ToListAsync(cancellationToken)
            : Task.FromResult(query.ToList());

    private IQueryable<Topic> ApplyKeywordFilter(IQueryable<Topic> topicsQuery, string rawKeyword)
    {
        var keyword = rawKeyword.Trim();
        var escapedKeyword = EscapeIlike(keyword);

        if (_inMemoryTopics is null
            && _db?.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            return topicsQuery.Where(t =>
                EF.Functions.ILike(t.TitleVi, $"%{escapedKeyword}%", @"\")
                || (t.TitleEn != null && EF.Functions.ILike(t.TitleEn, $"%{escapedKeyword}%", @"\")));
        }

        var lowerKeyword = keyword.ToLowerInvariant();
        return topicsQuery.Where(t =>
            t.TitleVi.ToLower().Contains(lowerKeyword)
            || (t.TitleEn != null && t.TitleEn.ToLower().Contains(lowerKeyword)));
    }

    public static string EscapeIlike(string input)
        => input
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
}
