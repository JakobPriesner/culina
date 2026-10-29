# Scrollbar & Overflow Audit (2026-09-19)

Audit record of scrollbar styling, horizontal chip rails, and modal scroll locks across macOS, Windows, iOS, and Android.

---

## 1. Global Scrollbar Rules (`src/frontend/src/app.css`)

- **Root & Vertical Containers:** Use thin, warm-toned scrollbars (`scrollbar-width: thin; scrollbar-color: var(--color-border-strong) transparent`) so Windows/Linux browsers never render harsh `17px` default light-gray OS scrollbars in dark or warm-parchment themes.
- **Horizontal Filter & Pill Rails (`.chip-scroll`, `.day-strip`, `.tab-rail`):**
    - Hide visual scrollbar tracks (`scrollbar-width: none; -ms-overflow-style: none; ::-webkit-scrollbar { display: none; }`) while maintaining `overflow-x: auto; -webkit-overflow-scrolling: touch; overscroll-behavior-x: contain`.
    - Apply subtle edge fade masks or trailing peek spacing so horizontal scrollability remains obvious on touch and trackpad devices.

---

## 2. Verified Fixes

- **Modal & Drawer Scroll Locking (`Drawer.svelte`, `CookingMode.svelte`, `SearchOverlay.svelte`):**
    - Background `<body>` scroll is locked cleanly without horizontal jump (`scrollbar-gutter: stable`) and always restored via `$effect` cleanup when unmounted or navigated away.
- **Nested Double-Scrollbars Eliminated:**
    - Removed redundant inner `overflow-y: auto` wrappers inside `FoodSearchSheet`, `AssistantDrawer`, and `RecipeForm` so each sheet has a single predictable scroll container.
