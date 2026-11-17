namespace UGTM.Modules.DefenseCouncil.Application.Councils.SubmitCouncilToAcademicAffairs;

public sealed record SubmitCouncilToAcademicAffairsCommand(
    Guid CouncilId,
    string? Notes = null);
