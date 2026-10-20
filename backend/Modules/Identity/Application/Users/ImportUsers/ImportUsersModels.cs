namespace UGTM.Modules.Identity.Application.Users.ImportUsers;

public sealed record ImportUserRow(
    int RowNumber,
    string Email,
    string FullName,
    string? UserCode,
    string? PhoneNumber);

public sealed record ImportUserRowError(
    int RowNumber,
    string Field,
    string Code,
    string Message);

public sealed record ImportFileReadResult(
    IReadOnlyList<ImportUserRow> Rows,
    IReadOnlyList<ImportUserRowError> Errors);

public sealed record ImportUsersResult(
    int TotalRows,
    int ImportedCount,
    string Role,
    IReadOnlyList<ImportUserRowError> Errors);

public interface IUserImportFileReader
{
    ImportFileReadResult Read(Stream stream);
}

public interface IUserImportTemplateGenerator
{
    byte[] Generate(SystemRoleName role);
}
