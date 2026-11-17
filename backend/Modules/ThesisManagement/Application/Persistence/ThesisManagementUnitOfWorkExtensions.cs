using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.ThesisManagement.Application.Persistence;

public static class ThesisManagementUnitOfWorkExtensions
{
    public static async Task<Error?> TrySaveChangesAsync(
        this IThesisUnitOfWork unitOfWork,
        Error concurrencyError,
        Error? uniqueConstraintError = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (ThesisManagementConcurrencyException)
        {
            return concurrencyError;
        }
        catch (ThesisManagementUniqueConstraintException)
        {
            return uniqueConstraintError ?? concurrencyError;
        }
    }
}
