using Microsoft.EntityFrameworkCore;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Contracts.AdvisorEvaluations;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Persistence.Repositories;

internal sealed class AdvisorEvaluationWorkflowContract(
    ThesisManagementDbContext context,
    IThesisManagementAuditOutbox auditOutbox)
    : IAdvisorEvaluationWorkflowContract
{
    public async Task<AdvisorEvaluationFinalReportContextContractDto?> GetFinalReportContextAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default)
    {
        if (finalReportId == Guid.Empty)
        {
            return null;
        }

        var report = await context.FinalReports.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == finalReportId, cancellationToken);
        if (report is null)
        {
            return null;
        }

        var group = await context.StudentGroups.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == report.GroupId, cancellationToken);
        if (group is null)
        {
            return new AdvisorEvaluationFinalReportContextContractDto(
                report.Id, report.GroupId, Guid.Empty, false,
                report.VersionType == FinalReportVersionType.InitialFinal,
                report.Status == FinalReportStatus.UnderEvaluation,
                false, report.CurrentVersionNo, null, null);
        }

        var file = await context.SubmissionFiles.AsNoTracking().SingleOrDefaultAsync(value =>
            value.EntityType == SubmissionEntityType.FinalReport
            && value.EntityId == report.Id
            && value.VersionNo == report.CurrentVersionNo,
            cancellationToken);
        return new AdvisorEvaluationFinalReportContextContractDto(
            report.Id,
            group.Id,
            group.TopicId,
            true,
            report.VersionType == FinalReportVersionType.InitialFinal,
            report.Status == FinalReportStatus.UnderEvaluation,
            group.Status == StudentGroupStatus.FinalReportSubmitted,
            report.CurrentVersionNo,
            file?.Id,
            file?.ChecksumSha256);
    }

    public async Task<AdvisorEvaluationGroupContextContractDto?> GetGroupContextAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        if (groupId == Guid.Empty)
        {
            return null;
        }

        var group = await context.StudentGroups.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == groupId, cancellationToken);
        return group is null
            ? null
            : new AdvisorEvaluationGroupContextContractDto(group.Id, group.TopicId);
    }

    public async Task<AdvisorEvaluationLockedFinalReportContractDto?> GetLockedInitialFinalReportAsync(
        Guid finalReportId,
        CancellationToken cancellationToken = default)
    {
        if (finalReportId == Guid.Empty)
        {
            return null;
        }

        var report = await context.FinalReports
            .SingleOrDefaultAsync(value => value.Id == finalReportId, cancellationToken);
        if (report is null
            || report.VersionType != FinalReportVersionType.InitialFinal
            || report.Status != FinalReportStatus.UnderEvaluation)
        {
            return null;
        }

        var group = await context.StudentGroups
            .SingleOrDefaultAsync(value => value.Id == report.GroupId, cancellationToken);
        if (group?.Status != StudentGroupStatus.FinalReportSubmitted)
        {
            return null;
        }

        var file = await context.SubmissionFiles.SingleOrDefaultAsync(value =>
            value.EntityType == SubmissionEntityType.FinalReport
            && value.EntityId == report.Id
            && value.VersionNo == report.CurrentVersionNo,
            cancellationToken);
        if (file?.ChecksumSha256 is not { Length: 64 } checksum)
        {
            return null;
        }

        TrackReportAndGroupConcurrency(report, group);
        return new AdvisorEvaluationLockedFinalReportContractDto(
            report.Id,
            group.Id,
            report.CurrentVersionNo,
            file.Id,
            checksum.ToLowerInvariant());
    }

    public async Task<AdvisorEvaluationGroupReadinessResultContractDto> TryMakeGroupReadyForReviewerAssignmentAsync(
        Guid groupId,
        Guid finalReportId,
        int expectedReportVersionNo,
        CancellationToken cancellationToken = default)
    {
        if (groupId == Guid.Empty)
        {
            return new AdvisorEvaluationGroupReadinessResultContractDto(
                AdvisorEvaluationGroupReadinessOutcome.GroupNotFound);
        }

        var group = await context.StudentGroups.SingleOrDefaultAsync(value => value.Id == groupId, cancellationToken);
        if (group is null)
        {
            return new AdvisorEvaluationGroupReadinessResultContractDto(
                AdvisorEvaluationGroupReadinessOutcome.GroupNotFound);
        }

        var report = await context.FinalReports.SingleOrDefaultAsync(value => value.Id == finalReportId, cancellationToken);
        if (report is null || report.GroupId != groupId)
        {
            return new AdvisorEvaluationGroupReadinessResultContractDto(
                AdvisorEvaluationGroupReadinessOutcome.FinalReportNotFound, groupId);
        }

        if (report.VersionType != FinalReportVersionType.InitialFinal
            || report.Status != FinalReportStatus.UnderEvaluation)
        {
            return new AdvisorEvaluationGroupReadinessResultContractDto(
                AdvisorEvaluationGroupReadinessOutcome.InvalidFinalReportState, groupId);
        }

        if (expectedReportVersionNo < 1 || report.CurrentVersionNo != expectedReportVersionNo)
        {
            return new AdvisorEvaluationGroupReadinessResultContractDto(
                AdvisorEvaluationGroupReadinessOutcome.ReportVersionMismatch, groupId);
        }

        if (!group.TryMakeReadyForReviewerAssignment())
        {
            return new AdvisorEvaluationGroupReadinessResultContractDto(
                AdvisorEvaluationGroupReadinessOutcome.InvalidGroupState, groupId);
        }

        context.Entry(report).Property(value => value.Status).IsModified = true;
        return new AdvisorEvaluationGroupReadinessResultContractDto(
            AdvisorEvaluationGroupReadinessOutcome.Succeeded, groupId);
    }

    public void AddDecisionAudit(AdvisorEvaluationDecisionAuditContractDto decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.EvaluationId == Guid.Empty || decision.GroupId == Guid.Empty
            || decision.FinalReportId == Guid.Empty || decision.ReportVersionNo < 1
            || decision.DecidedBy == Guid.Empty || decision.DecidedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Advisor-evaluation decision audit data is invalid.", nameof(decision));
        }

        auditOutbox.Add(new AuditIntegrationEntry(
            "StudentGroup",
            decision.GroupId.ToString(),
            decision.AllowsReviewerAssignment
                ? IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentApproved
                : IntegrationAuditActions.AdvisorEvaluationReviewerAssignmentRejected,
            "Succeeded",
            decision.AllowsReviewerAssignment ? null : "AdvisorEvaluationRejected",
            new
            {
                decision.EvaluationId,
                decision.FinalReportId,
                decision.ReportVersionNo,
                decision.EvaluationVersion,
                decision.AllowsReviewerAssignment,
                decision.HasRejectionReason,
                decision.DecidedBy,
                decision.DecidedAtUtc
            }), decision.DecidedAtUtc);
    }

    public async Task<bool> IsGroupReadyForReviewerAssignmentAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        if (groupId == Guid.Empty)
        {
            return false;
        }

        return await context.StudentGroups.AsNoTracking().AnyAsync(value =>
            value.Id == groupId && value.Status == StudentGroupStatus.ReadyForReviewerAssignment,
            cancellationToken);
    }

    private void TrackReportAndGroupConcurrency(Domain.Entities.FinalReport report, Domain.Entities.StudentGroup group)
    {
        // The operation only reads these values, but their xmin tokens must participate in the
        // eventual save so a concurrent stop, resubmission, or report version change aborts it.
        context.Entry(report).Property(value => value.Status).IsModified = true;
        context.Entry(group).Property(value => value.Status).IsModified = true;
    }
}
