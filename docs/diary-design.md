# Nutrition Diary (`Tagebuch`) Architecture & Design

Specification for Culina's personal food, macro, weight, and water tracking surface (`src/frontend/src/routes/tagebuch/`, `src/backend/Culina.Api/Endpoints/DiaryEndpoints.cs`).

---

## §0. Core Philosophy — Supportive, Non-Punitive Tracking

1. **Private to the Individual (`UserId`-scoped):** Unlike recipes, meal plans, and shopping lists (which belong to the shared `Household`), all diary entries (`FoodLogEntry`), goals (`NutritionGoal`), weight logs (`WeightEntry`), and water intake (`WaterIntakeLog`) are strictly private to the logged-in user.
2. **Calm, Neutral Visual Language:** Going over a calorie or macro target uses warm ochre (`--color-warning`), never alarmist red flashing, guilt copy, or streak-shaming.
3. **Zero-Friction Bridge from `Woche` & Cooking Mode:** Planned household meals for today appear as one-tap **"Geplant – Jetzt loggen"** suggestions inside the diary slots, pre-scaled to the user's portion size.

---

## §1–§4. Domain Entities & Data Model

### `FoodLogEntry` (§4)

- **Fields:** `Id`, `UserId`, `Date` (`DateOnly`), `MealSlot` (`Breakfast = 0 | Lunch = 1 | Dinner = 2 | Snack = 3`), `Title`, `Amount`, `Unit` (`g | ml | Portion | Stück`), `Calories`, `ProteinGrams`, `CarbsGrams`, `FatGrams`, `FiberGrams`, `SugarGrams`, `SourceType` (`Recipe | BlsFood | BarcodeProduct | CustomFood | QuickEntry | AiEstimate`), `RecipeId?`, `BlsFoodCode?`, `BarcodeEan?`, `LoggedAt`.
- **Snapshot Immutability:** Macros on `FoodLogEntry` are stored as an immutable point-in-time snapshot when logged so later edits to a recipe do not retroactively rewrite past diary days.

### `NutritionGoal`, `WeightEntry` & `WaterIntakeLog`

- **`NutritionGoal`:** `DailyCalories`, `ProteinGrams`, `CarbsGrams`, `FatGrams`, `FiberGrams`, `WaterMilliliters` (default `2000 ml`), plus optional Mifflin–St Jeor profile inputs (`HeightCm`, `BirthYear`, `BiologicalSex`, `ActivityLevel`, `GoalType`).
- **`WeightEntry`:** One entry per `(UserId, Date)` (`WeightKg`, `Notes`), with 7-day exponentially smoothed trend calculation (`EMA`, $\alpha = 0.25$) so daily water fluctuations do not obscure real progress.
- **`WaterIntakeLog`:** Daily total in `ml` with quick `+250 ml` (glass) and `+500 ml` (bottle) steppers.

---

## §5–§6. Daily View & Meal Slots (`+page.svelte`)

- **§5 Date Navigation & Energy Summary Header:**
    - Day switcher (`Heute`, swipe/arrow prev/next day, calendar picker) + compact 7-day calorie adherence strip.
    - Central energy ring (`gegessen` / `Ziel` / `übrig kcal`) paired with horizontal progress bars for **Eiweiß** (`--color-macro-protein`), **Kohlenhydrate** (`--color-macro-carbs`), **Fett** (`--color-macro-fat`), and **Ballaststoffe**.
- **§6 Four Meal Slots (`Frühstück`, `Mittagessen`, `Abendessen`, `Snacks`):**
    - Each slot shows its total `kcal`, logged items, any unlogged `MealPlanEntry` from `Woche` for that date/slot, and a `+ Hinzufügen` button opening `FoodSearchSheet.svelte`.
- **§6.4 Copy Day & Repeat Yesterday:** Users can copy an entire meal slot or full day (`POST /api/v1/diary/copy-day`) from yesterday or any recent date.

---

## §7–§8. Unified Food Logging Flows

See [`docs/food-log-redesign.md`](food-log-redesign.md) for the unified `FoodSearchSheet` interaction details:

- **§7 Multi-Source Search (`GET /api/v1/foods/search`):** Searches recent/frequent user logs, household `Recipes`, `BLS 4.0` generic foods, `CustomFoodItems`, and `Open Food Facts` in one debounced input.
- **§8 Barcode, Quick-Add & AI Photo/Text Estimation:**
    - **Barcode Scan:** Opens native MLKit camera or web `BarcodeScannerModal` → resolves EAN via `/api/v1/foods/barcode/{ean}` → pre-fills package serving weight.
    - **Quick Calorie Entry (`Schnelleintrag`):** Logs raw `kcal` + optional title/macros in 2 seconds (e.g., `"Restaurant Pizza – 850 kcal"`).
    - **AI Text / Photo Estimate (`/api/v1/nutrition/estimate-text` & `/estimate-photo`):** Parses natural language (`"2 Scheiben Vollkornbrot mit 30g Gouda und 1 Apfel"`) or a meal photo into itemized macro rows for review before saving.

---

## §9–§10. Analytics, Weight Trend & Goal Onboarding (`§9.4`, `§9.6`, `§10`)

- **§9.4 Weekly & 30-Day Macro Trends:** Bar chart showing daily calorie intake vs. target corridor (`±10%` neutral zone) and average weekly macro split.
- **§9.6 Weight Trend & EMA Smoothing:** Plots raw daily weigh-ins as muted dots and the 7-day exponential moving average (`EMA`) as a smooth primary curve.
- **§10 Goal Setup Sheet (`GoalSetupModal.svelte`):** Supports both direct manual macro entry and guided BMR/TDEE calculation with live preview.

---

## §15. Verification & Privacy Tests

- `DiaryEndpointsTests.cs` verifies strict `UserId` isolation (Household Member B receives `404` when attempting to read, update, or delete Household Member A's `FoodLogEntry`, `WeightEntry`, or `NutritionGoal`).
