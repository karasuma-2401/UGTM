using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.Library;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class ThesisLibraryEndpoints
{
    public static IEndpointRouteBuilder MapThesisLibraryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/library/theses").WithTags("ThesisManagement.Library");
        group.MapGet("", (HttpRequest request, IThesisLibraryCatalog catalog, TimeProvider clock) =>
        {
            request.HttpContext.Response.Headers.CacheControl = "no-store";
            string[] allowed = ["q", "year", "semester", "department", "sort", "page", "pageSize"];
            if (request.Query.Any(q => !allowed.Contains(q.Key, StringComparer.Ordinal) || q.Value.Count != 1)
                || !Number(request, "page", 1, out var page) || !Number(request, "pageSize", 12, out var size))
                return Invalid();
            return ThesisLibrarySearch.Search(catalog, new(request.Query["q"], request.Query["year"], request.Query["semester"],
                request.Query["department"], request.Query["sort"].FirstOrDefault() ?? "newest", page, size), clock.GetUtcNow()).ToHttpResult();
        }).AllowAnonymous().WithSummary("Tra cứu metadata khóa luận được chọn công bố, không trả URL tài liệu");
        group.MapGet("/{slug}", (string slug, HttpContext context, IThesisLibraryCatalog catalog, TimeProvider clock) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var item = ThesisLibrarySearch.Published(catalog, clock.GetUtcNow()).SingleOrDefault(p => p.Slug == slug);
            return item is null ? Results.NotFound(ApiResponse<object>.Fail(ApiError.Create(ThesisLibrarySearch.NotFound.Code, ThesisLibrarySearch.NotFound.Message)))
                : Results.Ok(ApiResponse<PublicThesisDto>.Ok(item.ToPublic()));
        }).AllowAnonymous().WithSummary("Thông tin khóa luận công khai, không chứa định danh hồ sơ lưu chiểu");
        group.MapGet("/{slug}/document", async (string slug, HttpContext context, ThesisLibraryDocumentHandler handler, CancellationToken token) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return (await handler.HandleAsync(slug, token)).ToHttpResult();
        }).RequireAuthorization().WithSummary("Tài khoản Active đọc PDF đã được duyệt công bố; URL private hết hạn sau 5 phút");
        return endpoints;
    }
    private static bool Number(HttpRequest request, string name, int fallback, out int value)
    {
        value = fallback;
        return !request.Query.ContainsKey(name) || int.TryParse(request.Query[name], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
    private static IResult Invalid() => Results.BadRequest(ApiResponse<object>.Fail(ApiError.Create(ThesisLibrarySearch.InvalidQuery.Code, ThesisLibrarySearch.InvalidQuery.Message)));
}
