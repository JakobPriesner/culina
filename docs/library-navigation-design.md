# Library (`Sammlung`) & Cookbook Navigation Design

Specification for how recipes, cookbooks (`Kochbücher`), smart filters, and connected libraries unify inside `Sammlung` (`src/frontend/src/routes/rezepte/+page.svelte` and `/kochbuecher`).

---

## 1. Unified Collection Model

Instead of forcing users to bounce between a `/rezepte` page and a separate `/kochbuecher` hierarchy to search their recipes, **Sammlung (`/rezepte`)** acts as a single unified lens:

- **Top Scope Bar (Cookbook & Library Pills):**
    - `Alle Rezepte` (default)
    - `♥ Favoriten` (`favoritesOnly=true`)
    - `Kompakt` (`isQuickReference=true`)
    - User's pinned **Kochbücher** (both manual and smart `SmartFilterQuery` cookbooks)
    - **Verbundene Sammlungen** (read-only feeds from partner households)
    - `Kochbücher verwalten` (`/kochbuecher` overview for creating, sharing, and reordering cookbooks)

---

## 2. Composable Search + Scope Filtering

- Selecting a cookbook pill sets `cookbookId` in the URL query string (`/rezepte?cookbook=<id>`) while keeping the full hybrid search bar (`q`), tag pills (`#vegetarisch`, `~30m`), and sort selector (`Relevanz`, `Neueste`, `Beste Bewertung`, `Schnellste`, `A–Z`, `Zufall`) active within that cookbook.
- Users can save any active search query + filter combination directly as a **Smart-Kochbuch** (`"Als Smart-Kochbuch speichern"`), which persists `Cookbook.SmartFilterQuery` and updates dynamically as matching recipes are added.
