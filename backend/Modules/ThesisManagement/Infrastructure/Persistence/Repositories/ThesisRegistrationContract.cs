using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Contracts.TopicRegistrations;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

public sealed class ThesisRegistrationContract : IThesisRegistrationContract
{
    private readonly ThesisManagementDbContext? _db;
    private readonly ITopicRegistrationStore? _registrationStore;
    private readonly IStudentInfoReader? _studentInfoReader;
    private readonly IEmailSender? _emailSender;
    private readonly TimeProvider? _timeProvider;
    private readonly IThesisEmailTemplateRenderer? _emailTemplateRenderer;
    private readonly IReadOnlyList<StudentGroup>? _inMemoryGroups;

    public ThesisRegistrationContract(
        ThesisManagementDbContext db,
        ITopicRegistrationStore registrationStore,
        IStudentInfoReader studentInfoReader,
        IEmailSender emailSender,
        TimeProvider timeProvider,
        IThesisEmailTemplateRenderer? emailTemplateRenderer = null)
    {
        _db = db;
        _registrationStore = registrationStore;
        _studentInfoReader = studentInfoReader;
        _emailSender = emailSender;
        _timeProvider = timeProvider;
        _emailTemplateRenderer = emailTemplateRenderer ?? new Notifications.ThesisEmailTemplateRenderer();
    }

    private ThesisRegistrationContract(IReadOnlyList<StudentGroup> inMemoryGroups)
    {
        _inMemoryGroups = inMemoryGroups;
    }

    public static ThesisRegistrationContract CreateInMemory(IEnumerable<StudentGroup> groups)
        => new(groups.ToList());

    public async Task<IReadOnlyDictionary<Guid, TopicRegistrationContractDto>> GetRegistrationsByTopicIdsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var topicIdList = topicIds.Distinct().ToList();
        if (topicIdList.Count == 0)
        {
            return new Dictionary<Guid, TopicRegistrationContractDto>();
        }

        var query = QueryGroups()
            .Where(g => topicIdList.Contains(g.TopicId) && g.Status != StudentGroupStatus.Stopped)
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.Id);

        var groups = query is IAsyncEnumerable<StudentGroup>
            ? await query.ToListAsync(cancellationToken)
            : query.ToList();

        var result = new Dictionary<Guid, TopicRegistrationContractDto>();
        foreach (var group in groups)
        {
            var members = group.Members
                .OrderBy(m => m.JoinedAt)
                .ThenBy(m => m.StudentId)
                .Select(m => new TopicMemberContractDto(m.StudentId, m.BatchId, m.JoinedAt))
                .ToList();

            result[group.TopicId] = new TopicRegistrationContractDto(
                group.Id,
                group.TopicId,
                group.Status.ToString(),
                group.CreatedAt,
                members);
        }

        return result;
    }

    public async Task<TopicRegistrationContractDto?> GetRegistrationByTopicIdAsync(
        Guid topicId,
        CancellationToken cancellationToken = default)
    {
        var query = QueryGroups()
            .Where(g => g.TopicId == topicId && g.Status != StudentGroupStatus.Stopped)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id);

        var group = query is IAsyncEnumerable<StudentGroup>
            ? await query.FirstOrDefaultAsync(cancellationToken)
            : query.FirstOrDefault();

        if (group is null)
        {
            return null;
        }

        var members = group.Members
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.StudentId)
            .Select(m => new TopicMemberContractDto(m.StudentId, m.BatchId, m.JoinedAt))
            .ToList();

        return new TopicRegistrationContractDto(
            group.Id,
            group.TopicId,
            group.Status.ToString(),
            group.CreatedAt,
            members);
    }

    public async Task<AdvisorAssignStudentsContractResultDto> AssignStudentsByAdvisorAsync(
        Guid topicId,
        Guid actorUserId,
        bool isAdmin,
        IReadOnlyCollection<AdvisorStudentAssignmentInputDto> students,
        CancellationToken cancellationToken = default)
    {
        if (_registrationStore is null || _studentInfoReader is null)
        {
            return AdvisorAssignStudentsContractResultDto.Failure(
                "ThesisManagement.NotConfigured",
                "Thesis registration store is not configured.");
        }

        if (topicId == Guid.Empty)
        {
            return AdvisorAssignStudentsContractResultDto.Failure(
                TopicRegistrationErrors.TopicNotFound.Code,
                TopicRegistrationErrors.TopicNotFound.Message);
        }

        if (students.Count == 0)
        {
            return AdvisorAssignStudentsContractResultDto.Failure(
                TopicRegistrationErrors.EmptyStudentList.Code,
                TopicRegistrationErrors.EmptyStudentList.Message);
        }

        var resolvedStudents = new List<StudentInfoContractDto>();
        foreach (var item in students)
        {
            var student = await _studentInfoReader.GetStudentByIdOrCodeAsync(item.StudentId, item.UserCode, cancellationToken);
            if (student is null)
            {
                return AdvisorAssignStudentsContractResultDto.Failure(
                    TopicRegistrationErrors.StudentNotFound.Code,
                    TopicRegistrationErrors.StudentNotFound.Message);
            }
            resolvedStudents.Add(student);
        }

        var distinctIds = resolvedStudents.Select(s => s.Id).Distinct().ToList();
        if (distinctIds.Count != resolvedStudents.Count)
        {
            return AdvisorAssignStudentsContractResultDto.Failure(
                TopicRegistrationErrors.DuplicateStudentInGroup.Code,
                TopicRegistrationErrors.DuplicateStudentInGroup.Message);
        }

        var nowUtc = (_timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var assignResult = await _registrationStore.AssignTopicStudentsByAdvisorAsync(
            topicId,
            actorUserId,
            isAdmin,
            distinctIds,
            nowUtc,
            cancellationToken);

        if (!assignResult.IsSuccess)
        {
            return AdvisorAssignStudentsContractResultDto.Failure(
                assignResult.Error.Code,
                assignResult.Error.Message);
        }

        if (_emailSender is not null)
        {
            foreach (var student in resolvedStudents)
            {
                if (string.IsNullOrWhiteSpace(student.Email)) continue;
                try
                {
                    var renderer = _emailTemplateRenderer ?? new Notifications.ThesisEmailTemplateRenderer();
                    var message = renderer.RenderAdvisorAssignedNotification(
                        student.Email,
                        student.FullName ?? string.Empty,
                        student.UserCode ?? string.Empty);
                    await _emailSender.SendAsync(message, cancellationToken);
                }
                catch { }
            }
        }

        return AdvisorAssignStudentsContractResultDto.Success(
            assignResult.Value.GroupId,
            topicId,
            assignResult.Value.BatchId,
            distinctIds);
    }

    public async Task<AdvisorUnassignStudentsContractResultDto> UnassignStudentsByAdvisorAsync(
        Guid topicId,
        Guid actorUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (_registrationStore is null)
        {
            return AdvisorUnassignStudentsContractResultDto.Failure(
                "ThesisManagement.NotConfigured",
                "Thesis registration store is not configured.");
        }

        var nowUtc = (_timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var result = await _registrationStore.UnassignTopicStudentsByAdvisorAsync(
            topicId,
            actorUserId,
            isAdmin,
            nowUtc,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return AdvisorUnassignStudentsContractResultDto.Failure(
                result.Error.Code,
                result.Error.Message);
        }

        return AdvisorUnassignStudentsContractResultDto.Success();
    }

    private IQueryable<StudentGroup> QueryGroups()
        => _inMemoryGroups is not null
            ? _inMemoryGroups.AsQueryable()
            : _db!.StudentGroups.AsNoTracking().Include(g => g.Members);
}
