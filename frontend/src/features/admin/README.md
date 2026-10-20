# Account management

Identity has three navigation entries:

- `/modules/identity/students`: Student accounts.
- `/modules/identity/staff`: all accounts without a Student role, including
  roleless accounts that administrators need to repair.
- `/modules/identity/permissions`: existing role/permission management.

The old `/modules/identity/accounts` route redirects to the student page.

## Account groups

Both lists use `GET /api/users` with `accountGroup=Student` or `accountGroup=Staff`.
The backend applies the group before counting and pagination. Each group has
separate query cache keys, totals, search/status filters, and 20/50-row pages.
Changing pages resets the form and filter state through the route component key.

The student form requires MSSV and fixes roles to `[Student]`. Staff forms default
to Lecturer and allow multiple non-Student roles. Existing backend permission
checks and protections for the last active administrator remain in place.

Groups derive from existing roles. This change needs no database migration and
does not introduce separate credentials, login flows, or profile fields.

## Student imports

Users with `identity.users.import` can download the authenticated Student template
and upload `.xlsx` through the existing import endpoint. The UI sends multipart
form data with role fixed to Student, displays row validation errors, and reads
`importedCount` from the backend response. Limits are 5 MB and 5,000 rows.

Imports are all-or-nothing and do not send activation emails. Students request
their link on `/activate`; managers may also request activation for an individual
pending account. Creating one account retains the existing activation email flow.

## Verification

From `frontend`, run `npm.cmd run test:accounts`, `npm.cmd run lint`, and
`npm.cmd run build`. Backend handler/filter/SQL translation tests live in
`UsersHandlerTests` and `UserAccountGroupTests`.

Restart a backend that was already running before this change so it recognizes
the new query parameter, then refresh the frontend. Frontend and backend must be
deployed together: an older backend will ignore the new group filter.
