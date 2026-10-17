using UGTM.BuildingBlocks.Domain;

namespace UGTM.Modules.Identity.Application.Bootstrap;

public interface IInitialAdminBootstrapper
{
    Task<Result<Guid>> BootstrapAdminAsync(
        string email,
        string password,
        string? fullName = null,
        string? userCode = null,
        CancellationToken cancellationToken = default);
}
