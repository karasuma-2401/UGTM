using UGTM.Modules.ThesisManagement.Domain.Entities;

namespace UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.Dtos;

public sealed record ReviewGateConfigurationDto(
    Guid Id,
    Guid BatchId,
    string GateType,
    DateTime InitialSubmissionDeadlineUtc,
    DateTime FacultyResponseDeadlineUtc,
    DateTime FinalClosureDeadlineUtc,
    Guid CreatedBy,
    DateTime CreatedAtUtc,
    Guid UpdatedBy,
    DateTime UpdatedAtUtc);

public static class ReviewGateConfigurationMapper
{
    public static ReviewGateConfigurationDto ToDto(ReviewGateConfiguration configuration)
        => new(
            configuration.Id,
            configuration.BatchId,
            configuration.GateType.ToString(),
            configuration.InitialSubmissionDeadlineUtc,
            configuration.FacultyResponseDeadlineUtc,
            configuration.FinalClosureDeadlineUtc,
            configuration.CreatedBy,
            configuration.CreatedAtUtc,
            configuration.UpdatedBy,
            configuration.UpdatedAtUtc);
}
