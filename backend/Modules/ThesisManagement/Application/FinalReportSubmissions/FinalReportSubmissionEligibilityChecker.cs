using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.AcademicPeriods.Contracts.Batches;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Domain.Repositories;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions;

public sealed record FinalReportSubmissionContext(
    StudentGroup Group,
    Guid StudentId,
    Guid BatchId,
    DateTime SubmittedAtUtc);

/// <summary>
/// Checks shared submission prerequisites without writing data. The submission handler must also
/// enforce the report's version/evaluation state and handle concurrent changes when saving.
/// </summary>
public sealed class FinalReportSubmissionEligibilityChecker(
    IStudentGroupRepository groupRepository,
    IMidtermReportRepository midtermReportRepository,
    IKltnBatchReader batchReader,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<FinalReportSubmissionContext>> CheckAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue || currentUser.UserId == Guid.Empty)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.AuthenticationRequired);
        }

        if (!currentUser.Roles.Any(role => string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.AccessDenied);
        }

        if (groupId == Guid.Empty)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.InvalidRequest);
        }

        var group = await groupRepository.GetByIdAsync(groupId, cancellationToken);
        if (group is null)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.GroupNotFound);
        }

        var membership = group.Members.SingleOrDefault(member => member.StudentId == currentUser.UserId.Value);
        if (membership is null || membership.ConfirmationStatus != MemberConfirmationStatus.Confirmed)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.AccessDenied);
        }

        if (group.Status is not (StudentGroupStatus.InProgress or StudentGroupStatus.FinalReportSubmitted))
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.InvalidGroupState);
        }

        var midterm = await midtermReportRepository.GetByGroupIdAsync(group.Id, cancellationToken);
        if (midterm is null || midterm.Status != MidtermReportStatus.Approved)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.MidtermNotApproved);
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var window = await batchReader.GetFinalReportSubmissionWindowAsync(membership.BatchId, nowUtc, cancellationToken);
        if (window is null)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.BatchNotFound);
        }

        if (window.BatchStatus != "Open" || !window.IsSubmissionWindowOpen)
        {
            return Result.Failure<FinalReportSubmissionContext>(FinalReportSubmissionErrors.OutsideAllowedWindow);
        }

        return Result.Success(new FinalReportSubmissionContext(group, currentUser.UserId.Value, membership.BatchId, nowUtc));
    }
}
