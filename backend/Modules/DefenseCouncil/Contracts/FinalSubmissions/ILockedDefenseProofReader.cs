namespace UGTM.Modules.DefenseCouncil.Contracts.FinalSubmissions;

public enum LockedDefenseOutcome { Succeeded, Missing, NotLocked, ContextInvalid }
public sealed record LockedDefenseProofContractDto(LockedDefenseOutcome Outcome, Guid GroupId, Guid BatchId,
    Guid? CouncilId, Guid? AssignmentId, Guid? MinuteId, uint? MinuteVersion,
    Guid? LockedBy, DateTime? LockedAtUtc, string? ProofSha256);

public interface ILockedDefenseProofReader
{
    Task<LockedDefenseProofContractDto> GetAsync(Guid groupId, CancellationToken token = default);
    Task<LockedDefenseProofContractDto> LockAsync(Guid groupId, CancellationToken token = default);
}
