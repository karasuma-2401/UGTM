using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.AcademicPeriods.Application.Persistence;

public static class AcademicPeriodsUnitOfWorkExtensions
{
    public static async Task<Error?> TrySaveChangesAsync(
        this IAcademicPeriodsUnitOfWork unitOfWork,
        Error concurrencyError,
        Error? uniqueConstraintError = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (AcademicPeriodsConcurrencyException)
        {
            return concurrencyError;
        }
        catch (AcademicPeriodsUniqueConstraintException)
        {
            return uniqueConstraintError ?? concurrencyError;
        }
    }
}
