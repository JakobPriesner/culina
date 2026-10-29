# Barcode Scanning Architecture

Specification for EAN/UPC product scanning across `Tagebuch` (nutrition logging), `Küche` (pantry check-in), and `Einkaufsliste` (`src/frontend/src/lib/components/BarcodeScannerModal.svelte`, `src/frontend/src/lib/native/barcode.ts`, and `src/backend/Culina.Infrastructure/Nutrition/OpenFoodFactsClient.cs`).

---

## §1–§2. Supported Symbologies & Entry Points

- **Symbologies:** `EAN-13` (standard European retail), `EAN-8` (small packaging), `UPC-A`, and `UPC-E`.
- **Surfaces:**
    1. **Tagebuch (`FoodSearchSheet`):** Scans packaged food directly into a meal slot with pre-filled macros per `100 g` and package serving size (`servingWeightGrams`).
    2. **Küche (`/kueche`):** Scans items into `PantryItems` with product name, default storage location, and package quantity.
    3. **Einkaufsliste (`/einkaufsliste`):** Scans an empty carton to add the product to the shopping list.

---

## §3. Three-Tier Client Scanner Cascade (`§3.1`–`§3.2`)

1. **§3.1 Native Capacitor MLKit (`@capacitor-mlkit/barcode-scanning`):**
    - Used automatically on iOS (`isNative()`). Hardware-accelerated Vision camera sheet with torch toggle, pinch-to-zoom, and haptic confirmation (`hapticMedium()`).
2. **§3.2 Browser Native `BarcodeDetector` API → `zxing-wasm` Fallback:**
    - Inside `BarcodeScannerModal.svelte`, checks `'BarcodeDetector' in window` first (zero-bundle hardware detection on Chrome/Android/macOS Safari).
    - If unavailable, lazy-loads `zxing-wasm` (`readBarcodesFromImageData`) on demand so the main application bundle incurs zero WASM weight until the camera button is tapped.
    - Camera stream requests `facingMode: { ideal: 'environment' }`, `1280×720`, continuous autofocus, and stops all `MediaStreamTrack`s immediately on modal close or route change.

---

## §4–§5. Backend Lookup & Local Cache (`GET /api/v1/foods/barcode/{ean}`)

- **§4 Ean Normalization & Checksum Validation:** Validates 8/12/13/14-digit GS1 Mod-10 check digit before querying.
- **§5 Lookup Order (`§5.1`–`§5.2`):**
    1. **§5.1 Household `CustomFoodItems` & `BarcodeProductCache`:** Instant `< 5 ms` PostgreSQL hit if the EAN was previously scanned or customized by the household (cache TTL: `30 days`).
    2. **§5.2 Open Food Facts v2 API (`OpenFoodFactsClient`):** Queries `https://world.openfoodfacts.org/api/v2/product/{ean}.json` with a descriptive `User-Agent: Culina/1.7 (+https://github.com/JakobPriesner/culina)`, preferring German product names (`product_name_de` → `product_name`), extracting `nutriments` (`energy-kcal_100g`, `proteins_100g`, `carbohydrates_100g`, `fat_100g`, `fiber_100g`, `sugars_100g`, `serving_quantity`), and upserting `BarcodeProductCache`.

---

## §6–§7. Unknown Barcode Fallback & OFF Contribution

- **§6 Unknown EAN Flow:** When a valid EAN returns `404` from both local cache and Open Food Facts, the UI immediately opens the inline **"Produkt nicht gefunden – Jetzt anlegen"** form with the scanned EAN pre-filled.
- **§7 Community Contribution:** Saving the new product creates a household `CustomFoodItem` immediately and optionally submits the product name + nutrition table back to Open Food Facts (`POST /api/v1/foods/off-contribute`; see [`docs/open-food-facts-contribution-design.md`](open-food-facts-contribution-design.md)).
