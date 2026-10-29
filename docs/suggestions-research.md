# Contextual Meal Suggestion Engine

Architecture and scoring specification for Culina's deterministic meal recommendation engine (`src/backend/Culina.Infrastructure/Services/RecipeSuggestionService.cs` & `Culina.Domain/Suggestions/RankingRules.cs`).

---

## §A–§B. Design Goals & Architecture

Culina recommends recipes for the weekly planner (`Woche`), dashboard (`Heute`), and Olli without external ML sidecars or cloud tracking:

- **Deterministic & Explainable:** Every suggested recipe carries a human-readable German reason chip (`"Vorrat fast komplett"`, `"Lange nicht gekocht"`, `"Passt perfekt zum Mittwochabend"`, `"Saison für Spargel"`).
- **Sub-25ms In-Memory Scoring:** Preloads candidate summaries for the household plus user interaction history (`CookHistoryEntries`, `MealPlanEntries`, `UserRecipeRatings`, `RecipeFavorites`, `PantryItems`, `SuggestionDismissals`, `SuggestionImpressions`) and scores candidates in pure domain logic (`RankingRules.cs`).

---

## §C. Behavioral Signals, Decay Curves & Feedback Tables

### Signal Weights & Exponential Half-Lives (§C.1–§C.3)

- **Cook History (`CookHistoryEntry`):** Positive affinity for recipes cooked and enjoyed, suppressed immediately after cooking by the **Recency Cooldown curve** (0.0 at day 0–2, rising smoothly back to 1.0 between day 7 and day 21, with a sweet-spot rediscovery boost at 21–60 days).
- **Planned Meals (`MealPlanEntry`):** Recipes already scheduled in the target week (+/- 4 days) are excluded from candidates; ingredients of adjacent planned meals contribute to **Cross-Day Ingredient Synergy** (using up half a cabbage or open cream carton).

### Dismissals & Impressions (§C.4 & §C.7)

- **`suggestion_dismissals` (§C.4):** Explicit user dismissal (`POST /api/v1/suggestions/dismiss`). Applies a hard exclusion for `7 days` and an exponentially decaying negative penalty (`half-life = 14 days`) up to `60 days`.
- **`suggestion_impressions` (§C.7):** Tracks shown suggestions (`POST /api/v1/suggestions/impressions`). Applies a short-term **Shown Fatigue penalty** (`Shown` term) over `48 hours` so refreshing or reopening the planner rotates fresh ideas instead of repeating the same top 3 cards.

---

## §D–§F. Suggestion Context & Purpose Modes (`SuggestionContext` & `SuggestionPurpose`)

`GET /api/v1/suggestions` evaluates candidates against a `SuggestionContext`:

- **Temporal context:** Target `Date` (`DateOnly`), `DayOfWeek` (weekday vs. weekend effort tolerance), `MealType` (`Breakfast | Lunch | Dinner | Snack | Prep`), and meteorological season / month.
- **Household state:** Stocked `PantryItems` (including `ExpiresAt` urgency), `PantryStaples`, already-planned recipes in the target week, and user `NutritionGoal` (remaining daily kcal/protein budget).
- **`SuggestionPurpose` modes:**
    - `Balanced` (default planner shortlist)
    - `UsePantry` (maximizes pantry coverage + expiring ingredients)
    - `QuickWeeknight` (enforces `<= 30 min` total time)
    - `Rediscover` (prioritizes high-rated recipes not cooked in `>= 30 days`)
    - `NewIdeas` (prioritizes never-cooked (`Ungekocht`) recipes)

---

## §G–§H. Multi-Factor Scoring Model & Ordering Rules (`RankingRules.cs`)

Each candidate recipe $r$ receives a composite score from 9 bounded domain terms in $[0, 1]$ minus the `Shown` fatigue penalty:

$$\text{Score}(r, c) = \sum_{k=1}^{9} w_k(p) \cdot S_k(r, c) - w_{\text{shown}} \cdot \text{Shown}(r, c)$$

### The 9 Scoring Terms (§G)

1. **`Pantry` ($S\_{\text{pantry}}$):** Fraction of non-staple recipe ingredients currently in `PantryItems`, with a `1.5×` multiplier for ingredients expiring within `4 days`.
2. **`ContextFit` ($S\_{\text{context}}$):** Match between target `MealType` / weekday vs. weekend time budget and recipe `Categories` + `TotalTimeMinutes` (e.g., penalizes `> 45 min` recipes on Monday–Thursday evenings unless prepped).
3. **`Recency` ($S\_{\text{recency}}$):** Cooldown and rediscovery curve over days since last cooked/planned (`0` if `< 3d`, peaks at `14–45d`).
4. **`Quality` ($S\_{\text{quality}}$):** Bayesian-smoothed user star rating (`1–5★`, prior `3.5★`) + `RecipeFavorite` bonus (`+0.20`).
5. **`Novelty` ($S\_{\text{novelty}}$):** Exploration bonus for never-cooked recipes (`CookCount == 0`) created within the last `90 days` so newly imported recipes do not sink below established favorites.
6. **`Synergy` ($S\_{\text{synergy}}$):** Shared perishable ingredients with other meals already planned in the same week (`MealPlanEntries`).
7. **`Seasonality` ($S\_{\text{season}}$):** Match against seasonal DACH produce calendar (`SeasonalIngredients`), gated by observed household usage (§K).
8. **`GoalFit` ($S\_{\text{goal}}$):** Alignment of `CaloriesPerServing` and `ProteinGramsPerServing` with the user's active `NutritionGoal` (neutral `0.5` when no goal is active).
9. **`Feedback` ($S\_{\text{feedback}}$):** Decay penalty from recent `suggestion_dismissals`.

### The 10 Behavioral Ordering Invariants (§H.1–§H.9)

Tested directly in `RankingRulesTests.cs`:

- **H.1:** Recipes already planned in the target week are excluded.
- **H.2:** Recipes cooked within the last 2 days are suppressed below fresh candidates.
- **H.3:** Expiring pantry match (`<= 3 days`) outranks an otherwise equal candidate without expiring items.
- **H.4:** On weeknights (`Mon–Thu`), quick recipes (`<= 30m`) outrank long projects (`>= 75m`) of equal rating.
- **H.5:** Meal-type category match (`Frühstück` at `Breakfast`) outranks mismatched categories (`Hauptgericht`).
- **H.6:** Higher user star rating (`5★` vs `2★`) wins when context and pantry are equal.
- **H.7:** Explicitly dismissed recipes within 7 days are suppressed.
- **H.8:** Repeatedly shown (`SuggestionImpressions` in last 24h) candidates yield to unshown runners-up.
- **H.9:** Purpose overrides (`UsePantry`, `QuickWeeknight`, `Rediscover`) shift weight vectors deterministically.

---

## §I–§K. Explainability, MMR Diversity & Observed Seasonality Gate

- **§I Explainability (35% Dominance Rule):** The primary reason badge (`ReasonText`) is chosen from the highest-contributing positive term $w_k S_k$. A specific badge (e.g., `"Vorrat: Tomaten läuft bald ab"` or `"In 20 Min. fertig"`) is emitted only when that term accounts for `>= 35%` of the positive score; otherwise a honest fallback label (`"Beliebtes Rezept"`, `"Noch nie gekocht"`) is used.
- **§J Maximal Marginal Relevance (MMR) Diversity:** Shortlists of size $K$ are selected greedily via MMR ($\lambda = 0.72$), penalizing candidates that share the same primary protein/base ingredient (`Pasta`, `Reis`, `Kartoffel`, `Huhn`) or identical primary category as an already-selected item in the shortlist.
- **§K Observed Seasonality Gate:** Seasonal ingredient boosts (`Spargel`, `Kürbis`, `Erdbeeren`, `Grünkohl`) only activate at full weight once the household has demonstrated seasonal affinity (at least 2 recipes or past cooks containing seasonal produce), avoiding irrelevant seasonal pushes on minimal libraries.

---

## §L–§O. Offline Evaluation, Replay & Weight Calibration (`§M.1`–`§M.3`)

Verified in `src/backend/Culina.Tests/Suggestions/`:

- **§M.1 Historical Replay (`OfflineReplayTests`):** Reconstructs point-in-time `SuggestionContext` snapshots prior to real historical `MealPlanEntry` / `CookHistoryEntry` events and measures HitRate@5 and MRR.
- **§M.2 Shortlist Quality Metrics (`ShortlistQualityTests`):** Asserts intra-list category/ingredient diversity (`>= 0.75`), zero cooldown violations, and latency `< 20 ms` on 500-recipe synthetic households.
- **§M.3 Coordinate-Descent Weight Calibration (`WeightCalibrationTests`):** Verifies that the default weight vector in `RankingRules.DefaultWeights` satisfies all H.1–H.9 ordering constraints with positive margin across perturbation sweeps.
