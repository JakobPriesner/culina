# Brand Assets, Icons & Illustration Guidelines

Specification for Culina's icons, app badges, placeholder artwork, and image pipeline (`src/frontend/static/` and `src/backend/Culina.Infrastructure/Services/ImageStorageService.cs`).

---

## 1. App Icon & Brand Mark

- **Motif:** Minimalist terracotta mortar-and-pestle / warm ceramic bowl silhouette on warm parchment (`#FAF7F2`) or deep terracotta (`#C85A32`).
- **Static & Native Assets:**
    - `src/frontend/static/favicon.svg`, `favicon.png`, `apple-touch-icon.png` (`180×180`), `icon-192.png`, `icon-512.png`, `icon-maskable-512.png`.
    - iOS & Mac Catalyst asset catalog in `src/frontend/ios/App/App/Assets.xcassets/AppIcon.appiconset`.

---

## 2. UI Iconography

- **Style:** Consistent `1.75px` stroke weight, rounded caps/joins (`stroke-linecap="round"`, `stroke-linejoin="round"`), sized on a `20×20` or `24×24` grid.
- **Colour Inheritance:** All inline SVGs use `stroke="currentColor"` so they adapt automatically to Warm Parchment (light) and Warm Espresso (dark) text/accent tokens.

---

## 3. Recipe Photography & Fallback Placeholders

- **Image Pipeline (`ImageStorageService`):**
    - Uploaded and scraped recipe photos are stripped of EXIF metadata, center/aspect-cropped or bounded to max `1600×1600px`, and encoded as `WebP` (`quality: 82`) in `/app/uploads`.
- **Category-Aware Botanical Placeholders:**
    - Recipes without a photo (`imageUrl == null`) never show a broken image icon or cold gray box; they render a warm category-keyed botanical/culinary illustration using `--color-bg-elevated` and `--color-accent-subtle`.
- **Optional AI Dish Illustration (`POST /api/v1/recipes/{id}/generate-image`):**
    - Generates a warm editorial food illustration/photo from the recipe's title and main ingredients using the configured image-capable provider (e.g., Gemini `gemini-2.5-flash-image` or OpenAI).
