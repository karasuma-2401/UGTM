using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.BuildingBlocks.Domain;
using UGTM.Modules.Notifications.Application.Notifications.Dtos;
using UGTM.Modules.Notifications.Application.Notifications.Handlers;

namespace UGTM.Modules.Notifications.Infrastructure.Endpoints;

public static class NotificationEndpoints
{
    public sealed record RegisterDeviceRequest(
        string InstallationId,
        string Token,
        string? Platform = "web");

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("", GetMyNotificationsAsync)
            .WithSummary("Lấy danh sách thông báo của người dùng hiện tại")
            .Produces<ApiResponse<PagedResult<NotificationItemDto>>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/unread-count", GetUnreadCountAsync)
            .WithSummary("Đếm số thông báo chưa đọc của người dùng hiện tại")
            .Produces<ApiResponse<UnreadCountDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}/read", MarkAsReadAsync)
            .WithSummary("Đánh dấu một thông báo là đã đọc")
            .Produces<ApiResponse<object?>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<ApiResponse<object>>(StatusCodes.Status404NotFound);

        group.MapPatch("/read-all", MarkAllAsReadAsync)
            .WithSummary("Đánh dấu tất cả thông báo chưa đọc là đã đọc")
            .Produces<ApiResponse<MarkAllNotificationsReadDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/devices", RegisterDeviceAsync)
            .WithSummary("Đăng ký hoặc cập nhật FCM token của thiết bị hiện tại")
            .Produces<ApiResponse<DeviceRegistrationDto>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/devices/{installationId}", RevokeDeviceAsync)
            .WithSummary("Thu hồi FCM token của thiết bị khi đăng xuất")
            .Produces<ApiResponse<object?>>()
            .Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> GetMyNotificationsAsync(
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        [FromQuery] bool? unreadOnly,
        GetMyNotificationsHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(
            new GetMyNotificationsQuery(pageNumber ?? 1, pageSize ?? 20, unreadOnly ?? false),
            cancellationToken)).ToHttpResult();

    private static async Task<IResult> GetUnreadCountAsync(
        GetUnreadNotificationCountHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(cancellationToken)).ToHttpResult();

    private static async Task<IResult> MarkAsReadAsync(
        Guid id,
        MarkNotificationReadHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(id, cancellationToken)).ToHttpResult();

    private static async Task<IResult> MarkAllAsReadAsync(
        MarkAllNotificationsReadHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(cancellationToken)).ToHttpResult();

    private static async Task<IResult> RegisterDeviceAsync(
        RegisterDeviceRequest request,
        HttpContext httpContext,
        RegisterDeviceHandler handler,
        CancellationToken cancellationToken)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        return (await handler.HandleAsync(
            new RegisterDeviceCommand(
                request.InstallationId,
                request.Token,
                request.Platform,
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent),
            cancellationToken)).ToHttpResult();
    }

    private static async Task<IResult> RevokeDeviceAsync(
        string installationId,
        RevokeDeviceHandler handler,
        CancellationToken cancellationToken)
        => (await handler.HandleAsync(installationId, cancellationToken)).ToHttpResult();

    private static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess
            ? Results.Ok(ApiResponse<T>.Ok(result.Value))
            : ToErrorResult(result.Error);

    private static IResult ToHttpResult(this Result result)
        => result.IsSuccess
            ? Results.Ok(ApiResponse.Ok())
            : ToErrorResult(result.Error);

    private static IResult ToErrorResult(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Json(
            ApiResponse.Fail(ApiError.Create(error.Code, error.Message)),
            statusCode: statusCode);
    }
}
