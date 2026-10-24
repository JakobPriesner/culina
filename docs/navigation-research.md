# Direct navigation: research and decision

Reviewed 16 September 2026. This is desk research and a design recommendation,
not a Culina user study. The user's repeated feedback supplies the specific
requirement: reaching recipes, cookbooks, or planning must not require opening a
collection menu on every visit.

## What the sources support

| Source | Finding | Implication for Culina |
| --- | --- | --- |
| [Apple: Tab bars](https://developer.apple.com/design/human-interface-guidelines/tab-bars?changes=l_1__4&language=objc) | Tabs navigate between top-level sections; actions belong elsewhere. Apple cautions that overflow makes destinations harder to reach and notice. | Use real destination links. Keep recipe creation as its own action. Avoid a tab that merely opens another menu. |
| [Google: Layouts and navigation patterns](https://developer.android.com/design/ui/mobile/guides/layout-and-content/layout-and-nav-patterns) | A navigation bar presents three to five destinations at the same hierarchy level; established patterns help people reuse familiar mental models. | Flatten the current five workflows into peers, with stable order and labels. |
| [NN/g: Hidden navigation study](https://www.nngroup.com/articles/hamburger-menus/) | In a 179-participant study across six websites, hidden navigation reduced discoverability and worsened several task measures. | Do not conceal frequently needed destinations simply to reduce visible controls. This older website study supports a direction, not a numerical prediction for this app. |
| [Dieter Rams via Vitsœ](https://www.vitsoe.com/us/about/good-design) | Rams emphasizes usefulness, understandability, restraint, and attention to detail. | Remove the unnecessary parent navigation layer. Retain understandable labels instead of using decorative minimalism to hide choices. This is our application of the principles, not a claim about what Rams would choose. |

## Comparable recipe apps

- [Paprika's own iOS guide](https://paprikaapp.com/help/ios/) describes Recipes,
  Groceries, Meals, Pantry, Browser, and Menus as task-oriented sections accessed
  through a side panel. Its recipes screen groups recipes by categories. The useful
  lesson is the clear separation of cooking tasks; its expandable menu is not a
  pattern to copy for a user who explicitly dislikes repeated menu opening.
- [Mela's product site](https://mela.recipes/) distinguishes recipe collection,
  calendar planning, groceries, and focused cooking. This supports organizing by
  the user's activity. Its product description does not establish that a particular
  navigation layout performs better, and we have not tested the current native app.
- [Crouton's product site](https://crouton.app/) presents recipe organization and
  weekly planning as core workflows, with scanning, importing, scaling, and timers
  as capabilities. Our inference: growth in features need not mean growth in
  top-level navigation. The marketing page is not usability evidence.

## Alternatives considered

| Approach | Benefit | Cost | Decision |
| --- | --- | --- | --- |
| Collection tab opens a sheet | Plenty of room for destinations | Repeated extra tap; parent tab behaves differently from peers | Remove |
| Horizontally scrolling destination strip | Can contain many items | Later choices can be offscreen; finding them adds gestures | Reject |
| Four core tabs, profile in the header | More space per core destination | Splits app navigation across locations; account is less apparent | Select (2026-10, replacing the five-tab layout) |
| One five-destination navigation | Every current area is visible and one tap away | Five is already the intended upper limit; requires short labels on phones | Selected first; superseded |
| Unified desktop sidebar and mobile tabs | Roomier desktop navigation | Changes placement between sizes and takes content width | Keep as a future option if primary areas genuinely grow |

## Selected structure

**Recipes · Cookbooks · Week · Shopping**

German: **Rezepte · Kochbücher · Woche · Einkauf**.

All four are normal links. The same order appears in the desktop top navigation
and the compact bottom navigation. Each destination keeps its selected state on
its child routes. There is no Recipe book parent tab, collection launcher,
secondary sidebar, or collection-navigation dialog. Recipe creation stays in the
header and does not consume a destination slot.

Settings are not a cooking workflow, so they left the destinations for the
account menu: the profile icon beside search in the header. It holds who is signed in,
the household switch, theme and language (changed on the spot), the way to
settings (also ⌘, / Ctrl-,) and sign out. The icon is lit while a settings page
is on screen, since no destination is lit there, and a dot on it says an
update is waiting. Search still finds Settings as a place. Existing cookbook creation and
other contextual actions stay on their relevant pages.

The shared toolbar, restrained colors, active selection shadow, and matching
logo/navbar material remain. Removing the sidebar gives the collection its
normal page width back.

## Growth policy and limits

This layout is scalable through **organization**, not through an unlimited number
of tabs. Examples of where future capabilities could belong:

- recipe imports and scanning: recipe creation;
- tags, favorites, and sorting: recipes;
- collection organization and sharing: cookbooks;
- schedules: week planning;
- list management: shopping;
- household, preferences, and account controls: the account menu and settings.

These are placement examples, not newly implemented features. A fifth genuinely
independent frequent workflow requires a new information-architecture decision
based on observed use. Do not silently add a More tab or shrink every label. The
cost of the selected design is a denser bottom bar; the benefit is direct access
and no changing navigation model between sections.

## Evaluation

Automated verification covers direct navigation to all four destinations, the
account menu, route
selection including cookbook detail, browser Back, persistent search state,
recipe creation, keyboard access, reflow, and accessibility. Desktop and compact
layouts must expose only one navigation region. Visual checks include German
labels at 320px and the desktop header at 1024px. These checks verify behavior
and layout, not user preference; a subsequent task-based user study should assess
finding a cookbook, planning a meal, shopping, and returning to a filtered recipe
list without instructions about where navigation is located.

Validation completed: type checking reported no errors or warnings; scoped lint
passed; 72 unit checks passed. The combined library/responsive browser run passed
36 tests (26 duplicate viewport cases intentionally skipped). After the final
320px label adjustment, the library suite passed all 10 applicable tests, including
single-line English/German navigation labels across light and dark modes (four
duplicate matrix cases skipped). Manual inspection confirmed the 320px labels and
1024px desktop header. Browser tests use controlled fixture data.

## A fifth destination for the food diary: measured, 22 October 2026

The food diary (`docs/diary-design.md` §7, bead `culina-v2-b3hl.19`) asked for
a fifth tab, **Tagebuch** / **Diary**, shown only to a person who switched the
diary on. It was decided on condition that the five-tab bar holds whole labels
at 320 px.

**Method.** A harness copied the bottom dock's CSS: `Navigation.svelte`'s
`.bottom` rules, including the `width < 25rem` block, and `BottomBar.svelte`'s
12 px margin. It used the real token values and the font stack (`system-ui`,
which is SF Pro on macOS and iOS). It ran in Chromium with emulated viewports.
Each label's rendered width was measured against the room inside its tab, at
weight 500 and at weight 600 (the active tab). Icons were 24 px placeholders.

**Results, bottom dock, German and English:**

| Viewport | Tabs | Room per label | Widest label (SF Pro) | Result |
| --- | --- | --- | --- | --- |
| 320 | 4 (today) | 67 px | Kochbücher 64.3 / 65.7 active | fits, 1.3 px spare |
| 320 | 5 | 52 px | Kochbücher 64.3 / 65.7; Cookbooks 59.9 / 61.2; Tagebuch 52.3 | **fails**: Kochbücher and Cookbooks overflow into their neighbours |
| 360 | 5 | 60 px | Kochbücher 65.7 | **fails** in both languages |
| 375 | 5 | 63 px | Kochbücher 65.7 | **fails** in German |
| 390 | 5 | 66 px | Kochbücher 65.7 | fits, 0.3 px spare |
| 414 | 5 | 64 px | Kochbücher 76.1, Cookbooks 70.9 (13 px labels from 25rem) | **fails** |
| 430 | 5 | 67 px | Kochbücher 76.1 | **fails** |

The five-tab bar fails on almost every phone width, not only at 320 px. It
holds only between about 386 and 399 px. Above 25rem the labels go back to
13 px and need more than the extra width gives. On Android (Roboto, about 54 px
for "Kochbücher" at 11 px) it would still fail at 320 px. Narrower margins
would win less than 2 px per tab.

At 200 % root text size even today's four-tab bar overflows at 320 px (every
label is 73–131 px in about 61 px). Text zoom therefore does not separate the
options. It is a limitation of today's bar, recorded here for whoever takes
on large text.

**Fallback measured: Rezepte and Kochbücher as one library tab.**
- Bibliothek, Woche, Einkauf and Tagebuch (Library, Week, Shopping, Diary) fit
  at 320 px: Bibliothek is 55 px in 67 px.
- They also fit at 430 px: 87 px of room.
- Without the diary the bar would have three tabs.

**Consequence.** Following this document's growth policy, labels are not
shrunk and no More tab is added. The conditional fifth tab cannot ship as
specified. The fallback decided with the diary was to merge Rezepte and
Kochbücher into one library destination. Jakob rejected it on 22 October 2026,
because it changes navigation for everyone (bead `culina-v2-b3hl.20`, closed).

Where the diary lives is now part of the app-wide structure decision in
`app-structure-design.md` §3 (bead `culina-v2-eawy.1`):

- **K:** four tabs, with personal tools in the header.
- **W:** Woche as a plan-and-list hub.
- **F:** a full-width bar that fits five tabs from about 345 px.

K and W were prototyped. **Jakob chose W on 24 October 2026**:
- The bar is Rezepte, Kochbücher, Woche and Tagebuch, for everyone.
- Woche holds the plan and the list: Plan | Liste on phones, the list beside
  the week from 80rem.
- Four tabs, measured whole at 320 px in German and English
  (`tests/e2e/woche.spec.ts`).

The design continues in `woche-design.md`.

Not measured: the desktop header at 1024 px with five destinations. That is
moot unless five ever return.
