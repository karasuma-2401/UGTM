using UGTM.Modules.Identity.Domain.Entities;
using UGTM.Modules.Identity.Domain.Enums;

namespace UGTM.Modules.Identity.Domain.Repositories;

public interface IOneTimeChallengeRepository
{
    void Add(OneTimeChallenge challenge);

    Task<OneTimeChallenge?> GetAsync(
        string selector,
        ChallengePurpose purpose,
        CancellationToken cancellationToken);
    Task<OneTimeChallenge?> GetForUpdateAsync(
        string selector,
        ChallengePurpose purpose,
        CancellationToken cancellationToken);

    Task InvalidateOutstandingAsync(
        Guid userId,
        ChallengePurpose purpose,
        DateTime now,
        CancellationToken cancellationToken);
}
