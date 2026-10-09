namespace UGTM.Modules.DefenseCouncil.Contracts.Grading;

public sealed record GradeComponent(string Code, decimal Score, decimal Weight, bool IsIgnoredInCalculation = false);

public sealed record CalculatedGrade(decimal FinalScore, decimal TotalWeight);

public interface IGradeCalculationService
{
    CalculatedGrade Calculate(IEnumerable<GradeComponent> components);
}
