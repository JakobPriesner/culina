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
| Four core tabs, profile in the header | More space per core destination | Splits app navigation across locations; account is less apparent | Viable, but not selected for this request |
| One five-destination navigation | Every current area is visible and one tap away | Five is already the intended upper limit; requires short labels on phones | Select |
| Unified desktop sidebar and mobile tabs | Roomier desktop navigation | Changes placement between sizes and takes content width | Keep as a future option if primary areas genuinely grow |

## Selected structure

**Recipes · Cookbooks · Week · Shopping · Settings**

German: **Rezepte · Kochbücher · Woche · Einkauf · Settings**.

All five are normal links. The same order appears in the desktop top navigation
and the compact bottom navigation. Each destination keeps its selected state on
its child routes. There is no Recipe book parent tab, collection launcher,
secondary sidebar, or collection-navigation dialog. Recipe creation stays in the
header and does not consume a destination slot. Existing cookbook creation and
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
- household, preferences, and account controls: Me.

These are placement examples, not newly implemented features. A sixth genuinely
independent frequent workflow requires a new information-architecture decision
based on observed use. Do not silently add a More tab or shrink every label. The
cost of the selected design is a denser bottom bar; the benefit is direct access
and no changing navigation model between sections.

## Evaluation

Automated verification covers direct navigation to all five destinations, route
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
