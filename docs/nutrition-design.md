# Nutrition Pipeline & Calculation Specification

Specification for Culina's multi-tier ingredient nutrition resolver, cooking retention adjustments, and BMR/TDEE energy calculations (`src/backend/Culina.Domain/Services/RecipeNutritionEstimator.cs`, `NutritionCalculator.cs`, and `Culina.Infrastructure/Nutrition/`).

---

## §A–§C. Data Sources & Multi-Tier Resolution Cascade

When computing nutrition for a recipe (`RecipeNutritionEstimator`) or searching foods (`GET /api/v1/foods/search`), Culina resolves each ingredient through a deterministic 4-tier cascade:

1. **Tier 1 — German Federal Food Code (`BLS 4.0`, `BlsFoodItems` table):**
    - Seeded at startup by `BlsSeederHostedService` (`~3,500` canonical German foods with `kcal`, `protein`, `carbs`, `fat`, `fiber`, `sugar` per `100 g` edible portion, plus culinary density `g/ml` and piece weights `g/Stück`).
    - Matched via normalized German name, synonym map, and `pg_trgm` similarity.
2. **Tier 2 — Household Custom Foods & Cached Products (`CustomFoodItems` & `BarcodeProductCache`):**
    - User-defined foods and previously scanned EAN products with verified macros per `100 g` and optional `servingWeightGrams`.
3. **Tier 3 — Open Food Facts v2 API (`OpenFoodFactsClient`):**
    - Live barcode (`GET /api/v1/foods/barcode/{ean}`) and text search fallback targeting DACH product data (`de.openfoodfacts.org`), cached locally in PostgreSQL (`BarcodeProductCache`, 30-day TTL).
4. **Tier 4 — AI Estimation Fallback (`POST /api/v1/nutrition/estimate-recipe/{id}`):**
    - For exotic or unmapped ingredients, calls the household's configured fast AI model with structured JSON output and sets `Recipe.NutritionIsAiEstimated = true`.

---

## §D–§F. Unit-to-Gram Normalization & Piece Weights

All nutrition reference values are stored per **`100 g` edible portion**. Before multiplying macros, `RecipeNutritionEstimator` converts `(Amount, Unit, IngredientName)` into grams:

| Input Unit                                                               | Conversion Rule                                                                                                                                                    |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `g`, `kg`, `mg`                                                          | Exact mass conversion (`1 kg = 1000 g`).                                                                                                                           |
| `ml`, `cl`, `dl`, `l`, `TL` (`5 ml`), `EL` (`15 ml`), `Tasse` (`240 ml`) | Multiplied by ingredient-specific **density** ($\rho$ in $\text{g/ml}$, e.g. `Olivenöl = 0.91`, `Weizenmehl = 0.55`, `Honig = 1.40`, `Wasser/Milch/Brühe = 1.00`). |
| `Stück`, `Zehe`, `Scheibe`, `Bund`, `Dose`, `Packung`, `Prise`           | Looked up in ingredient-specific **Portion Weight Table** (`Zwiebel = 85 g`, `Knoblauchzehe = 4 g`, `Ei = 55 g`, `Dose Tomaten = 400 g`, `Prise = 0.3 g`).         |

---

## §G–§J. Edible Yield, Fat Uptake & Cooking Retention Factors

Raw ingredient macros are adjusted for kitchen preparation:

- **§G Edible Portion Yield (`YieldFactor`):** Accounts for peel, core, bone, or shell removed before cooking when recipes specify gross count/weight (e.g., `Banane = 0.68`, `Avocado = 0.70`, `Zwiebel = 0.90`).
- **§H Frying Oil / Marinade Absorption:** When step instructions indicate pan-frying (`anbraten`) vs. deep-frying (`frittieren`) or marinating, only the absorbed fraction of cooking fat/marinade is counted unless the fat remains in a sauce.
- **§I–§J Nutrient Retention & Atwater Sanity Check:**
    - Macro totals per serving are rounded to integer `kcal` and `0.1 g` for `Protein`, `Carbs`, `Fat`, `Fiber`, `Sugar`.
    - Sanity guard ensures total calories align within tolerance of Atwater energy equivalents ($4 \cdot P + 4 \cdot C + 9 \cdot F + 2 \cdot \text{Fiber}$).

---

## §K–§M. BMR, TDEE & Macro Target Calculator (`NutritionCalculator.cs`)

Users can set manual calorie/macro targets in `NutritionGoal` or compute them from body metrics using the **Mifflin–St Jeor** equation (§M):

$$\text{BMR} = 10 \cdot \text{weight}_{\text{kg}} + 6.25 \cdot \text{height}_{\text{cm}} - 5 \cdot \text{age}_{\text{years}} + s \quad (s = +5 \text{ male},\; -161 \text{ female},\; -78 \text{ neutral})$$

- **Total Daily Energy Expenditure ($\text{TDEE} = \text{BMR} \times \text{PAL}$):**
    - `Sedentary` (`1.2`), `LightlyActive` (`1.375`), `ModeratelyActive` (`1.55`), `VeryActive` (`1.725`), `ExtraActive` (`1.9`).
- **Goal Calorie Adjustments & Default Macro Split:**
    - `LoseWeight` ($\text{TDEE} - 500\text{ kcal}$, floor `1200 kcal`), `Maintain` ($\text{TDEE}$), `GainMuscle` ($\text{TDEE} + 300\text{ kcal}$).
    - Default macro distribution: `Protein = 1.6–2.0 g/kg` (or `25%`), `Fat = 30%`, `Carbs = remaining kcal (45%)`.

---

## §N–§R. Caching, Confidence Badges & UI Presentation

- **§N–§P Recipe Cache Invalidation:** Saving changes to a recipe's `Servings` or `RecipeIngredients` recomputes deterministic BLS/cache nutrition immediately; if deterministic coverage is `>= 85%` of total ingredient mass, the result is persisted with `NutritionIsAiEstimated = false`.
- **§Q–§R UI Transparency:** Recipe detail (`RecipeNutritionBar.svelte`) displays `kcal`, `Eiweiß`, `Kohlenhydrate`, `Fett`, and `Ballaststoffe` per portion with a clear `BLS / berechnet` vs. `KI-Schätzung` indicator badge.
