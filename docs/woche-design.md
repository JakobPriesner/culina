# Weekly Planner (`Woche`) Architecture & Design

Specification for the 7-day household meal planner (`src/frontend/src/routes/woche/+page.svelte` and `src/backend/Culina.Api/Endpoints/MealPlanEndpoints.cs`).

---

## §1–§4. Layout & Temporal Structure

- **ISO Week Grid (Monday–Sunday):** Keyed by `WeekStartDate` (Monday). Header provides week switcher (`Vorherige`, `Heute`, `Nächste`), calendar jump, `Woche kopieren`, `Vorlagen`, and `Einkaufsliste erstellen`.
- **Responsive Viewport Adaptation:**
    - **Desktop (`>= 1024px`):** 7-column week board with collapsible slot rows (`Frühstück`, `Mittag`, `Abend`, plus optional `Snack` and `Vorbereitung / Meal Prep`).
    - **Mobile (`< 768px`):** Horizontal day-pill strip at top (`Mo`–`So`, auto-scrolled to today) + vertical timeline of the 7 days.

---

## §5. Core Planner Interactions (`§5.1`–`§5.8`)

- **§5.1 Contextual Picker Drawer:** Tapping `+` on any `(Date, MealType)` cell opens the recipe picker pre-populated with **contextual suggestions** (`GET /api/v1/suggestions?date=...&mealType=...`) plus instant search and a freeform custom note tab (`"Reste"`, `"Auswärts essen"`).
- **§5.2 Drag-and-Drop Rescheduling:** Planned meals can be dragged between any days/slots (pointer drag on desktop, long-press move sheet on mobile), updating `(Date, MealType)` optimistically.
- **§5.3 Per-Slot Portion Override:** Each `MealPlanEntry` stores its own `Servings` count (defaulting to household size or `Recipe.Servings`), adjustable inline via `- / +` steppers.
- **§5.4 Leftover & Batch-Cooking Flow (`Reste einplanen`):** Scaling a meal above household size or tapping `"Für morgen mitkochen"` creates a linked leftover entry (`LeftoverSourceEntryId`) on a subsequent day whose ingredients are **not** double-counted on the shopping list.
- **§5.5 Ingredient Availability Badges (`GET /api/v1/meal-plan/availability`):** Each planned card shows a subtle traffic-light indicator:
    - **Sage (`Vorrat komplett`):** All non-staple ingredients are in `PantryItems`.
    - **Terracotta (`Auf Einkaufsliste`):** Missing ingredients are already on the active shopping list.
    - **Ochre (`Zutaten fehlen`):** Missing ingredients are neither in pantry nor on the shopping list.
- **§5.6 One-Click Shopping List Generation:** `Einkaufsliste erstellen` aggregates all planned meals for the week (or selected date range), subtracts pantry stock, and merges items into `ShoppingList`.
- **§5.7 Mark as Cooked (`Gekocht`):** Checking off a planned meal invokes `POST /api/v1/recipes/{id}/cooked`, recording `CookedAt`, deducting pantry stock, and offering a 1-tap diary log.
- **§5.8 Week Templates (`MealPlanTemplate`):** Households can save recurring week patterns (e.g., `"Standard-Arbeitswoche"`) and apply them to any future week.

---

## §9–§10. Daily Calorie Preview & Prep Reminders (`§10.1`–`§10.5`)

- **§9 Day Energy Footer:** When recipes have nutrition estimates, each day column displays the summed planned `kcal` per person.
- **§10 Preparation & Thawing Hints (`§10.1`–`§10.5`):** Recipes with long resting/marinating times (`RestingTimeMinutes >= 180` or `#meal-prep` / `#über-nacht`) surface a reminder badge on the preceding evening slot (`"Vorbereitung am Vorabend beachten"`).
