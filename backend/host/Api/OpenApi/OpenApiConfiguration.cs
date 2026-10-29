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
            options.AddDocumentTransformer<ModuleTagOrderingDocumentTransformer>();
            options.AddOperationTransformer<BearerSecurityOperationTransformer>();
            options.AddOperationTransformer<IdempotencyOperationTransformer>();
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

    private sealed class ModuleTagOrderingDocumentTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (document.Paths != null)
            {
                var pingPaths = document.Paths.Keys
                    .Where(path => path.Contains("/ping", StringComparison.OrdinalIgnoreCase)
                                   || path.Contains("/api/modules/", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var path in pingPaths)
                {
                    document.Paths.Remove(path);
                }
            }

            var allTags = (document.Paths?.Values.AsEnumerable() ?? Enumerable.Empty<IOpenApiPathItem>())
                .Where(p => p.Operations != null)
                .SelectMany(p => p.Operations.Values)
                .SelectMany(op => (IEnumerable<OpenApiTagReference>?)op.Tags ?? [])
                .Select(t => t.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .OrderBy(GetModuleOrder)
                .ThenBy(name => name)
                .Select(name => new OpenApiTag { Name = name! })
                .ToList();

            document.Tags = new HashSet<OpenApiTag>(allTags);

            return Task.CompletedTask;
        }

        private static int GetModuleOrder(string? tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return 100;
            if (tagName.StartsWith("Identity", StringComparison.OrdinalIgnoreCase)) return 10;
            if (tagName.StartsWith("AcademicPeriods", StringComparison.OrdinalIgnoreCase)) return 20;
            if (tagName.StartsWith("TopicCatalog", StringComparison.OrdinalIgnoreCase)) return 30;
            if (tagName.StartsWith("ThesisManagement", StringComparison.OrdinalIgnoreCase)) return 40;
            if (tagName.StartsWith("DefenseCouncil", StringComparison.OrdinalIgnoreCase)) return 50;
            if (tagName.StartsWith("Notification", StringComparison.OrdinalIgnoreCase)) return 60;
            return 100;
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

    private sealed class IdempotencyOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            var relativePath = context.Description.RelativePath ?? string.Empty;
            var isIdempotentEndpoint = relativePath.Contains("advisor-evaluations", StringComparison.OrdinalIgnoreCase)
                                       || relativePath.Contains("approval", StringComparison.OrdinalIgnoreCase);

            if (!isIdempotentEndpoint)
            {
                return Task.CompletedTask;
            }

            operation.Parameters ??= [];
            if (!operation.Parameters.Any(p => string.Equals(p.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase)))
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "Idempotency-Key",
                    In = ParameterLocation.Header,
                    Required = false,
                    Description = "UUID duy nhất chống gửi trùng lặp. Ở môi trường Development, nếu để trống server sẽ tự động sinh UUID mới.",
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Format = "uuid"
                    }
                });
            }

            return Task.CompletedTask;
        }
    }
}
