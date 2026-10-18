using UGTM.Modules.Identity.Application.AuditLogs;
using UGTM.Modules.Identity.Application.Common;
using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;
using UGTM.Modules.Identity.Domain.Repositories;

namespace UGTM.Modules.Identity.Application.Users.ImportUsers;

public sealed class ImportUsersHandler(
    IUserImportFileReader fileReader,
    IUserRepository users,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IAuditLogWriter auditLogs)
{
    public const int MaxRows = 5_000;

    public async Task<Result<ImportUsersResult>> HandleAsync(
        ImportUsersCommand command,
        CancellationToken cancellationToken)
    {
        if (command.File is null
            || !command.File.CanRead
            || !string.Equals(Path.GetExtension(command.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ImportUsersResult>(IdentityErrors.InvalidImportFile);
        }

        var roleInput = command.Role?.Trim();
        if (int.TryParse(roleInput, out _)
            || !Enum.TryParse<SystemRoleName>(roleInput, ignoreCase: true, out var role)
            || !Enum.IsDefined(role)
            || role is not (SystemRoleName.Student or SystemRoleName.Lecturer))
        {
            return Result.Failure<ImportUsersResult>(IdentityErrors.InvalidImportRole);
        }

        ImportFileReadResult parsed;
        try
        {
            parsed = fileReader.Read(command.File);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or FormatException)
        {
            return Result.Failure<ImportUsersResult>(IdentityErrors.InvalidImportFile);
        }

        var errors = parsed.Errors.ToList();
        if (parsed.Rows.Count == 0 && errors.Count == 0)
        {
            errors.Add(new ImportUserRowError(
                0,
                "file",
                "EmptyFile",
                "Tệp không có dòng dữ liệu người dùng."));
        }

        if (parsed.Rows.Count > MaxRows)
        {
            errors.Add(new ImportUserRowError(
                0,
                "file",
                "TooManyRows",
                $"Tệp chỉ được chứa tối đa {MaxRows} dòng dữ liệu."));
        }

        var normalizedRows = parsed.Rows
            .Select(row => row with
            {
                Email = row.Email.Trim(),
                FullName = row.FullName.Trim(),
                UserCode = NormalizeOptional(row.UserCode)?.ToUpperInvariant(),
                PhoneNumber = NormalizeOptional(row.PhoneNumber)
            })
            .ToList();

        ValidateRows(normalizedRows, role, errors);
        AddDuplicateFileErrors(normalizedRows, errors);

        if (errors.Count > 0)
        {
            return Result.Success(new ImportUsersResult(
                normalizedRows.Count,
                0,
                role.ToString(),
                OrderErrors(errors)));
        }

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await users.AcquireAccountProvisioningLockAsync(ct);

            var normalizedEmails = normalizedRows
                .Select(row => row.Email.ToUpperInvariant())
                .ToArray();
            var userCodes = normalizedRows
                .Where(row => row.UserCode is not null)
                .Select(row => row.UserCode!)
                .ToArray();

            var existingEmails = await users.GetExistingNormalizedEmailsAsync(normalizedEmails, ct);
            var existingCodes = await users.GetExistingUserCodesAsync(userCodes, ct);

            foreach (var row in normalizedRows)
            {
                if (existingEmails.Contains(row.Email.ToUpperInvariant()))
                {
                    errors.Add(new ImportUserRowError(
                        row.RowNumber,
                        "email",
                        "DuplicateEmail",
                        "Email đã tồn tại trong hệ thống."));
                }

                if (row.UserCode is not null && existingCodes.Contains(row.UserCode))
                {
                    errors.Add(new ImportUserRowError(
                        row.RowNumber,
                        "userCode",
                        "DuplicateUserCode",
                        "Mã người dùng đã tồn tại trong hệ thống."));
                }
            }

            if (errors.Count > 0)
            {
                return Result.Success(new ImportUsersResult(
                    normalizedRows.Count,
                    0,
                    role.ToString(),
                    OrderErrors(errors)));
            }

            var now = clock.UtcNow;
            var importedUsers = normalizedRows
                .Select(row => User.Create(
                    Guid.NewGuid(),
                    row.Email,
                    row.Email.ToUpperInvariant(),
                    row.FullName,
                    row.UserCode,
                    row.PhoneNumber,
                    passwordHash: null,
                    AccountStatus.PendingActivation,
                    now))
                .ToArray();

            await users.AddRangeToRoleAsync(importedUsers, role, now, ct);
            auditLogs.Add(new AuditEntry(
                TargetType: "UserImport",
                TargetId: Guid.NewGuid().ToString("D"),
                Action: AuditActions.UsersImported,
                Outcome: AuditOutcomes.Success,
                ReasonCode: null,
                Metadata: new
                {
                    FileName = Path.GetFileName(command.FileName),
                    Role = role.ToString(),
                    ImportedCount = importedUsers.Length,
                    ActivationEmailsSent = 0
                }));

            await unitOfWork.SaveChangesAsync(ct);

            return Result.Success(new ImportUsersResult(
                normalizedRows.Count,
                importedUsers.Length,
                role.ToString(),
                []));
        }, cancellationToken);
    }

    private static void ValidateRows(
        IReadOnlyCollection<ImportUserRow> rows,
        SystemRoleName role,
        ICollection<ImportUserRowError> errors)
    {
        foreach (var row in rows)
        {
            if (!EmailValidator.IsValid(row.Email))
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "email",
                    "InvalidEmail",
                    "Email không hợp lệ hoặc vượt quá 150 ký tự."));
            }

            if (string.IsNullOrWhiteSpace(row.FullName)
                || row.FullName.Length > EmailValidator.MaxFullNameLength)
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "fullName",
                    "InvalidFullName",
                    "Họ tên là bắt buộc và không được vượt quá 150 ký tự."));
            }

            if (row.UserCode is not null && row.UserCode.Length > EmailValidator.MaxUserCodeLength)
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "userCode",
                    "InvalidUserCode",
                    "Mã người dùng không được vượt quá 20 ký tự."));
            }

            if (role == SystemRoleName.Student && string.IsNullOrWhiteSpace(row.UserCode))
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "userCode",
                    "RequiredUserCode",
                    "Mã sinh viên là bắt buộc."));
            }

            if (!PhoneNumberValidator.IsValid(row.PhoneNumber))
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "phoneNumber",
                    "InvalidPhoneNumber",
                    "Số điện thoại không đúng định dạng Việt Nam."));
            }
        }
    }

    private static void AddDuplicateFileErrors(
        IReadOnlyCollection<ImportUserRow> rows,
        ICollection<ImportUserRowError> errors)
    {
        foreach (var group in rows.GroupBy(row => row.Email, StringComparer.OrdinalIgnoreCase)
                     .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
        {
            foreach (var row in group)
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "email",
                    "DuplicateEmailInFile",
                    "Email bị trùng trong tệp import."));
            }
        }

        foreach (var group in rows
                     .Where(row => row.UserCode is not null)
                     .GroupBy(row => row.UserCode!, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            foreach (var row in group)
            {
                errors.Add(new ImportUserRowError(
                    row.RowNumber,
                    "userCode",
                    "DuplicateUserCodeInFile",
                    "Mã người dùng bị trùng trong tệp import."));
            }
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<ImportUserRowError> OrderErrors(
        IEnumerable<ImportUserRowError> errors)
        => errors
            .OrderBy(error => error.RowNumber)
            .ThenBy(error => error.Field, StringComparer.Ordinal)
            .ThenBy(error => error.Code, StringComparer.Ordinal)
            .ToArray();
}
