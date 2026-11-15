---
version: 1
slug: "res-councils-pages-councilminutespage-tsx-2f572252"
primary_target: "frontend/src/features/councils/pages/CouncilMinutesPage.tsx"
related_targets: ["frontend/src/features/councils/components/minutes","frontend/src/features/councils/hooks/useCouncilMinutes.ts"]
---

## Scope
Route `/councils/minutes` (and optional query `?assignmentId=<guid>`), Operate mode. Serves Council members (Chairman, Secretary, Member), Lecturer, Faculty, AcademicAffairs and Admin. Connects directly to existing `DefenseCouncil` BE endpoints (`GET /api/councils`, `GET /api/councils/schedule/{councilId}`, `GET /api/councils/assignments/{id}/minute-workspace`, `GET /api/councils/assignments/{id}/discrepancy-check`, `PUT /api/councils/assignments/{id}/minute?preview=true|false`, `POST /api/councils/assignments/{id}/minute/lock`, `POST /api/councils/assignments/{id}/minute/unlock`), plus quick link from `/councils/grading`.

## Direction contract
THESIS: An administrative defense-minute sheet paired with a live calculation and audit dock, refusing generic flat CRUD tabs where official document structure, score discrepancy resolution, and legal locking feel disconnected.
OWN-WORLD: Inherit UGTM Modern Clean SaaS + Fluent Design — `#f8fafc` slate canvas, `#ffffff` `rounded-2xl` cards with `border-slate-200/80` and subtle elevation, Inter typography with `tabular-nums` for scores/weights, blue `#2563eb` primary actions, `#2054a5` to `#fa7921` accent stripe on `PageHeader` and the formal minute sheet top edge, semantic rose/amber/emerald status pills with explicit Vietnamese text labels.
STORY: The user selects a batch and council group (from a filterable group bar/switcher drawer), inspects the official defense minute sheet (council roster, topic & students, member score matrix with dual-role reviewer badge, discrepancy warning, Secretary marking + exclusion reason, student feedback), previews formula impact live (`PUT ?preview=true`) with Before/After comparison, saves draft (`PUT`), locks with password re-authentication (`POST /minute/lock`), or performs Faculty/Admin override unlock (`POST /minute/unlock`), and toggles a print-ready A4 view (`window.print()`).
FIRST VIEWPORT: Compact `PageHeader` with `accent` stripe, batch selector, active group switcher button/drawer trigger, and Print/Read-only toggle; below it, a 12-column grid (`minmax(0,1fr)_340px` on `xl`): left official white minute sheet carrying the administrative header, topic/group/council metadata strip, interactive score table (with discrepancy highlight and exclusion strike-through), discrepancy resolution controls, and `RichTextEditor` student feedback; right sticky `340px` audit & formula dock carrying Before vs After score delta comparison, live formula explanation, lock readiness checklist, and primary Lock/Unlock/Save actions. Mobile stacks the formula dock cleanly beneath the sheet.
FORM: User-selected candidate `document-sheet` (index 1 on our grounded list; `seed:9fe1e0bbc25887f9`), code-led. Signature interaction: toggling Secretary marking (`None` / `AcceptedAsPerMinute` / `RejectedAsPerMinute` + target component) immediately calls `PUT /minute?preview=true` and animates the Before vs After score delta and formula breakdown in the right dock without losing scroll or form state.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Finish review
- **Verdict:** Shipped cleanly. Implemented the selected `document-sheet` layout for `/councils/minutes` paired with sticky Ledger calculation audit dock, group switcher drawer, password lock / Faculty override unlock dialogs, and print-ready A4 mode, plus quick link from `/councils/grading`, BE `Faculty` view permission fix (`CouncilMinuteAccess.CanView`), and rich demo seed script (`scripts/SeedCouncilMinutesDemo.ps1`).
- **Issues found & fixed during visual inspection:**
  1. Fixed right-edge table overflow on desktop `1440px` and mobile `390px` by tightening 6-column widths on `>=md` and adding stacked cards on `<md`.
  2. Fixed IEEE-754 `+0.01` delta glitch (`8.625 -> 8.63`) in `CouncilMinuteAuditDock` via AwayFromZero rounding + divisor-change guard.
  3. Replaced raw backend formula paragraph with structured **Ledger** rows (`Phương án 1`) per user selection, removed redundant subtitle under score table heading, and centered single-line lecturer Avatar rows.
  4. Synced initial TipTap Markdown canonicalization (`workspaceHydratedAtRef`) so `Có thay đổi chưa lưu` does not trigger falsely on untouched group load.

## Verification and documentation
- `dotnet test backend/tests/DefenseCouncil.Tests/UGTM.Modules.DefenseCouncil.Tests.csproj --filter "CouncilMinute" --nologo` -> **Passed 101/101** (`Failed: 0, Skipped: 0`).
- `.agents\skills\impeccable\scripts\impeccable.cmd detect --json ...` on all 5 touched UI files -> `[]` (0 violations).
- `npm --prefix frontend run lint` (`0 errors`) & `npm --prefix frontend run build` (`tsc -b && vite build` succeeded).
- Playwright visual + interaction check (`1440x900` desktop & `390x844` mobile) verified clean console (`0 errors`), Ledger live preview (`8.14 -> 8.46 +0.32`), DualRole `Chỉ tham dự`, Locked A4 Print Preview, UnlockedByOverride, Group Drawer, and `CouncilGradingPage` quick link (`GRADING_PAGE_MINUTES_LINK_PRESENT=True`, `INITIAL_IS_DIRTY_BADGE_PRESENT=False`).
- Documented surface conventions under `## Biên bản hội đồng (/councils/minutes)` in `DESIGN.md`.
