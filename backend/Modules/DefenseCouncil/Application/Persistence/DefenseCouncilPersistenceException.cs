namespace UGTM.Modules.DefenseCouncil.Application.Persistence;

public abstract class DefenseCouncilPersistenceException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class DefenseCouncilConcurrencyException(Exception innerException)
    : DefenseCouncilPersistenceException(
        "DefenseCouncil data was changed by another request.",
        innerException);

public sealed class DefenseCouncilUniqueConstraintException(
    string? constraintName,
    Exception innerException)
    : DefenseCouncilPersistenceException(
        "A DefenseCouncil uniqueness constraint was violated.",
        innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
