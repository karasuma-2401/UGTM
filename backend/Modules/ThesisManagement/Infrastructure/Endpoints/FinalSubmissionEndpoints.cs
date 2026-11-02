using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UGTM.BuildingBlocks.Application.Responses;
using UGTM.Modules.ThesisManagement.Application.FinalSubmissions;
using UGTM.Modules.ThesisManagement.Domain.Enums;

namespace UGTM.Modules.ThesisManagement.Infrastructure.Endpoints;

public static class FinalSubmissionEndpoints
{
    public const long MaximumRequestSizeBytes = 40 * 1024 * 1024 + 256 * 1024;
    public static IEndpointRouteBuilder MapFinalSubmissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/thesis/final-submissions").RequireAuthorization().WithTags("ThesisManagement.FinalSubmission");
        group.MapPut("/{id}/confirm-archive", ConfirmAsync)
            .Accepts<ConfirmFinalSubmissionRequest>("application/json")
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024))
            .WithSummary("GVHD/Khoa xác nhận đúng package hoặc yêu cầu sửa có lý do")
            .WithDescription("Action AdvisorConfirm, FacultyConfirm hoặc RequestRevision; không suy luận bước từ role. Idempotency-Key UUID bắt buộc; expectedSubmissionVersion là xmin và expectedPackageVersion là version đã xem. GVHD hiện tại/Admin bước 1, Faculty + manage/Admin bước 2. Đợt Open hoặc Closed. Hai xác nhận cùng package/manifest và Locked defense proof; ArchiveConfirmed khóa ý định mới. Replay vẫn kiểm tra quyền hiện tại, trả kết quả gốc.")
            .Produces<ApiResponse<FinalSubmissionConfirmationDto>>()
            .Produces<ApiResponse<object>>(400).Produces(401).Produces<ApiResponse<object>>(403)
            .Produces<ApiResponse<object>>(404).Produces<ApiResponse<object>>(409).Produces(413).Produces(415);
        group.MapPost("", SubmitAsync).DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestSizeBytes))
            .Accepts<SubmitFinalSubmissionRequest>("multipart/form-data")
            .WithSummary("Nộp/nộp lại package lưu chiểu PDF + DOCX có version")
            .WithDescription("Student Active/Confirmed đúng groupId; Idempotency-Key UUID bắt buộc. Đợt Open, trong ARCHIVAL_AND_PUBLICATION, có proof biên bản Locked. Mỗi file ≤20 MiB, đủ Abstract/Keywords Việt–Anh. expectedSubmissionVersion là xmin khi đã có aggregate. Submitted/NeedRevision cho nộp lại nguyên gói; AdvisorConfirmed/ArchiveConfirmed chặn ý định mới. Revalidate dưới khóa sau upload. Thành công lần đầu 201, nộp lại 200; replay trả DTO/status gốc. 400 sai form/file; 401/403 quyền; 409 state/version/context/key; 413 body quá lớn; 415 sai content type.")
            .Produces<ApiResponse<FinalSubmissionDto>>(201).Produces<ApiResponse<FinalSubmissionDto>>(200)
            .Produces<ApiResponse<object>>(400).Produces(401).Produces<ApiResponse<object>>(403)
            .Produces<ApiResponse<object>>(409).Produces(413).Produces(415);
        group.MapGet("/my-group", async (HttpRequest request, FinalSubmissionReadHandler handler, CancellationToken token) =>
            !Query(request, ["groupId"]) || !Id(request.Query["groupId"], out var id) ? Invalid() : (await handler.OwnAsync(id, token)).ToHttpResult())
            .WithSummary("Student xem hồ sơ của groupId được chọn").Produces<ApiResponse<FinalSubmissionDto>>();
        group.MapGet("/submission-eligibility", async (HttpRequest request, FinalSubmissionReadHandler handler, CancellationToken token) =>
            !Query(request, ["groupId"]) || !Id(request.Query["groupId"], out var id) ? Invalid() : (await handler.EligibilityAsync(id, token)).ToHttpResult())
            .WithSummary("Student xem điều kiện và xmin hiện tại, không cấp proof để ghi").Produces<ApiResponse<FinalSubmissionEligibilityDto>>();
        group.MapGet("/{id}", async (string id, HttpRequest request, FinalSubmissionReadHandler handler, CancellationToken token) =>
            !Query(request, []) || !Id(id, out var parsed) ? Invalid() : (await handler.DetailAsync(parsed, token)).ToHttpResult())
            .WithSummary("Đọc hồ sơ theo ownership hoặc quyền quản trị").Produces<ApiResponse<FinalSubmissionDto>>();
        group.MapGet("/{id}/packages", async (string id, HttpRequest request, FinalSubmissionReadHandler handler, CancellationToken token) =>
            !Query(request, []) || !Id(id, out var parsed) ? Invalid() : (await handler.PackagesAsync(parsed, token)).ToHttpResult())
            .WithSummary("Lịch sử package và review, version giảm dần").Produces<ApiResponse<IReadOnlyList<FinalSubmissionPackageDto>>>();
        group.MapGet("/{id}/packages/{packageVersion}/files/{fileRole}/download", async (string id, string packageVersion, string fileRole,
            HttpRequest request, FinalSubmissionReadHandler handler, CancellationToken token) =>
            !Query(request, []) || !Id(id, out var parsed) || !Positive(packageVersion, out var version) ? Invalid()
                : (await handler.DownloadAsync(parsed, version, fileRole, token)).ToHttpResult())
            .WithSummary("Kiểm tra quyền rồi cấp URL tải private TTL 300 giây").Produces<ApiResponse<FinalSubmissionDownloadDto>>();
        group.MapGet("", ListAsync).WithSummary("Faculty/PĐT/Admin hoặc GVHD xem danh sách trong scope")
            .Produces<ApiResponse<FinalSubmissionPageDto>>();
        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(HttpRequest request, FinalSubmissionAccess access,
        SubmitFinalSubmissionHandler handler, CancellationToken token)
    {
        if (access.AuthenticationError is { } error) return Failure(error.Code, error.Message, 401);
        if (!FinalSubmissionAccess.HasRole(await access.ActorAsync(false, token), "Student")) return Failure(FinalSubmissionErrors.AccessDenied.Code, FinalSubmissionErrors.AccessDenied.Message, 403);
        if (request.Query.Count != 0 || request.Headers["Idempotency-Key"].Count != 1 || !Id(request.Headers["Idempotency-Key"], out var key)) return Invalid();
        if (!request.HasFormContentType || !request.ContentType!.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)) return Results.StatusCode(415);
        if (request.ContentLength > MaximumRequestSizeBytes) return Results.StatusCode(413);
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
            bodyLimit.MaxRequestBodySize = MaximumRequestSizeBytes;
        request.HttpContext.Features.Set<IFormFeature>(new FormFeature(request, new FormOptions
        {
            MultipartBodyLengthLimit = MaximumRequestSizeBytes,
            ValueLengthLimit = 100_000,
            MultipartHeadersLengthLimit = 16 * 1024,
            ValueCountLimit = 8
        }));
        IFormCollection form;
        try { form = await request.ReadFormAsync(token); }
        catch (BadHttpRequestException e) { return e.StatusCode == 413 ? Results.StatusCode(413) : Invalid(); }
        catch (Exception e) when (e is InvalidDataException or IOException) { return Invalid(); }
        string[] fields = ["groupId", "expectedSubmissionVersion", "abstractVi", "abstractEn", "keywordsVi", "keywordsEn"];
        if (form.Keys.Any(k => !fields.Contains(k, StringComparer.OrdinalIgnoreCase) || form[k].Count != 1)
            || !Id(form["groupId"], out var groupId)) return Invalid();
        uint? expected = null;
        if (form.TryGetValue("expectedSubmissionVersion", out var raw) && !string.IsNullOrEmpty(raw))
        {
            if (!uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value == 0) return Invalid();
            expected = value;
        }
        var pdf = form.Files.Where(f => f.Name == "pdfFile").ToArray();
        var docx = form.Files.Where(f => f.Name == "docxFile").ToArray();
        if (form.Files.Count != 2 || pdf.Length != 1 || docx.Length != 1) return Invalid();
        if (pdf[0].Length > 20 * 1024 * 1024 || docx[0].Length > 20 * 1024 * 1024) return Results.StatusCode(413);
        await using var pdfStream = pdf[0].OpenReadStream();
        await using var docxStream = docx[0].OpenReadStream();
        var result = await handler.HandleAsync(new(groupId, expected, key, form["abstractVi"], form["abstractEn"],
            form["keywordsVi"], form["keywordsEn"], pdf[0].FileName, pdfStream, docx[0].FileName, docxStream), token);
        return result.IsFailure ? result.ToHttpResult() : Results.Json(ApiResponse<FinalSubmissionDto>.Ok(result.Value.Data), statusCode: result.Value.StatusCode);
    }
    private static async Task<IResult> ConfirmAsync(string id, HttpRequest request, ConfirmFinalSubmissionHandler handler, CancellationToken token)
    {
        if (!await handler.CanReviewAsync(token)) return Failure(FinalSubmissionErrors.AccessDenied.Code, FinalSubmissionErrors.AccessDenied.Message, 403);
        if (!Id(id, out var submission) || request.Query.Count != 0 || request.Headers["Idempotency-Key"].Count != 1
            || !Id(request.Headers["Idempotency-Key"], out var key)) return Invalid();
        if (!request.HasJsonContentType()) return Results.StatusCode(415);
        if (request.ContentLength > 16 * 1024) return Results.StatusCode(413);
        ConfirmFinalSubmissionCommand command;
        try
        {
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await request.Body.ReadAsync(buffer, token)) > 0)
            {
                if (body.Length + read > 16 * 1024) return Results.StatusCode(413);
                await body.WriteAsync(buffer.AsMemory(0, read), token);
            }
            using var json = JsonDocument.Parse(body.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            if (json.RootElement.ValueKind != JsonValueKind.Object) return Invalid();
            var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in json.RootElement.EnumerateObject())
                if (!new[] { "action", "expectedSubmissionVersion", "expectedPackageVersion", "reason" }.Contains(p.Name, StringComparer.OrdinalIgnoreCase)
                    || !fields.TryAdd(p.Name, p.Value)) return Invalid();
            if (!fields.TryGetValue("action", out var a) || a.ValueKind != JsonValueKind.String
                || !Enum.TryParse<FinalSubmissionReviewAction>(a.GetString(), false, out var action) || !Enum.IsDefined(action)
                || a.GetString() != action.ToString()
                || !fields.TryGetValue("expectedSubmissionVersion", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetUInt32(out var version) || version == 0
                || !fields.TryGetValue("expectedPackageVersion", out var pversion) || pversion.ValueKind != JsonValueKind.Number || !pversion.TryGetInt32(out var package) || package < 1)
                return Invalid();
            string? reason = null;
            if (fields.TryGetValue("reason", out var r))
            {
                if (r.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) return Invalid();
                reason = r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            }
            command = new(submission, action, version, package, key, reason);
        }
        catch (BadHttpRequestException e) { return e.StatusCode == 413 ? Results.StatusCode(413) : Invalid(); }
        catch (JsonException) { return Invalid(); }
        catch (InvalidOperationException) { return Invalid(); }
        return (await handler.HandleAsync(command, token)).ToHttpResult();
    }
    public sealed record ConfirmFinalSubmissionRequest(string Action, uint ExpectedSubmissionVersion, int ExpectedPackageVersion, string? Reason);
    private static async Task<IResult> ListAsync(HttpRequest request, FinalSubmissionReadHandler handler, CancellationToken token)
    {
        if (!Query(request, ["batchId", "status", "searchTerm", "pageNumber", "pageSize"])) return Invalid();
        Guid? batch = null;
        if (request.Query.ContainsKey("batchId")) { if (!Id(request.Query["batchId"], out var id)) return Invalid(); batch = id; }
        var page = 1;
        var size = 20;
        if (request.Query.ContainsKey("pageNumber") && !Positive(request.Query["pageNumber"], out page)) return Invalid();
        if (request.Query.ContainsKey("pageSize") && !Positive(request.Query["pageSize"], out size)) return Invalid();
        return (await handler.ListAsync(batch, request.Query["status"].FirstOrDefault(), request.Query["searchTerm"].FirstOrDefault(), page, size, token)).ToHttpResult();
    }
    private static bool Query(HttpRequest request, string[] allowed) => !(request.ContentLength > 0) && !request.Headers.ContainsKey("Transfer-Encoding")
        && request.Query.All(q => allowed.Contains(q.Key, StringComparer.Ordinal) && q.Value.Count == 1);
    private static bool Id(string? value, out Guid id) => Guid.TryParse(value, out id) && id != Guid.Empty;
    private static bool Positive(string? value, out int number) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;
    private static IResult Invalid() => Failure(FinalSubmissionErrors.InvalidRequest.Code, FinalSubmissionErrors.InvalidRequest.Message, 400);
    private static IResult Failure(string code, string message, int status) => Results.Json(ApiResponse<object>.Fail(ApiError.Create(code, message)), statusCode: status);
    public sealed record SubmitFinalSubmissionRequest(Guid GroupId, uint? ExpectedSubmissionVersion,
        string AbstractVi, string AbstractEn, string KeywordsVi, string KeywordsEn, IFormFile PdfFile, IFormFile DocxFile);
}
