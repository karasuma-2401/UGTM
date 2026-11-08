using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SaveCouncilMinute;

public sealed class SaveCouncilMinuteHandler(
    ICouncilRepository councilRepository,
    IDefenseCouncilUnitOfWork unitOfWork,
    CouncilMinuteWorkspaceLoader workspaceLoader,
    ICurrentUser currentUser)
{
    private static readonly HashSet<string> ValidComponentCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADVISOR", "GVHD",
        "REVIEWER", "GVPB",
        "COUNCIL_CHAIRMAN", "Chủ tịch", "CHAIRMAN",
        "COUNCIL_SECRETARY", "Thư ký", "SECRETARY",
        "COUNCIL_MEMBER", "UV", "MEMBER"
    };

    public async Task<Result<SaveCouncilMinuteResponseDto>> HandleAsync(
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
        var isSecretary = members.Any(m => m.UserId == currentUser.UserId && m.RoleInCouncil == CouncilMemberRole.Secretary);
        var isAdmin = currentUser.Roles.Contains("Admin") || currentUser.Roles.Contains("AcademicAffairs");

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

        var ctx = await workspaceLoader.LoadAsync(assignment, ct);

        // Tính toán trên memory
        var (scoreItems, discrepancySummary, calculationPreview) = CouncilMinuteWorkspaceCalculator.BuildWorkspaceData(
            ctx.EvalItems,
            savedSecretaryMarking: null,
            requestedSecretaryMarking: command.SecretaryMarking,
            requestedTargetComponentCode: command.TargetComponentCode,
            overrideReason: ctx.Minute?.OverrideReason);

        if (!command.IsPreview)
        {
            // Thực hiện ghi xuống DB
            // 1. Reset / Cập nhật cờ IsIgnoredInCalculation & ExclusionReason
            var ignoredCode = discrepancySummary.IgnoredComponentCode;

            // Update Advisor
            if (ctx.AdvisorEval != null)
            {
                var isIgnored = string.Equals(ignoredCode, "ADVISOR", StringComparison.OrdinalIgnoreCase);
                ctx.AdvisorEval.SetCalculationExclusion(isIgnored, isIgnored ? "RejectedAsPerMinute" : null);
            }

            // Update Reviewer
            if (ctx.ActiveReviewer?.Evaluation != null)
            {
                var isIgnored = string.Equals(ignoredCode, "REVIEWER", StringComparison.OrdinalIgnoreCase);
                ctx.ActiveReviewer.Evaluation.SetCalculationExclusion(isIgnored, isIgnored ? "RejectedAsPerMinute" : null);
            }

            // Update CouncilScores
            foreach (var cs in ctx.CouncilScores)
            {
                var member = members.FirstOrDefault(m => m.Id == cs.CouncilMemberId);
                var code = member?.RoleInCouncil switch
                {
                    CouncilMemberRole.Chairman => "COUNCIL_CHAIRMAN",
                    CouncilMemberRole.Secretary => "COUNCIL_SECRETARY",
                    _ => "COUNCIL_MEMBER"
                };

                var isIgnored = string.Equals(ignoredCode, code, StringComparison.OrdinalIgnoreCase);
                cs.IsIgnoredInCalculation = isIgnored;
                cs.ExclusionReason = isIgnored ? "RejectedAsPerMinute" : null;
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
}
