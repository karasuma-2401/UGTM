# Authentication API

**Trạng thái:** đã triển khai · **Cập nhật:** 18/10/2026
**Base path:** `/api/identity/auth`

Contract này mô tả đúng các endpoint Identity đang có trong backend. Source đối chiếu chính là thư mục [`Infrastructure/Endpoints`](../../backend/Modules/Identity/Infrastructure/Endpoints). Hệ thống chưa cung cấp API đăng nhập Google, SSO hoặc API công khai để tạo tài khoản.

## Quy ước

- Request và response dùng JSON `camelCase`; thời gian dùng ISO 8601 UTC.
- Endpoint có bảo vệ nhận `Authorization: Bearer <access-token>`.
- Access token nằm trong response JSON. Refresh token chỉ nằm trong cookie `HttpOnly` và không được trả trong body.
- Trình duyệt phải gửi request kèm cookie (`credentials: 'include'` hoặc `withCredentials: true`).
- Client xử lý lỗi theo HTTP status và `error.code`; `message` dùng để hiển thị.

Response nghiệp vụ thành công:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

Response nghiệp vụ thất bại:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "Identity.InvalidCredentials",
    "message": "Email, mã sinh viên hoặc mật khẩu không đúng.",
    "errors": null
  }
}
```

Các lỗi do pipeline ASP.NET Core tạo ra có thể không dùng envelope trên:

- JWT thiếu, hết hạn hoặc không hợp lệ: `401` với body rỗng.
- CSRF không hợp lệ: `400 application/problem+json`.
- JSON không đọc được hoặc media type không hỗ trợ: response mặc định của ASP.NET Core.
- Lỗi chưa xử lý: `500 application/problem+json`, kèm `requestId` để tra log.

## CSRF và cookie

`login`, `refresh` và `logout` yêu cầu CSRF token vì chúng đọc hoặc ghi refresh cookie. Trước khi gọi một trong ba endpoint này:

1. Gọi `GET /csrf` kèm credentials.
2. Lưu `data.token` trong bộ nhớ của tab.
3. Gửi token qua header `X-CSRF-TOKEN` và tiếp tục gửi credentials.

Cookie do backend quản lý:

| Môi trường | Refresh cookie | CSRF cookie | Thuộc tính |
| --- | --- | --- | --- |
| Development | `ugtm-refresh` | `ugtm-csrf` | `HttpOnly`, `SameSite=Strict`, `Path=/` |
| Production | `__Host-ugtm-refresh` | `__Host-ugtm-csrf` | như trên, thêm `Secure` |

Cookie CSRF là `HttpOnly`; client lấy request token từ body của `/csrf`, không đọc cookie.

## Endpoint

| Method | Path | Bearer | CSRF | Rate limit |
| --- | --- | :---: | :---: | --- |
| GET | `/csrf` | — | — | — |
| POST | `/login` | — | Có | 10 yêu cầu/phút/IP |
| POST | `/activation/validate` | — | — | 5 yêu cầu/10 phút/IP |
| POST | `/activation/complete` | — | — | 5 yêu cầu/10 phút/IP |
| POST | `/activation/resend` | — | — | 5 yêu cầu/10 phút/IP |
| GET | `/me` | Có | — | — |
| GET | `/sessions` | Có | — | — |
| DELETE | `/sessions/{sessionId}` | Có | — | — |
| DELETE | `/sessions` | Có | — | — |
| POST | `/refresh` | — | Có | — |
| POST | `/logout` | — | Có | — |
| POST | `/change-password` | Có | — | — |
| POST | `/recovery/start` | — | — | 5 yêu cầu/10 phút/IP |
| POST | `/recovery/verify` | — | — | 5 yêu cầu/10 phút/IP |
| POST | `/recovery/reset` | — | — | 5 yêu cầu/10 phút/IP |

Các endpoint cùng nhóm `activation` dùng chung một policy; ba endpoint `recovery` cũng dùng chung một policy. Khi bị giới hạn, backend trả `429`, header `Retry-After` và code `Identity.RateLimitExceeded`.

```json
{
  "success": false,
  "error": {
    "code": "Identity.RateLimitExceeded",
    "message": "Có quá nhiều yêu cầu. Vui lòng thử lại sau."
  }
}
```

Response rate limit do middleware tạo nên hiện không có `data` và `error.errors`.

## Kiểu dữ liệu

```ts
export interface ApiError {
  code: string;
  message: string;
  errors: Record<string, string[]> | null;
}

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error: ApiError | null;
}

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  userCode: string | null;
  roles: string[];
  permissions: string[];
}

export interface AuthenticatedSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserProfile;
}

export interface Session {
  id: string;
  authenticatedAt: string;
  lastSeenAt: string;
  idleExpiresAt: string;
  absoluteExpiresAt: string;
  userAgentSummary: string | null;
  isCurrent: boolean;
}

export interface RevokeSessionsResult {
  revokedCount: number;
  currentSessionRevoked: boolean;
}
```

## Lấy CSRF token

### `GET /csrf`

**200 OK**

```json
{
  "success": true,
  "data": {
    "token": "<csrf-request-token>"
  },
  "error": null
}
```

Mỗi lần gọi có thể thay token đang giữ. Client nên gọi lại sau khi tải trang hoặc khi server từ chối CSRF.

## Đăng nhập

### `POST /login`

```json
{
  "identifier": "24521186",
  "password": "ExamplePassword123!"
}
```

`identifier` nhận email hoặc mã người dùng. Backend trim và so khớp không phân biệt hoa thường. Mật khẩu được giữ nguyên khi kiểm tra.

**200 OK**

```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt>",
    "accessTokenExpiresAt": "2026-10-13T08:10:00Z",
    "user": {
      "id": "11111111-1111-4111-8111-111111111111",
      "email": "24521186@gm.uit.edu.vn",
      "fullName": "Nguyễn Văn A",
      "userCode": "24521186",
      "roles": ["Student"],
      "permissions": []
    }
  },
  "error": null
}
```

Response đồng thời đặt refresh cookie. Access token mặc định sống 10 phút; phiên có hạn nhàn rỗi 7 ngày và hạn tuyệt đối 30 ngày.

| Status | Code | Khi nào xảy ra |
| --- | --- | --- |
| 400 | Problem Details | CSRF token thiếu hoặc không hợp lệ |
| 401 | `Identity.InvalidCredentials` | Sai identifier hoặc mật khẩu |
| 403 | `Identity.AccountPendingActivation` | Tài khoản chưa kích hoạt |
| 403 | `Identity.AccountSuspended` | Tài khoản đang tạm ngưng |
| 403 | `Identity.AccountDisabled` | Tài khoản đã bị vô hiệu hóa |
| 429 | `Identity.AccountLocked` | Tài khoản bị khóa tạm thời sau 5 lần sai |
| 429 | `Identity.RateLimitExceeded` | Vượt rate limit của endpoint |

## Kích hoạt tài khoản

Tài khoản sinh viên được cấp nội bộ ở trạng thái `PendingActivation`. Email kích hoạt chứa URL `/activate#token=...`; token nằm trong fragment nên trình duyệt không gửi nó lên web server hoặc proxy. Frontend lấy token từ fragment và gọi hai endpoint dưới đây.

### `POST /activation/validate`

```json
{
  "token": "<activation-token>"
}
```

**200 OK**

```json
{
  "success": true,
  "data": {
    "maskedEmail": "2***6@gm.uit.edu.vn",
    "fullName": "Nguyễn Văn A",
    "expiresAt": "2026-10-14T08:00:00Z"
  },
  "error": null
}
```

Endpoint chỉ kiểm tra token, chưa tiêu thụ token.

### `POST /activation/complete`

```json
{
  "token": "<activation-token>",
  "password": "ExamplePassword123!"
}
```

**200 OK**

```json
{
  "success": true,
  "data": null,
  "error": null
}
```

Khi thành công, backend đặt mật khẩu, xác minh email, chuyển tài khoản sang `Active` và tiêu thụ token.

### `POST /activation/resend`

```json
{
  "identifier": "24521186"
}
```

**200 OK** với `data: null`. Backend luôn trả thành công nếu tài khoản không tồn tại hoặc không chờ kích hoạt để tránh lộ danh sách tài khoản. Với tài khoản hợp lệ, token cũ bị vô hiệu và email mới được đưa vào outbox.

Các lỗi riêng của luồng kích hoạt:

| Status | Code | Khi nào xảy ra |
| --- | --- | --- |
| 400 | `Identity.InvalidActivationToken` | Token sai, hết hạn, đã dùng hoặc tài khoản không còn chờ kích hoạt |
| 400 | `Identity.WeakPassword` | Mật khẩu mới không đạt chính sách |
| 429 | `Identity.RateLimitExceeded` | Vượt rate limit nhóm activation |

## Phiên đăng nhập

### `GET /me`

Header: `Authorization: Bearer <access-token>`

**200 OK:** `ApiResponse<UserProfile>` với cùng cấu trúc `user` của login. Dữ liệu roles và permissions được đọc lại từ database. Backend cũng kiểm tra phiên, trạng thái tài khoản và security stamp trước khi chạy endpoint.

### `GET /sessions`

Header: `Authorization: Bearer <access-token>`. Không có body và không cần CSRF token.

**200 OK:** `ApiResponse<Session[]>`. Chỉ các phiên chưa bị thu hồi, chưa hết idle timeout và chưa hết absolute timeout được trả về; danh sách sắp xếp theo `lastSeenAt` giảm dần. `isCurrent` xác định phiên gắn với access token đang gọi API. Backend không trả refresh token hoặc IP hash.

### `DELETE /sessions/{sessionId}`

Thu hồi một phiên đang hoạt động thuộc chính người dùng hiện tại. Không có body và không cần CSRF token.

**200 OK:** `ApiResponse<RevokeSessionsResult>` với `revokedCount = 1`. Nếu `currentSessionRevoked = true`, backend xóa refresh cookie và client phải xóa access token rồi chuyển về màn hình đăng nhập.

| Status | Code | Khi nào xảy ra |
| --- | --- | --- |
| 401 | body rỗng hoặc `Identity.InvalidSession` | Bearer token/phiên gọi API không hợp lệ |
| 404 | `Identity.SessionNotFound` | Phiên không tồn tại, đã hết hạn/thu hồi hoặc thuộc người dùng khác |

### `DELETE /sessions`

Thu hồi toàn bộ phiên đang hoạt động của người dùng hiện tại, bao gồm phiên đang gọi API. Không có body và không cần CSRF token.

**200 OK:** `ApiResponse<RevokeSessionsResult>`. Backend xóa refresh cookie hiện tại. Sau response này, client phải xóa access token và hồ sơ đang giữ; mọi token thuộc các phiên vừa thu hồi sẽ bị từ chối ở request kế tiếp.

### `POST /refresh`

Không có request body. Backend đọc refresh token từ cookie.

**200 OK:** `ApiResponse<AuthenticatedSession>`, cùng body với login. Backend xoay refresh token và cập nhật cookie trong cùng request. Token cũ đã tiêu thụ mà bị dùng lại sẽ làm phiên bị thu hồi.

| Status | Code | Khi nào xảy ra |
| --- | --- | --- |
| 400 | Problem Details | CSRF token thiếu hoặc không hợp lệ |
| 401 | `Identity.InvalidSession` | Cookie thiếu, token/phiên hết hạn, bị thu hồi hoặc không hợp lệ |

Khi refresh thất bại, backend xóa refresh cookie. Client xóa access token và hồ sơ đang giữ, sau đó chuyển về màn hình đăng nhập.

### `POST /logout`

Không có request body. Endpoint thu hồi phiên gắn với refresh cookie rồi xóa cookie.

**200 OK**

```json
{
  "success": true,
  "data": null,
  "error": null
}
```

Logout có tính idempotent: cookie thiếu, sai hoặc phiên đã thu hồi vẫn trả `200`. CSRF sai vẫn trả `400`.

## Mật khẩu

Chính sách dùng chung cho kích hoạt, đổi và đặt lại mật khẩu: ít nhất 12 ký tự, có chữ hoa, chữ thường, chữ số và ký tự đặc biệt.

### `POST /change-password`

Header: `Authorization: Bearer <access-token>`

```json
{
  "currentPassword": "ExamplePassword123!",
  "newPassword": "AnotherPassword456!"
}
```

**200 OK** với `data: null`. Backend đổi security stamp, thu hồi mọi phiên của người dùng và xóa refresh cookie hiện tại. Client phải đăng nhập lại.

| Status | Code | Khi nào xảy ra |
| --- | --- | --- |
| 400 | `Identity.CurrentPasswordIncorrect` | Mật khẩu hiện tại sai hoặc rỗng |
| 400 | `Identity.WeakPassword` | Mật khẩu mới không đạt chính sách |
| 401 | body rỗng | Bearer token thiếu hoặc không hợp lệ |

## Khôi phục mật khẩu

### `POST /recovery/start`

```json
{
  "email": "24521186@gm.uit.edu.vn"
}
```

**200 OK**

```json
{
  "success": true,
  "data": {
    "challengeId": "<opaque-selector>",
    "expiresAt": "2026-10-13T08:10:00Z"
  },
  "error": null
}
```

Backend trả cùng dạng response cho email không tồn tại, rỗng hoặc tài khoản chưa active. `challengeId` giả trong các trường hợp đó khiến bước verify thất bại theo cùng một cách. Với tài khoản hợp lệ, OTP 6 chữ số được gửi qua email; OTP sống 10 phút.

### `POST /recovery/verify`

```json
{
  "challengeId": "<challenge-id>",
  "code": "123456"
}
```

**200 OK**

```json
{
  "success": true,
  "data": {
    "resetGrant": "<one-time-reset-grant>",
    "expiresAt": "2026-10-13T08:20:00Z"
  },
  "error": null
}
```

OTP cho phép tối đa 5 lần thử. Verify thành công tiêu thụ OTP và cấp reset grant dùng một lần, mặc định sống 10 phút.

### `POST /recovery/reset`

```json
{
  "resetGrant": "<one-time-reset-grant>",
  "newPassword": "AnotherPassword456!"
}
```

**200 OK** với `data: null`. Backend tiêu thụ grant, đổi security stamp, thu hồi mọi phiên và xóa refresh cookie hiện tại.

| Status | Code | Khi nào xảy ra |
| --- | --- | --- |
| 400 | `Identity.InvalidOtp` | Challenge/OTP sai, hết hạn, đã dùng hoặc vượt số lần thử |
| 400 | `Identity.InvalidResetGrant` | Grant sai, hết hạn, đã dùng hoặc tài khoản không active |
| 400 | `Identity.WeakPassword` | Mật khẩu mới không đạt chính sách |
| 429 | `Identity.RateLimitExceeded` | Vượt rate limit nhóm recovery |

## Danh mục error code

| Code | Status | Ý nghĩa |
| --- | ---: | --- |
| `Identity.InvalidCredentials` | 401 | Thông tin đăng nhập không đúng |
| `Identity.AccountPendingActivation` | 403 | Chưa kích hoạt |
| `Identity.AccountSuspended` | 403 | Đang tạm ngưng |
| `Identity.AccountDisabled` | 403 | Đã vô hiệu hóa |
| `Identity.AccountLocked` | 429 | Khóa tạm do đăng nhập sai |
| `Identity.InvalidActivationToken` | 400 | Token kích hoạt không dùng được |
| `Identity.InvalidOtp` | 400 | OTP không dùng được |
| `Identity.InvalidResetGrant` | 400 | Reset grant không dùng được |
| `Identity.WeakPassword` | 400 | Mật khẩu không đạt chính sách |
| `Identity.CurrentPasswordIncorrect` | 400 | Mật khẩu hiện tại sai |
| `Identity.InvalidSession` | 401 | Phiên không còn hợp lệ |
| `Identity.SessionNotFound` | 404 | Phiên không tồn tại, không còn hoạt động hoặc không thuộc người dùng được phép thao tác |
| `Identity.RateLimitExceeded` | 429 | Quá nhiều request từ cùng IP |

`Identity.DuplicateAccount` và `Identity.UserNotFound` được định nghĩa cho application layer, hiện chưa được expose qua HTTP.

## Ví dụ gọi từ frontend

```ts
const API = 'http://localhost:5287/api/identity/auth';

const csrfResponse = await fetch(`${API}/csrf`, {
  credentials: 'include',
});
const csrf = (await csrfResponse.json()).data.token as string;

const loginResponse = await fetch(`${API}/login`, {
  method: 'POST',
  credentials: 'include',
  headers: {
    'Content-Type': 'application/json',
    'X-CSRF-TOKEN': csrf,
  },
  body: JSON.stringify({
    identifier: '24521186',
    password: 'ExamplePassword123!',
  }),
});
```

Giữ access token trong bộ nhớ ứng dụng và gửi nó qua Bearer header. Khi ứng dụng tải lại, gọi `/csrf`, sau đó `/refresh` để khôi phục phiên từ refresh cookie.
