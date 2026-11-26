using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UGTM.Modules.Identity.Contracts.Auditing;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Application.Auditing;
using UGTM.Modules.ThesisManagement.Application.Notifications;
using UGTM.Modules.ThesisManagement.Application.ReviewGateDeadlines;
using UGTM.Modules.ThesisManagement.Domain.Entities;
using UGTM.Modules.ThesisManagement.Domain.Enums;
using UGTM.Modules.ThesisManagement.Infrastructure.Auditing;
using UGTM.Modules.ThesisManagement.Infrastructure.Persistence;
using UGTM.Modules.TopicCatalog.Contracts.Topics;

namespace UGTM.Modules.ThesisManagement.Infrastructure.ReviewGateDeadlines;

public sealed class ReviewGateDeadlineEnforcer(
    ThesisManagementDbContext db,
    IThesisManagementAuditOutbox auditOutbox,
    IOptions<ReviewGateDeadlineEnforcementOptions> options,
    TimeProvider timeProvider,
    IThesisNotificationOutbox? notificationOutbox = null,
    ITopicSummaryReader? topicSummaryReader = null) : IReviewGateDeadlineEnforcer
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
        var stoppedGroups = new List<StudentGroup>();
        foreach (var group in groups)
        {
            var previousGroupStatus = group.Status.ToString();
            if (!group.TryStop())
            {
                continue;
            }

            stoppedGroups.Add(group);

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

        if (stoppedGroups.Count > 0)
        {
            if (notificationOutbox is not null)
            {
                await EnqueueDeadlineStoppedNotificationsAsync(
                    notificationOutbox,
                    stoppedGroups,
                    configuration,
                    nowUtc,
                    cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return stoppedGroups.Count;
    }

    private async Task EnqueueDeadlineStoppedNotificationsAsync(
        IThesisNotificationOutbox notificationOutbox,
        IReadOnlyCollection<StudentGroup> stoppedGroups,
        ReviewGateConfiguration configuration,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var groupIds = stoppedGroups.Select(g => g.Id).ToArray();
        var membersByGroup = (await db.StudentGroupMembers
                .AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && m.ConfirmationStatus == MemberConfirmationStatus.Confirmed)
                .Select(m => new { m.GroupId, m.StudentId })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.GroupId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.StudentId).Distinct().ToArray());

        IReadOnlyDictionary<Guid, TopicSummaryContractDto>? topicsById = null;
        if (topicSummaryReader is not null)
        {
            topicsById = await topicSummaryReader.GetByIdsAsync(
                stoppedGroups.Select(g => g.TopicId).Distinct(),
                cancellationToken);
        }

        var isSyllabusGate = configuration.GateType == ReviewGateType.Syllabus;
        var gateLabel = isSyllabusGate ? "Cổng thẩm định 1 (Đề cương)" : "Cổng thẩm định 2 (Giữa kỳ)";
        var studentActionPath = isSyllabusGate ? "/thesis/proposal" : "/thesis/midterm";

        foreach (var group in stoppedGroups)
        {
            TopicSummaryContractDto? topicSummary = null;
            topicsById?.TryGetValue(group.TopicId, out topicSummary);
            var shortTopic = ThesisNotificationFormatting.Shorten(topicSummary?.TitleVi, 65);

            var metadata = new List<NotificationEmailMetaItem>
            {
                new("Giai đoạn dừng", gateLabel)
            };
            if (!string.IsNullOrWhiteSpace(topicSummary?.TitleVi))
            {
                metadata.Add(new NotificationEmailMetaItem("Đề tài", topicSummary.TitleVi.Trim()));
            }
            metadata.Add(new NotificationEmailMetaItem(
                "Hạn chót đóng cổng (VN)",
                ThesisNotificationFormatting.FormatVietnamTime(configuration.FinalClosureDeadlineUtc)));
            metadata.Add(new NotificationEmailMetaItem("Lý do", "Quá hạn chót hoàn tất cổng thẩm định."));

            if (membersByGroup.TryGetValue(group.Id, out var studentIds) && studentIds.Count > 0)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "GROUP_STOPPED",
                        Title: "Thông báo dừng thực hiện đề tài KLTN",
                        Content: $"Nhóm quá hạn chót hoàn tất {gateLabel} và bị dừng thực hiện đề tài theo quy định.",
                        RecipientUserIds: studentIds,
                        DeduplicationKeyPrefix: $"GROUP_STOPPED:GATE:{configuration.GateType}:{group.Id:N}",
                        ResourceType: "StudentGroup",
                        ResourceId: group.Id,
                        AcademicBatchId: configuration.BatchId,
                        ActionPath: studentActionPath,
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Thông báo dừng thực hiện đề tài KLTN ({gateLabel})",
                        EmailCategoryLabel: gateLabel,
                        EmailCtaLabel: "Xem trạng thái hồ sơ",
                        EmailNotice: "Vui lòng liên hệ Văn phòng Khoa hoặc GVHD nếu cần giải đáp quy chế học vụ.",
                        EmailMetadata: metadata),
                    nowUtc);
            }

            if (topicSummary is { AdvisorId: var advisorId } && advisorId != Guid.Empty)
            {
                notificationOutbox.Enqueue(
                    new NotificationOutboxEntry(
                        NotificationType: "GROUP_STOPPED",
                        Title: $"Nhóm hướng dẫn bị dừng tại {gateLabel}",
                        Content: $"Nhóm đề tài \"{shortTopic}\" quá hạn {gateLabel} và đã dừng thực hiện đề tài.",
                        RecipientUserIds: [advisorId],
                        DeduplicationKeyPrefix: $"GROUP_STOPPED:GATE:{configuration.GateType}:{group.Id:N}:ADV",
                        ResourceType: "StudentGroup",
                        ResourceId: group.Id,
                        AcademicBatchId: configuration.BatchId,
                        ActionPath: "/topics/advising",
                        Priority: "Critical",
                        EnqueuePush: true,
                        EnqueueEmail: true,
                        EmailSubject: $"[UGTM] Dừng đề tài hướng dẫn do quá hạn: {shortTopic}",
                        EmailCategoryLabel: gateLabel,
                        EmailCtaLabel: "Xem nhóm hướng dẫn",
                        EmailMetadata: metadata),
                    nowUtc);
            }
        }
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
