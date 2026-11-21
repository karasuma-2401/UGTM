using UGTM.BuildingBlocks.Application.Caching;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.DefenseCouncil.Application.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Common;
using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;
using UGTM.Modules.DefenseCouncil.Application.Persistence;
using UGTM.Modules.DefenseCouncil.Domain.Enums;
using UGTM.Modules.DefenseCouncil.Domain.Repositories;
using UGTM.Modules.Identity.Contracts.Authorization;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.ManageCouncilGroups;

public sealed record ReorderCouncilGroupsCommand(Guid CouncilId, IReadOnlyList<CouncilAssignmentInputDto> Assignments);
public sealed record RemoveCouncilGroupCommand(Guid CouncilId, Guid AssignmentId);

public sealed class ManageCouncilGroupsHandler(
    ICouncilRepository councils,
    IDefenseCouncilUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    ICouncilSchedulingGuard schedulingGuard,
    ICacheService? cache = null)
{
    private bool CanManage => currentUser.IsAuthenticated && currentUser.Roles.Any(role => role is "Faculty" or "Admin");

    public Task<Result<IReadOnlyList<CouncilAssignmentResponseDto>>> ReorderAsync(
        ReorderCouncilGroupsCommand command, CancellationToken token = default)
    {
        if (!CanManage) return Task.FromResult(Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.Forbidden));
        return schedulingGuard.ExecuteAsync(async ct =>
        {
            var council = await councils.GetByIdWithDetailsAsync(command.CouncilId, ct);
            if (council is null) return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.NotFound);
            if (council.Status != CouncilStatus.Draft)
                return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(CouncilErrors.NotInDraftStatus);

            var existing = council.Assignments.ToList();
            var incoming = command.Assignments;
            if (incoming.Count != existing.Count || incoming.Any(item => item.OrderNumber < 1)
                || incoming.Select(item => item.GroupId).Distinct().Count() != incoming.Count
                || incoming.Select(item => item.OrderNumber).Distinct().Count() != incoming.Count
                || !incoming.Select(item => item.GroupId).ToHashSet().SetEquals(existing.Select(item => item.GroupId)))
                return Result.Failure<IReadOnlyList<CouncilAssignmentResponseDto>>(
                    Error.Validation("Council.InvalidGroupOrder", "Chỉ được đổi thứ tự các nhóm đã gán; mỗi nhóm cần một thứ tự dương, không trùng."));

            var byGroup = incoming.ToDictionary(item => item.GroupId, item => item.OrderNumber);
            var orders = existing.ToDictionary(item => item.Id, item => byGroup[item.GroupId]);
            if (existing.Any(item => item.OrderNumber != orders[item.Id]))
            {
                await councils.ReorderAssignmentsAsync(existing, orders, ct);
                council.ResetSubmission();
                await unitOfWork.SaveChangesAsync(ct);
                await DefenseCouncilCacheKeys.InvalidateCouncilAsync(cache, council.Id, council.BatchId, ct);
            }
            return Result.Success<IReadOnlyList<CouncilAssignmentResponseDto>>(existing
                .OrderBy(item => item.OrderNumber)
                .Select(item => new CouncilAssignmentResponseDto(item.Id, item.CouncilId, item.GroupId, item.OrderNumber))
                .ToList());
        }, token);
    }

    public Task<Result<bool>> RemoveAsync(RemoveCouncilGroupCommand command, CancellationToken token = default)
    {
        if (!CanManage) return Task.FromResult(Result.Failure<bool>(CouncilErrors.Forbidden));
        return schedulingGuard.ExecuteAsync(async ct =>
        {
            var council = await councils.GetByIdWithDetailsAsync(command.CouncilId, ct);
            if (council is null) return Result.Failure<bool>(CouncilErrors.NotFound);
            if (council.Status != CouncilStatus.Draft) return Result.Failure<bool>(CouncilErrors.NotInDraftStatus);
            var assignment = council.Assignments.FirstOrDefault(item => item.Id == command.AssignmentId);
            if (assignment is null) return Result.Failure<bool>(
                Error.NotFound("Council.AssignmentNotFound", "Không tìm thấy nhóm được gán trong hội đồng này."));
            if ((await councils.GetCouncilScoresWithDetailsByAssignmentIdAsync(assignment.Id, ct)).Count > 0
                || await councils.GetMinuteByAssignmentIdAsync(assignment.Id, ct) is not null
                || await councils.GetScoreSummaryByGroupIdAsync(assignment.GroupId, ct) is not null)
                return Result.Failure<bool>(Error.Conflict("Council.AssignmentHasWork",
                    "Nhóm đã có điểm hoặc biên bản bảo vệ, không thể gỡ khỏi hội đồng."));

            await councils.RemoveAssignmentAsync(assignment, ct);
            council.ResetSubmission();
            await unitOfWork.SaveChangesAsync(ct);
            await DefenseCouncilCacheKeys.InvalidateCouncilAsync(cache, council.Id, council.BatchId, ct);
            return Result.Success(true);
        }, token);
    }
}
