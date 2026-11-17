---
version: 1
slug: "ures-thesis-pages-studentarchivalpage-tsx-48414e4f"
primary_target: "frontend/src/features/thesis/pages/StudentArchivalPage.tsx"
related_targets: ["frontend/src/features/thesis/hooks/useStudentArchival.ts","frontend/src/features/thesis/components/archival/ArchivalFileDropSlot.tsx","frontend/src/features/thesis/components/archival/ArchivalPipelineStrip.tsx","frontend/src/features/thesis/components/archival/ArchivalDossierSidebar.tsx"]
---

## Scope
Route `/thesis/archival`, Operate mode. Serves confirmed `Student` members of a thesis group submitting, revising (`NeedRevision`), or tracking their post-defense archival dossier (`ThesisPdf` + `ThesisDocx` + bilingual abstracts & keywords VI/EN) during Stage 7 (`ARCHIVAL_AND_PUBLICATION`) once the group's defense council minute is `Locked`. Connects to `syllabusApi.getMyStatus`, `syllabusApi.getGroupDetail`, `archivalApi.getEligibility`, `archivalApi.getMySubmission`, `finalReportApi.getReviewerFeedback`, `archivalApi.getPackages`, `archivalApi.submitFinalSubmission`, and `archivalApi.getDownloadUrl`.

## Direction contract
THESIS: A state-driven archival dossier paired with a sticky 340px hairline-divided readiness dock, replacing three boxed step cards, generic file icons, and orphaned submit cards with clear lifecycle clarity across Blocked, Editable (NotSubmitted / NeedRevision / Submitted-before-lock), and Locked (AdvisorConfirmed / ArchiveConfirmed) states.
OWN-WORLD: Inherit UGTM Modern Clean SaaS + Fluent Design (`bg-surface-canvas`, `bg-surface-card`, `rounded-2xl`, `border-line/80`, Inter typography with `tabular-nums`, `#2054a5` to `#fa7921` PageHeader accent stripe, no icon inside PageHeader, semantic `tone-*` badges and `InlineAlert` banners, `ThesisFileTypeIcon` for PDF/DOCX assets, `PdfPreviewDialog`, `ArchivalHistoryDialog` with `onPreviewPdf`, and `ConfirmDialog`).
STORY: The student immediately sees whether archival submission is unlocked, needs revision, or is locked/confirmed; reviews post-defense council & faculty feedback alongside the upload zone; drags & drops `.pdf` + `.docx` with instant local or server PDF preview; inspects live keyword tags and character counts for bilingual VI/EN abstracts/keywords; confirms submission in a summary `ConfirmDialog`; avoids losing edits via `useBlocker` + `beforeunload`; and tracks the 3-step pipeline + version history from the sticky 340px right dock.
FIRST VIEWPORT: `PageHeader` (`accent`, no icon per user instruction, concise group/topic description, `StatusBadge` + version pill + `Làm mới` button); compact 3-milestone hairline progress strip (`ArchivalPipelineStrip`) (`Sinh viên nộp hồ sơ` -> `GVHD xác nhận` -> `Khoa nghiệm thu`) when confirmed member; responsive 2-column grid (`lg:grid-cols-[minmax(0,1fr)_340px]`): left column adapts cleanly to state (`Blocked` shows eligibility & council lock blockers without disabled form clutter; `Editable` shows revision/submitted callout + unified `Card` with `divide-y` containing drag-and-drop `ArchivalFileDropSlot`s using `ThesisFileTypeIcon` + `<Eye />` preview, bilingual abstracts & keyword tag previews, and integrated footer action bar; `Locked/Confirmed` shows verified archival package download/preview cards + read-only bilingual metadata); right sticky `340px` hairline-divided dock (`ArchivalDossierSidebar`) holds live `4/4` readiness checks + Stage 7 deadline, post-defense feedback & official results link, and version history drawer button (`onPreviewPdf` enabled).
FORM: Selected candidate `state-driven-archival-dossier-and-rail` (index 6 on grounded list; `seed:705aacd7053d3c38`), code-led. Signature interaction: live drag-and-drop PDF/DOCX slots with instant local/server `PdfPreviewDialog`, real-time bilingual keyword chip parsing, and `ConfirmDialog` package verification + `useBlocker` / `beforeunload` unsaved draft guard.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Finish review
- **Verdict:** Shipped cleanly. Redesigned `/thesis/archival` (`StudentArchivalPage.tsx`, `useStudentArchival.ts`, `ArchivalFileDropSlot.tsx`, `ArchivalPipelineStrip.tsx`, `ArchivalDossierSidebar.tsx`, `archival.ts`) to a **State-Driven Archival Dossier + Sticky 340px Readiness Rail** matching Modern Clean SaaS + Fluent Design.
- **Changes implemented & verified:**
  1. Removed `icon={<Archive />}` from `PageHeader` and eliminated decorative card-header icon clutter, synchronizing with `AdvisorArchivalPage` and `ArchivalReviewPage`.
  2. Synchronized file icons to `<ThesisFileTypeIcon />` + `<Eye />` + `<Download />` across editable dropzones (`ArchivalFileDropSlot`) and read-only submitted cards, plus wired `onPreviewPdf` / `previewingKey` into `ArchivalHistoryDialog`.
  3. Replaced the 3 equal-width boxed step cards with hairline-divided `ArchivalPipelineStrip`, unified the main upload + bilingual metadata sheet (`divide-y divide-line-subtle`) with an integrated footer CTA + `ConfirmDialog`, and unified the right column into `ArchivalDossierSidebar` (`340px`, `divide-y`).
  4. Added live drag-and-drop `.pdf`/`.docx` slots, local & server `PdfPreviewDialog`, live keyword chip preview (`parseArchivalKeywords`), word/character counters, `Submitted` pending-review `InlineAlert`, and `useBlocker` + `beforeunload` unsaved draft protection.
  5. Opened Stage 7 (`ARCHIVAL_AND_PUBLICATION`) for batches `ad3bc9b1` & `e1a10001` and locked defense minute on `HĐ-TEST-06` for `24520700` so both `24520700` and Group C (`22520305`/`22520306`) as well as existing `SeedArchivalDemo` (`22520501..22520512`) are directly testable.
- **Issues caught & fixed during visual inspection:**
  1. Anchored `Phiên bản` and `Đủ 4 trường` badges cleanly at top-right (`min-w-0 flex-1` without wrapping below subtitles).
  2. Compacted `Bỏ chọn` in `ArchivalFileDropSlot` to an icon button (`size-8`) so `Xem trước`, `[×]`, and `Đổi tệp` fit on one aligned row on desktop.
  3. Added contextual `Submitted` info banner explaining `canSubmit` replacement (`v{next}`) while pending advisor confirmation.
- **Verification:**
  - `impeccable detect --json` on all 4 archival UI `.tsx` files -> `[]` (0 violations).
  - `npm --prefix frontend run build` (`tsc -b && vite build`) succeeded; `npm --prefix frontend run lint` (`oxlint`) reported `0 errors` and 0 warnings in touched files.
  - Playwright screenshots inspected visually at `1440x900` Light/Dark and `390x844` Mobile across Group C (`22520305` editable/preview/confirm), `22520505` (`NeedRevision`), `22520501` (`Submitted` + history drawer), and `22520509` (`ArchiveConfirmed` read-only), with `0` uncaught `pageerror`s.
  - Documented conventions under `## Hồ sơ lưu chiểu (/thesis/archival)` in `DESIGN.md`.
