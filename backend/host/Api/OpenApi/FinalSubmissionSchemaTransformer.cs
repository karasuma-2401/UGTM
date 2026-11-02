using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

namespace UGTM.Api.OpenApi;

internal sealed class FinalSubmissionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(FinalSubmissionEndpoints.ConfirmFinalSubmissionRequest) && schema.Properties is { } properties)
        {
            schema.AdditionalPropertiesAllowed = false;
            schema.Required = new HashSet<string> { "action", "expectedSubmissionVersion", "expectedPackageVersion" };
            properties["action"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = ReviewerEvaluationOperationTransformer.Names("AdvisorConfirm", "FacultyConfirm", "RequestRevision") };
            properties["expectedSubmissionVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "1", Maximum = "4294967295", Description = "xmin của aggregate đã xem." };
            properties["expectedPackageVersion"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "1", Maximum = "2147483647" };
            properties["reason"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, MaxLength = 1000, Description = "RequestRevision: trim, 1–1000 Unicode runes. Confirm: null hoặc bỏ field." };
        }
        return Task.CompletedTask;
    }
}
