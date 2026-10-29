# Open Food Facts Community Contribution Flow

Specification for contributing missing barcode products back to the Open Food Facts (OFF) database when a user scans an unknown EAN (`src/backend/Culina.Infrastructure/Nutrition/OpenFoodFactsContributor.cs`, `POST /api/v1/foods/off-contribute`).

---

## 1. User Flow on Unknown Barcode (`404`)

1. User scans an EAN-8 / EAN-13 barcode in `Tagebuch` or `Küche` that is missing from both `BarcodeProductCache` and Open Food Facts.
2. The UI opens the inline **"Produkt nicht gefunden – Jetzt erfassen"** sheet with the scanned `ean` pre-filled.
3. User enters:
    - **Produktname** (required, e.g. `"Bio Haferdrink Barista"`)
    - **Marke** (optional, e.g. `"Alnatura"`)
    - **Nährwerte pro 100 g / 100 ml:** `Kalorien (kcal)`, `Eiweiß (g)`, `Kohlenhydrate (g)`, `Fett (g)`, optional `Ballaststoffe (g)`, `Zucker (g)`, and `Portionsgröße (g)`.
    - Checkbox **"An Open Food Facts spenden (anonym)"** (checked by default).

---

## 2. Dual Persistence & Background Submission

1. **Immediate Local Save:** The product is saved immediately into the household's `CustomFoodItems` and `BarcodeProductCache` so the user's diary/pantry action completes with zero latency even if Open Food Facts is slow or unreachable.
2. **Sanity Validation Before Upstream Write:**
    - Valid GS1 Mod-10 checksum (`8`, `12`, `13`, or `14` digits; excludes internal in-store `20..29` weight-embedded prefixes).
    - Non-empty product name (`2–120` chars).
    - Plausible macro bounds per `100 g`: `0 <= kcal <= 900`, `0 <= P, C, F <= 100`, and `P + C + F <= 105 g`, with Atwater energy consistency (`|kcal - (4P + 4C + 9F)| <= max(60, 0.25 * kcal)`).
3. **OFF API Write (`POST https://world.openfoodfacts.org/cgi/product_jqm2.pl`):**
    - Sends `code`, `product_name_de`, `brands`, `nutriment_energy-kcal`, `nutriment_proteins`, `nutriment_carbohydrates`, `nutriment_fat`, `nutrition_data_per=100g`, and `comment="Contributed via Culina self-hosted app"` with `User-Agent: Culina/1.7`.
    - No user ID, household ID, or personal data is ever transmitted to Open Food Facts.
