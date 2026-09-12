# Culina — design system

How the frontend looks, and the structure that keeps it looking like one
product. Component placement rules come from `sveltekit-components-and-pages`;
this document fixes the **tokens, the theme architecture and the component
inventory**.

## Design intent

Food photography is the content. Everything else gets out of its way.

- **Warm neutrals.** Cream in light, warm charcoal in dark — never blue-black,
  which makes food look grey and dead.
- **One accent**, reserved for the primary action on a screen. If two things are
  accented, neither is primary (principle 10).
- **Photos are the cards.** A recipe card is an image with a title beneath it,
  not a bordered box containing an image inside another rounded rectangle
  (principle 12).
- **Type does the structuring**, whitespace does the grouping, borders are a
  last resort (principles 11, 13).
- **Cook-mode text is sized to be read at arm's length across a counter**, not
  to fit a spec sheet.
- Motion only where it explains something (principle 16).

## Theme architecture — three layers

The requirement is that a second theme is *one file and zero component
changes*. That only holds if components never touch a colour directly.

```
src/lib/design-system/
  tokens/
    primitives.css     layer 1 — raw ramps. No component may name one.
    semantic.css       layer 2 — the contract. Declares every semantic token
                       with a light default so nothing is ever undefined.
    scales.css         space, radius, type, shadow, z-index, duration
  themes/
    warm-paper.css     layer 3 — assigns layer 1 to layer 2, per mode
    index.ts           the theme registry
```

### Layer 1 — primitives

Raw ramps only, no meaning attached:

```css
:root {
  --c-sand-50:  #fdfbf7;   --c-sand-100: #f7f2e8;   --c-sand-200: #ece3d4;
  --c-sand-300: #d9cbb5;   --c-sand-400: #b9a488;   --c-sand-500: #8f7a5f;
  --c-sand-600: #6b5943;   --c-sand-700: #4a3d2f;   --c-sand-800: #302820;
  --c-sand-900: #1f1a15;   --c-sand-950: #141110;

  --c-terracotta-300: #e8a284;  --c-terracotta-500: #c65f3f;
  --c-terracotta-600: #a94c30;  --c-terracotta-700: #8a3d26;

  --c-sage-500: #5c7a5c;   --c-amber-500: #c98a2e;   --c-rust-600: #b23b30;
}
```

### Layer 2 — semantic tokens (the contract)

**The only thing a component may reference.** The full set, and every theme
must define all of them:

```
Surfaces   --surface  --surface-raised  --surface-sunken  --surface-overlay
           --surface-accent-subtle
Text       --text  --text-muted  --text-subtle  --text-on-accent
           --text-danger  --text-success
Lines      --border  --border-strong  --border-focus
Accent     --accent  --accent-hover  --accent-active  --accent-contrast
Status     --danger --danger-hover --success --warning  (+ -subtle variants)
Effects    --shadow-card  --shadow-overlay  --scrim
```

Scales live in `scales.css` and are theme-independent:

```
--space-1 … --space-16        4 px base, 1 2 3 4 6 8 12 16 24 32 48 64
--radius-sm|md|lg|full        4 / 8 / 16 / 9999
--text-xs … --text-4xl        clamp()-based fluid steps
--text-cook                   clamp(1.5rem, 1.1rem + 1.6vw, 2.25rem)
--duration-fast|base|slow     120 / 200 / 320 ms
--ease-out|--ease-spatial
```

### Layer 3 — a theme

```css
/* themes/warm-paper.css */
[data-theme="warm-paper"] {
  --surface: var(--c-sand-50);
  --surface-raised: #ffffff;
  --text: var(--c-sand-900);
  --accent: var(--c-terracotta-600);
  /* … every semantic token … */
}

[data-theme="warm-paper"][data-mode="dark"] {
  --surface: var(--c-sand-950);
  --surface-raised: var(--c-sand-900);
  --text: var(--c-sand-100);
  --accent: var(--c-terracotta-500);
  /* … every semantic token, again … */
}
```

`data-theme` and `data-mode` both sit on `<html>`. Mode is `light | dark |
system`; `system` resolves to a concrete value before paint, so
`prefers-color-scheme` never has to be queried inside a component.

### What makes this safe rather than merely intended

1. **`semantic.css` declares every token with a fallback**, so a partial theme
   degrades instead of rendering invisible text.
2. **A contract test** (`themes.spec.ts`) parses every file in `themes/`,
   extracts the declared custom properties, and fails if any theme is missing
   a token listed in `semantic.css` — in either mode. It also rejects a token a
   theme declares that the contract does not, and a `var(--c-…)` pointing at a
   primitive that does not exist.
3. **The same test asserts WCAG AA contrast** for the pairs the UI really
   stacks — 4.5:1 for text, 3:1 for a focus ring, an input border or a status
   colour — in both modes. A palette that reads well in light and turns muddy in
   dark is the normal way a theme fails, and it is invisible in review.
4. **A raw-colour rule** (`lint.ts`, run by `lint.spec.ts`) fails on a hex
   colour, a colour function, a named colour or a `--c-*` primitive appearing in
   any stylesheet, `<style>` block or inline `style` outside
   `design-system/tokens/` and `design-system/themes/`.
5. **An unknown-token rule**, in the same file. A `var(--space-5)` that nothing
   declares is not an error in CSS — the declaration is simply dropped — and the
   scale is sparse on purpose, so reaching for a plausible-sounding step is an
   easy mistake with a loud result and no warning.

The contract is *derived*, never restated: a token added to `semantic.css`
tomorrow is required of every theme without anyone editing the test.

Adding *cool editorial* later is then: one CSS file, one registry line, and the
tests tell you the moment it is incomplete.

### No-flash theming

The resolved theme and mode are mirrored to `localStorage` and applied by a
tiny **nonce'd** inline script in `app.html` before first paint. The server
already issues a per-response CSP nonce (`cookie-auth-and-security`), so this
needs no `unsafe-inline`. The server-side user setting remains the source of
truth and reconciles on boot.

## Component inventory

### `lib/design-system/` — domain-free

If it knows what a recipe is, it is in the wrong folder.

| | |
| --- | --- |
| Actions | `Button` (primary/secondary/ghost/danger × sm/md/lg, `loading`, `iconOnly`), `IconButton`, `Menu`, `Switch` |
| Input | `TextInput`, `TextArea`, `Select`, `Checkbox`, `RadioGroup`, `Stepper`, `SearchField`, `Field` (label + hint + error + a11y wiring) |
| Containment | `Card`, `Sheet` (mobile bottom sheet, desktop dialog), `Modal`, `Popover`, `Tabs`, `Disclosure` |
| Feedback | `Toast` + `toaster`, `Skeleton`, `EmptyState`, `ErrorState`, `ProgressBar`, `Spinner` (first boot only) |
| Display | `Badge`, `Avatar`, `Icon`, `Image` (aspect-ratio box, lazy, blur-up), `Divider`, `VisuallyHidden` |

Every one of them: driven entirely by props, no store import, no `fetch`, real
semantic elements, visible focus, `prefers-reduced-motion` respected.

### `lib/features/*/components/` — domain-aware

```
recipes/   RecipeCard  RecipeCardSkeleton  RecipeGrid  RecipeSurface
           ServingsControl  ScaleToAmountSheet  IngredientList  IngredientRow
           StepList  StepText  RecipeMeta  RecipeSearchField  TagFilter
           TimeFilter  RecipeEditor  IngredientEditorRow  StepEditorRow
cooking/   CookSurface  CookStep  CookIngredientStrip  CookProgress
           TimerChip  TimerTray  NowCookingBar  MadeItButton  PersonalNotePanel
shopping/  ShoppingSection  ShoppingItemRow  AddFromRecipeSheet  SectionPicker
household/ HouseholdSwitcher  MemberList  InvitationPanel
app/       AppShell  NavRail  NavBar  ThemeToggle  LocalePicker  OfflineBadge
```

## The interaction that defines the product

`/recipes/[id]` and `/recipes/[id]/cook` render the **same** `RecipeSurface`
with a different `emphasis` prop (`read` | `cook`).

| | `read` | `cook` |
| --- | --- | --- |
| Ingredients | full list, body size | contracts to `CookIngredientStrip` — **same screen position** — showing only the current step's ingredients |
| Steps | all visible, body size | current step at `--text-cook`, neighbours dimmed but present |
| Chrome | normal | recedes; wake lock engaged |
| Servings control | above the ingredients | **same place**, still usable mid-cook |

The transition is a continuous re-layout, not a page swap: the step being read
**grows in place** (View Transitions API where supported, a FLIP fallback
otherwise). Under `prefers-reduced-motion` it is an instant cut — which is
still correct, because the layout positions were chosen so that nothing has to
move for the result to make sense.

That property — *things stay where you expect them to be* — is the reason the
two states share one component instead of being two pages that happen to show
the same data.

## Accessibility baseline

Non-negotiable, and cheaper now than retrofitted: WCAG AA contrast for every
token pair in **both** modes (asserted by the contract test), real `<button>`
and `<a>`, a label for every input, one `<h1>` per page, focus visible and
moved deliberately after dialogs and navigation, ≥ 44 px touch targets (≥ 56 px
in cook mode — wet hands are not precise), colour never the sole carrier of
meaning, and `prefers-reduced-motion` honoured by every transition.

## Motion inventory

The complete list. Anything not here does not animate.

| What | Duration | Why it exists |
| --- | --- | --- |
| Servings number morph + highlight | 400 ms | shows *which* amounts changed |
| read ⇄ cook re-layout | 320 ms | preserves spatial context |
| Step advance | 200 ms | direction of travel |
| Sheet / modal enter | 200 ms | where it came from |
| Toast enter/leave | 200 ms | arrival without a jump |
| Shopping item check | 200 ms | the row sinking is the confirmation |
| Skeleton shimmer | 1.4 s loop | static tint under reduced motion |
