using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.ReviewGateDeadlines;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;

namespace UGTM.Modules.ThesisManagement.Infrastructure.ReviewGateDeadlines;

public sealed class ReviewGateDeadlineEnforcer(
    ThesisManagementDbContext db,
    IThesisManagementAuditOutbox auditOutbox,
    IOptions<ReviewGateDeadlineEnforcementOptions> options,
    TimeProvider timeProvider) : IReviewGateDeadlineEnforcer
{
    private readonly int _batchSize = options.Value.BatchSize;

    public async Task<ReviewGateDeadlineEnforcementResult> EnforceAsync(
        CancellationToken cancellationToken = default)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var expiredConfigurations = await db.ReviewGateConfigurations
            .AsNoTracking()
            .Where(configuration => configuration.FinalClosureDeadlineUtc < nowUtc)
            .OrderBy(configuration => configuration.FinalClosureDeadlineUtc)
            .ThenBy(configuration => configuration.Id)
            .ToListAsync(cancellationToken);

        var closedGroupCount = 0;
        foreach (var configuration in expiredConfigurations)
        {
            closedGroupCount += await CloseGroupsForConfigurationAsync(
                configuration,
                nowUtc,
                cancellationToken);
        }

        return new ReviewGateDeadlineEnforcementResult(closedGroupCount);
    }

    private async Task<int> CloseGroupsForConfigurationAsync(
        ReviewGateConfiguration configuration,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var groups = await GetExpiredPendingGroupsAsync(configuration, cancellationToken);
        var closedGroupCount = 0;
        foreach (var group in groups)
        {
            var previousGroupStatus = group.Status.ToString();
            if (!group.TryStop())
            {
                continue;
            }

            closedGroupCount++;

            auditOutbox.Add(
                new AuditIntegrationEntry(
                    "StudentGroup",
                    group.Id.ToString(),
                    IntegrationAuditActions.ReviewGateDeadlineClosed,
                    "Succeeded",
                    "FinalClosureDeadlineElapsed",
                    new
                    {
                        configuration.BatchId,
                        GateType = configuration.GateType.ToString(),
                        configuration.FinalClosureDeadlineUtc,
                        PreviousGroupStatus = previousGroupStatus
                    }),
                nowUtc);
        }

        if (closedGroupCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return closedGroupCount;
    }

    private Task<List<StudentGroup>> GetExpiredPendingGroupsAsync(
        ReviewGateConfiguration configuration,
        CancellationToken cancellationToken)
        => configuration.GateType switch
        {
            ReviewGateType.Syllabus => db.StudentGroups
                .FromSqlInterpolated(
                    $"""
                    SELECT sg.*, sg.xmin
                    FROM thesis.student_group AS sg
                    WHERE EXISTS (
                        SELECT 1
                        FROM thesis.student_group_member AS sgm
                        WHERE sgm.group_id = sg.id AND sgm.batch_id = {configuration.BatchId})
                      AND sg.status IN (
                          {StudentGroupStatus.Registered.ToString()},
                          {StudentGroupStatus.SyllabusSubmitted.ToString()},
                          {StudentGroupStatus.SyllabusRevisionRequired.ToString()})
                      AND NOT EXISTS (
                        SELECT 1
                        FROM thesis.syllabus AS s
                        WHERE s.group_id = sg.id AND s.status = {SyllabusStatus.Approved.ToString()})
                    ORDER BY sg.id
                    LIMIT {_batchSize}
                    FOR UPDATE OF sg SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken),
            ReviewGateType.Midterm => db.StudentGroups
                .FromSqlInterpolated(
                    $"""
                    SELECT sg.*, sg.xmin
                    FROM thesis.student_group AS sg
                    WHERE EXISTS (
                        SELECT 1
                        FROM thesis.student_group_member AS sgm
                        WHERE sgm.group_id = sg.id AND sgm.batch_id = {configuration.BatchId})
                      AND sg.status IN ({StudentGroupStatus.InProgress.ToString()}, {StudentGroupStatus.MidtermSubmitted.ToString()})
                      AND NOT EXISTS (
                        SELECT 1
                        FROM thesis.midterm_report AS mr
                        WHERE mr.group_id = sg.id AND mr.status = {MidtermReportStatus.Approved.ToString()})
                    ORDER BY sg.id
                    LIMIT {_batchSize}
                    FOR UPDATE OF sg SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken),
            _ => Task.FromResult(new List<StudentGroup>())
        };

}
