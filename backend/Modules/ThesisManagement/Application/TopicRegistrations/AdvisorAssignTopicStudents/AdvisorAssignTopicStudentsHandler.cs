using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Email;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.AdvisorAssignTopicStudents;

public sealed class AdvisorAssignTopicStudentsHandler(
    ITopicRegistrationStore store,
    IStudentInfoReader studentInfoReader,
    IEmailSender emailSender,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IThesisEmailTemplateRenderer emailTemplateRenderer)
{
    public async Task<Result<AdvisorTopicRegistrationResultDto>> HandleAsync(
        AdvisorAssignTopicStudentsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<AdvisorTopicRegistrationResultDto>(
                Error.Unauthorized("Auth.Unauthorized", "Vui lòng đăng nhập để thực hiện thao tác."));
        }

        var isAdmin = HasRole(currentUser, "Admin");
        var isLecturer = HasRole(currentUser, "Lecturer");
        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.OnlyAdvisorsAllowed);
        }

        if (command.TopicId == Guid.Empty)
        {
            return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.TopicNotFound);
        }

        var hasIds = command.StudentIds is { Count: > 0 };
        var hasCodes = command.StudentCodes is { Count: > 0 };

        if (!hasIds && !hasCodes)
        {
            return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.EmptyStudentList);
        }

        var resolvedStudents = new List<StudentInfoContractDto>();

        if (command.StudentIds is not null)
        {
            foreach (var studentId in command.StudentIds)
            {
                if (studentId == Guid.Empty)
                {
                    return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.StudentNotFound);
                }

                var student = await studentInfoReader.GetStudentByIdOrCodeAsync(studentId, null, cancellationToken);
                if (student is null)
                {
                    return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.StudentNotFound);
                }

                resolvedStudents.Add(student);
            }
        }

        if (command.StudentCodes is not null)
        {
            foreach (var code in command.StudentCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.StudentNotFound);
                }

                var student = await studentInfoReader.GetStudentByIdOrCodeAsync(null, code.Trim(), cancellationToken);
                if (student is null)
                {
                    return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.StudentNotFound);
                }

                resolvedStudents.Add(student);
            }
        }

        var distinctIds = resolvedStudents.Select(s => s.Id).Distinct().ToList();
        if (distinctIds.Count != resolvedStudents.Count)
        {
            return Result.Failure<AdvisorTopicRegistrationResultDto>(TopicRegistrationErrors.DuplicateStudentInGroup);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var result = await store.AssignTopicStudentsByAdvisorAsync(
            command.TopicId,
            currentUser.UserId.Value,
            isAdmin,
            distinctIds,
            nowUtc,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        // Send confirmation/notification emails to assigned students
        await TrySendNotificationEmailsAsync(resolvedStudents, cancellationToken);

        return result;
    }

    private async Task TrySendNotificationEmailsAsync(
        IReadOnlyList<StudentInfoContractDto> students,
        CancellationToken cancellationToken)
    {
        foreach (var student in students)
        {
            if (string.IsNullOrWhiteSpace(student.Email))
            {
                continue;
            }

            try
            {
                var message = emailTemplateRenderer.RenderAdvisorAssignedNotification(
                    student.Email,
                    student.FullName ?? string.Empty,
                    student.UserCode ?? string.Empty);

                await emailSender.SendAsync(message, cancellationToken);
            }
            catch
            {
                // Email delivery failure should not fail the registration transaction
            }
        }
    }

    private static bool HasRole(ICurrentUser user, string role)
        => user.Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
