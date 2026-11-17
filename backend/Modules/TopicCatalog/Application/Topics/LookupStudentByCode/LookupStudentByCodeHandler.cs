using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.Identity.Contracts.Users;

namespace UGTM.Modules.TopicCatalog.Application.Topics.LookupStudentByCode;

public sealed record TopicStudentLookupDto(string UserCode, string FullName);

public sealed class LookupStudentByCodeHandler(IStudentInfoReader studentInfoReader, ICurrentUser currentUser)
{
    private const int MaxUserCodeLength = 20;

    public async Task<Result<TopicStudentLookupDto>> HandleAsync(
        string? userCode,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            return Result.Failure<TopicStudentLookupDto>(
                Error.Unauthorized("Topics.AuthenticationRequired", "Vui lòng đăng nhập để tra cứu sinh viên."));
        }

        if (!currentUser.Roles.Any(role => role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<TopicStudentLookupDto>(
                Error.Forbidden("Topics.AccessDenied", "Bạn không có quyền tra cứu sinh viên cho đề xuất đề tài."));
        }

        var normalizedCode = userCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode) || normalizedCode.Length > MaxUserCodeLength)
        {
            return Result.Failure<TopicStudentLookupDto>(
                Error.Validation("Topics.InvalidStudentCode", "Mã số sinh viên không hợp lệ (tối đa 20 ký tự)."));
        }

        var student = await studentInfoReader.GetStudentByIdOrCodeAsync(null, normalizedCode, cancellationToken);
        if (student is null || string.IsNullOrWhiteSpace(student.UserCode))
        {
            return Result.Failure<TopicStudentLookupDto>(
                Error.NotFound("Topics.StudentNotFound", "Không tìm thấy sinh viên đang hoạt động với mã số này."));
        }

        return Result.Success(new TopicStudentLookupDto(student.UserCode, student.FullName));
    }
}
