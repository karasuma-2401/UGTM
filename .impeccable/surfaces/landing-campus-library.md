# Landing page and thesis library

Primary target: frontend/src/features/landing/LandingPage.tsx
Related targets: frontend/src/features/landing, frontend/src/features/library
Mode: Persuade; library: Read / Operate.

## Direction contract

THESIS: UIT Campus × Kho tri thức khóa luận, approved in conversation. Campus earns attention; searching completed work and entering UGTM are immediately available.

OWN-WORLD: Preserve UGTM blue and orange. A light ivory campus presentation with Manrope Vietnamese lettering, deep blue reading sections, generous space, rounded controls and restrained rules. Scoped to public pages; application tokens and dashboard stay intact.

STORY: Understand UGTM, explore recognizable UIT architecture, discover approved thesis metadata, sign in for documents and enter the existing workflow.

FIRST VIEWPORT: Desktop left text and two direct actions; right a large architectural campus island with soft daylight. At narrow sizes the copy precedes the model, without overlaid body text. Navigation always exposes the library and account entry.

FORM: User-pinned campus journey followed immediately by library search, then research workflow and system entry. seed: none (pinned). Build path: code-led.

SIGNATURE: A reusable GLB campus with named viewpoints and optional orbit controls, replacing the original long scroll-bound tour. Respect reduced motion, pause offscreen, use a skeleton during loading and reserve the photographic fallback for failures.

Campus update: Keep a single, enlarged interactive campus in the hero, with its controls directly below. Remove the separate lower campus section. Reload and reset face gate A as confirmed by the user's reference: gate in the foreground, A/library to the left, E to the right. Preserve the original magnification; expand/recenter the overview canvas around actual mesh bounds rather than backing the camera away. The 20-second guided loop uses 21 camera stops: enter the gate, fly between buildings and through B's courtyard, pass C, climb E and circle the entire campus. Shot-specific timing slows architectural reveals and accelerates aerial transitions, with continuous speed at joins, a closed seam and pause/resume in place. Anonymous walking figures represent current landing visitors and signed-in users, using a shared backend presence roster; deduplicate tabs/accounts, allow pausing and respect reduced motion. Keep the library directly after the hero.

Constraints: no invented publications, metrics or testimonials. Campus follows UIT's published DGNL 2026 schematic and façade guide, not surveyed geometry. Heights and dimensions remain approximate. Public metadata is explicitly curated; private archive endpoints remain protected. Empty, failed and loading data remain distinct.

Photo refinement: Use the user's 23 campus photographs for permanent architectural details: A/B gates and UIT landmark, façade louvers/columns, B's open galleries, C's atrium pond, connected covered paths, yellow/red slatted event-square shades, sports courts, parking and dining. Keep the existing overview magnification. The hero reserves only its original breakpoint height, with an absolutely positioned render canvas anchored above the controls; orbiting must not change header/copy/panel positions or scroll height. Do not reserve the largest orbit envelope as blank layout space. Interactive touch drags control the model without scrolling the page.

Latest user corrections: Remove the entire footer row containing the walker pause button and UIT 360° link. Keep reduced-motion support. Add +/− zoom buttons and native wheel/pinch zoom in manual interaction mode, with reset to the original scale. Freeze framing when interaction starts so auto-fit cannot cancel zoom. The user marked and explicitly confirmed an extension OUTSIDE the existing rear tree line beside the low dining/service house. Join a sports annex to the old z=-63 boundary, extending to z=-101; move all courts, fences, benches and water shade there, with a path through the retained tree line. Do not put courts on the interior lawn around the house. Trim gate B's approach road to the campus island edge.
