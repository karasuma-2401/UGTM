using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.FinalSubmissions;
using UGTM.Modules.DefenseCouncil.Application.ScoreSummaries;
using UGTM.Modules.DefenseCouncil.Application.Auditing;
using UGTM.Modules.Identity.Contracts.Auditing;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;

public sealed class SaveCouncilMinuteHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    CouncilMinuteWorkspaceLoader workspaceLoader,
    ICurrentUser currentUser,
    ICouncilMinuteWriteGuard writeGuard,
    IFinalSubmissionAuthorityReader authorityReader,
    IDefenseCouncilAuditOutbox audit,
    TimeProvider? clock = null)
{
    private static readonly HashSet<string> ValidComponentCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADVISOR", "GVHD",
        "REVIEWER", "GVPB",
        "COUNCIL_CHAIRMAN", "Chủ tịch", "CHAIRMAN",
        "COUNCIL_SECRETARY", "Thư ký", "SECRETARY",
        "COUNCIL_MEMBER", "UV", "MEMBER"
    };

    public Task<Result<SaveCouncilMinuteResponseDto>> HandleAsync(SaveCouncilMinuteCommand command, CancellationToken ct = default)
        => !currentUser.IsAuthenticated || !currentUser.UserId.HasValue
            ? Task.FromResult(Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.Forbidden))
            : command.IsPreview ? HandleCoreAsync(command, ct)
            : writeGuard.ExecuteAsync(command.AssignmentId, token => HandleCoreAsync(command, token), ct);

    private async Task<Result<SaveCouncilMinuteResponseDto>> HandleCoreAsync(
        SaveCouncilMinuteCommand command,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.Forbidden);
        }

        var assignment = await councilRepository.GetAssignmentWithCouncilAndMembersAsync(command.AssignmentId, ct);
        if (assignment == null)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.AssignmentNotFound);
        }

        var council = assignment.Council;
        var members = council.Members.ToList();

        // Kiểm tra phân quyền:
        // Nếu không phải preview, người dùng phải là Thư ký (Secretary) hoặc Admin
        var authority = command.IsPreview ? null : await authorityReader.GetAsync(currentUser.UserId.Value, ct);
        var isSecretary = ScoreSummaryAccess.Role(authority, "Lecturer") && members.Any(m => m.UserId == currentUser.UserId && m.RoleInCouncil == CouncilMemberRole.Secretary);
        var isAdmin = ScoreSummaryAccess.Role(authority, "Admin") || ScoreSummaryAccess.Role(authority, "AcademicAffairs");

        if (command.IsPreview && !CouncilMinuteAccess.CanView(council, currentUser))
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.Forbidden);
        }

        if (!command.IsPreview && !isSecretary && !isAdmin)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.OnlySecretaryCanSaveMinute);
        }

        // Kiểm tra trạng thái khóa
        var minute = assignment.Minute ?? await councilRepository.GetMinuteByAssignmentIdAsync(assignment.Id, ct);
        if (!command.IsPreview && minute != null && minute.LockStatus == CouncilMinuteLockStatus.Locked)
        {
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.MinuteLocked);
        }

        // Validate SecretaryMarking & TargetComponentCode
        var marking = command.SecretaryMarking?.Trim();
        if (marking is not null && !new[] { "None", "AcceptedAsPerMinute", "RejectedAsPerMinute" }.Contains(marking, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.InvalidSecretaryMarking);
        command = command with { SecretaryMarking = marking, TargetComponentCode = NormalizeTarget(command.TargetComponentCode) };
        var isRejection = string.Equals(command.SecretaryMarking, "RejectedAsPerMinute", StringComparison.OrdinalIgnoreCase);
        if (isRejection)
        {
            if (string.IsNullOrWhiteSpace(command.TargetComponentCode))
            {
                return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.TargetComponentRequiredForRejection);
            }

            if (!ValidComponentCodes.Contains(command.TargetComponentCode.Trim()))
            {
                return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.InvalidTargetComponent(command.TargetComponentCode));
            }
        }

        var ctx = await workspaceLoader.LoadAsync(assignment, ct, lockConfiguration: !command.IsPreview);
        if (isRejection && !ctx.EvalItems.Any(item => item.ComponentCode == command.TargetComponentCode
                && item.Score.HasValue && !(item.ComponentCode == "COUNCIL_MEMBER" && item.IsDualRoleReviewer)))
            return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.InvalidTargetComponent(command.TargetComponentCode!));

        // Tính toán trên memory
        var (scoreItems, discrepancySummary, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            ctx.EvalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: command.SecretaryMarking,
            requestedTargetComponentCode: command.TargetComponentCode,
            overrideReason: ctx.Minute?.OverrideReason,
            configuration: ctx.Configuration);

        if (!command.IsPreview)
        {
            var reason = command.Reason?.Trim();
            var exclusions = ctx.EvalItems.Zip(scoreItems).Where(pair => pair.First.SourceId != Guid.Empty)
                .ToDictionary(pair => pair.First.SourceId, pair => pair.Second.IsIgnoredInCalculation);
            var changesExclusion = ctx.EvalItems.Any(item => item.SourceId != Guid.Empty && exclusions[item.SourceId] != item.IsIgnoredInCalculation);
            if ((changesExclusion || isRejection) && (string.IsNullOrWhiteSpace(reason) || reason.Length is < 10 or > 1000))
                return Result.Failure<SaveCouncilMinuteResponseDto>(CouncilErrors.ExclusionReasonRequired);
            var before = Snapshot(ctx);
            // Thực hiện ghi xuống DB
            // 1. Reset / Cập nhật cờ IsIgnoredInCalculation & ExclusionReason
            // Update Advisor
            if (ctx.AdvisorEval != null)
            {
                var isIgnored = exclusions.GetValueOrDefault(ctx.AdvisorEval.Id);
                ctx.AdvisorEval.SetCalculationExclusion(isIgnored, isIgnored ? isRejection ? reason : ctx.AdvisorEval.ExclusionReason : null);
            }

            // Update Reviewer
            if (ctx.ActiveReviewer?.Evaluation != null)
            {
                var evaluation = ctx.ActiveReviewer.Evaluation;
                var isIgnored = exclusions.GetValueOrDefault(evaluation.Id);
                evaluation.SetCalculationExclusion(isIgnored, isIgnored ? isRejection ? reason : evaluation.ExclusionReason : null);
            }

            // Update CouncilScores
            foreach (var cs in ctx.CouncilScores)
            {
                var isIgnored = exclusions.GetValueOrDefault(cs.Id);
                cs.IsIgnoredInCalculation = isIgnored;
                cs.ExclusionReason = isIgnored ? isRejection ? reason : cs.ExclusionReason : null;
            }

            // 2. Lưu CouncilMinute
            var overrideMarker = string.Equals(command.SecretaryMarking, "AcceptedAsPerMinute", StringComparison.OrdinalIgnoreCase)
                ? "ACCEPTED_AS_PER_MINUTE"
                : null;

            await councilRepository.SaveCouncilMinuteDraftAsync(
                assignment.Id,
                command.StudentFeedback,
                overrideMarker,
                ct);

            audit.Add(new AuditIntegrationEntry("CouncilMinute", assignment.Id.ToString(),
                "DefenseCouncil.CouncilMinute.Saved", "Succeeded", changesExclusion ? "CalculationExclusionChanged" : null,
                new
                {
                    assignment.GroupId,
                    assignment.CouncilId,
                    AssignmentId = assignment.Id,
                    Reason = reason,
                    Before = before,
                    After = Snapshot(ctx, command.StudentFeedback, replaceFeedback: true),
                    command.SecretaryMarking,
                    command.TargetComponentCode,
                    calculationPreview.ConfigurationId,
                    calculationPreview.ConfigurationVersion
                }),
                (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);

            await unitOfWork.SaveChangesAsync(ct);
        }

        var currentLockStatus = ctx.Minute?.LockStatus.ToString() ?? "Open";

        return Result.Success(new SaveCouncilMinuteResponseDto(
            assignment.Id,
            currentLockStatus,
            command.StudentFeedback ?? ctx.Minute?.RequiredRevisions ?? ctx.Minute?.GeneralFeedback,
            command.IsPreview,
            discrepancySummary,
            calculationPreview,
            scoreItems));
    }

    private static object Snapshot(CouncilMinuteWorkspaceContext context, string? feedback = null, bool replaceFeedback = false) => new
    {
        Advisor = context.AdvisorEval is { } a ? new { a.Id, a.Score, a.IsIgnoredInCalculation, a.ExclusionReason } : null,
        Reviewer = context.ActiveReviewer?.Evaluation is { } r ? new { r.Id, r.Score, r.IsIgnoredInCalculation, r.ExclusionReason } : null,
        Council = context.CouncilScores.Select(s => new { s.Id, s.Score, s.IsIgnoredInCalculation, s.ExclusionReason }).ToArray(),
        StudentFeedback = replaceFeedback ? (string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim())
            : context.Minute?.RequiredRevisions ?? context.Minute?.GeneralFeedback
    };

    private static string? NormalizeTarget(string? target) => target?.Trim().ToUpperInvariant() switch
    {
        "ADVISOR" or "GVHD" => "ADVISOR",
        "REVIEWER" or "GVPB" => "REVIEWER",
        "COUNCIL_CHAIRMAN" or "CHAIRMAN" or "CHỦ TỊCH" => "COUNCIL_CHAIRMAN",
        "COUNCIL_SECRETARY" or "SECRETARY" or "THƯ KÝ" => "COUNCIL_SECRETARY",
        "COUNCIL_MEMBER" or "MEMBER" or "UV" => "COUNCIL_MEMBER",
        _ => target?.Trim()
    };
}
