using UGTM.Modules.Identity.Contracts.Users;
using UGTM.Modules.Notifications.Contracts.Notifications;
using UGTM.Modules.ThesisManagement.Contracts.Reminders;

namespace UGTM.Modules.Notifications.Infrastructure.Reminders;

internal static class StudentDeadlineReminderBuilder
{
    public static List<NotificationIngestionRequest> Build(
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> studentDirectory,
        int[] studentThresholds)
    {
        if (studentThresholds.Length == 0 || studentDirectory.Count == 0)
        {
            return [];
        }

        var requests = new List<NotificationIngestionRequest>();
        var groupsById = context.Thesis.Groups.ToDictionary(g => g.GroupId);

        CollectSyllabusReminders(requests, context, studentDirectory, studentThresholds);
        CollectMilestoneReminders(requests, context, groupsById, studentDirectory, studentThresholds);
        CollectMidtermReminders(requests, context, studentDirectory, studentThresholds);
        CollectFinalReportReminders(requests, context, studentDirectory, studentThresholds);
        CollectDefenseReminders(requests, context, groupsById, studentDirectory, studentThresholds);
        CollectArchivalReminders(requests, context, studentDirectory, studentThresholds);

        return requests;
    }

    private static void CollectSyllabusReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        int[] thresholds)
    {
        var gate = context.SyllabusGate;
        if (gate is null)
        {
            return;
        }

        var initialThreshold = DeadlineReminderFormatting.MatchSmallestThreshold(
            gate.InitialSubmissionDeadlineUtc, context.NowUtc, thresholds);
        if (initialThreshold.HasValue)
        {
            var targets = context.Thesis.Groups.Where(g =>
                string.Equals(g.Status, "Registered", StringComparison.OrdinalIgnoreCase)
                && g.SyllabusStatus is null);

            AddGroupBatchReminders(
                requests, context, directory, targets,
                "SYLLABUS_INITIAL_DEADLINE_SOON",
                $"Sắp hết hạn nộp Đề cương KLTN (còn {initialThreshold.Value}h)",
                $"Nhóm chưa nộp Đề cương lần đầu. Vui lòng hoàn tất trước {DeadlineReminderFormatting.FormatVn(gate.InitialSubmissionDeadlineUtc)}.",
                "/thesis/proposal",
                id => $"REMINDER:SYLLABUS_INITIAL:{id:N}:{initialThreshold.Value}h",
                "Mốc nộp Đề cương lần đầu",
                gate.InitialSubmissionDeadlineUtc);
        }

        var finalThreshold = DeadlineReminderFormatting.MatchSmallestThreshold(
            gate.FinalClosureDeadlineUtc, context.NowUtc, thresholds);
        if (finalThreshold.HasValue)
        {
            var targets = context.Thesis.Groups.Where(g =>
                string.Equals(g.Status, "SyllabusRevisionRequired", StringComparison.OrdinalIgnoreCase)
                && string.Equals(g.SyllabusStatus, "NeedRevision", StringComparison.OrdinalIgnoreCase));

            AddGroupBatchReminders(
                requests, context, directory, targets,
                "SYLLABUS_FINAL_DEADLINE_SOON",
                $"Sắp hết hạn nộp lại Đề cương KLTN (còn {finalThreshold.Value}h)",
                $"Nhóm chưa nộp lại Đề cương chỉnh sửa. Hạn đóng Cổng 1 là {DeadlineReminderFormatting.FormatVn(gate.FinalClosureDeadlineUtc)}.",
                "/thesis/proposal",
                id => $"REMINDER:SYLLABUS_FINAL:{id:N}:{finalThreshold.Value}h",
                "Đóng Cổng 1 — Nộp lại Đề cương",
                gate.FinalClosureDeadlineUtc);
        }
    }

    private static void CollectMilestoneReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, ThesisGroupReminderDto> groupsById,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        int[] thresholds)
    {
        foreach (var milestone in context.Thesis.PendingMilestones)
        {
            if (!groupsById.TryGetValue(milestone.GroupId, out var grp)
                || !string.Equals(grp.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var threshold = DeadlineReminderFormatting.MatchSmallestThreshold(
                milestone.DeadlineUtc, context.NowUtc, thresholds);
            if (!threshold.HasValue)
            {
                continue;
            }

            AddStudentReminderRequests(
                requests, directory, grp.ConfirmedStudentIds,
                context.Batch.BatchId, milestone.MilestoneId, "ProgressMilestone",
                "MILESTONE_DEADLINE_SOON",
                $"Sắp hết hạn mốc tiến độ (còn {threshold.Value}h)",
                $"Mốc \"{DeadlineReminderFormatting.Shorten(milestone.Title, 45)}\" chưa hoàn tất. Vui lòng nộp trước {DeadlineReminderFormatting.FormatVn(milestone.DeadlineUtc)}.",
                "/thesis/milestones",
                $"REMINDER:MILESTONE:{milestone.MilestoneId:N}:{threshold.Value}h",
                $"Mốc tiến độ: {milestone.Title}",
                DeadlineReminderFormatting.GetTopicTitle(context.Topics, grp.TopicId),
                milestone.DeadlineUtc);
        }
    }

    private static void CollectMidtermReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        int[] thresholds)
    {
        var gate = context.MidtermGate;
        if (gate is null)
        {
            return;
        }

        var initialThreshold = DeadlineReminderFormatting.MatchSmallestThreshold(
            gate.InitialSubmissionDeadlineUtc, context.NowUtc, thresholds);
        if (initialThreshold.HasValue)
        {
            var targets = context.Thesis.Groups.Where(g =>
                string.Equals(g.Status, "InProgress", StringComparison.OrdinalIgnoreCase)
                && g.MidtermStatus is null);

            AddGroupBatchReminders(
                requests, context, directory, targets,
                "MIDTERM_INITIAL_DEADLINE_SOON",
                $"Sắp hết hạn nộp Báo cáo giữa kỳ (còn {initialThreshold.Value}h)",
                $"Nhóm chưa nộp Báo cáo giữa kỳ lần đầu. Hạn chót là {DeadlineReminderFormatting.FormatVn(gate.InitialSubmissionDeadlineUtc)}.",
                "/thesis/midterm",
                id => $"REMINDER:MIDTERM_INITIAL:{id:N}:{initialThreshold.Value}h",
                "Mốc nộp Báo cáo giữa kỳ",
                gate.InitialSubmissionDeadlineUtc);
        }

        var finalThreshold = DeadlineReminderFormatting.MatchSmallestThreshold(
            gate.FinalClosureDeadlineUtc, context.NowUtc, thresholds);
        if (finalThreshold.HasValue)
        {
            var targets = context.Thesis.Groups.Where(g =>
                string.Equals(g.MidtermStatus, "NeedRevision", StringComparison.OrdinalIgnoreCase));

            AddGroupBatchReminders(
                requests, context, directory, targets,
                "MIDTERM_FINAL_DEADLINE_SOON",
                $"Sắp hết hạn nộp lại Báo cáo giữa kỳ (còn {finalThreshold.Value}h)",
                $"Nhóm chưa nộp bản sửa Báo cáo giữa kỳ. Hạn đóng Cổng 2 là {DeadlineReminderFormatting.FormatVn(gate.FinalClosureDeadlineUtc)}.",
                "/thesis/midterm",
                id => $"REMINDER:MIDTERM_FINAL:{id:N}:{finalThreshold.Value}h",
                "Đóng Cổng 2 — Nộp lại Giữa kỳ",
                gate.FinalClosureDeadlineUtc);
        }
    }

    private static void CollectFinalReportReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        int[] thresholds)
    {
        var stage5 = context.Stage5;
        if (stage5 is null)
        {
            return;
        }

        var threshold = DeadlineReminderFormatting.MatchSmallestThreshold(stage5.DeadlineAtUtc, context.NowUtc, thresholds);
        if (!threshold.HasValue)
        {
            return;
        }

        var initialTargets = context.Thesis.Groups.Where(g =>
            string.Equals(g.MidtermStatus, "Approved", StringComparison.OrdinalIgnoreCase)
            && string.Equals(g.Status, "InProgress", StringComparison.OrdinalIgnoreCase)
            && !g.HasInitialFinalReport);

        AddGroupBatchReminders(
            requests, context, directory, initialTargets,
            "FINAL_REPORT_DEADLINE_SOON",
            $"Sắp hết hạn nộp Báo cáo cuối kỳ (còn {threshold.Value}h)",
            $"Nhóm chưa nộp Báo cáo cuối kỳ. Vui lòng nộp trước {DeadlineReminderFormatting.FormatVn(stage5.DeadlineAtUtc)}.",
            "/thesis/final-report",
            id => $"REMINDER:FINAL_REPORT:{id:N}:{threshold.Value}h",
            "Nộp Báo cáo cuối kỳ (Giai đoạn 5)",
            stage5.DeadlineAtUtc);

        var revisedTargets = context.Thesis.Groups.Where(g =>
            context.Council.ReviewerVisibleGroupIds.Contains(g.GroupId)
            && !g.HasRevisedFinalReport);

        AddGroupBatchReminders(
            requests, context, directory, revisedTargets,
            "REVISED_FINAL_REPORT_DEADLINE_SOON",
            $"Nhắc nộp Bản sửa sau phản biện (còn {threshold.Value}h)",
            $"Nhóm chưa nộp bản chỉnh sửa sau phản biện. Hạn chót là {DeadlineReminderFormatting.FormatVn(stage5.DeadlineAtUtc)}.",
            "/thesis/final-report",
            id => $"REMINDER:REVISED_FINAL_REPORT:{id:N}:{threshold.Value}h",
            "Nộp Bản chỉnh sửa sau phản biện",
            stage5.DeadlineAtUtc);
    }

    private static void CollectDefenseReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, ThesisGroupReminderDto> groupsLookup,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        int[] thresholds)
    {
        foreach (var council in context.Council.ActiveCouncils)
        {
            var defenseStartUtc = DeadlineReminderFormatting.EstimateCouncilStartUtc(council.DefenseDate, council.TimeSlot);
            var threshold = DeadlineReminderFormatting.MatchSmallestThreshold(defenseStartUtc, context.NowUtc, thresholds);
            if (!threshold.HasValue)
            {
                continue;
            }

            foreach (var groupId in council.AssignedGroupIds)
            {
                if (!groupsLookup.TryGetValue(groupId, out var grp))
                {
                    continue;
                }

                AddStudentReminderRequests(
                    requests, directory, grp.ConfirmedStudentIds,
                    context.Batch.BatchId, council.CouncilId, "Council",
                    "COUNCIL_DEFENSE_UPCOMING_STUDENT",
                    $"Nhắc lịch bảo vệ KLTN sắp tới (còn {threshold.Value}h)",
                    $"Nhóm sẽ bảo vệ tại Hội đồng {council.CouncilCode} ({council.DefenseDate:dd/MM/yyyy}, {council.TimeSlot}, Phòng {council.Room}).",
                    "/dashboard",
                    $"REMINDER:COUNCIL_DEFENSE_STUDENT:{council.CouncilId:N}:{grp.GroupId:N}:{threshold.Value}h",
                    $"Bảo vệ Hội đồng {council.CouncilCode}",
                    DeadlineReminderFormatting.GetTopicTitle(context.Topics, grp.TopicId),
                    defenseStartUtc);
            }
        }
    }

    private static void CollectArchivalReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        int[] thresholds)
    {
        var stage7 = context.Stage7;
        if (stage7 is null)
        {
            return;
        }

        var threshold = DeadlineReminderFormatting.MatchSmallestThreshold(stage7.DeadlineAtUtc, context.NowUtc, thresholds);
        if (!threshold.HasValue)
        {
            return;
        }

        var closedMinuteGroupIds = context.Council.ActiveCouncils
            .SelectMany(c => c.AssignedGroupIds.Except(c.UnclosedMinuteGroupIds))
            .ToHashSet();

        var targets = context.Thesis.Groups.Where(g =>
            closedMinuteGroupIds.Contains(g.GroupId)
            && (g.ArchivalStatus is null
                || string.Equals(g.ArchivalStatus, "NotSubmitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(g.ArchivalStatus, "NeedRevision", StringComparison.OrdinalIgnoreCase)));

        AddGroupBatchReminders(
            requests, context, directory, targets,
            "ARCHIVAL_SUBMISSION_DEADLINE_SOON",
            $"Sắp hết hạn nộp Hồ sơ lưu chiểu (còn {threshold.Value}h)",
            $"Nhóm chưa hoàn tất Hồ sơ lưu chiểu. Vui lòng nộp trước {DeadlineReminderFormatting.FormatVn(stage7.DeadlineAtUtc)}.",
            "/thesis/archival",
            id => $"REMINDER:ARCHIVAL_SUBMISSION:{id:N}:{threshold.Value}h",
            "Nộp Hồ sơ lưu chiểu (Giai đoạn 7)",
            stage7.DeadlineAtUtc);
    }

    private static void AddGroupBatchReminders(
        List<NotificationIngestionRequest> requests,
        BatchReminderScanContext context,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        IEnumerable<ThesisGroupReminderDto> groups,
        string notificationType,
        string title,
        string content,
        string actionPath,
        Func<Guid, string> deduplicationKeyFactory,
        string milestoneLabel,
        DateTime deadlineUtc)
    {
        foreach (var grp in groups)
        {
            AddStudentReminderRequests(
                requests, directory, grp.ConfirmedStudentIds,
                context.Batch.BatchId, grp.GroupId, "StudentGroup",
                notificationType,
                title,
                content,
                actionPath,
                deduplicationKeyFactory(grp.GroupId),
                milestoneLabel,
                DeadlineReminderFormatting.GetTopicTitle(context.Topics, grp.TopicId),
                deadlineUtc);
        }
    }

    private static void AddStudentReminderRequests(
        List<NotificationIngestionRequest> requests,
        IReadOnlyDictionary<Guid, NotificationRecipientContractDto> directory,
        IReadOnlyList<Guid> studentIds,
        Guid batchId,
        Guid resourceId,
        string resourceType,
        string notificationType,
        string title,
        string content,
        string actionPath,
        string deduplicationKey,
        string milestoneLabel,
        string topicTitle,
        DateTime deadlineUtc)
    {
        foreach (var studentId in studentIds)
        {
            if (!directory.TryGetValue(studentId, out var student))
            {
                continue;
            }

            requests.Add(new NotificationIngestionRequest(
                RecipientId: student.UserId,
                Title: title,
                Content: content,
                NotificationType: notificationType,
                SourceModule: "Notifications",
                DeduplicationKey: deduplicationKey,
                ResourceType: resourceType,
                ResourceId: resourceId,
                AcademicBatchId: batchId,
                ActionPath: actionPath,
                Priority: "Critical",
                EnqueuePush: true,
                EnqueueEmail: true,
                RecipientEmail: student.Email,
                EmailSubject: $"[UGTM] {title}",
                RecipientFullName: student.FullName,
                EmailCategoryLabel: "Nhắc hạn KLTN",
                EmailCtaLabel: "Thực hiện ngay",
                EmailNotice: "Vui lòng hoàn tất trước thời hạn để tránh bị khóa cổng hoặc dừng đề tài.",
                EmailMetadata:
                [
                    new("Mốc thời hạn", milestoneLabel),
                    new("Đề tài", topicTitle),
                    new("Hạn chót (VN)", DeadlineReminderFormatting.FormatVn(deadlineUtc))
                ]));
        }
    }
}
