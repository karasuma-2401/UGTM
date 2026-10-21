using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.AcademicPeriods.Application.Common;

public static class ScoreConfigurationErrors
{
    public static readonly Error AccessDenied = Error.Forbidden(
        "ScoreConfiguration.AccessDenied",
        "Bạn không có quyền truy cập cấu hình điểm.");

    public static readonly Error BatchNotFound = Error.NotFound(
        "ScoreConfiguration.BatchNotFound",
        "Không tìm thấy đợt khóa luận.");

    public static readonly Error NotFound = Error.NotFound(
        "ScoreConfiguration.NotFound",
        "Không tìm thấy phiên bản cấu hình điểm.");

    public static readonly Error ActiveNotFound = Error.NotFound(
        "ScoreConfiguration.ActiveNotFound",
        "Đợt khóa luận chưa có cấu hình điểm đang hoạt động.");

    public static readonly Error DraftAlreadyExists = Error.Conflict(
        "ScoreConfiguration.DraftAlreadyExists",
        "Đợt khóa luận đã có một bản cấu hình điểm Draft.");

    public static readonly Error ActiveNotEditable = Error.Conflict(
        "ScoreConfiguration.ActiveNotEditable",
        "Chỉ cấu hình điểm ở trạng thái Draft mới được chỉnh sửa.");

    public static readonly Error InvalidPercentageTotal = Error.Validation(
        "ScoreConfiguration.InvalidPercentageTotal",
        "Tổng trọng số của phương thức Percentage phải bằng chính xác 100.");

    public static readonly Error InvalidCoefficient = Error.Validation(
        "ScoreConfiguration.InvalidCoefficient",
        "Hệ số phải là số nguyên không âm và tổng hệ số phải lớn hơn 0.");

    public static readonly Error DuplicateComponentCode = Error.Validation(
        "ScoreConfiguration.DuplicateComponentCode",
        "Mã thành phần điểm không được trùng trong cùng một version.");

    public static readonly Error DuplicateComponentOrder = Error.Validation(
        "ScoreConfiguration.DuplicateComponentOrder",
        "Thứ tự thành phần điểm phải là số dương và không được trùng.");

    public static readonly Error InvalidMinimumScore = Error.Validation(
        "ScoreConfiguration.InvalidMinimumScore",
        "Điểm sàn phải để trống hoặc nằm trong khoảng từ 0 đến 10.");

    public static readonly Error EmptyComponents = Error.Validation(
        "ScoreConfiguration.EmptyComponents",
        "Cấu hình phải có ít nhất một thành phần điểm.");

    public static readonly Error ChangeReasonRequired = Error.Validation(
        "ScoreConfiguration.ChangeReasonRequired",
        "Phải cung cấp lý do khi tạo phiên bản cấu hình điểm tiếp theo.");

    public static readonly Error InvalidRequest = Error.Validation(
        "ScoreConfiguration.InvalidRequest",
        "Thông tin cấu hình điểm không hợp lệ.");

    public static readonly Error InvalidStatusTransition = Error.Conflict(
        "ScoreConfiguration.InvalidStatusTransition",
        "Trạng thái hiện tại không cho phép thực hiện thao tác này.");
}
