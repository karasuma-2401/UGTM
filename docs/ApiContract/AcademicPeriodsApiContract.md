# AcademicPeriods API contract

Trạng thái: phần quản lý đợt KLTN và lịch 7 giai đoạn (SCRUM-81) đã triển khai. API cấu hình điểm (SCRUM-83) chưa thuộc contract này.

Base route: `/api/academic-periods/batches`.

Tất cả endpoint yêu cầu Bearer token. Endpoint đọc yêu cầu `academic.batches.read`; endpoint ghi yêu cầu `academic.batches.manage`. Migration mặc định cấp quyền đọc cho sáu vai trò hệ thống và quyền ghi chỉ cho `Admin`, `AcademicAffairs`.

Response thành công dùng `ApiResponse<T>`. Response lỗi có error `code`, `message`; mã HTTP là `400` cho input/domain value không hợp lệ, `401` khi chưa xác thực, `403` khi thiếu quyền, `404` khi không tìm thấy và `409` cho xung đột trạng thái hoặc dữ liệu.

## Endpoints

| Method | Route | Permission | Response |
| --- | --- | --- | --- |
| `POST` | `/` | `academic.batches.manage` | `KltnBatchDetailDto` |
| `GET` | `/` | `academic.batches.read` | `PagedResult<KltnBatchSummaryDto>` |
| `GET` | `/{batchId}` | `academic.batches.read` | `KltnBatchDetailDto` |
| `PUT` | `/{batchId}/schedule` | `academic.batches.manage` | `KltnBatchDetailDto` |
| `POST` | `/{batchId}/open` | `academic.batches.manage` | `KltnBatchDetailDto` |
| `POST` | `/{batchId}/close` | `academic.batches.manage` | `KltnBatchDetailDto` |
| `POST` | `/{batchId}/archive` | `academic.batches.manage` | `KltnBatchDetailDto` |
| `GET` | `/{batchId}/timeline` | `academic.batches.read` | `KltnBatchTimelineDto` |
| `GET` | `/{batchId}/current-stage` | `academic.batches.read` | `CurrentStageResultDto` |

## Tạo đợt

`POST /api/academic-periods/batches`

```json
{
  "batchName": "KLTN HK1 2026-2027",
  "academicYear": "2026-2027",
  "semester": 1,
  "startDate": "2026-09-07",
  "templateCode": "UIT_KLTN_STANDARD"
}
```

`templateCode` có thể bỏ trống và mặc định là `UIT_KLTN_STANDARD`. Server lấy template version đang active, tính `endDate` và materialize đúng 7 stage; client không truyền `endDate`. Một cặp năm học/học kỳ chỉ được tạo một lần qua use case này.

## Danh sách và chi tiết

`GET /api/academic-periods/batches` hỗ trợ query `academicYear`, `semester`, `status`, `pageNumber`, `pageSize`. `status` nhận `Draft`, `Open`, `Closed`, `Archived`; page mặc định `1/20` và page size tối đa 100.

DTO chi tiết chứa thông tin batch và danh sách `stages` theo `orderIndex`. Ngày dùng chuỗi ISO `yyyy-MM-dd`; deadline là thời điểm UTC.

## Cập nhật lịch

`PUT /api/academic-periods/batches/{batchId}/schedule`

```json
{
  "changeReason": "Điều chỉnh theo lịch nghỉ của trường",
  "stages": [
    {
      "orderIndex": 1,
      "stageCode": "TOPIC_PREPARATION",
      "stageName": "Chuẩn bị đề tài",
      "startDate": "2026-09-07",
      "endDate": "2026-09-13",
      "deadlineAtUtc": "2026-09-13T16:59:00Z"
    }
  ]
}
```

Request thực tế phải gửi nguyên bộ đúng 7 stage. Stage phải có order liên tục 1–7, code duy nhất, phủ kín khoảng batch, không chồng lấn/không có khoảng trống và deadline phải nằm trong stage theo timezone của batch. `changeReason` bắt buộc khi batch đang `Open`. Batch `Closed` hoặc `Archived` không cho sửa lịch.

## Vòng đời

Transition hợp lệ duy nhất là `Draft -> Open -> Closed -> Archived`. Mở batch yêu cầu đủ lịch và có một score configuration `Active` thuộc chính batch. Mọi lệnh thành công ghi audit outbox và dữ liệu domain trong cùng lần `SaveChanges`.

## Timeline và current stage

Timeline trả `state` của từng stage là `Upcoming`, `Current` hoặc `Completed`, được tính theo `TimeZoneId` của batch (`Asia/Ho_Chi_Minh` với template mặc định) và khoảng ngày đóng `[StartDate, EndDate]`.

Current-stage trả `currentStage: null` khi ngày địa phương nằm ngoài toàn bộ lịch. Khi có stage hiện tại, response gồm `isDeadlinePassed` và `remainingSeconds`; thời gian còn lại không âm và bằng `0` sau deadline.

## Error codes

- `AcademicBatch.AccessDenied`
- `AcademicBatch.NotFound`
- `AcademicBatch.ActiveTemplateNotFound`
- `AcademicBatch.InvalidRequest`
- `AcademicBatch.DuplicateAcademicPeriod`
- `AcademicBatch.InvalidStatusTransition`
- `AcademicBatch.ScoreConfigurationRequired`
