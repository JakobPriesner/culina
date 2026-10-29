# App Structure & Navigation Architecture

Information architecture, route map, and shell navigation contract for Culina (`src/frontend/src/routes/+layout.svelte`, `Sidebar.svelte`, `BottomNav.svelte`, `Header.svelte`).

---

## §1–§3. Five Primary Destinations

Culina organizes every workflow around **5 top-level destinations** shared identically between the desktop sidebar (`Sidebar.svelte`) and mobile bottom tab bar (`BottomNav.svelte`):

| #   | Label        | Route            | Purpose                                                                                                                                                                    |
| --- | ------------ | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Heute**    | `/`              | Daily kitchen dashboard: today's planned meals (`Woche`), expiring pantry alerts, active cook session resume banner, quick contextual suggestions, and diary ring summary. |
| 2   | **Sammlung** | `/rezepte`       | Unified recipe library, hybrid search & filter bar, manual/smart **Kochbücher** (`/kochbuecher`), and connected partner libraries.                                         |
| 3   | **Woche**    | `/woche`         | 7-day household meal planner (`Mo–So`), availability indicators, week templates, and batch shopping list generator.                                                        |
| 4   | **Einkauf**  | `/einkaufsliste` | Real-time SSE-synced supermarket shopping list + tab switch to household pantry & staples (**Küche / Vorrat** at `/kueche`).                                               |
| 5   | **Tagebuch** | `/tagebuch`      | Personal nutrition diary (`Frühstück`, `Mittag`, `Abend`, `Snacks`), barcode scanner, macro rings, water stepper, and weight EMA trend.                                    |

---

## §4–§5. Secondary Routes & Global Overlays

- **Recipe Views:** `/rezepte/[id]` (detail + portion scaling + inline cooking mode), `/rezepte/neu` (multimodal URL/photo/text/archive intake), `/rezepte/[id]/bearbeiten` (structured editor).
- **Cookbooks & Public Shares:** `/kochbuecher`, `/kochbuecher/[id]`, `/shared/cookbook/[token]`, `/shared/recipe/[token]`.
- **Settings & Account:** `/einstellungen` (Profile, Passkeys, Household members & invites, AI provider keys, Personal Access Tokens, Connected Libraries, Full ZIP Archive export/import).
- **Global Persistent Overlays:**
    - **Search / Command Overlay (`Cmd/Ctrl+K`):** Instant recipe completions and filter jumps.
    - **Olli Kitchen Assistant (`AssistantDrawer.svelte`):** Context-aware streaming drawer receiving current route/recipe/week context (`pageContext`).
    - **Floating Timer Bar (`FloatingTimerBar.svelte`):** Persistent pill pinned above the bottom nav whenever one or more cooking timers are ticking.

---

## §6–§7. Navigation State, Scroll Restoration & Transitions (`§6.3`, `§7`)

- **§6.3 Collection Filter Persistence:** Navigating from `/rezepte?q=pasta#vegetarisch` into `/rezepte/[id]` and pressing Back restores the exact URL query parameters, active cookbook pill, and scroll offset.
- **§7 View Transitions & Shell Stability:** Top-level tab switches preserve shell chrome without layout shift; `CookingMode` and fullscreen modals lock background body scroll cleanly (`overflow: hidden`) and release it on unmount.
