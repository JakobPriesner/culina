# Ingredient Name Normalization & Formatting Rules

Specification for parsing, normalizing, and displaying German ingredient names across recipe intake, shopping aggregation, pantry matching, and search (`src/backend/Culina.Domain/Services/IngredientNameNormalizer.cs` & `IngredientParser.cs`).

---

## §A–§C. Structural Separation: `Name` vs. `Notes`

Every `RecipeIngredient` strictly separates the **canonical buyable ingredient** (`Name`) from **preparation instructions** (`Notes`):

| Raw Imported Line                                     | `Amount` | `Unit`  | Canonical `Name`     | `Notes` (Prep / State)              |
| ----------------------------------------------------- | -------- | ------- | -------------------- | ----------------------------------- |
| `"2 große Zwiebeln, fein gewürfelt"`                  | `2`      | `Stück` | `Zwiebeln`           | `groß, fein gewürfelt`              |
| `"250 g Hähnchenbrustfilet, in Streifen geschnitten"` | `250`    | `g`     | `Hähnchenbrustfilet` | `in Streifen geschnitten`           |
| `"1 Dose (400 g) gehackte Tomaten"`                   | `1`      | `Dose`  | `Gehackte Tomaten`   | `400 g`                             |
| `"etwas Olivenöl zum Anbraten"`                       | `null`   | `null`  | `Olivenöl`           | `zum Anbraten` (`Scalable = false`) |

This separation ensures the shopping list displays `"Zwiebeln"` and merges quantities across recipes rather than creating separate lines for `"Zwiebeln, fein gewürfelt"` and `"Zwiebeln, in Ringe geschnitten"`.

---

## §D. Normalization Pipeline (`IngredientNameNormalizer`) (`§D.1`–`§D.4`)

Used when matching `RecipeIngredient` ↔ `PantryItem` ↔ `ShoppingListItem`:

1. **§D.1 Case, Punctuation & Parenthetical Stripping:** Lowercases, strips parenthetical weight/brand notes `(ca. 200 g)`, and trims trailing comma/dash clauses.
2. **§D.2 Preparation Adjective Stripping:** Removes non-identity culinary modifiers (`frisch`, `gehackt`, `gewürfelt`, `gerieben`, `geschält`, `gekocht`, `zimmerwarm`, `bio`) while **preserving identity-changing qualifiers** (`getrocknete Tomaten` ≠ `Tomaten`; `geräucherter Tofu` ≠ `Tofu`; `rote Linsen` ≠ `braune Linsen`; `Schlagsahne` ≠ `Saure Sahne`).
3. **§D.3 German Plural-to-Singular Stemming:** Maps common German plurals to a shared canonical stem (`Zwiebeln → zwiebel`, `Karotten → karotte`, `Tomaten → tomate`, `Kartoffeln → kartoffel`, `Eier → ei`, `Äpfel → apfel`, `Knoblauchzehen → knoblauch`).
4. **§D.4 Synonym Equivalence Map:** Groups equivalent German grocery terms under one shopping/pantry key (`Frühlingszwiebel = Lauchzwiebel`, `Karotte = Möhre`, `Sahne = Schlagsahne = Süße Sahne`, ` Kichererbsen (Dose) = Kichererbsen`).

---

## §E–§G. Supermarket Aisle Classification & Display Formatting (`§G`)

- **§E–§F Aisle Assignment (`ShoppingCategoryClassifier`):** Maps normalized ingredient stems to supermarket aisles (`Produce`, `Bakery`, `DairyEggs`, `MeatFish`, `Pantry`, `Frozen`, `Drinks`, `Household`, `Other`) so the shopping list sorts automatically in walking order.
- **§G Display Capitalization:** Canonical ingredient names are stored and displayed in standard German sentence/noun capitalization (`Olivenöl`, `Gehackte Tomaten`, `Frühlingszwiebeln`).
