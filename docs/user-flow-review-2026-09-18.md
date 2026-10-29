# End-to-End User Flow Review (2026-09-18)

Summary of the core household user journeys audited and streamlined across Culina.

---

## 1. Audited Core Journeys

1. **Recipe Intake → Library (`Sammlung`):**
    - URL paste, photo/PDF upload, or text paste in `/rezepte/neu` extracts structured ingredients (`Name` vs `Notes`), steps (with auto-detected `DurationMinutes` timers), tags, and deterministic BLS nutrition in one review pass, warning inline if a duplicate lookalike (`GET /api/v1/recipes/lookalikes`) already exists.
2. **Weekly Planning (`Woche`) → Supermarket (`Einkauf`):**
    - Adding recipes to `Mo–So` slots checks pantry coverage (`GET /api/v1/meal-plan/availability`).
    - Clicking `Einkaufsliste erstellen` merges compatible units (`g`/`kg`, `ml`/`l`, `TL`/`EL`), subtracts stocked `PantryItems` and `PantryStaples`, and groups items by supermarket walking aisle with real-time SSE sync.
3. **Cooking (`Kochmodus`) → Pantry Deduction & Diary (`Tagebuch`):**
    - Starting `CookingMode` keeps the screen awake, tracks step progress and concurrent timers across devices (`ActiveCookSession`), and on completion (`Heute gekocht`) records cook history, deducts used pantry quantities, and logs the meal to the user's private nutrition diary.
4. **Daily Nutrition Logging (`Tagebuch`):**
    - Planned meals from `Woche` appear at the top of today's diary slots for 1-tap logging; packaged foods scan via native MLKit / `BarcodeDetector` / `zxing-wasm` with automatic fallback to inline product creation and optional Open Food Facts contribution.
