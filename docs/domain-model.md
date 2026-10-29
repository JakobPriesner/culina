# Domain Model

Canonical reference for Culina's entities, invariants, ownership axes, scaling rules, shopping merge logic, and external sync boundaries (`src/backend/Culina.Domain` & `Culina.Infrastructure`).

---

## 1. Ownership & Authorization Axes

Culina separates **identity**, **household workspace**, and **cookbook sharing**:

```
User (1) ──belongs to──> Household (1)
 │                         ├─ MealPlan / MealPlanEntries
 │                         ├─ ShoppingLists / ShoppingListItems
 │                         ├─ PantryItems / PantryStaples
 │                         ├─ AppSettings (AI keys, Ollama URL)
 │                         └─ ConnectedLibraries (remote feeds)
 │
 ├── Recipe (CreatedByUserId, HouseholdId)
 │     └─ RecipeShares (per-user view/edit permission)
 │
 ├── Cookbook (OwnerId, Visibility: Private | Shared | Household | Public)
 │     ├─ CookbookMembers (Role: View | Edit | Manage)
 │     └─ CookbookRecipes (M:N join, OR dynamic SmartFilterQuery)
 │
 └── Per-User Private State (UserId-scoped, never shared across household):
       ├─ UserRecipeRating (1–5 stars + personal notes)
       ├─ RecipeFavorite, CookHistoryEntry, ActiveCookSession
       └─ FoodLogEntry, NutritionGoal, WeightEntry, WaterIntakeLog, CustomFoodItem
```

- **Household (`Household`):** Every user belongs to one active household (`User.HouseholdId`).Joining an invite (`HouseholdInvite`) migrates the user's recipes and private state into the target household.
- **Recipe visibility (`RecipeAccessExtensions`):** A user can view a recipe if it belongs to their household, is in a cookbook they have access to, or is explicitly shared with them via `RecipeShare`. Editing requires ownership, household membership, `RecipeShare(CanEdit)`, or `CookbookMember(Edit | Manage)`.

---

## 2. Core Entities & Invariants

### Recipe Aggregate (`Recipe`)

- **Fields:** `Id`, `Title` (1–200 chars), `Description`, `Servings` (1–100, default `4`), `PrepTimeMinutes`, `CookTimeMinutes`, `RestingTimeMinutes`, `Difficulty` (`Easy | Medium | Hard`), `SourceUrl`, `ImageUrl`, `Notes`, `IsQuickReference` (`kompakt` note card without structured steps), `CreatedByUserId`, `HouseholdId`, `Version` (concurrency counter incremented on every edit, exposed as HTTP `ETag`).
- **Nutrition cache:** `CaloriesPerServing`, `ProteinGramsPerServing`, `CarbsGramsPerServing`, `FatGramsPerServing`, `FiberGramsPerServing`, `SugarGramsPerServing`, `NutritionEstimatedAt`, `NutritionIsAiEstimated`.
- **Children:**
    - **`RecipeIngredient`:** `Name` (required), `Amount` (`decimal?`), `AmountMax` (`decimal?`, for ranges like `100–150 g`), `Unit` (normalized via `UnitCatalog`), `GroupName` (optional section header, e.g. `"Für den Teig"`), `Notes` (prep hint, e.g. `"fein gewürfelt"`), `IsOptional`, `Scalable` (default `true`), `SortOrder`.
    - **`RecipeStep`:** `StepNumber` (contiguous `1..N`), `Instruction`, `GroupName`, `DurationMinutes` (optional timer trigger), `TemperatureCelsius`, `TemperatureUnit`, `StepIngredients` (M:N link to `RecipeIngredient` with optional step-specific `Amount`/`Unit`).
    - **`Category` & `Tag`:** Recipes link to `Category` (curated meal types: `Hauptgericht`, `Frühstück`, `Backen`, etc.) and `Tag` (freeform lowercase slugs, e.g. `vegetarisch`, `feierabend`, `one-pot`).
    - **`RecipeRelation`:** Directed link (`SourceRecipeId -> TargetRecipeId`) with `RelationType` (`SideDish`, `BaseRecipe`, `Variant`).

### Cookbooks (`Cookbook`)

- **Types:** Manual (`SmartFilterQuery == null`, ordered via `CookbookRecipe.SortOrder`) or Smart (`SmartFilterQuery != null`, evaluated dynamically via `SmartFilterEvaluator` using the search query grammar).
- **Sharing:** `CookbookMember` grants `View`, `Edit`, or `Manage`. `ShareToken` exposes read-only public access at `/shared/cookbook/{token}` when `IsPublicLinkEnabled == true`.

### Meal Plan (`MealPlan` & `MealPlanEntry`)

- Keyed by `(HouseholdId, WeekStartDate)` where `WeekStartDate` is always **ISO Monday** (`DayOfWeek.Monday`).
- **`MealPlanEntry`:** `Date` (`DateOnly`), `MealType` (`Breakfast = 0`, `Lunch = 1`, `Dinner = 2`, `Snack = 3`, `Prep = 4`), `RecipeId` (nullable if `CustomTitle` is set), `Servings` (overrides `Recipe.Servings` for scaling, shopping generation, and diary logging), `Notes`, `CookedAt` (`DateTimeOffset?`), `LeftoverSourceEntryId` (links a planned leftover meal to its origin cook entry).

### Shopping List (`ShoppingList` & `ShoppingListItem`)

- Households maintain one primary active `ShoppingList` (`IsPrimary = true`) plus optional named lists.
- **`ShoppingListItem`:** `Name`, `Amount`, `Unit`, `Category` (supermarket aisle `ShoppingCategory`: `Produce`, `Bakery`, `DairyEggs`, `MeatFish`, `Pantry`, `Frozen`, `Drinks`, `Household`, `Other`), `IsChecked`, `CheckedAt`, `SourceRecipeId`, `SourceMealPlanEntryId`, `SortOrder`.
- Real-time updates broadcast over SSE (`ShoppingStreamBroadcaster`) to all connected household clients.

### Pantry (`PantryItem` & `PantryStaple`)

- **`PantryItem`:** Quantified inventory (`Name`, `Amount`, `Unit`, `StorageLocation`: `Fridge | Freezer | Pantry | SpiceRack`, `ExpiresAt`, `MinimumAmount` for low-stock warnings).
- **`PantryStaple`:** Unquantified household basics (`Salz`, `Pfeffer`, `Olivenöl`, `Wasser`) assumed always in stock during availability checks and shopping generation.

---

## 3. Scaling, Units & Shopping Aggregation

### Unit Families (`UnitCatalog` & `IngredientAggregator`)

- **Mass:** `mg` (`0.001 g`), `g` (`1 g`), `kg` (`1000 g`). Auto-upgrades display to `kg` at `>= 1000 g`.
- **Volume:** `ml` (`1 ml`), `cl` (`10 ml`), `dl` (`100 ml`), `l` (`1000 ml`), `TL` (`5 ml`), `EL` (`15 ml`), `Tasse` (`240 ml`). Auto-upgrades `ml` to `l` at `>= 1000 ml`.
- **Count / Culinary:** `Stück`, `Zehe`, `Scheibe`, `Bund`, `Dose`, `Glas`, `Packung`, `Becher`, `Prise`, `Msp.`.
- See [`docs/scaling-rules.md`](scaling-rules.md) for non-linear spice dampening ($s^{0.7}$), pan/tin area scaling, and culinary rounding rules (`QuantityFormatter`).

### Shopping List Consolidation (`IngredientAggregator`)

When generating shopping items from a meal plan (`POST /api/v1/shopping/from-meal-plan`):

1. Scale each planned recipe's ingredients by `Entry.Servings / Recipe.Servings` (skipping `IsOptional == true` unless requested).
2. Normalize ingredient names via `IngredientNameNormalizer` (case-folding, singular/plural stem matching in German).
3. Group by normalized name:
    - Compatible units within the same family (`250 g` + `1 kg` → `1,25 kg`; `2 EL` + `50 ml` → `80 ml`) merge into a single line item.
    - Incompatible units (`200 g Tomaten` + `1 Dose Tomaten`) remain separate line items.
4. Subtract matching `PantryItem` quantities (when unit families match) and suppress `PantryStaple` matches completely.

### Ingredient Suggestions (`#ingredient-suggestions`)

`GET /api/v1/ingredients/suggestions` merges three sources ranked by prefix match + household frequency:

1. Distinct ingredient names used across the household's recipes (`RecipeIngredients`).
2. Active `PantryItems` and `PantryStaples`.
3. Curated German base catalog (`BLS 4.0` common ingredients) with default unit and supermarket aisle (`ShoppingCategory`).

---

## 4. Cooking Mode & Active Cook Sessions

- **`ActiveCookSession`:** Server-persisted per-user session (`RecipeId`, `Servings`, `CurrentStepNumber`, `CompletedStepNumbersJson`, `CheckedIngredientsJson`, `TimersJson`, `StartedAt`, `UpdatedAt`). Allows seamless handoff between phone, tablet, and desktop.
- **`POST /api/v1/recipes/{id}/cooked`:** Records a `CookHistoryEntry`, marks any matching `MealPlanEntry` for today as cooked (`CookedAt`), optionally deducts scaled ingredients from `PantryItems` (`PantryDeductionService`), and writes a `FoodLogEntry` to the user's nutrition diary.

---

## 5. Intake, Connected Libraries & Archives

### Multimodal Recipe Intake (`IntakeSession`)

- Supports **URL scraping** (`SchemaOrgRecipeExtractor`: JSON-LD `@type: Recipe`, Microdata, site-specific fallbacks, plus AI cleanup), **Images/Photos/PDFs** (multi-page vision extraction), **Free text**, and **Third-party imports** (Paprika `.paprikarecipes`, Nextcloud Cookbook, Mealie, Tandoor).
- All outbound HTTP fetches pass through `SafeHttpDownloader` (DNS pinning, blocks loopback/RFC1918/link-local/CGNAT/cloud metadata IPs, enforces timeouts and byte caps).

### Connected Libraries (`ConnectedLibrary` & `SyncedRecipe`)

- Allows a household to subscribe read-only to another Culina instance's public cookbook or library feed (`GET /api/v1/library-sync/feed/{token}`).
- `LibrarySyncBackgroundService` polls every `15 min` using conditional `If-None-Match` (`ETag`), upserting `SyncedRecipe` rows. Users can fork a `SyncedRecipe` into a native editable `Recipe` in their own household.

### Full Household Archive (`ArchiveService`)

- `GET /api/v1/archive/export` streams a deterministic `.zip` containing `archive.json` (`schemaVersion: 2` — recipes, cookbooks, meal plans, pantry, ratings, cook history, diary logs) plus `/images/*.webp`.
- `POST /api/v1/archive/import` validates paths against Zip-Slip, deduplicates recipes by `(Title, SourceUrl)` or content hash, and remaps internal IDs atomically.
