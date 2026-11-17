namespace UGTM.Modules.AcademicPeriods.Application.Persistence;

public abstract class AcademicPeriodsPersistenceException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class AcademicPeriodsConcurrencyException(Exception innerException)
    : AcademicPeriodsPersistenceException(
        "AcademicPeriods data was changed by another request.",
        innerException);

public sealed class AcademicPeriodsUniqueConstraintException(
    string? constraintName,
    Exception innerException)
    : AcademicPeriodsPersistenceException(
        "An AcademicPeriods uniqueness constraint was violated.",
        innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
