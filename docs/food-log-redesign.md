# Food Log Sheet Redesign (`FoodSearchSheet`)

Interaction specification for the unified meal-logging drawer in `Tagebuch` (`src/frontend/src/lib/components/diary/FoodSearchSheet.svelte`).

---

## §1. Design Principles & Single-Sheet Architecture

Logging a meal must take `< 5 seconds` for recurring items and `< 15 seconds` for new items without modal stacking:

- **Single Bottom Drawer / Modal (`FoodSearchSheet.svelte`):** Replaces separate modals for recipe picking, BLS food lookup, barcode scanning, and quick kcal entry with one unified sheet scoped to the active `(Date, MealSlot)`.
- **Multi-Add Session Counter:** Users can tap `+` on multiple items (e.g., _Haferflocken_, _Milch_, _Banane_) inside a single open sheet session with immediate toast/badge feedback before closing.

---

## §2–§3. Zero-Query Default State & Recents (`§3.4`)

Before typing a single character (`query === ''`), the sheet displays:

1. **Planned in `Woche` for this slot:** Unlogged `MealPlanEntries` for `(Date, MealSlot)` at the very top with a one-tap `Loggen` action.
2. **§3.4 Slot-Aware Recent & Frequent Items:** The user's most recently and frequently logged items for that specific `MealSlot` (e.g., morning coffee + muesli appear in `Frühstück`, not `Abendessen`), remembering the exact last-used `Amount` and `Unit`.

---

## §4. Unified Search Results & Inline Portion Stepper (`§4.1`–`§4.4`)

- **§4.1 Single Blended Ranking (`GET /api/v1/foods/search`):**
    - Combines **User History**, **Household Recipes** (`Rezept` badge), **Custom Foods** (`Eigenes`), **BLS 4.0** (`BLS`), and **Open Food Facts** (`Marke`) into a single scannable list.
- **§4.2 Inline Portion Adjustment (No Nested Sub-Modal):**
    - Tapping a result expands an inline portion bar directly inside the row with unit pills (`g`, `Portion` / `Stück`, `EL`), quick presets (`50 g`, `100 g`, `150 g`, `1 Portion`), live-recalculated `kcal | P | C | F`, and a primary `Hinzufügen` button.
- **§4.4 Quick-Tap `+` Action:**
    - Tapping the trailing `+` button on any row logs the item immediately using its default/last-used portion (`1 Portion` for recipes, `servingWeightGrams` or `100 g` for foods) without expanding the inline editor.

---

## §5–§7. Quick Actions Bar (Barcode, AI & Custom Food) (`§7`)

Pinned below the search input:

- **Barcode scannen:** Launches native MLKit scanner / `BarcodeScannerModal`; if the EAN is unknown in Open Food Facts, transitions inline into the custom product creator (`open-food-facts-contribution-design.md`).
- **KI-Text / Foto:** Expands an inline prompt/camera capture that resolves into editable candidate rows.
- **§7 Schnelleintrag & Eigenes Lebensmittel:** Inline form for logging a fast `kcal` estimate or saving a reusable `CustomFoodItem` with optional EAN.
