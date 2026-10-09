using UGTM.Modules.DefenseCouncil.Contracts.Grading;
using UGTM.Modules.DefenseCouncil.Domain.Grading;

namespace UGTM.Modules.DefenseCouncil.Application.Grading;

public sealed class GradeCalculationService(ThesisGradeCalculator calculator) : IGradeCalculationService
{
    public CalculatedGrade Calculate(IEnumerable<GradeComponent> components)
    {
        ArgumentNullException.ThrowIfNull(components);
        var result = calculator.Calculate(components.Select(x =>
            new GradeContribution(x.Code, x.Score, x.Weight, x.IsIgnoredInCalculation)));

        return new CalculatedGrade(result.FinalScore, result.TotalWeight);
    }
}
