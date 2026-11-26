using System.Globalization;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.Reminders;

namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

internal static class StaffDeadlineDigestBuilder
{
    public static async Task<List<NotificationIngestionRequest>> BuildAsync(
        BatchReminderScanContext context,
        INotificationRecipientDirectory recipientDirectory,
        int staffMaxHours,
        CancellationToken cancellationToken)
    {
        var buckets = new Dictionary<Guid, List<StaffDigestSectionAccumulator>>();

        await CollectTopicPreparationSectionsAsync(context, buckets, recipientDirectory, staffMaxHours, cancellationToken);
        await CollectReviewGateResponseSectionsAsync(
            context, buckets, recipientDirectory, staffMaxHours,
            context.SyllabusGate,
            g => string.Equals(g.SyllabusStatus, "Submitted", StringComparison.OrdinalIgnoreCase),
            "Đề cương",
            "Hồ sơ Đề cương chờ Khoa thẩm định",
            "/thesis/review/syllabus",
            cancellationToken);
        await CollectReviewGateResponseSectionsAsync(
            context, buckets, recipientDirectory, staffMaxHours,
            context.MidtermGate,
            g => string.Equals(g.MidtermStatus, "Submitted", StringComparison.OrdinalIgnoreCase),
            "Giữa kỳ",
            "Báo cáo giữa kỳ chờ Khoa thẩm định",
            "/thesis/review/midterm",
            cancellationToken);
        CollectEvaluationSections(context, buckets, staffMaxHours);
        CollectDefenseAndMinuteSections(context, buckets, staffMaxHours);
        await CollectArchivalAndScoreSectionsAsync(context, buckets, recipientDirectory, staffMaxHours, cancellationToken);

        return await MaterializeDigestRequestsAsync(context, buckets, recipientDirectory, cancellationToken);
    }

    private static async Task CollectTopicPreparationSectionsAsync(
        BatchReminderScanContext context,
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        INotificationRecipientDirectory recipientDirectory,
        int staffMaxHours,
        CancellationToken cancellationToken)
    {
        var stage1 = context.Stage1;
        if (stage1 is null || !DeadlineReminderFormatting.IsWithinWindow(stage1.DeadlineAtUtc, context.NowUtc, staffMaxHours))
        {
            return;
        }

        foreach (var byAdvisor in context.Topics.DraftTopics.GroupBy(t => t.AdvisorId))
        {
            AddStaffDigestSection(
                buckets, byAdvisor.Key,
                "Đề tài nháp chưa gửi duyệt (Giai đoạn 1)",
                "/topics/advising",
                byAdvisor.Select(t => $"\"{DeadlineReminderFormatting.Shorten(t.TitleVi, 55)}\" (hạn {DeadlineReminderFormatting.FormatVn(stage1.DeadlineAtUtc)})").ToList());
        }

        if (context.Topics.SubmittedTopics.Count == 0)
        {
            return;
        }

        var facultyUsers = await recipientDirectory.ResolveActiveRecipientsAsync(
            roleNames: ["Faculty"], cancellationToken: cancellationToken);
        var items = context.Topics.SubmittedTopics
            .Take(10)
            .Select(t => $"Đề tài chờ duyệt: \"{DeadlineReminderFormatting.Shorten(t.TitleVi, 55)}\"")
            .ToList();
        if (context.Topics.SubmittedTopics.Count > 10)
        {
            items.Add($"… và {context.Topics.SubmittedTopics.Count - 10} đề tài khác");
        }

        foreach (var staff in facultyUsers)
        {
            AddStaffDigestSection(
                buckets, staff.UserId,
                $"Đề tài chờ Khoa thẩm định ({context.Topics.SubmittedTopics.Count} đề tài)",
                "/topics/review",
                items);
        }
    }

    private static async Task CollectReviewGateResponseSectionsAsync(
        BatchReminderScanContext context,
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        INotificationRecipientDirectory recipientDirectory,
        int staffMaxHours,
        ReviewGateReminderDto? gate,
        Func<ThesisGroupReminderDto, bool> pendingPredicate,
        string itemPrefix,
        string sectionTitlePrefix,
        string actionPath,
        CancellationToken cancellationToken)
    {
        if (gate is null || !DeadlineReminderFormatting.IsWithinWindow(gate.FacultyResponseDeadlineUtc, context.NowUtc, staffMaxHours))
        {
            return;
        }

        var pending = context.Thesis.Groups.Where(pendingPredicate).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var reviewers = await recipientDirectory.ResolveActiveRecipientsAsync(
            permissionCodes: [PermissionCodes.ThesisReviewGatesReview],
            cancellationToken: cancellationToken);
        var items = pending
            .Take(10)
            .Select(g => $"{itemPrefix} đề tài \"{DeadlineReminderFormatting.Shorten(DeadlineReminderFormatting.GetTopicTitle(context.Topics, g.TopicId), 55)}\" (hạn {DeadlineReminderFormatting.FormatVn(gate.FacultyResponseDeadlineUtc)})")
            .ToList();

        foreach (var staff in reviewers)
        {
            AddStaffDigestSection(
                buckets, staff.UserId,
                $"{sectionTitlePrefix} ({pending.Count} nhóm)",
                actionPath,
                items);
        }
    }

    private static void CollectEvaluationSections(
        BatchReminderScanContext context,
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        int staffMaxHours)
    {
        var stage5 = context.Stage5;
        if (stage5 is null || !DeadlineReminderFormatting.IsWithinWindow(stage5.DeadlineAtUtc, context.NowUtc, staffMaxHours))
        {
            return;
        }

        var unevaluatedByAdvisor = context.Thesis.Groups
            .Where(g => g.HasInitialFinalReport && !context.Council.AdvisorEvaluatedGroupIds.Contains(g.GroupId))
            .Select(g => new { Group = g, Topic = context.Topics.TopicsById.GetValueOrDefault(g.TopicId) })
            .Where(x => x.Topic is not null && x.Topic.AdvisorId != Guid.Empty)
            .GroupBy(x => x.Topic!.AdvisorId);

        foreach (var advisorGroup in unevaluatedByAdvisor)
        {
            AddStaffDigestSection(
                buckets, advisorGroup.Key,
                $"Nhóm đã nộp Báo cáo cuối kỳ chờ GVHD duyệt ({advisorGroup.Count()} nhóm)",
                "/thesis/lecturer-grading",
                advisorGroup.Select(x => $"\"{DeadlineReminderFormatting.Shorten(x.Topic!.TitleVi, 55)}\" (hạn {DeadlineReminderFormatting.FormatVn(stage5.DeadlineAtUtc)})").ToList());
        }

        var groupTopicLookup = context.Thesis.Groups.ToDictionary(g => g.GroupId, g => g.TopicId);
        foreach (var reviewerGroup in context.Council.PendingReviewerAssignments.GroupBy(a => a.ReviewerId))
        {
            var items = reviewerGroup
                .Select(a =>
                {
                    var title = groupTopicLookup.TryGetValue(a.GroupId, out var topicId)
                        ? DeadlineReminderFormatting.GetTopicTitle(context.Topics, topicId)
                        : "Đề tài KLTN";
                    return $"Phản biện đề tài \"{DeadlineReminderFormatting.Shorten(title, 55)}\" (hạn {DeadlineReminderFormatting.FormatVn(stage5.DeadlineAtUtc)})";
                })
                .ToList();

            AddStaffDigestSection(
                buckets, reviewerGroup.Key,
                $"Bài phản biện GVPB chưa hoàn tất ({reviewerGroup.Count()} nhóm)",
                "/thesis/lecturer-grading",
                items);
        }
    }

    private static void CollectDefenseAndMinuteSections(
        BatchReminderScanContext context,
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        int staffMaxHours)
    {
        foreach (var council in context.Council.ActiveCouncils)
        {
            var defenseStartUtc = DeadlineReminderFormatting.EstimateCouncilStartUtc(council.DefenseDate, council.TimeSlot);
            if (!DeadlineReminderFormatting.IsWithinWindow(defenseStartUtc, context.NowUtc, staffMaxHours))
            {
                continue;
            }

            foreach (var memberId in council.MemberUserIds)
            {
                AddStaffDigestSection(
                    buckets, memberId,
                    $"Lịch bảo vệ Hội đồng {council.CouncilCode} sắp tới",
                    "/councils/grading",
                    [$"Ngày {council.DefenseDate:dd/MM/yyyy}, ca {council.TimeSlot}, Phòng {council.Room} ({council.AssignedGroupIds.Count} nhóm)"]);
            }
        }

        var stage6 = context.Stage6;
        if (stage6 is null || !DeadlineReminderFormatting.IsWithinWindow(stage6.DeadlineAtUtc, context.NowUtc, staffMaxHours))
        {
            return;
        }

        var todayVn = DateOnly.FromDateTime(context.NowUtc.AddHours(7));
        foreach (var council in context.Council.ActiveCouncils.Where(c =>
                     c.DefenseDate <= todayVn && c.UnclosedMinuteGroupIds.Count > 0))
        {
            foreach (var leaderId in council.ChairmanAndSecretaryIds)
            {
                AddStaffDigestSection(
                    buckets, leaderId,
                    $"Biên bản Hội đồng {council.CouncilCode} chưa khóa ({council.UnclosedMinuteGroupIds.Count} nhóm)",
                    "/councils/minutes",
                    [$"Hạn khóa biên bản Giai đoạn 6: {DeadlineReminderFormatting.FormatVn(stage6.DeadlineAtUtc)}"]);
            }
        }
    }

    private static async Task CollectArchivalAndScoreSectionsAsync(
        BatchReminderScanContext context,
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        INotificationRecipientDirectory recipientDirectory,
        int staffMaxHours,
        CancellationToken cancellationToken)
    {
        var stage7 = context.Stage7;
        if (stage7 is null || !DeadlineReminderFormatting.IsWithinWindow(stage7.DeadlineAtUtc, context.NowUtc, staffMaxHours))
        {
            return;
        }

        var pendingAdvisorArchival = context.Thesis.Groups
            .Where(g => string.Equals(g.ArchivalStatus, "Submitted", StringComparison.OrdinalIgnoreCase))
            .Select(g => new { Group = g, Topic = context.Topics.TopicsById.GetValueOrDefault(g.TopicId) })
            .Where(x => x.Topic is not null && x.Topic.AdvisorId != Guid.Empty)
            .GroupBy(x => x.Topic!.AdvisorId);

        foreach (var advisorGroup in pendingAdvisorArchival)
        {
            AddStaffDigestSection(
                buckets, advisorGroup.Key,
                $"Hồ sơ lưu chiểu chờ GVHD xác nhận ({advisorGroup.Count()} nhóm)",
                "/advisor/archival",
                advisorGroup.Select(x => $"\"{DeadlineReminderFormatting.Shorten(x.Topic!.TitleVi, 55)}\" (hạn {DeadlineReminderFormatting.FormatVn(stage7.DeadlineAtUtc)})").ToList());
        }

        var unpublishedScoreGroups = context.Thesis.Groups
            .Where(g => !context.Council.PublishedScoreGroupIds.Contains(g.GroupId))
            .ToList();
        if (unpublishedScoreGroups.Count > 0)
        {
            var aaUsers = await recipientDirectory.ResolveActiveRecipientsAsync(
                roleNames: ["AcademicAffairs"], cancellationToken: cancellationToken);
            foreach (var staff in aaUsers)
            {
                AddStaffDigestSection(
                    buckets, staff.UserId,
                    $"Bảng điểm tổng kết chưa công bố ({unpublishedScoreGroups.Count} nhóm)",
                    "/councils/results",
                    [$"Còn {unpublishedScoreGroups.Count} nhóm chưa công bố điểm tổng kết trước hạn {DeadlineReminderFormatting.FormatVn(stage7.DeadlineAtUtc)}"]);
            }
        }

        var pendingArchivalClosure = context.Thesis.Groups
            .Where(g => string.Equals(g.ArchivalStatus, "AdvisorConfirmed", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (pendingArchivalClosure.Count > 0)
        {
            var archivalManagers = await recipientDirectory.ResolveActiveRecipientsAsync(
                permissionCodes: [PermissionCodes.ThesisFinalSubmissionsManage],
                cancellationToken: cancellationToken);
            foreach (var staff in archivalManagers)
            {
                AddStaffDigestSection(
                    buckets, staff.UserId,
                    $"Hồ sơ lưu chiểu chờ chốt hoàn tất ({pendingArchivalClosure.Count} nhóm)",
                    "/thesis/review/archival",
                    [$"Còn {pendingArchivalClosure.Count} hồ sơ đã qua GVHD đang chờ chốt trước {DeadlineReminderFormatting.FormatVn(stage7.DeadlineAtUtc)}"]);
            }
        }
    }

    private static async Task<List<NotificationIngestionRequest>> MaterializeDigestRequestsAsync(
        BatchReminderScanContext context,
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        INotificationRecipientDirectory recipientDirectory,
        CancellationToken cancellationToken)
    {
        if (buckets.Count == 0)
        {
            return [];
        }

        var staffDirectory = (await recipientDirectory.ResolveActiveRecipientsAsync(
                userIds: buckets.Keys.ToArray(),
                cancellationToken: cancellationToken))
            .ToDictionary(r => r.UserId);

        var requests = new List<NotificationIngestionRequest>(buckets.Count);
        var dateStamp = context.NowUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        foreach (var (userId, sections) in buckets)
        {
            if (!staffDirectory.TryGetValue(userId, out var recipient) || sections.Count == 0)
            {
                continue;
            }

            var digestSections = sections
                .Select(s => new NotificationEmailDigestSection(
                    Title: s.Title,
                    Summary: $"{s.Items.Count} đầu việc sắp đến hạn trong 24 giờ tới.",
                    ActionPath: s.ActionPath,
                    Items: s.Items))
                .ToArray();

            requests.Add(new NotificationIngestionRequest(
                RecipientId: recipient.UserId,
                Title: $"Tổng hợp {sections.Count} đầu việc KLTN sắp đến hạn (24h)",
                Content: $"Bạn có {sections.Count} nhóm đầu việc thuộc đợt {context.Batch.BatchName} sắp đến hạn trong 24h tới.",
                NotificationType: "STAFF_DEADLINE_DIGEST_24H",
                SourceModule: "Notifications",
                DeduplicationKey: $"REMINDER:STAFF_DIGEST:{context.Batch.BatchId:N}:{dateStamp}",
                ResourceType: "KltnBatch",
                ResourceId: context.Batch.BatchId,
                AcademicBatchId: context.Batch.BatchId,
                ActionPath: sections[0].ActionPath,
                Priority: "Critical",
                EnqueuePush: true,
                EnqueueEmail: true,
                RecipientEmail: recipient.Email,
                EmailSubject: $"[UGTM] Nhắc việc sắp đến hạn 24h — Đợt {context.Batch.BatchName}",
                RecipientFullName: recipient.FullName,
                EmailCategoryLabel: "Tổng hợp nhắc hạn 24h",
                EmailCtaLabel: "Mở hệ thống UGTM",
                EmailNotice: "Email tổng hợp các đầu việc còn tồn đọng sắp đến hạn trong 24 giờ tới.",
                EmailDigestSections: digestSections));
        }

        return requests;
    }

    private static void AddStaffDigestSection(
        Dictionary<Guid, List<StaffDigestSectionAccumulator>> buckets,
        Guid userId,
        string title,
        string actionPath,
        List<string> items)
    {
        if (userId == Guid.Empty || items.Count == 0)
        {
            return;
        }

        if (!buckets.TryGetValue(userId, out var list))
        {
            list = [];
            buckets[userId] = list;
        }

        list.Add(new StaffDigestSectionAccumulator(title, actionPath, items));
    }
}
