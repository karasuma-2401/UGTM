using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UGTM.Api.OpenApi;

public static class OpenApiConfiguration
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddUgtmOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecurityDocumentTransformer>();
            options.AddOperationTransformer<BearerSecurityOperationTransformer>();
        });

        return services;
    }

    private sealed class BearerSecurityDocumentTransformer(
        IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            document.Info ??= new OpenApiInfo();
            document.Info.Title = "UGTM API";
            document.Info.Version = context.DocumentName;

            var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
            if (authenticationSchemes.All(scheme => scheme.Name != BearerScheme))
            {
                return;
            }

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??=
                new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT access token returned by the login endpoint."
            };
        }
    }

    private sealed class BearerSecurityOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();
            var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
            if (allowsAnonymous || !requiresAuthorization)
            {
                return Task.CompletedTask;
            }

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = []
            });

            return Task.CompletedTask;
        }
    }
}
