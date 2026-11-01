using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using UGTM.Modules.DefenseCouncil.Application.ReviewerEvaluations;
using UGTM.Modules.DefenseCouncil.Contracts.ReviewerEvaluations;

namespace UGTM.Api.OpenApi;

internal sealed class ReviewerEvaluationSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type == typeof(ReviewerEvaluationDto) && schema.Properties is { } properties)
        {
            properties["status"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = ReviewerEvaluationOperationTransformer.Names("Draft", "Submitted") };
            properties["defenseApproval"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null, Enum = ReviewerEvaluationOperationTransformer.Names("Approved", "Rejected", null) };
        }
        if (context.JsonTypeInfo.Type == typeof(ReviewerDefenseEligibilityDto) && schema.Properties is { } gate)
            gate["reasonCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.String | JsonSchemaType.Null,
                Enum = ReviewerEvaluationOperationTransformer.Names([.. Enum.GetNames<ReviewerDefenseEligibilityReason>(), null])
            };
        if (context.JsonTypeInfo.Type == typeof(ReviewerFeedbackDto)) schema.AdditionalPropertiesAllowed = false;
        return Task.CompletedTask;
    }
}
