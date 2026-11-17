using UGTM.Modules.AcademicPeriods.Domain.Entities;

namespace UGTM.Modules.AcademicPeriods.Application.Batches.Dtos;

public static class AcademicBatchMapper
{
    public static KltnBatchDetailDto ToDetail(KltnBatch batch)
        => new(
            batch.Id,
            batch.BatchName,
            batch.AcademicYear,
            batch.Semester,
            batch.StartDate,
            batch.EndDate,
            batch.TimeZoneId,
            batch.ScheduleTemplateVersionId,
            batch.Status.ToString(),
            batch.CreatedAt,
            batch.UpdatedAt,
            batch.Stages
                .OrderBy(stage => stage.OrderIndex)
                .Select(stage => new StageDto(
                    stage.Id,
                    stage.OrderIndex,
                    stage.StageCode,
                    stage.StageName,
                    stage.StartDate,
                    stage.EndDate,
                    stage.DeadlineAtUtc,
                    stage.IsOverridden))
                .ToArray());
}
