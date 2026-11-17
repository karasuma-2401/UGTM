namespace UGTM.Modules.ThesisManagement.Application.TopicRegistrations.Expiration;

public interface ITopicRegistrationExpirationEnforcer
{
    Task<TopicRegistrationExpirationResult> EnforceAsync(CancellationToken cancellationToken = default);
}

public sealed record TopicRegistrationExpirationResult(int ExpiredCount, int BlockedCount);
