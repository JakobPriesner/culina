# Culina — design system

How the frontend looks, and the structure that keeps it looking like one
product. Component placement rules come from `sveltekit-components-and-pages`;
this document fixes the **tokens, the theme architecture and the component
inventory**.

## September 2026 refinement

Culina pairs warm ivory and charcoal surfaces with olive actions and a quiet
serif wordmark. Recipe titles use Georgia; controls, quantities, and instructions
keep system typography. The collection separates page identity from a compact search, filter, and count
toolbar, with gutters that adapt to narrow phones. Photographs
lead recipe cards, while recipes without a photo sit on a soft paper surface.

The reading and cooking surfaces use a softly shaded ingredient panel, generous
instruction spacing, and a visible action dock. Authentication and the optional
recipe preview use a deep olive photographic feature panel. Its foreground and
background have explicit semantic tokens (`--text-on-feature` and
`--surface-feature`), tested for contrast in both modes and mapped to ink on white
for print. Focus inside the dark preview panel uses its light foreground.

Review `/design/recipes` for the interactive recipe concept and `/design` for
primitives in development. The concept uses labelled temporary sample data and
shares its palette, typography, and controls with the production app. Both
preview routes retain the release guard. [The product plan](product-quality-plan.md)
explains the release scope and quality contracts.

## Library and shared control polish

The recipe and cookbook libraries share `PageHeader`: an editorial title,
a short supporting line, and contextual creation actions. One shared navigation
exposes Recipes, Cookbooks, Week, Shopping, and Me directly. The same order and
route selection appear in the desktop top bar and, below 64rem, the bottom bar.
There is no collection submenu or secondary sidebar. Cookbook detail uses the
shared navigation too. The [research and current decision](navigation-research.md)
explain the evidence and the explicit five-destination limit.

`NewRecipeLink` remains a direct link in the header on every signed-in page,
with a visible label from 80rem and an accessible, titled plus control below.
The logo and main navigation retain their matched translucent backgrounds.
Current destinations use a raised selection shadow, text emphasis, and
`aria-current`. Every navigation item is a link, not a modal launcher.

The recipe toolbar aligns a rounded rectangular search field with an equally
tall `FilterChip`; its pressed state includes a checkmark as well as color.
Count and sort information sit together at the trailing edge, reflowing below
the controls on phones. Creation buttons share plus icons and rounded corners;
green remains reserved for recipe creation. The 30-minute filter queries the
API and composes with text search.

Library search and the quick filter survive recipe navigation within the current
session. They reset on household changes and sign-out. Search supports Escape to
clear while preserving focus, and a separate action clears both filters. Counts
use Paraglide plural variants in English and German, including cookbook counts,
servings, and pieces.

Photographed recipes retain image-led cards. Recipes without photographs use a
paper surface, a small cooking mark, and a ruled heading area. Card links have a
visible directional affordance and a whole-card keyboard focus ring. The feature
link uses the feature foreground for both text and focus in light and dark mode.

Shared buttons reserve their label geometry while a centered spinner appears;
the label remains available to assistive technology. Disabled and pending states
retain readable text. Search and form controls share a separated focus outline.

`library-polish.spec.ts` checks the search round trip, composed filters, reset,
Escape behavior, reflow, and automated accessibility at 320, 390, 768, and 1280px
in both locales and modes using temporary API fixtures.

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
           --surface-accent-subtle  --surface-feature
Text       --text  --text-muted  --text-subtle  --text-on-accent
           --text-danger  --text-success  --text-on-feature
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
| Input | `TextInput`, `TextArea`, `Select`, `Checkbox`, `RadioGroup`, `Stepper`, `SearchField`, `Field` (label + hint + error + a11y wiring), `FilePicker` (the clipped file input, opened by a control beside it), `ImageField` (preview when set, template of the same size when not) |
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

Sizing note: the space scale is sparse (1, 2, 3, 4, 6, 8, 12, 16, 24) and there
are three control heights — `--control-sm` is 2.75rem, not because it looks
small but because 44px is the floor for something a wet thumb has to hit while
the phone is propped against a mixing bowl. "Small" means dense, never hard to
press.

`inputs/control.css` is the one global class in the app (`.ds-control`). It
describes what a typed-into or chosen-from control looks like, once, because a
text input, a select and a search field that each own a copy of that are three
things that drift — and the drift is only visible when two of them sit next to
each other in a form.
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

## Responsive layout contract

Breakpoints follow the space the content needs. A tablet keeps bottom navigation
until the brand, all destination labels and connection status fit across the
header. The content stays capped at `--layout-wide` (80rem) on larger monitors;
prose and shopping lists keep the narrower `--measure`.

| Minimum viewport width | Layout behavior |
| --- | --- |
| Base, including 320px | One-column collection and planner; bottom navigation; stacked recipe reading; settings categories wrap above their panel; authentication shows the form. |
| 40rem / 640px | Two-column recipe collection and planner. Search and action rows can share space where their content fits. |
| 48rem / 768px | Sheets become centered dialogs. This changes overlay geometry without forcing the page into a desktop layout. |
| 64rem / 1024px | Top navigation replaces bottom navigation. Recipe ingredients and method sit side by side; settings gains a sidebar; authentication and featured recipes gain a photo column. Collections and planner use three columns. |
| 80rem / 1280px | The planner displays all seven days across. Other content retains its maximum width. |

Use `min-width` for enhancements and `width < …` for their exact complement,
so fractional viewport widths do not create a gap. Breakpoint values are literal
`rem` values in CSS because custom properties cannot be used in native media
query conditions. Do not add a viewport breakpoint for each individual control.
The 40rem collection breakpoint is shared by the real and preview collections;
the 64rem split is shared by navigation and complex page layouts.

Spacing is fluid between these transitions. `--layout-page-space` scales page
padding from 1.5rem to 3rem, and `--layout-section-gap` scales large gaps over the
same range. `--layout-gutter-start` and `--layout-gutter-end` include device safe
areas so the header, page and persistent controls share an alignment even in
landscape. The viewport permits safe-area coverage without disabling zoom.

Ingredient fields use **container queries**: a narrow shopping list on a large
monitor must behave like a narrow form. Amount and unit stay together, followed
by full-width name and preparation fields. A simple three-field entry fits on
one row at 28rem of actual field space; the four-field recipe entry needs 36rem.
The ingredient editor puts its actions below the fields under 44rem of editor
space; shopping entry does so under 40rem. These are component fit thresholds,
not additional device categories.
Below 24rem of editor space, written ingredient names get their own full-width
line below the amount and actions. This also accommodates enlarged text.

Buttons keep a minimum height of 44px (56px for cooking actions) and grow when a
translated label needs another line. Long names, email addresses and URLs may
wrap. Action groups wrap as groups; content must not be hidden with page-level
horizontal overflow clipping to make a test pass.

The desktop header has a transparent full-width container. The brand card and
navigation track share the same softly translucent background and blur. Only the
controls intercept pointer events, so exposed content behind the header stays
usable. Direct recipe creation stays in the header. Navigation links lead directly
to each workflow on both desktop and mobile. Below 32rem of viewport height the
header returns to document flow to preserve reading space. Glass surfaces use
semantic theme tokens shared by both color modes.

The shell measures bottom navigation and the resume bar to keep recipe actions
above them. Cooking keeps only step navigation sticky; its stop action stays in
document flow so two footers never compete for the same inset. The preview
measures its cooking dock too, reserving its actual
height rather than assuming one row. Below 32rem of viewport height, the top
header and recipe action docks return to normal document flow to preserve
reading space. Sheets and dialogs use dynamic viewport height; their body
scrolls while the heading, close control and footer stay available.

Cookbook shelves scroll horizontally within the page. Keyboard focus reveals
the whole card, with room for its focus ring; snapping is disabled while focus
is in the shelf. Popovers constrain their width to the viewport and scroll
within the space beside their trigger. If rotation or scrolling takes the
trigger off-screen, the open panel moves into the viewport until the trigger
returns. Browsers without sized anchor positioning use a centered panel.

`tests/e2e/responsive.spec.ts` checks real production routes with deterministic
API fixtures and long content in both English and German. It covers 320, 390,
639/640, 767/768, 1023/1024, 1279/1280 and 1536px, plus short landscape. Checks
include document and child overflow, useful ingredient input widths, a single
visible navigation, grid transitions, editing, planner overlays and reachable
cooking controls. Fixture tests cover layout; the existing signed-in suites
remain responsible for backend integration.
`tests/e2e/responsive-transitions.spec.ts` also covers live resizing during
cooking, keyboard traversal of the cookbook shelf, rotation with forms and
ingredient pickers open, long ingredient lists, and 200% text at 320, 640 and
1280px. Both desktop and mobile browser profiles exercise these interactions.

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

## Waiting, emptiness and failure

`createLoadingState()` in `lib/app/` is the only implementation of the timing
rules, and every feature uses it rather than its own `setTimeout` — the moment
two screens disagree about these numbers the app feels inconsistent and nobody
can say why.

- **~150 ms before anything appears.** Most responses arrive inside that, and a
  skeleton that flashes for one frame makes a fast app feel broken.
- **~300 ms minimum once shown.** Otherwise a response landing at 160 ms
  produces a flicker that reads as a glitch rather than as progress.
- **~10 s to "this is taking longer than usual", with a retry.** Better than
  spinning forever while the person wonders whether to reload.

`Skeleton` shows the shape of what is coming, so the layout does not jump. It is
`aria-hidden`; the container carries `aria-busy`, because a screen reader listing
twelve empty boxes is worse than silence. Under `prefers-reduced-motion` it is a
flat tint, which says "this is coming" just as well.

`EmptyState` insists on a real action, and on distinguishing **"you have not made
one yet"** from **"your filter matched nothing"** — the second is a mistake to
undo, the first is an invitation. "No results" alone tells someone what they can
already see.

`ErrorState` keeps the page frame, says what failed in plain language, offers
retry, and shows the request id in small print. Nobody reads that id until it
matters, and then it is the whole conversation.

`BusyRegion` is the rule that a refetch of data already on screen **keeps the old
data**. Replacing a list you are reading with a skeleton loses your place, loses
your scroll position, and tells you less than the stale list did.

## Overlays

`Sheet`, `Modal` and `Popover` all come from one place; no feature builds its
own. `Sheet` and `Modal` are the same `Dialog` with different geometry — a sheet
rises from the bottom edge on a phone, where a thumb already is, and becomes a
centred dialog past 48rem, because a full-width strip along the bottom of a
desktop window is a long way from where the eye is. One component and one API,
so a feature cannot pick the wrong one for the viewport.

`Dialog` is a native `<dialog>` opened with `showModal()`. The browser already
traps focus, makes the rest of the page inert, puts the dialog in the top layer
above every stacking context, closes on Escape, and restores focus to whatever
opened it. A hand-rolled version of that is a few hundred lines and is still
worse on a screen reader. What is done by hand: locking the page's scroll
(counted, so a dialog above a dialog does not unlock early), labelling, a
visible close control — Escape and the backdrop both work but neither is
discoverable — and keeping the caller's `open` in step with the browser's own
closing.

`Popover` is the native `popover` attribute, which brings light dismiss, the top
layer and the trigger pairing for free. For a decision that must be answered,
use `Sheet` or `Modal`: a popover that must not be dismissed is a modal wearing
the wrong clothes.

Because all of this is browser behaviour, it is asserted in Playwright rather
than jsdom — jsdom implements `<dialog>` without the top layer that gives it
those properties.

## Toasts instead of confirmations

Asking "are you sure?" before something reversible costs everyone a decision to
protect against a mistake that was already cheap to fix. Culina does the thing
and offers **Undo** in a toast.

Each toast is `role="status"` — a polite live region, never assertive: it reports
something that already happened. The clock pauses on hover *and* on focus,
because a keyboard user reading with focus inside the toast is doing exactly what
a pointer user hovering is doing. Undo removes the message as it runs, so a
second press cannot undo the undo. At most three stack; beyond that it is a log,
not a notification.

## Never dim text with opacity

Reducing a text element's opacity blends it toward whatever is behind it by an
amount no palette review can see and the theme's own contrast test cannot
reach — the tokens are all correct, and the rendered page is not. The cooking
screen dimmed its non-current steps to `0.45`, which put body text at **2.7:1**
against the page in light mode. The intent was right; opacity was the wrong way
to express it.

Recede with a colour the contract already proves readable — `--text-muted` —
and with size and weight. Opacity is for things that are not text.

The rendered pages are checked by axe on every route in both modes, which is
the layer that catches this. The token table catches what the design system
promises; only the page catches what it renders.

## A control that changes what it says is one control

Two controls in an `{#if}`/`{:else}` that swap places are two elements, and
swapping them takes focus with them. The cooking screen did that at the last
step: the keyboard user who had just pressed Next found themselves back at the
top of the page, at exactly the moment the remaining action was "I made it".

Render one control and change its label and its handler. Use two only when they
are genuinely two things a person might choose between.

## Settings refinement — 15 September 2026

Settings has an anatomy rather than a scroll. The category rail keeps its pills
and gains an uppercase caption and, past 64rem, a sticky position, so the way
out of a long archive is never above the fold. The category's heading and its
one-line lead belong to `me/+layout.svelte`, not to the three pages: the heading
is the word the rail is already showing, and three pages writing it separately is
three places for it to drift. The panel is capped at 46rem — a settings row is a
label on the left and a control on the right, and a 1400px monitor puts a
centimetre of nothing between them.

Inside a category, `SettingsSection` and `SettingsRow` (local to the settings
routes — they are page furniture, not primitives) give every setting one
anatomy: a title and a sentence outside the enclosure, where type does the
structuring, and the run of related controls inside a hairline border, which is
the one thing whitespace cannot say. Rows carry a label, an optional line about
what the setting does *not* do, and the control on the trailing edge; under
30rem of row — a container query, not a viewport one — the control takes its own
line. A row's label is a `span`, because the control already carries its own
label and two labels on one field is an axe failure; `group` is the exception,
naming a set of radios through `role="group"`.

Appearance chooses a mode outright. `ThemeChoice` shows light, dark and follow
the device as three native radios in a segmented track, with the chosen one
raised out of it rather than only tinted. The header keeps the cycling
`ThemeToggle`, where there is room for one icon and the choice is a passing one.
The language and measurement pickers now share `.ds-control` and clip their own
labels beside the row's.

## Recipe preview refinement — 13 September 2026

The development-only `/design/recipes` experience extends the editorial direction with consistent food photography. Collection entries use photographs, typography and spacing without an enclosing card background. A favourite remains a separate, labelled control above the photograph. The feature image fills its reserved area at tablet widths; Image's `fill` option is for parents that already reserve height.

Reading shows ingredients beside the complete method on larger screens. Cooking keeps only the current instruction visible, with a quiet segmented progress line and persistent previous/next controls. On mobile, the shared Sheet exposes ingredients and portions from the bottom controls; on wider screens they stay beside the method. Instructions receive focus on entry and on a step change. Controls remain usable at 320px, with safe-area padding and no motion requirement.

Preview progress belongs to the recipe collection, rather than to a detail component that disappears when closed. Returning to the collection preserves search, filters, favourites, portions, ingredient checks and current steps for the mounted preview session. It restores the opening control's focus and the collection's scroll position. A Continue cooking action makes an active session visible. Reloading or changing language remounts this temporary preview; this is not an implementation of durable cooking recovery.

The shared Image primitive also provides a labelled fallback and accepts a new source after an image failure. These behaviors apply wherever the primitive is used. New sample photography is documented in `design-assets.md`.

## Print

Print is a medium, not a theme. `tokens/print.css` is imported after the themes
and collapses every one of them — and both modes — to ink on paper, because a
printer has one background and it is white, and the browser's "background
graphics" setting is off by default anyway: a dark theme that printed as dark
would come out as a page of toner behind every word, or as white text on white.

It assigns the **whole** semantic set, for the same reason a theme does. A
partial palette is how one forgotten token ends up as pale grey on white, and
that is exactly the token nobody checks before sending a page to a printer.

What is hidden on paper is hidden by the component that owns it — the shell
hides its header and bars, the recipe surface hides its photograph and its
servings control — so it is always obvious what is being hidden and why. The one
global rule covers what is never content anywhere: form fields, dialogs, live
regions. Deliberately **not** every button: a step's ingredient reference is a
button so that pointing at it lights up the ingredient list, and a blanket rule
took the amounts out of the printed steps, which is the one thing they are for.
