using UGTM.Modules.DefenseCouncil.Application.Councils.Dtos;

namespace UGTM.Modules.DefenseCouncil.Application.Councils.GetCouncilsOverview;

public sealed record GetCouncilsOverviewQuery(
    Guid? BatchId = null,
    string? Keyword = null,
    string? Status = null,
    string? LockStatus = null,
    int PageNumber = 1,
    int PageSize = 50);
