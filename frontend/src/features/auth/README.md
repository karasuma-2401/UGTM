# Authentication frontend

The UI uses the Identity backend at `VITE_API_BASE_URL` (development default:
`http://localhost:5287`). Scalar: `http://localhost:5287/scalar/v1`.

## Connected flows

- Login: fetch CSRF, send identifier/password, receive an access token and an
  HttpOnly refresh cookie. Remembering an identifier only remembers MSSV/email.
- Activation: `/activate` requests an email through `activation/resend`.
  `/activate#token=...` validates the email token, then completes activation with
  the initial password. Success returns to login; it does not create a session.
- Recovery: email OTP, verification, and password reset use the recovery APIs.
- Logout: wait for an in-flight refresh, revoke the server session, then clear
  local state. If server logout fails, the login screen explains that server
  revocation was not confirmed.
- Startup: verify `/me` before admitting protected routes. If no access token is
  present, try the HttpOnly refresh cookie first. A rejected session clears local
  state; connection/server errors show a retry action and block protected pages.
- Refresh: startup and HTTP retries share one rotating refresh request. Retried
  requests cannot enter an infinite refresh loop.

SSO is not connected. Auth pages use real API services; the obsolete demo mocks have been removed.

## Activation requirements

The account must already exist in `PendingActivation`. Requesting an activation
email does not register an account. Email delivery requires the backend outbox
processor and configured mail transport. `PublicWebBaseUrl` must point at the
frontend so emailed links open `/activate#token=...`.

Activation APIs are anonymous and do not require CSRF. The three routes share
5 requests per 10 minutes per IP. A successful resend does not reveal whether an
account exists. Resending invalidates older activation links.

## Local checks

Run these commands from `frontend/`:

```powershell
npm.cmd run test:auth
npm.cmd run lint
npm.cmd run build
```

The Node tests execute the TypeScript services with controlled HTTP/storage
boundaries, including refresh races, cookie-only startup, outages, and API bodies.
They do not exercise real email delivery or a real account's activation.

For manual verification, use a pending test account, request its activation email,
open the newest link, set a strong password, and log in. Refresh the browser and
check that `/me` is requested. Log out and verify the server rejects the old access
token. Reusing the activation link must fail.
