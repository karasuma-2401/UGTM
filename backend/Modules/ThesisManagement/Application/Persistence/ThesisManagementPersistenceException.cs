namespace UGTM.Modules.ThesisManagement.Application.Persistence;

public abstract class ThesisManagementPersistenceException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class ThesisManagementConcurrencyException(Exception innerException)
    : ThesisManagementPersistenceException(
        "ThesisManagement data was changed by another request.",
        innerException);

public sealed class ThesisManagementUniqueConstraintException(
    string? constraintName,
    Exception innerException)
    : ThesisManagementPersistenceException(
        "A ThesisManagement uniqueness constraint was violated.",
        innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
