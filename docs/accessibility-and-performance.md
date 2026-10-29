# Accessibility & Performance Standards

Targets, budgets, and verification gates for Culina's web and native surfaces.

---

## 1. Accessibility (WCAG 2.2 AA)

- **Colour Contrast:**
    - Primary and secondary body text (`--color-text-primary`, `--color-text-secondary`) maintain `>= 4.5:1` contrast against both `--color-bg-canvas` and `--color-bg-surface` in light and dark modes.
    - UI components, input borders on focus, and active state indicators maintain `>= 3:1`.
- **Keyboard & Focus Management:**
    - Global shortcuts (`Cmd/Ctrl+K` for Olli / search, `Escape` to close overlays, arrow keys in autocomplete lists) never trap keyboard tab navigation.
    - Modals and bottom drawers (`Drawer.svelte`) trap focus while open, announce via `role="dialog"` + `aria-modal="true"`, and restore focus to the trigger element on close.
    - Every icon-only button (`Button`, `Header`, `CookingMode`, `FloatingTimerBar`) carries an explicit German `aria-label`.
- **Touch Target Floors:**
    - Standard interactive controls: minimum `44×44px` hit area.
    - Kitchen / Cooking Mode (`CookingMode.svelte`, step timers, portion steppers): minimum `48×48px` hit area with `>= 8px` gap between adjacent actions.
- **Screen Readers & Live Regions:**
    - Timer expirations, shopping list SSE updates, toast notifications, and search result counts announce via polite `aria-live="polite"` / `role="status"` regions.
- **Reduced Motion:**
    - `@media (prefers-reduced-motion: reduce)` disables transform animations and collapses transition durations in `app.css`.

---

## 2. Performance Budgets

| Metric                                 | Target               | Mechanism                                                                                                                                                                     |
| -------------------------------------- | -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Initial JS Bundle (gzipped)**        | `< 120 KB`           | Route-level code splitting in SvelteKit; heavy utilities (`zxing-wasm` barcode reader, chart helpers) lazy-loaded on demand.                                                  |
| **Largest Contentful Paint (LCP)**     | `< 1.8 s` (4G / LAN) | Preloaded Variable fonts (`Plus Jakarta Sans`, `Newsreader`), server-converted `.webp` recipe images (`<= 1600px`), `loading="lazy"` + `decoding="async"` on offscreen cards. |
| **Interaction to Next Paint (INP)**    | `< 100 ms`           | Optimistic UI updates on shopping check/uncheck, favorites, ratings, and meal plan drag-and-drop.                                                                             |
| **Recipe Search Latency (`p95`)**      | `< 35 ms`            | Precomputed `recipe_search_documents` table with 4 GIN indexes (`tsvector` DE/EN + `pg_trgm` title/ingredients) and single-pass union index scan.                             |
| **Completion Overlay Latency (`p95`)** | `< 15 ms`            | Prefix/trigram index lookup capped at 8 suggestions (`GET /api/v1/recipes/completions`).                                                                                      |
