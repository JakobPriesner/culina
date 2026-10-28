# Recipe page — where the actions go

| | |
| --- | --- |
| Status | Proposal, 27 October 2026. Nothing built. |
| Decides | The order, weight and place of every action on the recipe page (`/recipes/[id]`), on a phone, an iPad and a desktop |
| Touches | `features/recipes/surface/SurfaceHead.svelte`, `RecipeActions.svelte`, `CookingAction.svelte`, `dockCookingAction.ts`, `features/recipes/detail/RecipeBackLink.svelte`, `RecipeTags.svelte`, `SurfaceServings.svelte` |
| Builds on | `app-structure-design.md` §7 (the loop, the shell's bottom accessory), `design-system.md` (buttons, insets), commit 9b523236 (actions off the photograph on a phone) |

**The rule from here on.** The recipe page has one primary action, *Kochen
starten*, and it is the only filled button on the page. It sits where the
reader decides, under the title, and follows the reader as one compact control
once that place scrolls away. Everything else is quieter, labelled, and fewer.

---

## 1. What the page does today

Seen on 27 October 2026 at 1440×900, 1100×800, 1024×768, 820×1180, 390×844 and
320×640 with *Thunfischnudeln* (photo, 3 ingredients, 6 steps, 5 tags).

| # | Finding | Where |
| --- | --- | --- |
| R1 | **The title breaks inside a word.** At 1024 px (iPad landscape, small laptops) the three labelled buttons and the menu take 605 px of the title row, the title gets 321 px, and `overflow-wrap: anywhere` sets *Thunfischnudel / n*. German compound titles (*Rindergeschnetzeltes*, *Zwetschgendatschi*) make this common, not an edge case. | 1024–1180 px |
| R2 | **The head has no primary action.** Plan, list, edit and the menu are four identical bordered capsules. *Kochen starten*, the action the page exists for, is not in the head at all; it floats, centred, at the bottom edge. | all |
| R3 | **The floating cook bar covers what is read.** On a 1024×768 first screen it sits over the meta line (*2 Portionen · ca. 227 kcal*); while reading it sits over the method on every width. | all |
| R4 | **On a phone the bottom is two floating layers deep.** The cook bar (two 173 × 78 px buttons) sits on the tab bar. At 320×640 they cover 184 px, 29 % of the screen, and hide the servings stepper on the first screen. | < 52rem |
| R5 | **The cook bar's labels wrap.** At 20 px in 173 px halves, *Kochen / starten* and *Hab ich / gekocht* take two lines each, which is why the bar is 78 px tall. `design-system.md` says 56 px for cooking actions. | < 52rem |
| R6 | **"Hab ich gekocht" is drawn as an equal of "Kochen starten".** Same size, same row, half the width each. Recording a meal cooked without cook mode is the rarer path, but the layout says the two are a coin toss. | all |
| R7 | **On a phone and an iPad the head actions are unlabelled circles.** A calendar with a tick, a basket, a pencil and three dots. Below 64rem every label is hidden, so *Einplanen* and *Auf die Einkaufsliste* are guesses, and the basket's *already on the list* state is a 6 px tick. | < 64rem |
| R8 | **Everything is the same capsule.** Actions, the servings stepper, *Auf meine Menge anpassen*, the five tags (44 px bordered pills) and the cook bar's secondary button share one outline style. Doing, adjusting and filtering look alike, so nothing leads. | all |
| R9 | **What stays in reach after scrolling differs by width.** On a phone the condensed title bar appears but carries no actions; on a desktop the shell header stays but the recipe's actions scroll away. Plan, list and edit are reachable from the top only. | all |

What already works and must stay: the photograph is never covered by
controls (9b523236); the list action says when the recipe is *already on the
list*; delete is last in the menu, apart and red; *Bearbeiten* is a link that
opens in a new tab; the condensed back bar on a phone.

## 2. Research

Read on 27 October 2026. HIG quotes come from the pages' current text
(Toolbars absorbed Navigation bars in June 2025 and gained Liquid Glass in
December 2025). "Observed" means seen in the app, not documented.

| Source | What it says | For this page |
| --- | --- | --- |
| [HIG Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons) | "Keep the number of prominent buttons to one or two per view." "Use style — not size — to visually distinguish the preferred choice." Labels start with a verb; hit region at least 44×44 pt. | One filled lead (R2, R6). The lead and *Hab ich gekocht* must differ in style, not be equal halves. |
| [HIG Toolbars](https://developer.apple.com/design/human-interface-guidelines/toolbars) | Don't put too many items in a toolbar; aim for at most three groups; "Only specify one primary action", on the trailing side; trailing items stay visible at all widths; prefer symbols "except for actions like edit that aren't well-represented by symbols"; a large title that shrinks on scroll. | Condensed bar: title leading, lead + ••• trailing (R9). Edit keeps a word. |
| [HIG Pull-down buttons](https://developer.apple.com/design/human-interface-guidelines/pull-down-buttons), [Menus](https://developer.apple.com/design/human-interface-guidelines/menus), [Context menus](https://developer.apple.com/design/human-interface-guidelines/context-menus) | A More menu is worth it from about three items; "Avoid putting all of a view's actions in one pull-down button"; an ellipsis hides what is inside; frequent items first, related ones grouped, Delete last and destructive. | Four or five rare items in •••; plan, list, edit outside. |
| [HIG Layout](https://developer.apple.com/design/human-interface-guidelines/layout), [Designing for iOS](https://developer.apple.com/design/human-interface-guidelines/designing-for-ios) | Most important items near the top and leading side; group related functions; size classes may change how much is *visible*, never what is *available*; limit on-screen controls while keeping secondary actions discoverable. | Same action set at every width; only labels and grouping change. |
| [HIG Typography](https://developer.apple.com/design/human-interface-guidelines/typography) | Keep truncation to a minimum; let text take the lines it needs; keep hierarchy at large text sizes. | The title wraps whole (R1); labels may wrap at 200 % text, never at 100 % (R5). |
| [HIG Materials](https://developer.apple.com/design/human-interface-guidelines/materials), [Liquid Glass announcement](https://www.apple.com/newsroom/2025/06/apple-introduces-a-delightful-and-elegant-new-software-design/) | Glass "floats above the content layer"; "Don't use Liquid Glass in the content layer"; use sparingly; controls give way to content; bars shrink on scroll. | At most one floating layer; nothing floats over the method (R3, R4). |
| Apple News+ Food ([Mac guide](https://support.apple.com/guide/news/iph2ec80e7c7/mac), [iPad guide](https://support.apple.com/en-in/guide/ipad/ipad7c876750/ipados)) | Save and Cook are direct buttons on a recipe; **Mark as Cooked** sits in More, with notes, units and scaling. | Apple's own recipe UI demotes "cooked" to the menu (R6). |
| Apple Music album, App Store product page (observed) | Play / Shuffle under the artwork, ••• for the rest; the App Store's GET button reappears in the navigation bar once the big one scrolls off. | The lead under the title, then compact in the bar. |
| Mela ([MacStories](https://www.macstories.net/reviews/mela-an-elegant-and-innovative-recipe-and-cooking-app-for-iphone-ipad-and-mac/)), Paprika ([help](https://paprikaapp.com/help/ios)), Crouton ([MacStories](https://www.macstories.net/reviews/crouton-review-an-elegant-modern-recipe-manager-and-cooking-aid/)), NYT Cooking ([help](https://thenewyorktimeshelpcenter.helpjuice.com/19698276311700-Cooking-Recipe-Box)) | Mela: Cook / Groceries / Scale "just beneath the name of the dish". Paprika: grocery basket and meal-plan calendar as toolbar icons, share/print/duplicate in an Actions menu. Crouton: a play button starts cooking. NYT: grocery list under the ingredients. | Cook under the title is the genre's convention; plan and list stay one tap away. |
| Dieter Rams ([Vitsœ](https://www.vitsoe.com/us/about/good-design)) | "Less, but better." | Fewer outlines, one lead (R8). |
| Luke Wroblewski ([2007](https://www.lukew.com/ff/entry.asp?571)) | "Reducing the visual prominence of secondary actions minimizes the risk for potential errors." | Tinted, not outlined, secondary actions. |
| Steven Hoober ([UXmatters 2013](https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php)) | 49 % one-handed, 36 % cradled, 15 % two-handed; grips change every few seconds; he later disowned the "thumb zone" drawings, and the centre of the screen is touched most. | No case for pinning the lead to the bottom edge; a full-width button mid-screen after the photo is reachable. |
| Fitts's and Hick's laws ([Laws of UX](https://lawsofux.com/fittss-law/), [Hick](https://lawsofux.com/hicks-law/)) | Time to hit a target grows with distance and shrinks with size; decision time grows with the number of choices — highlight the recommended one. | A large lead; three visible secondaries, not seven. |
| Jakob Nielsen ([progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/)), NN/g ([contextual menus, 2025](https://www.nngroup.com/articles/contextual-menus-guidelines/); [flat UI, 2017](https://www.nngroup.com/articles/flat-ui-less-attention-cause-uncertainty/); [sticky headers, 2021](https://www.nngroup.com/articles/sticky-headers/)) | Show the few most important options first, split by frequency; menus are wrong for frequent actions; weak clickability signifiers cost about 22 % more time; sticky headers use the least space that stays usable. | Frequent outside •••; tinted buttons keep a visible fill (not ghost); the condensed bar stays one row. |
| Material 3 ([buttons](https://material-web.dev/components/button/), [FAB](https://material-web.dev/components/fab/)) | Filled for important, final actions; outlined is medium; text buttons lowest; the FAB is the screen's most important action. | Same ladder; we keep the lead in the content rather than a FAB (§5). |

## 3. Principles for the recipe page

1. **One lead.** One filled button per screen: *Kochen starten* (or *Weiterkochen*).
   Everything else is tinted or plain. (R2, R6, R8)
2. **The title owns its line.** Actions never share a row with the title, at any
   width. A title wraps between words, never inside one. (R1)
3. **Put the action where the decision is made, then keep it in reach.** The lead
   sits under the title and the meta line. When it scrolls away, it reappears as
   one compact control, never as a band over the method. (R3, R9)
4. **At most one floating layer above the tab bar, one line tall.** (R4, R5)
5. **Say it in words when there is room for words.** Icons carry a label below
   or beside them on every width; an icon alone is kept for universally known
   glyphs (the menu's •••, back). (R7)
6. **Doing, adjusting and filtering look different.** Actions are buttons;
   servings is a stepper; tags are text chips. (R8)
7. **Frequent outside, occasional inside the menu, destructive last.** Three
   frequent actions stay visible; cookbook, share, copy, *Hab ich gekocht* and
   delete live in •••. (Keep what works.)

## 4. Proposal

### 4.1 The head, all widths

```
[ photograph ]

Thunfischnudeln                                   ← title alone, wraps between words
2 Portionen · ca. 227 kcal · 3× gekocht
Abgelegt in culina.jakobpri.de · 27.10.2026

[ ▶  Kochen starten ]   ◌ Einplanen   ◌ Einkaufsliste   ◌ Bearbeiten   (•••)
  filled, the only one     tinted or plain, labelled                 menu
```

- `SurfaceHead`'s `.titleRow` grid goes. The title is a block of its own;
  the action row follows the meta line. That alone fixes R1 at every width.
- Title wrapping: `overflow-wrap: break-word` (only a word wider than the
  whole line may break) plus `hyphens: auto` with `lang="de"`, so a very long
  compound on a 320 px phone hyphenates instead of splitting at a random letter.
  `text-wrap: balance` keeps two-line titles even.
- The action row is `RecipeActions` with *Kochen starten* moved into it as the
  first, filled item, and a visible gap between it and the three quieter
  actions. *Weiterkochen* replaces it while a session is open; after *Hab ich
  gekocht* the wrap-up card already stands in for it (`wrappingUp`).
- The three frequent actions become tinted buttons (no outline; a soft fill of
  `--surface-sunken` or similar) so the filled lead is unmistakable and the
  page has fewer outlines.

### 4.2 On a phone (< 52rem)

```
‹                                         (the sticky back bar, as today)
[ photograph ]
Thunfischnudeln
2 Portionen · ca. 227 kcal
[ ▶  Kochen starten                                    ]   ← full width, 56 px, one line
 ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐
 │   📅   │ │   🧺   │ │   ✎    │ │  •••   │                ← icon + caption tiles,
 │Planen  │ │Einkauf │ │Ändern  │ │ Mehr   │                  like the iOS contact card
 └────────┘ └────────┘ └────────┘ └────────┘
[ − 2 + ]  Auf meine Menge anpassen
```

- The lead is one full-width button, one line, 56 px: the same place on every
  recipe, mid-screen once the photo has scrolled a little (Hoober: the centre is
  touched most; a pinned bottom bar buys no reach worth the text it covers).
- The other actions become four equal tiles with an icon and a short caption
  (*Planen*, *Einkauf*, *Ändern*, *Mehr*) — the pattern of the iOS Contacts card
  (message, call, video, mail) and Apple Maps' place card (observed). Labels come back
  (R7) without the row growing past one line at 320 px (288 px of column:
  4 × 66 px tiles and 3 × 8 px gaps).
  The list tile reads *Auf der Liste* with a filled glyph when `listed`.
- **When the head scrolls away**, the condensed back bar (already there) gets
  the actions it lacks (R9): `‹  Thunfischnudeln  ·········  ▶ Kochen  •••`. The
  small *Kochen* pill on the trailing side is the App Store's pattern: the GET
  button moves into the navigation bar once the big one has scrolled off.
- **No cook bar at the bottom any more.** That removes the second floating
  layer (R4) and the wrapped labels (R5). At the end of the method a plain,
  in-flow row closes the reading: *Kochen starten* (filled) and *Hab ich gekocht*
  (plain), where the decision is made — the place `CookingAction` was always
  meant to settle into, without the docking in between.

### 4.3 On an iPad and a desktop (≥ 52rem)

```
‹ Alle Rezepte
[ photograph ]
Thunfischnudeln
2 Portionen · ca. 227 kcal · Abgelegt in …
[ ▶ Kochen starten ]   Einplanen   Auf die Einkaufsliste   Bearbeiten   •••
```

- All labels shown from 52rem up (today they hide below 64rem). The row is
  about 640 px wide; under the title it has the full column, so it fits from
  52rem (832 px) without collapsing. Below that, the phone tiles take over.
- When the title scrolls under the shell header, a slim recipe strip slides in
  under it: title on the leading side, *Kochen starten* (small, filled) and •••
  on the trailing side. Same rule as the phone's bar, so one component serves
  both (`RecipeBackLink` grows a trailing slot).
- No floating band over the method (R3).

### 4.4 Quieter controls below the head

- **Servings**: keep the stepper, make *Auf meine Menge anpassen* a plain text
  button beside it (it is an adjustment, not an action).
- **Tags**: text chips without an outline, `--text-sm`, a soft fill, 32 px
  visual height inside a 44 px hit area. They read as metadata and filters,
  not as five more buttons.
- **Note**: *+ Notiz für nächstes Mal* stays a text link.

### 4.5 The menu (•••)

In this order: *Zu einem Kochbuch hinzufügen*, *Teilen*, *Kopieren* (inherited
recipes), — separator —, *Hab ich gekocht*, — separator —, *Rezept löschen*
(red). *Hab ich gekocht* is also at the end of the method (§4.2), so the menu
is its second door, not its only one.

## 5. Not now

- A floating action button. Material's FAB is for the screen's one constructive
  action and Apple has no equivalent on a content page; the compact control in
  the bar does the same job without covering text.
- Moving plan, list and edit into the photograph (9b523236 decided against it).
- A Liquid Glass bottom toolbar on the phone recipe page. It would be the
  native iOS 26 place for actions, but the tab bar already floats there and the
  shell slides it away while reading (69b0eb6e); a second floating bar is what
  R4 is about. Revisit with the native app (`native-app-design.md`).

## 6. Phasing

1. **Fix the defects (small, safe).** Title on its own line and wrapping
   between words (R1); cook bar labels on one line at 56 px (R5). No new
   pattern; can ship alone.
2. **The lead in the head.** *Kochen starten* first and filled in the action row;
   the three others tinted; labels from 52rem (R2, R6, R7 on iPad).
3. **One floating layer.** Remove the docked cook bar; condensed bar with
   *Kochen* and ••• on phone and desktop; in-flow closing row at the end of the
   method (R3, R4, R9).
4. **Phone tiles and quieter controls.** Icon + caption tiles; tags and
   *Auf meine Menge anpassen* toned down (R7, R8).

Each phase is checked at 320×640, 390×844, 820×1180, 1024×768 and 1440×900,
with the 200 % text size the design system already tests, and with an
inherited recipe (copy instead of edit) and a recipe without steps (no cook).
