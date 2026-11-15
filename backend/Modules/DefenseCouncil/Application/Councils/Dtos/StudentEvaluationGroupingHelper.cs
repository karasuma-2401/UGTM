using UGTM.Modules.DefenseCouncil.Domain.Entities;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

public static class StudentEvaluationGroupingHelper
{
    public static IReadOnlyList<CriterionScoreInputDto>? NormalizeInputs(
        IReadOnlyList<CriterionScoreInputDto>? details,
        IReadOnlyList<StudentCriterionScoresInputDto>? studentScores)
    {
        if (studentScores is { Count: > 0 })
        {
            var flattened = new List<CriterionScoreInputDto>();
            if (details is { Count: > 0 })
            {
                flattened.AddRange(details);
            }

            foreach (var student in studentScores)
            {
                if (student?.Details is null)
                {
                    continue;
                }

                foreach (var detail in student.Details)
                {
                    if (detail is not null)
                    {
                        flattened.Add(new CriterionScoreInputDto(detail.CriteriaId, detail.Score, student.StudentId));
                    }
                }
            }

            return flattened;
        }

        return details;
    }

    public static bool HasDuplicateCriteriaPerScope(IReadOnlyList<CriterionScoreInputDto> details)
    {
        var seen = new HashSet<(Guid? StudentId, Guid CriteriaId)>();
        foreach (var d in details)
        {
            var normalizedStudentId = d.StudentId is { } sid && sid != Guid.Empty ? sid : (Guid?)null;
            if (!seen.Add((normalizedStudentId, d.CriteriaId)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasMixedStudentAndGroupLevelDetails(IReadOnlyList<CriterionScoreInputDto> details)
    {
        var hasStudent = details.Any(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty);
        var hasNullStudent = details.Any(d => !d.StudentId.HasValue || d.StudentId.Value == Guid.Empty);
        return hasStudent && hasNullStudent;
    }

    public static bool HasInvalidStudentId(IReadOnlyList<CriterionScoreInputDto> details)
        => details.Any(d => d.StudentId.HasValue && d.StudentId.Value == Guid.Empty);

    public static bool AllCriteriaCompletedForEachScope(
        IReadOnlyList<CriterionScoreInputDto> details,
        int requiredCriteriaCount,
        IReadOnlyCollection<Guid>? requiredStudentIds = null)
    {
        if (requiredCriteriaCount <= 0)
        {
            return true;
        }

        var hasStudent = details.Any(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty);
        if (!hasStudent)
        {
            return details.Count == requiredCriteriaCount;
        }

        var byStudent = details
            .Where(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty)
            .GroupBy(d => d.StudentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CriteriaId).Distinct().Count());

        if (byStudent.Count == 0)
        {
            return false;
        }

        if (requiredStudentIds is { Count: > 0 } && !requiredStudentIds.All(id => byStudent.ContainsKey(id)))
        {
            return false;
        }

        return byStudent.Values.All(count => count == requiredCriteriaCount);
    }

    public static decimal ComputeAggregateScore(IReadOnlyList<CriterionScoreInputDto> details)
    {
        if (details.Count == 0)
        {
            return 0m;
        }

        var studentGroups = details
            .Where(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty)
            .GroupBy(d => d.StudentId!.Value)
            .ToList();

        if (studentGroups.Count > 0)
        {
            var studentTotals = studentGroups
                .Select(g => Math.Round(g.Sum(x => x.Score), 2, MidpointRounding.AwayFromZero))
                .ToList();
            return Math.Round(studentTotals.Average(), 2, MidpointRounding.AwayFromZero);
        }

        return Math.Round(details.Sum(x => x.Score), 2, MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyDictionary<Guid, decimal> ComputeStudentTotalsFromEntities(
        IEnumerable<EvaluationDetail> details)
    {
        return details
            .Where(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty)
            .GroupBy(d => d.StudentId!.Value)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Sum(x => x.Score), 2, MidpointRounding.AwayFromZero));
    }

    public static IReadOnlyList<StudentEvaluationScoreDto>? BuildStudentScores(
        IReadOnlyList<EvaluationDetailResponseDto>? details,
        int totalCriteriaCount = 0)
    {
        if (details is not { Count: > 0 })
        {
            return null;
        }

        var studentGroups = details
            .Where(d => d.StudentId.HasValue && d.StudentId.Value != Guid.Empty)
            .GroupBy(d => d.StudentId!.Value)
            .OrderBy(g => g.Key)
            .ToList();

        if (studentGroups.Count == 0)
        {
            return null;
        }

        var resolvedTotalCriteria = totalCriteriaCount > 0
            ? totalCriteriaCount
            : studentGroups.Max(g => g.Count());

        return studentGroups
            .Select(g =>
            {
                var studentDetails = g.ToList();
                var totalScore = Math.Round(studentDetails.Sum(x => x.Score), 2, MidpointRounding.AwayFromZero);
                return new StudentEvaluationScoreDto(
                    g.Key,
                    totalScore,
                    studentDetails.Count,
                    resolvedTotalCriteria,
                    studentDetails);
            })
            .ToList();
    }
}
