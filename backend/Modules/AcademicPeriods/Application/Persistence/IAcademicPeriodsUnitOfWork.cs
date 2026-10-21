namespace UGTM.Modules.AcademicPeriods.Application.Persistence;

public interface IAcademicPeriodsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
