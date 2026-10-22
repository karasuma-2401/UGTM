# AcademicPeriods API contract

Trạng thái: quản lý đợt KLTN/lịch 7 giai đoạn (SCRUM-81) và cấu hình điểm có version (SCRUM-83) đã triển khai.

Base route: `/api/academic-periods/batches`.

Tất cả endpoint yêu cầu Bearer token. Batch dùng `academic.batches.read/manage`; cấu hình điểm dùng `academic.score-config.read/manage`. Migration mặc định cấp quyền đọc batch cho sáu vai trò hệ thống. Quyền đọc và quản lý cấu hình điểm chỉ được cấp mặc định cho `Admin`, `AcademicAffairs`.

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

Base route cấu hình điểm: `/api/academic-periods/batches/{batchId}/score-configurations`.

| Method | Route | Permission | Response |
| --- | --- | --- | --- |
| `POST` | `/drafts` | `academic.score-config.manage` | `ScoreConfigurationDto` |
| `PUT` | `/drafts/{configurationId}` | `academic.score-config.manage` | `ScoreConfigurationDto` |
| `POST` | `/{configurationId}/publish` | `academic.score-config.manage` | `ScoreConfigurationDto` |
| `GET` | `/active` | `academic.score-config.read` | `ScoreConfigurationDto` |
| `GET` | `/{configurationId}` | `academic.score-config.read` | `ScoreConfigurationDto` |
| `GET` | `/history` | `academic.score-config.read` | `IReadOnlyList<ScoreConfigurationDto>` |

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

## Cấu hình điểm có version

Lifecycle của một version là `Draft -> Active -> Retired`. Active/Retired là bất biến; muốn thay đổi cấu hình đang dùng phải clone Active thành Draft version kế tiếp. Mỗi batch có tối đa một Draft và một Active, được bảo vệ bằng filtered unique indexes tại PostgreSQL.

Tạo version đầu tiên:

```json
{
  "calculationMethod": "Percentage",
  "changeReason": null,
  "components": [
    {
      "componentCode": "ADVISOR",
      "componentName": "Điểm GVHD",
      "source": "Advisor",
      "applicability": "Always",
      "weight": 40,
      "isRequired": true,
      "minimumScore": null,
      "requiresApproval": false,
      "orderIndex": 1
    },
    {
      "componentCode": "REVIEWER",
      "componentName": "Điểm GVPB",
      "source": "Reviewer",
      "applicability": "Always",
      "weight": 30,
      "isRequired": true,
      "minimumScore": 5,
      "requiresApproval": true,
      "orderIndex": 2
    },
    {
      "componentCode": "COUNCIL",
      "componentName": "Điểm hội đồng",
      "source": "CouncilAverage",
      "applicability": "Always",
      "weight": 30,
      "isRequired": true,
      "minimumScore": null,
      "requiresApproval": false,
      "orderIndex": 3
    }
  ]
}
```

Nếu batch đã có Active, `POST /drafts` tự clone thành version kế tiếp. Khi đó `changeReason` bắt buộc; có thể bỏ `calculationMethod/components` để giữ nguyên cấu hình Active, sau đó chỉnh bằng `PUT /drafts/{configurationId}`.

Quy tắc validation:

- `Percentage`: trọng số không âm và tổng bằng chính xác `100`.
- `IntegerCoefficient`: hệ số là số nguyên không âm và tổng lớn hơn `0`.
- Component code và `orderIndex` duy nhất trong một version; order phải dương.
- `minimumScore` là `null` hoặc thuộc `0..10`.
- `source`: `Advisor`, `Reviewer`, `CouncilChairman`, `CouncilSecretary`, `CouncilMember`, `CouncilAverage`, `Custom`.
- `applicability`: `Always`, `ReviewerInCouncil`, `ReviewerOutsideCouncil`.

Khi publish version mới, hệ thống retire Active cũ trước rồi activate Draft mới trong cùng database transaction để không vi phạm unique Active. Hai thay đổi và audit outbox cùng commit hoặc cùng rollback.

## Error codes

- `AcademicBatch.AccessDenied`
- `AcademicBatch.NotFound`
- `AcademicBatch.ActiveTemplateNotFound`
- `AcademicBatch.InvalidRequest`
- `AcademicBatch.DuplicateAcademicPeriod`
- `AcademicBatch.InvalidStatusTransition`
- `AcademicBatch.ScoreConfigurationRequired`
- `AcademicBatch.ConcurrencyConflict`
- `ScoreConfiguration.AccessDenied`
- `ScoreConfiguration.BatchNotFound`
- `ScoreConfiguration.NotFound`
- `ScoreConfiguration.ActiveNotFound`
- `ScoreConfiguration.DraftAlreadyExists`
- `ScoreConfiguration.ActiveNotEditable`
- `ScoreConfiguration.InvalidPercentageTotal`
- `ScoreConfiguration.InvalidCoefficient`
- `ScoreConfiguration.DuplicateComponentCode`
- `ScoreConfiguration.DuplicateComponentOrder`
- `ScoreConfiguration.InvalidMinimumScore`
- `ScoreConfiguration.EmptyComponents`
- `ScoreConfiguration.ChangeReasonRequired`
- `ScoreConfiguration.InvalidRequest`
- `ScoreConfiguration.InvalidStatusTransition`
- `ScoreConfiguration.ConcurrencyConflict`

Các mutation trả `409 Conflict` khi dữ liệu đã bị request khác cập nhật. Client cần tải lại batch/configuration hiện tại trước khi gửi lại thao tác; hệ thống không ghi đè âm thầm.
