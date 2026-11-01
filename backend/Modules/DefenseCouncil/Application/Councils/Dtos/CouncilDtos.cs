using UGTM.Modules.DefenseCouncil.Domain.Enums;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

public sealed record CreateCouncilInputDto(
    Guid BatchId,
    string CouncilCode,
    DateOnly DefenseDate,
    string TimeSlot,
    string Room);

public sealed record CouncilResponseDto(
    Guid Id,
    Guid BatchId,
    string CouncilCode,
    DateOnly DefenseDate,
    string TimeSlot,
    string Room,
    string Status,
    string? DecisionNumber,
    DateOnly? SignedDate,
    string? DecisionFileUrl,
    Guid? ApprovedBy,
    DateTime? ApprovedAt);

public sealed record CouncilMemberInputDto(
    Guid UserId,
    CouncilMemberRole Role);

public sealed record AddCouncilMembersInputDto(
    IReadOnlyList<CouncilMemberInputDto>? Members = null,
    Guid? UserId = null,
    CouncilMemberRole? Role = null);

public sealed record CouncilMemberResponseDto(
    Guid Id,
    Guid CouncilId,
    Guid UserId,
    string Role,
    string? FullName = null,
    string? Email = null,
    string? UserCode = null);

public sealed record CouncilAssignmentInputDto(
    Guid GroupId,
    int OrderNumber);

public sealed record AssignCouncilGroupsInputDto(
    IReadOnlyList<CouncilAssignmentInputDto>? Assignments = null,
    Guid? GroupId = null,
    int? OrderNumber = null);

public sealed record CouncilAssignmentResponseDto(
    Guid Id,
    Guid CouncilId,
    Guid GroupId,
    int OrderNumber,
    string? TopicTitleVi = null,
    string? TopicTitleEn = null);

public sealed record ApproveCouncilInputDto(
    string DecisionNumber,
    DateOnly? SignedDate = null,
    string? DecisionFileUrl = null);

public sealed record CouncilScheduleStudentDto(
    Guid StudentId,
    string FullName,
    string Email,
    string? UserCode);

public sealed record CouncilScheduleGroupDto(
    Guid GroupId,
    int OrderNumber,
    Guid TopicId,
    string TopicTitleVi,
    string? TopicTitleEn,
    Guid AdvisorId,
    string? AdvisorName,
    Guid? ReviewerId,
    string? ReviewerName,
    bool IsReviewerInCouncil,
    IReadOnlyList<CouncilScheduleStudentDto> Students);

public sealed record CouncilScheduleMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string? UserCode,
    string Role);

public sealed record CouncilScheduleResponseDto(
    Guid CouncilId,
    Guid BatchId,
    string CouncilCode,
    DateOnly DefenseDate,
    string TimeSlot,
    string Room,
    string Status,
    string? DecisionNumber,
    DateOnly? SignedDate,
    IReadOnlyList<CouncilScheduleMemberDto> Members,
    IReadOnlyList<CouncilScheduleGroupDto> Groups);

public sealed record CriterionScoreInputDto(
    Guid CriteriaId,
    decimal Score);

public sealed record SubmitCouncilScoreInputDto(
    IReadOnlyList<CriterionScoreInputDto> Details,
    string? Feedback = null);

public sealed record EvaluationDetailResponseDto(
    Guid Id,
    Guid CriteriaId,
    string CriteriaCode,
    string CriteriaName,
    decimal Score,
    decimal MaxScore);

public sealed record CouncilScoreResponseDto(
    Guid Id,
    Guid CouncilAssignmentId,
    Guid CouncilMemberId,
    Guid UserId,
    decimal Score,
    string? Feedback,
    DateTime EvaluatedAt,
    IReadOnlyList<EvaluationDetailResponseDto> Details);

public sealed record MemberScoreDetailDto(
    Guid CouncilMemberId,
    Guid UserId,
    string FullName,
    string Email,
    string? UserCode,
    string Role,
    decimal? Score,
    string? Feedback,
    DateTime? EvaluatedAt,
    bool IsReviewerInCouncil,
    bool IsBlockedFromScoring);

public sealed record CouncilDiscrepancyResponseDto(
    Guid AssignmentId,
    Guid CouncilId,
    Guid GroupId,
    bool IsDiscrepancyDetected,
    decimal WarningThreshold,
    decimal? ScoreDifference,
    decimal? MaxScore,
    decimal? MinScore,
    string? WarningMessage,
    bool AllScoresSubmitted,
    decimal? AverageCouncilScore,
    decimal? FinalScore,
    IReadOnlyList<MemberScoreDetailDto> MemberScores);

