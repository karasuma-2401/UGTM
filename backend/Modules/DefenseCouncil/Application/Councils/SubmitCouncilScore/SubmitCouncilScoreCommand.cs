using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilScore;

public sealed record SubmitCouncilScoreCommand(
    Guid AssignmentId,
    IReadOnlyList<CriterionScoreInputDto> Details,
    string? Feedback = null);
