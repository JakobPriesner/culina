# Design System — Warm Modernism

Visual and interaction specification for Culina. Every component in `src/frontend/src` consumes the CSS custom properties defined in `src/frontend/src/app.css`.

---

## 1. Aesthetic Direction

**Warm Modernism** combines editorial cookbook typography (`Newsreader` optical serif) with Swiss functional precision (`Plus Jakarta Sans` UI + `JetBrains Mono` quantities/timers) on warm parchment and alabaster surfaces.

- **No cold SaaS grays or neon accents:** Surfaces use warm linen/stone undertones; primary actions use terracotta (`--color-accent`) and herbal sage (`--color-secondary`).
- **Tactile clarity:** Hairline borders (`1px solid var(--color-border-subtle)`), subtle elevation, and generous touch targets (`>= 44px`, `>= 48px` in cooking mode).

---

## 2. Three-Layer Colour Token Architecture

Never reference raw hex values or `--palette-*` primitives directly in components; always use **Layer 2 semantic tokens**.

### Layer 1 & 2 — Core Semantic Surface & Text Tokens

| Semantic Token           | Light Mode (Warm Parchment) | Dark Mode (Warm Espresso) | Usage                                                                     |
| ------------------------ | --------------------------- | ------------------------- | ------------------------------------------------------------------------- |
| `--color-bg-canvas`      | `#FAF7F2`                   | `#141210`                 | App background canvas                                                     |
| `--color-bg-surface`     | `#FFFFFF`                   | `#1E1B18`                 | Cards, sheets, list containers                                            |
| `--color-bg-elevated`    | `#F3EFE6`                   | `#282420`                 | Inputs, pills, secondary wells, hover states                              |
| `--color-bg-glass`       | `rgba(250, 247, 242, 0.85)` | `rgba(20, 18, 16, 0.85)`  | Sticky headers, bottom nav, floating bars (`backdrop-filter: blur(16px)`) |
| `--color-text-primary`   | `#1C1917`                   | `#F5F2EB`                 | Headings, body copy, ingredient names                                     |
| `--color-text-secondary` | `#57534E`                   | `#B8B0A4`                 | Metadata, prep notes, secondary labels                                    |
| `--color-text-muted`     | `#8C857B`                   | `#7D756B`                 | Placeholders, disabled text, timestamps                                   |
| `--color-border-subtle`  | `#E7E2D8`                   | `#2E2924`                 | Card outlines, hairlines, table dividers                                  |
| `--color-border-strong`  | `#D4CDBE`                   | `#423B34`                 | Focused inputs, active chip outlines                                      |

### Brand, Semantic & Macro Accent Tokens

| Token                   | Light                  | Dark      | Usage                                               |
| ----------------------- | ---------------------- | --------- | --------------------------------------------------- |
| `--color-accent`        | `#C85A32` (Terracotta) | `#E0724A` | Primary CTAs, active tabs, timers, links            |
| `--color-accent-subtle` | `#F9EBE5`              | `#331E16` | Selected pill background, active nav indicator      |
| `--color-secondary`     | `#4A6B53` (Sage)       | `#699174` | Pantry/in-stock badges, vegetarian tags, completion |
| `--color-warning`       | `#C27D1A` (Ochre)      | `#E59A30` | Expiring pantry items, calorie over-target warnings |
| `--color-danger`        | `#B83B3B`              | `#E05858` | Destructive actions, errors, allergen exclusions    |
| `--color-macro-kcal`    | `#C85A32`              | `#E0724A` | Calorie progress ring & sparkline                   |
| `--color-macro-protein` | `#4A6B53`              | `#699174` | Protein macro bar (Sage)                            |
| `--color-macro-carbs`   | `#C27D1A`              | `#E59A30` | Carbohydrate macro bar (Ochre)                      |
| `--color-macro-fat`     | `#8C5E88`              | `#B07DAB` | Fat macro bar (Plum)                                |

---

## 3. Typography & Numeric Discipline

| Token            | Font Stack                                   | Role                                                            |
| ---------------- | -------------------------------------------- | --------------------------------------------------------------- |
| `--font-display` | `'Newsreader', Georgia, serif`               | Recipe titles, page headers (`h1`, `h2`), editorial pull-quotes |
| `--font-sans`    | `'Plus Jakarta Sans', system-ui, sans-serif` | All UI controls, body copy, step instructions, navigation       |
| `--font-mono`    | `'JetBrains Mono', ui-monospace, monospace`  | Ingredient amounts, timers, macro gram readouts                 |

### Fluid Type Scale & Numeric Rules

- `--text-xs` (`0.75rem`), `--text-sm` (`0.875rem`), `--text-base` (`1rem`), `--text-lg` (`1.125rem`), `--text-xl` (`1.25rem`), `--text-2xl` (`clamp(1.5rem, 2.2vw, 1.875rem)`), `--text-3xl` (`clamp(1.875rem, 3vw, 2.5rem)`).
- **Tabular numerals:** Always apply `font-variant-numeric: tabular-nums` on ingredient amounts, portion steppers, countdown timers, and nutrition tables so columns never jitter during scaling or ticking.
- **Fractions:** Use Unicode vulgar fractions (`½`, `¼`, `¾`, `⅓`, `⅔`) formatted by `QuantityFormatter`.

---

## 4. Spacing, Radii & Layout Breakpoints

- **Spacing scale (`4px` base):** `--space-1` (`4px`), `--space-2` (`8px`), `--space-3` (`12px`), `--space-4` (`16px`), `--space-5` (`20px`), `--space-6` (`24px`), `--space-8` (`32px`), `--space-10` (`40px`), `--space-12` (`48px`).
- **Border radii:** `--radius-sm` (`6px`), `--radius-md` (`10px`), `--radius-lg` (`14px` — standard cards/inputs), `--radius-xl` (`20px` — modals/sheets), `--radius-full` (`9999px` — pills/chips).
- **Breakpoints & Navigation Shell:**
    - **Mobile (`< 768px`):** Single-column layout, bottom tab bar (`56px + env(safe-area-inset-bottom)`), bottom sheets (`Drawer.svelte`) for contextual actions.
    - **Tablet (`768px – 1023px`):** Collapsible navigation rail, 2-column recipe grids, split ingredient + step layout in recipe detail.
    - **Desktop (`>= 1024px`):** Persistent left sidebar (`240px`), max content container `1200px` (`760px` reading measure for instructions), centered modals.

---

## 5. Motion & Transitions

| Token               | Value                           | Usage                                            |
| ------------------- | ------------------------------- | ------------------------------------------------ |
| `--ease-out-expo`   | `cubic-bezier(0.16, 1, 0.3, 1)` | Sheet entry, modal pop, card expansion           |
| `--ease-in-out`     | `cubic-bezier(0.4, 0, 0.2, 1)`  | Colour/border state transitions                  |
| `--duration-fast`   | `140ms`                         | Hover, button press (`scale(0.98)`), chip toggle |
| `--duration-normal` | `220ms`                         | Drawer slide, popover fade, tab indicator        |
| `--duration-slow`   | `360ms`                         | Page view transitions, layout reflows            |

All animations respect `@media (prefers-reduced-motion: reduce)` by collapsing durations to `0.01ms` and disabling spring transforms.

---

## 6. Component & Accessibility Standards

- **Touch targets:** Minimum `44×44px` across the app; `48×48px` in `CookingMode.svelte` and `FloatingTimerBar.svelte` (wet/messy hands).
- **Focus rings:** Visible `2px solid var(--color-accent)` with `2px` offset on `:focus-visible`; never suppress outline without a high-contrast replacement.
- **Scrollbars:** Custom overlays use thin, unobtrusive scrollbars (`scrollbar-width: thin`); horizontal chip rows hide scrollbars cleanly while preserving touch/wheel momentum.
- **Olli Assistant:** Triggered via header button or `Cmd/Ctrl+K`. Never auto-popups unsolicited tooltips; tool actions that mutate household state (`plan_meal`, `add_shopping_items`, `create_recipe`) render explicit confirmation cards or undo toasts.
