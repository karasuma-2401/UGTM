namespace UGTM.Modules.DefenseCouncil.Domain.Grading;

public sealed record GradeContribution(string Code, decimal Score, decimal Weight, bool IsIgnoredInCalculation = false);

public sealed record GradeCalculationResult(decimal FinalScore, decimal TotalWeight, IReadOnlyList<GradeContribution> Contributions);
