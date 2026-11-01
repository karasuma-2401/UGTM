using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UGTM.Modules.Identity.Infrastructure.Authentication;

internal static class AntiforgeryValidationHelper
{
    public static async Task ValidateRequestSafelyAsync(this IAntiforgery antiforgery, HttpContext httpContext)
    {
        var env = httpContext.RequestServices.GetService<IHostEnvironment>();
        // Trong môi trường Development, nếu không truyền header X-CSRF-TOKEN (hoặc để trống)
        // thì bỏ qua kiểm tra để thuận tiện khi test API trên Scalar/Postman
        if (env?.IsDevelopment() == true && string.IsNullOrWhiteSpace(httpContext.Request.Headers["X-CSRF-TOKEN"]))
        {
            return;
        }

        await antiforgery.ValidateRequestAsync(httpContext);
    }
}

