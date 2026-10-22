using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.ReviewGateConfigurations.UpsertReviewGateConfiguration;

public sealed record UpsertReviewGateConfigurationCommand(
    Guid BatchId,
    ReviewGateType GateType,
    DateTime InitialSubmissionDeadlineUtc,
    DateTime FacultyResponseDeadlineUtc,
    DateTime FinalClosureDeadlineUtc);
