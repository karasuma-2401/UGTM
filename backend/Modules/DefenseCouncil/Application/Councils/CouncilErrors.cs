using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.DefenseCouncil.Application.Councils;

public static class CouncilErrors
{
    public static Error NotFound =>
        Error.NotFound("Council.NotFound", "Không tìm thấy hội đồng bảo vệ.");

    public static Error NotInDraftStatus =>
        Error.Conflict("Council.NotInDraftStatus", "Chỉ có thể chỉnh sửa hội đồng khi đang ở trạng thái dự thảo (Draft).");

    public static Error BatchNotFound =>
        Error.NotFound("Council.BatchNotFound", "Đợt KLTN không tồn tại.");

    public static Error DuplicateCode =>
        Error.Conflict("Council.DuplicateCode", "Mã hội đồng đã tồn tại trong đợt KLTN này.");

    public static Error RoomScheduleConflict =>
        Error.Conflict("Council.RoomScheduleConflict", "Phòng thi đã được xếp cho hội đồng khác trong cùng ca và ngày bảo vệ.");

    public static Error MemberScheduleConflict(string details) =>
        Error.Conflict("Council.MemberScheduleConflict", $"Giảng viên đã có lịch tham gia hội đồng khác trong cùng ca và ngày bảo vệ: {details}.");

    public static Error MemberIsGroupAdvisor(string lecturerName, string groupDetails) =>
        Error.Conflict("Council.MemberIsGroupAdvisor", $"Giảng viên {lecturerName} là GVHD của nhóm ({groupDetails}) trong hội đồng, vi phạm quy định chống xung đột lợi ích.");

    public static Error AdvisorIsInCouncil(string advisorName, string groupDetails) =>
        Error.Conflict("Council.AdvisorIsInCouncil", $"Nhóm ({groupDetails}) có GVHD là {advisorName} đang là thành viên của hội đồng này, vi phạm quy định chống xung đột lợi ích.");

    public static Error DuplicateChairman =>
        Error.Conflict("Council.DuplicateChairman", "Hội đồng chỉ được có tối đa 01 Chủ tịch.");

    public static Error DuplicateSecretary =>
        Error.Conflict("Council.DuplicateSecretary", "Hội đồng chỉ được có tối đa 01 Thư ký.");

    public static Error DuplicateMember(string lecturerName) =>
        Error.Conflict("Council.DuplicateMember", $"Giảng viên {lecturerName} đã là thành viên của hội đồng này.");

    public static Error MemberNotFound(Guid userId) =>
        Error.NotFound("Council.MemberNotFound", $"Không tìm thấy thông tin giảng viên hoặc tài khoản không hoạt động (Id: {userId}).");

    public static Error GroupNotFound(Guid groupId) =>
        Error.NotFound("Council.GroupNotFound", $"Không tìm thấy nhóm đề tài sinh viên (Id: {groupId}).");

    public static Error GroupNotInBatch(Guid groupId) =>
        Error.Validation("Council.GroupNotInBatch", $"Nhóm đề tài {groupId} không thuộc cùng đợt KLTN với hội đồng.");

    public static Error GroupStopped(Guid groupId) =>
        Error.Validation("Council.GroupStopped", $"Nhóm đề tài {groupId} đã dừng thực hiện hoặc không hợp lệ để bảo vệ.");

    public static Error GroupDefenseRejectedByReviewer(Guid groupId) =>
        Error.Conflict("Council.GroupDefenseRejectedByReviewer", $"Nhóm đề tài {groupId} đã bị giảng viên phản biện đánh giá không đủ điều kiện bảo vệ.");

    public static Error GroupAlreadyAssigned(Guid groupId) =>
        Error.Conflict("Council.GroupAlreadyAssigned", $"Nhóm đề tài {groupId} đã được phân vào một hội đồng bảo vệ.");

    public static Error DuplicateOrderNumber =>
        Error.Conflict("Council.DuplicateOrderNumber", "Thứ tự bảo vệ (OrderNumber) bị trùng lặp giữa các nhóm trong hội đồng.");

    public static Error MissingChairman =>
        Error.Validation("Council.MissingChairman", "Hội đồng phải có 01 Chủ tịch trước khi phê duyệt.");

    public static Error MissingSecretary =>
        Error.Validation("Council.MissingSecretary", "Hội đồng phải có 01 Thư ký trước khi phê duyệt.");

    public static Error NoAssignedGroups =>
        Error.Validation("Council.NoAssignedGroups", "Hội đồng phải có ít nhất 01 nhóm đề tài được phân công trước khi phê duyệt.");

    public static Error AlreadyApproved =>
        Error.Conflict("Council.AlreadyApproved", "Hội đồng đã được phê duyệt quyết định thành lập trước đó.");

    public static Error ScheduleNotPublished =>
        Error.NotFound("Council.ScheduleNotPublished", "Lịch bảo vệ của hội đồng chưa được công bố.");

    public static Error DecisionNumberRequired =>
        Error.Validation("Council.DecisionNumberRequired", "Số quyết định thành lập hội đồng là bắt buộc.");

    public static Error Forbidden =>
        Error.Forbidden("Council.Forbidden", "Bạn không có quyền thực hiện thao tác này.");

    public static Error AssignmentNotFound =>
        Error.NotFound("Council.AssignmentNotFound", "Không tìm thấy phân công nhóm đề tài trong hội đồng.");

    public static Error UserNotCouncilMember =>
        Error.Forbidden("Council.UserNotCouncilMember", "Người dùng không phải là thành viên của hội đồng này.");

    public static Error NotActiveForScoring =>
        Error.Conflict("Council.NotActiveForScoring", "Hội đồng phải ở trạng thái đã phê duyệt (Active) mới có thể thực hiện chấm điểm.");

    public static Error ReviewerCouncilMemberCannotScoreTwice =>
        Error.Conflict("Council.ReviewerCouncilMemberCannotScoreTwice", "Thành viên hội đồng đồng thời là Giảng viên phản biện của nhóm đề tài này, không được nhập điểm hội đồng lần thứ hai.");

    public static Error MinuteLocked =>
        Error.Conflict("Council.MinuteLocked", "Biên bản hội đồng đã bị khóa, không thể nhập hoặc chỉnh sửa điểm.");

    public static Error CriteriaRequired =>
        Error.Validation("Council.CriteriaRequired", "Danh sách điểm tiêu chí không được để trống.");

    public static Error DuplicateCriteriaInRequest =>
        Error.Validation("Council.DuplicateCriteriaInRequest", "Phát hiện tiêu chí đánh giá bị lặp lại trong yêu cầu chấm điểm.");

    public static Error CriteriaNotFound(Guid criteriaId) =>
        Error.Validation("Council.CriteriaNotFound", $"Tiêu chí đánh giá không tồn tại hoặc không thuộc đợt KLTN này (Id: {criteriaId}).");

    public static Error ScoreExceedsMaxScore(string criteriaName, decimal score, decimal maxScore) =>
        Error.Validation("Council.ScoreExceedsMaxScore", $"Điểm tiêu chí '{criteriaName}' ({score:F2}) vượt quá điểm tối đa cho phép ({maxScore:F2}).");

    public static Error CriteriaScoreNegative(string criteriaName) =>
        Error.Validation("Council.CriteriaScoreNegative", $"Điểm tiêu chí '{criteriaName}' không được nhỏ hơn 0.");

    public static Error MissingRequiredCriteria(string criteriaName) =>
        Error.Validation("Council.MissingRequiredCriteria", $"Chưa nhập điểm cho tiêu chí bắt buộc: '{criteriaName}'.");

    public static Error OnlySecretaryCanSaveMinute =>
        Error.Forbidden("Council.OnlySecretaryCanSaveMinute", "Chỉ Thư ký của hội đồng (hoặc Admin) mới có quyền lưu nháp biên bản.");

    public static Error TargetComponentRequiredForRejection =>
        Error.Validation("Council.TargetComponentRequiredForRejection", "Bắt buộc phải chỉ định mã đầu điểm/vai trò cần loại (target_component_code) khi chọn không công nhận theo biên bản (RejectedAsPerMinute).");

    public static Error InvalidTargetComponent(string componentCode) =>
        Error.Validation("Council.InvalidTargetComponent", $"Mã đầu điểm/vai trò cần loại không hợp lệ hoặc không tồn tại trong hội đồng: '{componentCode}'.");
}
