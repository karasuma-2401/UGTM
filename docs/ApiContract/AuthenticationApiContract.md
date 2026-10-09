# API Contract — Authentication

**Version:** 0.1
**Ngày:** 09/10/2026  
**Base path:** `/api/identity/auth`

**Phạm vi:** đăng nhập bằng MSSV/email, phiên đăng nhập, kiểm tra hồ sơ và kích hoạt
tài khoản, OTP, khôi phục mật khẩu, đổi mật khẩu sau đăng nhập và hợp đồng tích hợp
SSO/Google.

## 1. Quy ước chung
- Request/response JSON dùng `camelCase`, `Content-Type: application/json`.
- API cần xác thực nhận `Authorization: Bearer <accessToken>`.
- ID người dùng là UUID; thời gian là ISO 8601 UTC, ví dụ `2026-10-09T03:00:00Z`.
- HTTP status phản ánh kết quả thực tế, không trả HTTP 200 cho lỗi.
- Response dùng `success`, `data`, `error` theo `ApiResponse<T>` của backend.
- `data` là `null` khi lỗi hoặc khi thao tác thành công không có dữ liệu trả về.
- `error` là `null` khi thành công. `error.errors` là `null` nếu không có lỗi từng field.
- Không trả password, password hash hoặc refresh token trong hồ sơ người dùng.
- Response có token dùng `Cache-Control: no-store`. Production dùng HTTPS.
- Mọi response auth/hồ sơ dùng `Cache-Control: no-store`; không log body chứa
  mật khẩu, OTP, token, header Authorization hoặc URL callback OAuth đầy đủ.
- Body JSON không hợp lệ trả 400 `VALIDATION_ERROR`; sai Content-Type trả 415
  `UNSUPPORTED_MEDIA_TYPE`; vượt 16 KiB trả 413 `PAYLOAD_TOO_LARGE`.
- Lỗi nội bộ trả 500 `INTERNAL_ERROR`, không trả stack trace; dịch vụ phụ trợ tạm
  không sẵn sàng trả 503 `AUTH_SERVICE_UNAVAILABLE`. Thêm `X-Request-Id` để tra log.
- `429` và lỗi cooldown có `Retry-After` dạng số giây nguyên dương. CORS expose
  `Retry-After`, `X-Request-Id`; allowlist origin rõ ràng, không dùng wildcard với cookie.
- Không gửi token/OTP qua query string, ngoại trừ mã trao đổi OAuth một lần ở mục 14.

### Thành công:

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

### Lỗi validation:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Dữ liệu không hợp lệ.",
    "errors": {
      "email": ["Email không đúng định dạng."],
      "password": ["Vui lòng nhập mật khẩu."]
    }
  }
}
```

Frontend xử lý theo HTTP status và `error.code`; không phân nhánh theo nội dung
`message`. Key trong `errors` trùng tên field trong request.

## 2. Danh sách endpoint

| Method | Endpoint | Xác thực | Mục đích |
| --- | --- | --- | --- |
| POST | `/api/identity/auth/login` | Không | Đăng nhập |
| POST | `/api/identity/auth/refresh` | Refresh token trong body | Cấp cặp token mới |
| POST | `/api/identity/auth/logout` | Refresh token trong body | Đăng xuất phiên hiện tại |
| GET | `/api/identity/auth/me` | Bearer token | Lấy hồ sơ và quyền hiện tại |
| POST | `/api/identity/auth/change-password` | Bearer token | Đổi mật khẩu |
| GET | `/api/identity/auth/capabilities` | Không | Các phương thức đã cấu hình |
| POST | `/api/identity/auth/activation/lookup` | Không | Kiểm tra MSSV và điều kiện KLTN |
| POST | `/api/identity/auth/activation/request` | Lookup token trong body | Lưu yêu cầu kích hoạt, gửi OTP |
| POST | `/api/identity/auth/forgot-password` | Không | Yêu cầu OTP khôi phục |
| POST | `/api/identity/auth/otp/resend` | Challenge token trong body | Gửi lại/đổi kênh OTP |
| POST | `/api/identity/auth/otp/verify` | Challenge token + OTP trong body | Kích hoạt hoặc cấp quyền reset |
| POST | `/api/identity/auth/reset-password` | Reset token trong body | Đặt mật khẩu mới sau OTP |
| POST | `/api/identity/auth/external/{provider}/start` | Không | Bắt đầu SSO/Google |
| GET | `/api/identity/auth/external/{provider}/callback` | State/correlation của provider | Callback server |
| POST | `/api/identity/auth/external/exchange` | Code + browser binding | Đổi code lấy phiên UGTM |

`provider` chỉ nhận `sso` hoặc `google`. Các endpoint đăng nhập ngoài chỉ bật khi
đã cấu hình provider; chưa cấu hình trả 503 `AUTH_PROVIDER_UNAVAILABLE`.
Tra cứu đề tài công khai thuộc TopicCatalog, không thuộc contract auth.

## 3. Kiểu dữ liệu dùng chung

```ts
export interface ApiError {
  code: string;
  message: string;
  errors: Record<string, string[]> | null;
}

export type ApiResponse<T> =
  | { success: true; data: T; error: null }
  | { success: false; data: null; error: ApiError };

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  roles: string[]; // Mã vai trò backend đọc từ database
  permissions: string[];
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  tokenType: 'Bearer';
  expiresIn: number; // Thời gian sống access token, đơn vị giây
  refreshTokenExpiresAt: string; // ISO 8601 UTC
}

export interface LoginRequest {
  identifier: string; // MSSV hoặc email
  password: string;
  rememberMe: boolean;
}

export interface LoginData extends AuthTokens {
  user: UserProfile;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface LogoutRequest {
  refreshToken: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}
```


## 4. Đăng nhập

**POST `/api/identity/auth/login`**

Request:

```json
{
  "identifier": "24521186",
  "password": "ExamplePassword123!",
  "rememberMe": false
}
```

- `identifier`: bắt buộc, tối đa 150 ký tự; trim. Có `@` thì kiểm tra định dạng
  email và so khớp normalized email không phân biệt hoa/thường; nếu không, phải
  là MSSV gồm 8 chữ số, tra `UserCode` liên kết với hồ sơ sinh viên.
  Không tạo email từ MSSV. Email cán bộ không bị giới hạn domain ở bước login.
- Normalized email phải unique; MSSV phải unique trong tập tài khoản sinh viên.
- `rememberMe`: boolean, mặc định false nếu thiếu. Access token sống 900 giây;
  refresh session sống tối đa 12 giờ nếu false, 7 ngày nếu true. Refresh không
  kéo dài hạn tuyệt đối. Frontend luôn dùng thời hạn server trả, không hardcode.
- `password`: bắt buộc; giữ nguyên ký tự, không trim. Chính sách mật khẩu mới
  không áp dụng khi kiểm tra mật khẩu đăng nhập của tài khoản đã tồn tại.

**200 OK — `ApiResponse<LoginData>`:**

```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt-access-token>",
    "refreshToken": "<opaque-refresh-token>",
    "tokenType": "Bearer",
    "expiresIn": 900,
    "refreshTokenExpiresAt": "2026-10-10T15:00:00Z",
    "user": {
      "id": "11111111-1111-4111-8111-111111111111",
      "email": "24521186@gm.uit.edu.vn",
      "fullName": "Nguyễn Chí Nguyên",
      "roles": ["Student"],
      "permissions": []
    }
  },
  "error": null
}
```

| Status | Code | Điều kiện |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | Body hoặc field không hợp lệ |
| 401 | `AUTH_INVALID_CREDENTIALS` | MSSV/email không tồn tại hoặc mật khẩu sai; dùng cùng thông báo |
| 403 | `AUTH_ACCOUNT_NOT_ACTIVATED` | Đúng mật khẩu nhưng hồ sơ chưa kích hoạt |
| 403 | `AUTH_ACCOUNT_DISABLED` | Đúng thông tin đăng nhập nhưng tài khoản bị vô hiệu hóa |
| 403 | `AUTH_ACCOUNT_LOCKED` | Đúng thông tin đăng nhập nhưng tài khoản đang bị khóa |
| 429 | `AUTH_TOO_MANY_ATTEMPTS` | Vượt giới hạn đăng nhập; trả header `Retry-After` tính bằng giây |

## 5. Refresh token

**POST `/api/identity/auth/refresh`**

Request:

```json
{ 
  "refreshToken": "<current-refresh-token>" 
}
```

`refreshToken` là chuỗi bắt buộc, không rỗng. Không cần access token còn hạn.

**200 OK — `ApiResponse<AuthTokens>`**, trả tất cả field của `AuthTokens`.

```json
{
  "success": true,
  "data": {
    "accessToken": "<new-jwt-access-token>",
    "refreshToken": "<new-opaque-refresh-token>",
    "tokenType": "Bearer",
    "expiresIn": 900,
    "refreshTokenExpiresAt": "2026-10-16T03:00:00Z"
  },
  "error": null
}
```

- Mỗi lần refresh thành công cấp access token và refresh token mới; token refresh
  cũ bị tiêu thụ, không thể dùng lại.
- Backend xử lý rotation nguyên tử; chỉ một request sử dụng token cũ thành công.
- Refresh token mới thuộc cùng phiên; giữ nguyên hạn tuyệt đối của phiên.
- Token cũ đã bị tiêu thụ được dùng lại: thu hồi phiên đó, trả `AUTH_REFRESH_TOKEN_REUSED`.
- Kiểm tra trạng thái tài khoản trước khi cấp token; token mới chứa quyền hiện tại.

| Status | Code | Điều kiện |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | Thiếu token hoặc body không hợp lệ |
| 401 | `AUTH_REFRESH_TOKEN_INVALID` | Token không tồn tại hoặc đã bị thu hồi |
| 401 | `AUTH_REFRESH_TOKEN_EXPIRED` | Token hết hạn |
| 401 | `AUTH_REFRESH_TOKEN_REUSED` | Token đã dùng bị sử dụng lại |
| 403 | `AUTH_ACCOUNT_DISABLED` | Tài khoản bị vô hiệu hóa |
| 403 | `AUTH_ACCOUNT_LOCKED` | Tài khoản bị khóa |

## 6. Đăng xuất

**POST `/api/identity/auth/logout`**

Request:

```json
{ 
  "refreshToken": "<refresh-token-of-current-session>" 
}
```

**200 OK:**

```json
{ 
  "success": true, 
  "data": null, 
  "error": null 
}
```

- Thu hồi toàn bộ chuỗi refresh token của phiên được nhận diện, kể cả khi gửi một
  refresh token cũ thuộc phiên đó. Không ảnh hưởng phiên trên thiết bị khác.
- Token không tồn tại, hết hạn hoặc phiên đã thu hồi vẫn trả 200 (idempotent).
- Body sai/thiếu token trả 400 `VALIDATION_ERROR`.
- Không cần Bearer token để người dùng vẫn đăng xuất được khi access token hết hạn.
- Frontend luôn xóa token và profile cục bộ, kể cả khi gọi API thất bại. Nếu lỗi
  mạng, việc thu hồi phía server chưa được bảo đảm.

## 7. Hồ sơ người dùng hiện tại

**GET `/api/identity/auth/me`**  
**Header:** `Authorization: Bearer <accessToken>`  
Không có request body.

**200 OK — `ApiResponse<UserProfile>`**. `data` có cấu trúc giống `user` trong
response đăng nhập; backend đọc hồ sơ, roles và permissions hiện tại từ dữ liệu.
Bao gồm cả role mới được tạo và gán cho người dùng khi hệ thống đang vận hành.
Gọi `/me` cập nhật hồ sơ frontend, không thay đổi các claims trong JWT đã cấp.
Backend kiểm tra trạng thái tài khoản hiện tại cho `/me` và đổi mật khẩu, kể cả
khi access token vẫn còn hạn.

| Status | Code | Điều kiện |
| --- | --- | --- |
| 401 | `AUTH_UNAUTHENTICATED` | Thiếu token, token không hợp lệ hoặc người dùng không còn tồn tại |
| 401 | `AUTH_ACCESS_TOKEN_EXPIRED` | Access token hết hạn |
| 403 | `AUTH_ACCOUNT_DISABLED` | Tài khoản bị vô hiệu hóa |
| 403 | `AUTH_ACCOUNT_LOCKED` | Tài khoản bị khóa |

## 8. Đổi mật khẩu

**POST `/api/identity/auth/change-password`**  
**Header:** `Authorization: Bearer <accessToken>`

Request:

```json
{
  "currentPassword": "ExamplePassword123!",
  "newPassword": "AnotherPassword456!"
}
```

- Hai field đều bắt buộc, không trim.
- Chính sách `newPassword` dùng chung với kích hoạt/reset tại mục 15; mật khẩu
  mới phải khác mật khẩu hiện tại (so sánh qua password hasher).
- `confirmPassword` chỉ dùng trên form frontend; không gửi lên API.
- Backend lấy user từ access token; request không nhận `userId`.
- Đổi mật khẩu thành công thu hồi refresh token của tất cả phiên, bao gồm phiên
  hiện tại. Frontend xóa thông tin đăng nhập và chuyển về `/login`.

**200 OK:** `{"success":true,"data":null,"error":null}`.

| Status | Code | Điều kiện |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | Thiếu field hoặc mật khẩu mới vi phạm chính sách; lỗi nằm ở `newPassword` |
| 400 | `AUTH_CURRENT_PASSWORD_INVALID` | Mật khẩu hiện tại sai; lỗi nằm ở `currentPassword` |
| 401 | `AUTH_UNAUTHENTICATED` | Thiếu/không hợp lệ access token |
| 401 | `AUTH_ACCESS_TOKEN_EXPIRED` | Access token hết hạn |
| 403 | `AUTH_ACCOUNT_DISABLED` | Tài khoản bị vô hiệu hóa |
| 403 | `AUTH_ACCOUNT_LOCKED` | Tài khoản bị khóa |
| 429 | `AUTH_TOO_MANY_ATTEMPTS` | Vượt giới hạn thử mật khẩu hiện tại; có `Retry-After` |
