using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Application.FinalReportSubmissions.GetFinalReportHistory;

public sealed record GetFinalReportHistoryQuery(Guid GroupId,
    FinalReportVersionType VersionType = FinalReportVersionType.InitialFinal);
