# Barcode scanning

Phase 1 of `culina-v2-nb65.6`: the plan the bead asks for before any code.
It settles four things: how a code becomes a product, what each place in the
app does with one, how the camera behaves, and what still works offline.

Labels used below:

- **Fact**: checked, with the source named.
- **Assumption**: not verified; stated so it can be checked before building on it.
- **Decision needed**: Jakob's call, with a recommendation.

---

## 1. What the bead asks, against what Culina has

| The bead | What Culina has today | Consequence |
| --- | --- | --- |
| "Restock" from a scan | No pantry. Culina keeps a shopping list, not a stock count. | A scan *outside* a recipe means "put this on the list". Nothing gets restocked. |
| Scanning while cooking "crosses off prep items" | Cooking has no per-ingredient tick. Lines are read, never checked. | Needs a small new state: an ingredient line marked *got out*, kept for the cook session. This plan proposes it (§4.2). |
| Scanning while cooking "surfaces cook times" | Nothing anywhere stores a product's cook time. | Open Food Facts (OFF) has no structured cooking time (**Assumption**, check before phase 4). The only reliable source is the household: the first time someone scans *Barilla Spaghetti n.5*, they can note "9 min", and every later scan shows it. |
| "Open Food Facts API" | `nutrition-design.md` §M rejects *any* runtime API ("the first unsolicited outbound call") and the OFF barcode database. | OFF can only be an **opt-in** instance setting, off by default. Without it, a code is known only once the household has named it (§3). |
| ZXing WASM fallback | Document CSP is `script-src 'self' 'nonce-…'` with Trusted Types. | Compiling WebAssembly needs `'wasm-unsafe-eval'` in `script-src`. Decided 2026-10-27: added (§5.1). |

None of this blocks the feature. It changes which parts are cheap and which
need a decision first.

## 2. Platform facts

- **Fact** (caniuse `mdn-api_barcodedetector`, MDN, 2026-10-13): `BarcodeDetector`
  ships in Chrome for Android. Desktop Chrome and Edge are listed as *partial*
  from 83 (not every operating system provides a detector). Safari on macOS
  and iOS has it **disabled by default**. Firefox does not have it.
  So the native detector covers Android phones. It does not cover iPhones,
  which are likely half of any household. A fallback is not optional if
  iPhones should scan.
- **Fact**: `getUserMedia({ video: { facingMode: 'environment' } })` works in
  Safari and Chrome for an installed PWA and in the browser, only in a secure
  context. The `Permissions-Policy` already allows `camera=(self)`
  (`Api/Middleware/SecurityHeaders.cs`) for recipe photos.
- **Fact**: a torch is `track.applyConstraints({ advanced: [{ torch: true }] })`,
  offered only where `track.getCapabilities().torch` is true. That means Chrome
  on Android. iOS Safari does not expose it. The control is shown only when the
  capability is there.
- **Fact**: `navigator.vibrate` exists on Android Chrome and not in iOS Safari.
  A haptic "lock" is therefore a progressive extra. The visual and spoken lock
  (frame turns solid, `aria-live` announces the product) is the real
  confirmation.
- **Fact** (OFF documentation, "Conditions to use the API"): product reads are
  limited to **15 per minute per IP**. Every call must send a User-Agent that
  names the app and a contact. The data is **ODbL**: attribution, and
  share-alike for a database that *merges* it with other data.
- **Assumption**: the Capacitor app (`docs/native-app-design.md`, epic
  `culina-v2-tj23`) could use the platform scanner (ML Kit on Android,
  VisionKit `DataScannerViewController` on iOS) through a plugin. That would
  make the WASM fallback a web-only concern. Not designed here; the scanner
  module below has one `detect()` seam for it.

## 3. From a code to a product

One endpoint, read in three steps, the first answer wins:

```
GET /api/v1/households/{householdId}/products/{gtin}
```

1. **The household's own memory** (`household_products`, §3.1). This is what the
   household said about this code: what it is called, which ingredient it is,
   and optionally a note and a cook time. It always wins, even over OFF, because
   it is the household's word.
2. **Open Food Facts**, only if the instance enabled it (§5.2). The server asks
   once per code and keeps the answer (`product_lookups`, §3.2), including
   "OFF does not know this code", so a shop's own-brand item is not asked for
   again on every scan. The browser never talks to OFF: the CSP's
   `connect-src 'self'` stays, the rate limit applies to the server's one IP,
   and the server sends the User-Agent.
3. **Unknown.** The answer is `404 products.unknown`. The scan sheet then asks
   for a name with one text field. The ingredient name inputs' autocomplete
   applies, so the name is likely one the recipes already use. The answer is
   saved to the household's memory (acceptance 6).

The response is one shape whatever answered:

```jsonc
{
  "gtin": "8076800195057",          // normalised: digits only, UPC-A padded to 13
  "name": "Barilla Spaghetti n.5",  // what to show
  "ingredient": "Spaghetti",        // what to match recipe lines and list items by
  "quantity": 500, "unit": "g",     // from the pack, when known
  "cookMinutes": 9,                 // household memory only
  "note": null,
  "source": "household" | "openFoodFacts",
  "attribution": null | { "name": "Open Food Facts", "url": "https://world.openfoodfacts.org/product/…", "licence": "ODbL" }
}
```

`ingredient` is the field that makes the contexts work. From the household it
is whatever they typed. From OFF it is derived on the server: the product's
`generic_name_de`/`generic_name` if present, else the first
`categories_tags_de` entry that `IngredientWords` reads as a head noun the
lexicon knows, else null. A null ingredient still adds the product name to a
shopping list. It cannot match a recipe line.

### 3.1 `household_products`

```sql
create table household_products (
  household_id uuid not null references households_with_deleted (id) on delete cascade,
  gtin text not null check (gtin ~ '^[0-9]{8,14}$'),
  name text not null check (length(name) between 1 and 200),
  ingredient text check (length(ingredient) <= 200),
  quantity numeric(10, 3), unit text,
  cook_minutes int check (cook_minutes between 1 and 600),
  note text check (length(note) <= 500),
  created_by uuid references users (id) on delete set null,
  updated_at timestamptz not null default now(),
  version bigint not null default 1,
  primary key (household_id, gtin)
);
```

The endpoints are `PUT /api/v1/households/{householdId}/products/{gtin}`
(`If-Match` for changes) and `DELETE` on the same path. Inheriting households
read along the inheritance chain, nearest first, the way nutrition corrections
do. The rows go into the household archive export with everything else the
household owns.

**As built (`culina-v2-nb65.6.1`).** The table is migration 0054, `updated_at` has no
default (the repository writes it), and the unit and quantity rules are those of a
recipe line. `GET .../products/{gtin}` answers from `household_products` only: the
response has `householdId` (whose row it is) and `version` instead of `source` and
`attribution`, which arrive with Open Food Facts (§5.2) as optional fields, so adding
them breaks nothing. `PUT` without `If-Match` names a barcode (`201`, or `428
products.already_named`); with it, it changes that version. `DELETE` needs `If-Match`.
`GET .../products` lists the whole library, the nearest household first per code, for
the offline cache (§7). The full contract is in `docs/api.md`, "Products".

### 3.2 `product_lookups` (only with OFF enabled)

This holds `gtin`, the raw OFF fields we use (not the whole document),
`found boolean` and `fetched_at`. Rows are re-asked after 30 days, misses
after 7. It is the server's cache of a third party, not household data. It is
never merged into `household_products`: copying an OFF name into the household
table on save would make that table a merged database under ODbL. The household
row holds what the person typed or *confirmed*. Confirming an OFF suggestion is
the person's act, so it is their data.

## 4. What a scan does where

A scan always resolves first (§3). The page that is open decides what happens
next. Pages register a **scan purpose** in a small store while they are
mounted. The scan button reads it, and the sheet's buttons follow it. There is
no route sniffing in the scanner itself.

```ts
// lib/features/scanning/purpose.svelte.ts
type ScanPurpose =
  | { kind: 'shopping'; householdId: string }                    // list, front page, week
  | { kind: 'recipe'; recipeId: string; householdId: string }    // detail and cook mode
```

### 4.1 Front page, shopping list, week: add to the list

The sheet shows the product and one primary button, **Add to the list**. It
goes through the shopping store's existing `add` (with `quantity`/`unit` when
the pack says them), so sections, merging and the undo toast behave as for
typing. A second scan right after keeps the sheet open, so you can scan the
cupboard empty in one go. The sheet counts what was added and closes with
**Done**.

### 4.2 Recipe and cook mode: match the recipe

The product's `ingredient` is matched against the recipe's lines with the same
reading search uses (`IngredientWords` head, `foldText`), so *Spaghetti n.5*
matches *500 g Spaghetti*. What the sheet does next:

- **Match**: it names the line ("This is the *500 g Spaghetti*"). If the
  household noted a cook time, it shows it, with a **Start 9 min timer** button
  that uses the cooking timers that already exist. The line is marked *got out*
  in the cook session, shown as a quiet check in the ingredient list. This is
  the bead's "cross off". It is a per-session flag and is never saved to the
  recipe.
- **No match**: it says so, and offers **Add to the list** as above. A product
  that is not in the recipe is most likely one you need to buy.

## 5. Decisions needed

### 5.1 The fallback decoder (blocks iPhone support)

Recommendation: the `barcode-detector` ponyfill (it wraps `zxing-wasm`, about
1 MB of WASM). It is imported **only when** `BarcodeDetector` is missing, from
the scanner chunk, and never on a cold load. Its `.wasm` is served from
`/_app/` (`locateFile`), never a CDN. This needs `'wasm-unsafe-eval'` added to
the document's `script-src`. That permits compiling WebAssembly. It does not
permit `eval` of strings, so Trusted Types and the nonce stay as they are.
(`securityheaders` and the e2e `trusted-types.spec.ts` policy change together.)

The alternative with no CSP change is to type the digits under the
viewfinder, without camera decoding on iPhones. That works, but it is not what
the bead means by scanning.

**As built (`culina-v2-nb65.6.3`).** Jakob chose the ponyfill on 2026-10-27.
`barcode-detector` 3.2.2 and `zxing-wasm` 3.1.3 are exact dependencies; the
second is the version the first was built against, and only there for its
`.wasm` (a unit test fails if the two drift apart). `scanner.ts` tries
`nativeDetector()` first, then `fallbackDetector()`, which imports
`barcode-detector/ponyfill` dynamically and compiles the reader before the
camera opens. Where neither works (no camera, offline before the decoder was
ever fetched, a policy that refuses WebAssembly), the digits are typed. The
reader `.wasm` (1.1 MB, 0.36 MB as Brotli) is a Vite `?url` asset served from
`/_app/immutable/assets/`, handed to the ponyfill through `locateFile`. The
library's default `locateFile` names jsdelivr; `build-tools/selfHostedWasm.ts`
removes that address at build time and fails the build if a CDN address is
left. The build precompresses `.wasm`, and the service worker does not install
it up front, so a phone with its own decoder never downloads it. The document's
`script-src` is `'self' 'nonce-…' 'wasm-unsafe-eval'` (`SecurityHeaders.cs`),
and the app's own shell (`build-tools/nativeShell.ts`) carries the same source,
since WKWebView has no `BarcodeDetector`. `'unsafe-eval'` stays absent, which the
security header tests assert as a quoted token.

Timing note: `package.json` and `pnpm-lock.yaml` currently have someone's
dependency update staged. Adding the package waits until that lands, or the
two lockfile changes collide.

### 5.2 Open Food Facts (blocks automatic product names)

Recommendation: an admin setting `Products:OpenFoodFacts`, **off by default**,
shown on the server settings page next to the AI providers. It would carry the
same honesty as those: "Culina will ask openfoodfacts.org about each new
barcode; the code leaves your server." Without it, everything works from
household memory. The first scan of each product costs one typed name.

**As built (27–28 October 2026, decided by Jakob).** One admin switch, no
per-person opt-in: `GET/PUT /api/v1/settings/products` (`openFoodFacts`, plus
an optional `contact` sent in the User-Agent; without one Culina names its
project page, so nothing personal leaves the server), stored as the instance
settings group `products`, with no restart. **On by default**, reversing the
recommendation above: Jakob, 28 October, "the scanner should not require
entering everything, but ask Open Food Facts, and only when it is not found
the user enters the data". An instance whose administrator saved it off stays
off. `/users/me` says `openFoodFacts` so the client offers nothing when it is
off. Two reference
endpoints, kept apart from the household's memory instead of folded into it:
`GET /api/v1/products/{gtin}` (`world.openfoodfacts.org/api/v2/product`, cached
in `product_lookups`, migration 0058: hits 30 days, misses 7, an old answer
served when OFF is busy or away) and `GET /api/v1/products?q=` (one search on
request, via `search.openfoodfacts.org`, filtered to `en:germany`; the older
`cgi/search.pl` ranked "Fromage Blanc" first for "skyr"). The server keeps under
12 product reads and 8 searches a minute. Each answer carries `doubts`
(`CatalogueProduct.Doubts`: no energy, macros missing, 4/4/9 mismatch over
±20 %, kJ/kcal mismatch, impossible values) and the product page `url` for the
attribution. Taking a product over is `POST /households/{id}/foods` with `gtin`
and `fromCatalogue`: the food is kept with `source` `openFoodFacts`, or
`openFoodFactsEdited` when the values differ from the cached answer, and the
household's product memory learns the code when it did not know it, so the
next scan is one step. `GET .../products/{gtin}` returns that `food`. The
diary's Eintragen sheet is the first place a scan is reachable
(`DiaryProduct.svelte`). Not built yet: OFF attribution in the recipe
breakdown and the diary entry's detail (they still say "from the package"), and
the "Datenquellen" page. The ODbL question to reuse@openfoodfacts.org
(`culina-v2-b3hl.18`) is still open.

## 6. Camera ergonomics

- **One sheet**, `ScanSheet.svelte`, full-height on a phone, with the
  viewfinder on top and the answer below. The thumb stays at the bottom, where
  the answer's buttons are.
- **The rear camera** (`facingMode: 'environment'`) at 1280×720. A barcode
  needs width, not pixels, and 720p keeps decoding fast on mid-range phones.
- **Decoding** runs per `requestVideoFrameCallback` and is throttled to about
  8 attempts a second. It stops while the sheet is closed, the tab is hidden,
  or a result is showing.
- **Lock**: two equal reads in a row, so a half-visible code does not resolve
  wrongly. Then the frame turns solid, `navigator.vibrate(30)` fires where it
  exists, and an `aria-live` region names the product.
- **Torch** toggle only where `getCapabilities().torch` exists.
- **Typing the digits** is always offered under the viewfinder: for a crumpled
  code, a denied camera, or a laptop with no rear camera. The check digit is
  validated before anything is sent.
- **Permission denied**: the sheet says how to allow the camera again for
  *this* platform, using wording already written for recipe photos. Typing the
  digits stays available.
- Formats: EAN-13, EAN-8, UPC-A, UPC-E. These are what groceries carry. QR is
  out: no grocery identity lives in one.

## 7. Offline

- The household's memory is small (hundreds of rows at most). The service
  worker's `culina-private` cache keeps `GET …/products` (the list form), so a
  known code resolves with no network. The sheet looks it up locally first.
- An unknown code offline can still be named. The PUT is queued with the same
  retry the shopping list uses for offline adds. The shopping add itself already
  works offline.
- An OFF lookup needs the server and the server's internet. Offline, a code the
  household has not named says "Not known here yet. Name it, or try again when
  online", with the name field ready.

## 8. Phases

Each is its own bead under `culina-v2-nb65.6`, in this order:

1. **Household product memory** (backend): migration, the three endpoints,
   inheritance, archive export, tests. No decisions needed.
2. **Scan sheet with the native detector and typed digits** (frontend):
   camera, lock, torch, haptics, purpose store, unknown → name it. No decisions
   needed. iPhones get the typed-digits path until phase 3.
3. **Fallback decoder**: built, see §5.1.
4. **Recipe context**: matching, *got out* state in the cook session, cook-time
   timer.
5. **Shopping context**: continuous add, count, scan button on the front page,
   list and week.
6. **Open Food Facts**: after §5.2 is decided. Server lookup, cache, settings
   page, attribution in the sheet.
7. **Offline**: cached memory, queued naming, measured with `@offline` e2e.

## 9. Not doing

| | Why |
| --- | --- |
| A pantry or stock count | Culina has none, and a scanner is not a reason to start one. A scan adds to the list. |
| Nutrition from OFF per product | `nutrition-design.md` §M: recipes say "200 g Butter", not a product. A scan does not change a recipe's figures. |
| Scanning from photos in the library | The camera path covers the bead. A still image goes through the same `detect()` later if asked for. |
| QR codes, recipe links by QR | Not groceries. Sharing already has links. |
