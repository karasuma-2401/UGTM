using System.Security.Cryptography;
using System.Text;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Identity.Contracts.Authorization;
using UGTM.Modules.ThesisManagement.Application.Common;
using UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Dtos;

namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.ConfirmTopicRegistration;

public sealed class ConfirmTopicRegistrationHandler(
    ITopicRegistrationStore store,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    public async Task<Result<ConfirmRegistrationResultDto>> HandleAsync(
        ConfirmTopicRegistrationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure<ConfirmRegistrationResultDto>(TopicRegistrationErrors.InvalidConfirmationToken);
        }

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(command.Token.Trim()))).ToLowerInvariant();

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var currentStudentId = currentUser.IsAuthenticated ? currentUser.UserId : null;

        return await store.ConfirmRegistrationAsync(tokenHash, currentStudentId, nowUtc, cancellationToken);
    }
}
