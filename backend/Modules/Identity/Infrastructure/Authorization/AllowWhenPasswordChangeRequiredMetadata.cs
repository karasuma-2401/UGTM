using Microsoft.AspNetCore.Builder;

namespace UGTM.Modules.Identity.Infrastructure.Authorization;

public sealed class AllowWhenPasswordChangeRequiredMetadata;

public static class PasswordChangeRequiredEndpointConventionBuilderExtensions
{
    private static readonly AllowWhenPasswordChangeRequiredMetadata Metadata = new();

    public static TBuilder AllowWhenPasswordChangeRequired<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(Metadata));
        return builder;
    }
}
