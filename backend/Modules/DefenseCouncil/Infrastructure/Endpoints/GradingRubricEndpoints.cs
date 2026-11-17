using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.DefenseCouncil.Infrastructure.Persistence;

namespace UGTM.Modules.DefenseCouncil.Infrastructure.Endpoints;

public static class GradingRubricEndpoints
{
    public sealed record ReplaceGradingRubricRequest(IReadOnlyList<GradingCriterionInput>? Criteria);

    public static IEndpointRouteBuilder MapGradingRubricEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/batches/{batchId:guid}/grading-rubric")
            .RequireAuthorization().WithTags("DefenseCouncil.GradingRubric");
        group.MapGet("", async (Guid batchId, GradingRubricManager manager, CancellationToken token) =>
                (await manager.GetAsync(batchId, token)).ToHttpResult())
            .WithSummary("Đọc tiêu chí chấm của đợt")
            .Produces<ApiResponse<GradingRubricDto>>().Produces(401).Produces(403).Produces(404);
        group.MapPut("", async (Guid batchId, ReplaceGradingRubricRequest input, GradingRubricManager manager, CancellationToken token) =>
                (await manager.ReplaceAsync(batchId, input.Criteria, token)).ToHttpResult())
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
            .WithSummary("Cấu hình tiêu chí chấm có tổng tối đa bằng 10")
            .WithDescription("Faculty, AcademicAffairs hoặc Admin có thể thay toàn bộ tiêu chí trước khi chấm. Khi đã có chi tiết chấm, barem được khóa vĩnh viễn.")
            .Produces<ApiResponse<GradingRubricDto>>().Produces(400).Produces(401).Produces(403).Produces(404).Produces(409);
        return endpoints;
    }
}
