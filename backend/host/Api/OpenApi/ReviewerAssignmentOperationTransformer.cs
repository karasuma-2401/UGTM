using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UGTM.Api.OpenApi;

// These handlers parse HttpRequest themselves to keep strict, consistent 400 responses.
// Declare the same public inputs here because ApiExplorer cannot infer those query/header fields.
internal sealed class ReviewerAssignmentOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath?.Trim('/').Replace("{id:guid}", "{id}", StringComparison.OrdinalIgnoreCase);
        var method = context.Description.HttpMethod;
        if (method is "POST" or "DELETE" &&
            (path == "api/thesis/reviewer-assignments" || path == "api/thesis/reviewer-assignments/{id}"))
        {
            Add("Idempotency-Key", JsonSchemaType.String, "UUID không rỗng, bắt buộc ở mọi môi trường; retry cùng key và body.",
                required: true, format: "uuid", location: ParameterLocation.Header);
        }
        if (method != "GET") return Task.CompletedTask;
        switch (path)
        {
            case "api/thesis/reviewer-assignments":
                GuidQuery("batchId"); GuidQuery("groupId"); GuidQuery("reviewerId");
                Add("isActive", JsonSchemaType.Boolean, "Nếu có, ưu tiên hơn includeHistory; mặc định chỉ active.");
                Add("includeHistory", JsonSchemaType.Boolean, "Mặc định false; true bao gồm lịch sử khi không truyền isActive.");
                Paging("Tìm literal trong tên đề tài hoặc tên/mã giảng viên, kể cả tài khoản đã ngừng hoạt động.");
                break;
            case "api/thesis/reviewer-assignment-eligibility":
                GuidQuery("batchId", true);
                Add("includeAssigned", JsonSchemaType.Boolean, "Mặc định false; true bao gồm nhóm đủ điều kiện đã có phân công active.");
                Paging("Tìm literal trong tên đề tài.");
                break;
            case "api/thesis/reviewer-candidates":
                GuidQuery("groupId", true);
                Paging("Tìm literal trong tên hoặc mã giảng viên Active có Lecturer, khác GVHD hiện tại.");
                break;
            case "api/thesis/me/reviewer-assignments":
                GuidQuery("batchId");
                Paging("Tìm literal trong tên đề tài. Chỉ active của chính mình; reviewerId/includeHistory/isActive bị từ chối.");
                break;
        }
        return Task.CompletedTask;

        void GuidQuery(string name, bool required = false)
            => Add(name, JsonSchemaType.String, "UUID không rỗng.", required, "uuid");
        void Paging(string keywordDescription)
        {
            Add("keyword", JsonSchemaType.String, keywordDescription + " Trim, tối đa 200 ký tự.");
            Add("pageNumber", JsonSchemaType.Integer, "Mặc định 1; giá trị dưới 1 được đưa về 1.", format: "int32");
            Add("pageSize", JsonSchemaType.Integer, "Mặc định 20; giới hạn vào khoảng 1–100.", format: "int32");
        }
        void Add(string name, JsonSchemaType type, string description, bool required = false,
            string? format = null, ParameterLocation location = ParameterLocation.Query)
        {
            operation.Parameters ??= [];
            if (operation.Parameters.Any(parameter => parameter.Name == name && parameter.In == location)) return;
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = name,
                In = location,
                Required = required,
                Description = description,
                Schema = new OpenApiSchema { Type = type, Format = format }
            });
        }
    }
}
