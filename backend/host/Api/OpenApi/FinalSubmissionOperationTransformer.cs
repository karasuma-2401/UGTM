using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UGTM.Api.OpenApi;

internal sealed class FinalSubmissionOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var path = context.Description.RelativePath?.Trim('/');
        if (path is null || !path.StartsWith("api/thesis/final-submissions", StringComparison.Ordinal)) return Task.CompletedTask;
        if (context.Description.HttpMethod == "POST")
            Add("Idempotency-Key", JsonSchemaType.String, true, "uuid", ParameterLocation.Header);
        if (context.Description.HttpMethod == "GET")
        {
            if (path.EndsWith("/my-group", StringComparison.Ordinal) || path.EndsWith("/submission-eligibility", StringComparison.Ordinal))
                Add("groupId", JsonSchemaType.String, true, "uuid");
            if (path == "api/thesis/final-submissions")
            {
                Add("batchId", JsonSchemaType.String, false, "uuid");
                Add("status", JsonSchemaType.String);
                Add("searchTerm", JsonSchemaType.String);
                Add("pageNumber", JsonSchemaType.Integer, false, "int32");
                Add("pageSize", JsonSchemaType.Integer, false, "int32");
            }
            foreach (var code in new[] { "400", "401", "403", "404" })
            {
                operation.Responses ??= new();
                operation.Responses.TryAdd(code, new OpenApiResponse
                {
                    Description = code switch
                    { "400" => "Invalid selector/query", "401" => "Authentication required", "403" => "Access denied", _ => "Submission/file not found" }
                });
            }
        }
        return Task.CompletedTask;
        void Add(string name, JsonSchemaType type, bool required = false, string? format = null, ParameterLocation location = ParameterLocation.Query)
        {
            operation.Parameters ??= [];
            if (operation.Parameters.Any(p => p.Name == name && p.In == location)) return;
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = name,
                In = location,
                Required = required,
                Description = "Selector được kiểm tra trên server; field trùng/sai kiểu bị 400.",
                Schema = new OpenApiSchema { Type = type, Format = format }
            });
        }
    }
}
