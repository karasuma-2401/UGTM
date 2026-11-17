---
version: 1
slug: "atures-academic-pages-scoreconfigpage-tsx-0ed274a2"
primary_target: "frontend/src/features/academic/pages/ScoreConfigPage.tsx"
related_targets: ["frontend/src/features/academic/components/score-config"]
---

## Scope
Route /admin/grade-config, Operate. Preserve APIs, validation, version lifecycle and shared navigation. User chose workspace with editable table left and summary/save right; equal priority for editing and version awareness.

## Direction contract
THESIS: A focused grading workspace with the component table as the main working area and a quiet persistent summary.
OWN-WORLD: Inherit UGTM Inter, blue primary, slate canvas, white surfaces and subtle borders. Solid blue business actions; no new dependencies.
STORY: Select a batch, set calculation rules, edit weights, inspect the live summary and save using existing version controls.
FIRST VIEWPORT: Compact page heading and utility actions; a wide left column with labeled batch selector and two rules, followed by the component table and integrated add action. A 300px right column carries status, batch, definition list and save. Mobile stacks the summary after the table; individual table rows become labeled blocks.
FORM: User-selected candidate 3, workspace. seed:d892fb3063200c58. Code-led. Signature interaction: editing a weight updates its share and the summary without changing focus.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Finish review
Disposition: ship, independent impeccable_finish_reviewer. Reviewed 1440px, 1280px, 1847px and 390px captures, add-component and history dialogs. No material fixes requested. Evidence uses mocked API data in an isolated browser; real authenticated server integration remains unverified.

## Verification and documentation
Lint and production build passed with existing unrelated warnings. All 6 codex-review-fixes tests passed, including save-before-publish. Manual detector returned no findings. Browser checks verified weight totals and dirty state, percentage conversion, minimum-score clearing, invalid totals, clone dialog, empty batch state, and no document overflow. Existing PRODUCT.md and DESIGN.md were checked against the result and preserved: this surface inherits current tokens, typography and components; no shared design-system change. No shipping raster assets were introduced. Browser fixtures and screenshots remain in ignored artifacts/playwright.
