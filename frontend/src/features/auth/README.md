# Auth frontend

This feature implements the login, account activation, recovery, OTP verification,
and password reset screens using an in-memory demo service.

## Structure

- `pages/`: one route-level component per screen.
- `components/`: reusable auth form components with named props interfaces.
- `layouts/`: the shared header, background, and footer.
- `services/`: real API functions for endpoints with defined request/response contracts.
- `mocks/`: demo service, fixtures, and local simulation state types.
- `utils/`: pure password validation functions.
- `constants/`: storage keys and OTP settings; no demo credentials.
- `types/`: auth request/response contracts and shared auth domain types.
- `styles/`: feature CSS, scoped using the `auth-` prefix.

## Conventions

- Use PascalCase for React components and their filenames.
- Use camelCase for variables and functions; prefix event handlers with `handle`.
- Use `is`/`has` prefixes for boolean values and UPPER_SNAKE_CASE for constants.
- Declare component props with a named `ComponentNameProps` interface.
- Keep service logic and validation outside JSX.
- Use relative imports inside this feature and `@/` for shared frontend modules.
- Keep `ApiResponse` and `ApiError` in `shared/api/types.ts`; auth types have one
  source of truth in `features/auth/types/auth.types.ts`. Shared infrastructure
  may import these contracts using `import type`, without loading auth UI or mocks.
- Format with Prettier: two spaces, single quotes, semicolons, and trailing commas.

## Local checks

Run `npm.cmd run format:auth`, `npm.cmd run format:auth:check`,
`npm.cmd run lint`, and `npm.cmd run build` from `frontend/`.

## API integration boundary

The pages currently import `mocks/authMock.ts` explicitly. Adding `services/authApi.ts`
does not enable real authentication or change route protection.

The API service covers login, refresh, logout, current user, and authenticated
password changes. It returns `ApiResponse<T>` on successful HTTP responses;
HTTP failures are Axios errors containing the contract error envelope in
`response.data`. Callers handle `error.code` and field errors, not message text.

Login, refresh, and logout use a public HTTP client to avoid automatic refresh
on invalid login credentials. Authenticated operations use the shared client.
The shared interceptor performs automatic refresh; do not also trigger a manual
refresh for the same failure. Session/profile cleanup is the caller's responsibility.

Activation, recovery, and OTP still use mocks because their detailed API contract
is unfinished. Define those request/response shapes before adding API functions.
When connecting real login, also wire `AuthProvider`, `/me`, and protected routes.

## Demo flow

Open `/login`. The initial account is `24521186` / `UGTM@2025SecureKey`.
The demo OTP is `123456`, valid for five minutes, with a 45-second resend
cooldown and a five-attempt limit. Password changes exist only in memory;
reload restores the initial password. Remembering an account stores only
its identifier. SSO, Google, email, and SMS do not contact external services.
