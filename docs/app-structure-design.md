# Culina — one app, one system (structure research)

| | |
| --- | --- |
| Status | Research and proposal, 22 October 2026. §9 lists the decisions. **24 October 2026: structure W chosen** (Woche holds the plan and the list); its design continues in `woche-design.md`. The other decisions in §9 are still open. |
| Written | 22 October 2026 |
| Decides | How Culina is structured so that recipes, the week, shopping, a personal food diary and product data (Open Food Facts) behave as one system on phone and desktop |
| Beads | `culina-v2-eawy` (epic) and its children `.1`–`.10`. The diary is `culina-v2-b3hl`; the navigation numbers are in `navigation-research.md`. |

**Why this document exists.** The food diary (`diary-design.md`) needed a place
in the app, and none fits cleanly:

- A fifth tab does not fit. "Kochbücher" overflows the floating dock at 320 to
  430 px (`navigation-research.md`, 22 October).
- Merging Rezepte and Kochbücher was rejected, because it changes navigation
  for everyone.
- Merging Woche and Einkauf is Jakob's instinct, but he is unsure.

Underneath the placement question is a bigger one: how a person moves through
Culina, and how fast they can add, plan, create and log from wherever they
are. Open Food Facts adds a third: one food catalogue for recipes, shopping and
the diary.

The goal, in Jakob's words: keep the smoothness that makes Culina pleasant
today, improve it, and stay **one app, one system**, not features bolted
together.

Labels, as in the other design docs:

- **Fact**: read from this repository or a primary source, cited.
- **Reasoning**: an argument from those.
- **Assumption**: not verified.
- **Decision needed**: Jakob's call, with a recommendation.

The research came from five passes, each written up in its own report: design
principles, user feedback, a competitor benchmark, Open Food Facts, and a
code-level audit of today's flows. Claims that were checked against their
source say so. The rest are marked as recalled or as inference.

---

## 1. What Culina is today (audit, from the source)

**Fact.** The audit counted taps from the code, not from a running app.

### 1.1 Structure

- **Four peer destinations**, the same order on desktop (header) and phone
  (floating dock): Rezepte `/`, Kochbücher, Woche `/plan`, Einkauf
  (`lib/app/navigation.ts`).
- **Header tools:** search (⌘K, Ctrl-K, `/` on every page) and the account
  menu (settings, ⌘,).
- **The header "+"** (`NewRecipeLink`) appears only on the library.
- **ShellDock** sits above the dock. It already holds the persistent "now"
  state: import progress, and the *NowCookingBar* to resume cooking.

### 1.2 What is already good, and must be kept

- **Optimistic writes with rollback:**
  - shopping ticks and section moves (`shopping.svelte.ts`);
  - moving a meal in the week (`mealPlan.move`);
  - `lib/app/optimistic.ts`.
- **Toasts with undo instead of confirmations:** made-it, ticks and moves.
  Navigation actions ride in the toast ("Liste öffnen").
- **Loading that does not flicker:** a 150 ms delay and a 300 ms minimum.
  Skeletons mirror the layout, and stale data stays visible while it
  refreshes.
- **Motion with a reason** (120/200 ms dissolve, 280 ms sheet rise, a moving
  selection marker). Reduced motion is honoured. The layout never jumps: the
  dock reserves its space.
- **Context travels with you.** Servings from the plan reach the recipe and
  cook mode (`?yield=`), and a cooking session survives navigation.

### 1.3 Friction the audit found

| # | Finding | Where |
| --- | --- | --- |
| F1 | **No global add.** Adding a shopping item, planning, or creating a recipe means navigating first. The "+" means "new recipe" only on the library. Search can navigate but never act. | `NewRecipeLink`, `offersNewRecipe` |
| F2 | **Planning works two ways.** `PlanRecipeSheet` (from a recipe, home or cookbook) has day, slot, servings, leftovers and shop, then a toast. The week's own "+" (`PlanRecipePicker`) plans at once, with no servings, no toast and no undo. | `features/planning` |
| F3 | **A recipe reaches the list three ways.** `AddToListSheet` reviews ingredients at the planned servings. `ShoppingRecipePicker` adds in one tap at the recipe's own yield, with no review. Cookbook and week add in bulk. | `features/recipes/detail`, `features/shopping` |
| F4 | **Einkauf never links back to Woche.** Woche reaches the list through the shop bar, a toast and a badge. | `ShoppingMeals.svelte` |
| F5 | **Optimism is uneven.** Adding an item, adding a recipe, removing, clearing bought, planning and unplanning all wait for the server. Removing, unplanning and "clear bought" have no undo. | stores |
| F6 | **The shopping add field is not focused on arrival** (also flagged on 18 September). | `ShoppingAddLine` |
| F7 | **The Today card is narrow.** It appears on Rezepte and Woche only, and has no shopping prompt even though `isOnShoppingList` exists. | `TodayMeal.svelte` |
| F8 | **Search cannot find the plan,** bought items or cooking history. | `places.ts` |
| F9 | **The share target works only in the Android PWA.** iOS has no share path beyond clipboard help. | `shareTarget.ts` |

### 1.4 Taps today, phone

| Job | From its own tab | From an unrelated page |
| --- | --- | --- |
| Add "Milch" to the list | 2 + typing | 3 + typing |
| Plan a recipe for a day | — | 4–5 (open recipe, Plan, day, confirm) |
| Plan from the week | 3, but no servings and no undo | 3 |
| New recipe | 2 + title (library only) | 3 + title, or ⌘K |
| Recipe to list | 3 | 4–5 |
| Tick an item in the shop | 1 (optimistic, undo) | — |
| Made it | 1 (Today card, recipe, cook mode) | 2–3 |

**Reasoning.** Culina is smooth *inside* each feature. The seams show
*between* features: you go somewhere before you can act, and the same job
behaves differently depending on where you start. "One system" therefore
means fixing those seams before adding anything new.

---

## 2. Research

### 2.1 Designers and design organisations

Each line is the principle and what it means for Culina. Details and sources
are in the design-principles report. HIG and Material specifics are partly
recalled, because their pages could not be fetched.

| Voice | Principle | What it means for Culina |
| --- | --- | --- |
| **Jony Ive** | Simplicity is order, not just less clutter. Good design feels "inevitable". | Judge the structure by whether it follows the loop capture → plan → shop → cook → remember. The diary must sit at a natural point in that loop. Labels overflowing a fifth tab show a structure under strain; shrinking them is not the fix. |
| **Apple HIG / iOS 26** | Tabs navigate and never act. Keep about 3–5 tabs and never hide them. iOS 26 floats the tab bar, minimises it on scroll, gives search its own trailing slot, and adds a *bottom accessory* above the bar for persistent state such as Now Playing. | Culina's dock and ShellDock already are this pattern. Persistent "now" state (cooking, imports, maybe today's diary) belongs in the accessory, not in a tab. A "+" is an action, so it belongs in a toolbar or a floating control. |
| **Google, Material 3** | 3–5 destinations of equal importance. A rail on medium and expanded windows. The FAB is the screen's main action. Same destinations, different chrome per window size. | Culina's "same order everywhere" is Material's adaptive principle. A context-aware add is endorsed. A personal, opt-in feature is not a destination of equal importance. |
| **Dieter Rams** | Understandable, unobtrusive, honest, thorough, less but better. | An opt-in, private diary must *look* optional and private. Merge Woche and Einkauf only if the result explains itself better. "What we eat" and "what I buy in the shop" are two concepts. |
| **Don Norman** | Conceptual model, mapping, signifiers, feedback. | The model is **household** (recipes, week, list) versus **me** (diary, settings). Map the diary's entry to "me" and to the meal being eaten. Every log needs visible feedback and an undo. |
| **Luke Wroblewski** | Mobile first. "Obvious always wins": visible navigation raised engagement and hidden drawers lowered it (Polar, Zeebox vs Facebook, Redbooth; checked). | Do not hide a daily feature in a menu. A visible header button is acceptable; an overflow menu is not. |
| **Marcotte, Frost** | One responsive system of reusable components. | One plan sheet, one add-to-list sheet and one food picker, the same everywhere. That is the fix for F2 and F3. |
| **NN/g** | Visibility of status, consistency, recognition over recall, accelerators for experts. Hamburger menus are the least discoverable (checked). | Keyboard shortcuts and a command palette are the expert layer on desktop. A separate system for power users is not. |
| **Things 3** | One "Magic Plus", Quick Entry from anywhere, capture first and organise later. | One add, always in the same place, preselected by context. |
| **Linear** (Karri Saarinen) | Opinionated software, speed as a feature, keyboard-first (sources checked). | Choose one structure. No "customise your tabs". |

**Where they disagree.**
- Apple and Material keep actions out of the tab bar, while trackers put a
  "+" in it (MyFitnessPal).
- Rams and Ive want fewer visible things, while Wroblewski and NN/g prefer
  visible over hidden, even at the cost of clutter.

**Their combined verdicts:**
- **Merge Woche and Einkauf:** against, as two different jobs. Link them
  strongly instead.
- **A header diary button:** a qualified yes.
- **A context-aware global add:** a strong yes, and the biggest gain available.
- **Diary as the home:** no. Culina's home is the library.

### 2.2 Users

From the user-feedback report. Reddit was largely not indexed, so the sources
are app stores, aggregators and some biased vendor blogs, marked as such.

- **The MyFitnessPal "Today" redesign (April 2026)** is the cautionary tale.
  - The anger was about regression in the *one daily job*: "4–5 clicks
    instead of 1–2". The full day was no longer visible at a glance, and
    copying entries got riskier.
  - Users asked for a "Classic View".
  - A redesign that adds taps to the most frequent job loses users, even when
    it looks cleaner.
- **Shopping lists** (Bring!, AnyList, Paprika):
  - Users praise one-tap entry (Bring!'s icon grid), live sync with a partner,
    and recipe-to-list with a way to untick what is already at home.
  - Bring!'s "I'm going shopping" ping was found faster than texting.
  - The list is often used **without** a plan (milk, household goods).
- **Plan with list.** No evidence was found that users want both on **one
  screen**. They praise the *flow* from plan to list (Plan to Eat derives the
  list from a date range; Samsung Food adds in one click).
- **Recipe import.** Silent failures and parse errors frustrate users most.
  Progress and failure must be visible. Culina's import progress already sits
  in the ShellDock.
- **Why people quit food diaries.**
  - Tedious entry: one food journal kept only 3 % of 190,000 downloads past
    the first week.
  - Redesigns that add taps.
  - Paywalled basics: MyFitnessPal moving barcode scanning behind a paywall is
    still the most-cited complaint, years later.
  - Unreliable data.
  - Guilt mechanics.

**Jobs ranked by frequency, with a tap budget.** The ranking is an inference
from the sources above.

| # | Job | Budget |
| --- | --- | --- |
| 1 | Log a repeat or recent food (diary users) | 1–2 |
| 2 | Add an item to the list | 1 + typing, no page change |
| 3 | Tick an item in the shop | 1, one-handed |
| 4 | See today (what to cook, diary total) | 0, on landing |
| 5 | Plan a recipe for a day | 2–3 |
| 6 | Recipe to list, unticking what is at home | 1–2 |
| 7 | Find a recipe | 1 to search, results while typing |
| 8 | Capture a recipe (link, share sheet) | 1 action, visible progress |
| 9 | Made it, and log my portion | 1 |
| 10 | Review the week | 1, not the default |

**Must be optimistic, with undo:** adding and ticking list items, diary
entries, copying yesterday, planning and moving meals, made-it and logging a
portion. Shared things must show the other person's changes live.

### 2.3 Competitors

From the benchmark. Help centres were the primary sources; anything unchecked
is marked.

| App | Phone structure | Plan vs list | Quick actions |
| --- | --- | --- | --- |
| **Cookidoo** (2025, German) | 5 tabs: Für Dich, Entdecken, Meine Rezepte, Meine Woche, Einkaufen (checked) | **separate tabs** | "Heute kochen", other days via a menu |
| **Plan to Eat** | Cook, Plan, Shop (checked) | **separate tabs**. The list is derived from a date range and merges by name and unit. | "+" per date, "Add New Item" in the list, share-sheet clipper |
| **Paprika 3** | Recipes, Groceries, Meals, Pantry… (checked) | **separate**. "Add to grocery list" acts on the current view, with a review. | Widget (today's meals), Siri add, Watch ticks, share extension |
| **AnyList** | Lists, Meal Plan (checked in part) | **separate**. "Add ingredients to list" picks dates and lets you untick. | Tap an ingredient to add it |
| **Mealime** | Meal plan first, list behind an icon (checked) | the list is born from the plan | — |
| **Mealie, Tandoor** (self-hosted) | sidebar | **separate**, with a cart icon at the plan | "+" on the list |
| **MyFitnessPal** | Today, Progress, Plan, "+", More (bar partly checked) | groceries **inside** Plan (Premium+, checked) | a blue "+" for every logging tool; More moved behind the profile icon |
| **MacroFactor** | Dashboard, Food Log, Strategy | — | a large "+" on every main page; long-press the app icon for search or barcode; widgets |
| **Yazio** | Diary, Recipes, Fasting, Analysis, Profile (checked) | none | widgets, Siri (marketing claims) |

**Patterns.**
1. Kitchen apps keep plan and list as separate destinations, joined by an
   "add to list" action at the plan.
2. Trackers put one "+" within thumb reach.
3. Capture is separate from organising: share sheets and quick entry come
   first.
4. **Nobody connects cooking to the diary** in a kitchen app. Culina's
   *made it → Als Portion eintragen* would be a real differentiator.
5. Showing plan and list side by side on wide screens is documented by
   nobody. That is an opening, not a norm.

**Why Cookidoo fits five German tabs.** It uses the platform's full-width tab
bar with roughly 10 pt labels. Culina's floating, inset dock gives each tab
about 38 px less in total and uses 11 px labels. This matters for option F in
§3.

### 2.4 Open Food Facts

See §6. In short: crowd-sourced, about 4 million products worldwide, German
coverage only partly measured. The public API allows **15 product reads and 10
searches per minute per IP** (checked) and asks for bulk downloads above a few
hundred products. Licensing is ODbL with share-alike, plus CC BY-SA for
images.

---

## 3. The structural options

Every option keeps what §1.2 lists. They differ in how the diary is reached
and whether Woche and Einkauf merge.

### K — Kitchen four, with personal tools in the header *(recommended)*

```
phone, diary on                              desktop
┌─────────────────────────────────┐          ┌───────────────────────────────────────────────┐
│ (•)            [+] [📖] [🔍] [👤] │          │ culina.  Rezepte Kochbücher Woche Einkauf   [+][📖][🔍][👤] │
│                                 │          └───────────────────────────────────────────────┘
│  … page …                       │
│ ┌ Heute: Linsencurry  [Kochen] ✓ ┐│  ← Today card (home, Woche): cook, made it,
│ │ mind. 1.240 kcal · 64 g Eiweiß ││    diary line when the diary is on
│ └────────────────────────────────┘│
│ [ Weiter kochen · Schritt 4 ]    │  ← ShellDock (the "accessory"), as today
│ ( Rezepte Kochbücher Woche Einkauf )│
└─────────────────────────────────┘
```

- **The bar stays as it is** for everyone.
- **The header "+" becomes the global add** on every page (§4).
- **Diary users get a Tagebuch button** in the header tools. On phones up to
  about 440 px the wordmark then hides behind the logo, as it already does at
  320 px.
- **The Today card** gains the diary line and a shopping prompt.
- **On wide screens**, Woche shows the shopping list as a side pane (§5).
- **Measured:** the header fits at 320 px (about 268 of 288 px).

**For it:**
- Non-users pay nothing.
- No new navigation rules.
- It follows Apple (personal tools in the toolbar), MyFitnessPal (More behind
  the profile icon) and Norman (personal next to "me").

**Against it:**
- The diary is an icon, not a labelled tab: weaker for a habit used several
  times a day.
- On a phone recipe page the header is hidden. The Today card and the
  *made it* toast cover that case.

### W — "Woche" as the kitchen hub (plan and list in one tab)

```
phone                                         desktop / tablet ≥ 64rem
┌─────────────────────────────────┐          ┌───────────────────────────────────────────────┐
│ Woche        [ Plan | Liste ]   │          │ Woche                                          │
│                                 │          │ ┌ Mo … So ─────────────────┐ ┌ Einkauf ───────┐ │
│  segment remembers the last     │          │ │ week grid                 │ │ list, live     │ │
│  view; the "Liste" badge shows  │          │ │                           │ │                │ │
│  open items                     │          │ └───────────────────────────┘ └────────────────┘ │
│ ( Rezepte Kochbücher Woche Tagebuch )│     └───────────────────────────────────────────────┘
└─────────────────────────────────┘
```

- **Woche holds both views.** On phones a segment switches between Plan and
  Liste, and remembers the last one. On wide screens they sit side by side.
- **The freed slot** goes to a Tagebuch tab for diary users. Four tabs fit at
  320 px. Everyone else sees three tabs.

**For it:**
- Jakob's instinct.
- The diary becomes a real, labelled destination.
- The relationship between plan and list is visible.
- It folds F3 and F4 away.

**Against it:**
- The list, a top-three job used in the shop with one hand, goes behind a
  segment for **every** household. Remembering the last segment lowers that
  cost but does not remove it.
- Every competitor that was checked except MyFitnessPal keeps them apart.
- The designers' verdict is against: two jobs, two contexts.
- Users were not found to ask for it. This is exactly the MyFitnessPal
  failure mode: a frequent job gains a tap for the sake of a structure.
- Households that use the list without a plan look for "Einkauf" and find
  "Woche".

### F — A full-width tab bar, five tabs (design-system change)

- **The idea.** Replace the floating inset dock with a full-width bar, which
  is how Cookidoo and Yazio fit five tabs. At 11 px labels "Kochbücher" needs
  65.7 px.
  - A full-width bar gives 68 px at 360 px and 71 px at 375 px, so five tabs
    fit.
  - At 320 px it gives 60 px, so it still fails. That would need a hyphenated
    "Koch-/bücher" at that width only.
  - From 400 px the labels must stay at 11 px.
  - These numbers are calculated from §2 in `navigation-research.md`, not
    rendered.

**For it:** the diary gets a real tab, and nothing is merged.

**Against it:**
- It changes the dock's look for everyone. The floating pill matches iOS 26
  and the motion work (the selection marker, the slide on scroll).
- 320 px still needs an exception.
- A conditional fifth tab changes the bar per person.

### T — "Heute" as the home *(not recommended)*

- **The idea.** The diary or a Today view becomes the first tab, as in
  MyFitnessPal and Yazio.
- **Against it:**
  - It displaces the library, Culina's core.
  - It is empty for non-users.
  - It is the change most likely to repeat MyFitnessPal's backlash.

### Comparison

| | K | W | F | T |
| --- | --- | --- | --- | --- |
| Cost to households without a diary | none | list one tap deeper | the dock's look changes | home changes |
| Diary reachability | header icon, Today line | labelled tab | labelled tab | home |
| Respects the measured 320 px rule | yes | yes | only with an exception | yes |
| Matches competitors' kitchen pattern | yes | no (only MyFitnessPal) | yes (Cookidoo) | no |
| Designers' verdict | for | against | mixed | against |
| Reversibility | high | low (muscle memory) | medium | low |

**Recommendation: K**, together with §4 and §5. They remove most of the
friction that makes a separate diary tab feel necessary.

**Decision needed.** Jakob's instinct for W is reasonable, and the evidence
against it is indirect (no user study). Culina's smoothness is felt, not read,
so the proposal is to **decide by feel**: build K and W as two prototypes
behind a development switch, use each on a phone for a few days, then choose
(bead `.2`).

---

## 4. One add, everywhere

**The single biggest gain** for "quickly add an item, plan something, create a
recipe", whatever is decided in §3. It answers F1, F2, F3 and F6.

### 4.1 The "+" becomes Culina's one add

- **Same place on every page, phone and desktop:** the header tools, where the
  library's "+" already sits.
- **Desktop shortcut `N`** (when not typing). It joins ⌘K and `/`.
- **It opens one sheet, "Hinzufügen".** On phones it is a bottom sheet;
  from 48rem it is a dialog, as every Culina sheet already is. It has a row of
  intents and the matching field right away:

```
┌ Hinzufügen ─────────────────────────────┐
│ ( Einkauf | Planen | Rezept | Essen* )   │  ← SegmentedControl; *only with the diary on
│ ┌─────────────────────────────────────┐ │
│ │ 2 l Milch                         ⏎ │ │  ← focused, keyboard already up
│ └─────────────────────────────────────┘ │
│  Milch · Hafermilch · Mehl            │  ← suggestions (the existing ComboField)
└─────────────────────────────────────────┘
```

**The intent is preselected by context:**
- Einkauf: an item.
- Woche: planning, with the day you were looking at.
- Rezepte: a recipe.
- Tagebuch: food, for the current meal.

Elsewhere it opens on the intent you used last.

**Each intent reuses the one component that already does the job:**

| Intent | Reuses | After it |
| --- | --- | --- |
| **Einkauf** | `ShoppingAddLine`: amount and unit parsing, suggestions, and stays open for the next item | Optimistic. A toast offers Rückgängig and "Liste öffnen". |
| **Planen** | Recipe search, then **the one `PlanRecipeSheet`** (day, slot, servings, leftovers, shop) | Toast with "Woche zeigen" and undo |
| **Rezept** | The new-recipe title field, plus link, paste and photo (`OtherWaysIn`) | Goes to the editor, as today |
| **Essen** | The diary's food picker (`diary-design.md` §6) | Optimistic, with undo |

**Tap budget from any page:**

| Job | Taps |
| --- | --- |
| Add "Milch" | 1 + typing |
| Plan a known recipe | about 3 (+, type, choose, confirm) |
| New recipe | 1 + title |
| Log a recent food | 2 |

### 4.2 Search that can act

⌘K and `/` already search recipes and *places*. The proposal adds *actions*
when the text reads like one:
- "Milch" offers **"Milch auf die Liste"**.
- A recipe result offers **"Planen…"** and **"Auf die Liste"**.

This is the Linear-style expert layer: the same intents as §4.1, not a second
system. The search also learns the plan (F8): "Dienstag" finds Tuesday's
meals.

### 4.3 One job, one component

- **Planning:** the week's "+" opens the same `PlanRecipeSheet`, with the day
  filled in. It gains servings, a toast and an undo. `PlanRecipePicker` stays
  as the recipe step inside it (F2).
- **Recipe to list:** one `AddToListSheet` everywhere. From Einkauf's "Rezept
  hinzufügen" it opens after the recipe is chosen, with every ingredient
  ticked, so it is still two taps. The servings come from the plan if the
  meal is planned (F3).
- **Optimistic with undo everywhere** §2.2 asks for:
  - adding an item;
  - removing an item;
  - clear bought (one undo for all);
  - planning;
  - unplanning (F5).
- **Einkauf autofocuses its add field** on desktop. On phones it does not,
  because that would open the keyboard over the list in the shop; there the
  "+" covers the job (F6).

### 4.4 Outside the browser (native shells, later)

- **App-icon shortcuts:** Artikel hinzufügen, Rezept planen, Essen eintragen
  (MacroFactor's pattern).
- **A widget:** today's meal, and the list count.
- **An iOS share extension,** to close F9.
- **Siri / Shortcuts:** "Milch auf die Einkaufsliste".

All of them open the same Hinzufügen intents.

---

## 5. Woche and Einkauf: one relationship, two places

This takes Jakob's instinct, that they belong together, without making the
list harder to reach in the shop.

- **Phone: both tabs stay.**
  - Einkauf gains a strip at the top: "Diese Woche: 5 Mahlzeiten, 2 noch
    nicht auf der Liste → hinzufügen · Woche öffnen" (F4).
  - Woche keeps its shop bar.
  - The "auf der Liste" badge on a planned meal becomes a link.
- **Wide screens (≥ 64rem): Woche shows the list as a live side pane.**
  - Planning at the desk shows what the plan does to the list as it happens.
  - Einkauf stays its own page too.
  - No competitor that was checked does this. It is "wide screens get more,
    never different".
- **The Today card becomes the bridge** (F7). When the next meal's
  ingredients are not on the list, it says so, with "auf die Liste".
- **"Ich gehe einkaufen"** (from Bring!) is a later idea: one tap tells the
  household. It would need push to other members, and it is out of scope here.

---

## 6. Open Food Facts: one food catalogue

### 6.1 What it adds, and what it is not

**What it adds.** Products, not just foods: "this exact milk", with brand,
pack size and the label's nutrition per 100 g. It suits scanning, the diary,
household package foods and, later, the shopping list ("which milk do we
buy").

**What it is not.** It is not a federal table. OFF is crowd-sourced, and its
quality varies by product:
- **Germany** (secondary): about 250,000 products. About 36 % have a
  Nutri-Score, and 84 % of the 50,000 most-scanned are complete.
- **Known errors:** kJ and kcal mixed up, per-serving values typed as per
  100 g, typos.

So BLS stays the source for generic foods and recipes. OFF is the source for
**packaged products**, and every figure says which source it came from.

### 6.2 One catalogue, three sources

```
                 ┌──────────── Lebensmittel-Auswahl (one picker) ────────────┐
                 │  Haushalt         Produkte              Lebensmittel       │
                 │  (your own,       (Open Food Facts,     (BLS 4.0,          │
                 │   package foods)   barcodes, brands)     generic, cited)   │
                 └───────────────────────────────────────────────────────────┘
used by:  diary · recipe editor (nutrition corrections) · scan sheet · shopping (later)
```

- **One picker component** for every place that chooses a food. Today the
  recipe correction sheets and household foods each pick their own way.
- **Every row carries a source chip:**
  - "Bundeslebensmittelschlüssel"
  - "Open Food Facts · von Nutzern eingetragen", with a link to the product
    page
  - "Von dir eingetragen"
- **A "Datenquellen" page** lists the licences: BLS under CC BY 4.0, OFF under
  ODbL with OFF's attribution.

### 6.3 Architecture, in two stages

**Stage 1: lookup by barcode, on demand.**
- **Two switches:**
  - **Admin** turns OFF on (`Products:OpenFoodFacts`), with a contact address
    for the User-Agent.
  - **Each person** opts in: "Strichcodes bei Open Food Facts nachschlagen".
    The barcode a person scans is that person's behaviour. Someone who has not
    opted in gets the existing "name it yourself" flow, without a nag.
  - The settings say plainly that the code and the server's IP address leave
    the server.
- **Server-side only.** The API sends `Culina/<version> (<contact>)` and a
  `fields=` filter, and a limiter keeps it under 15 requests per minute. The
  browser keeps `connect-src 'self'`.
- **Cache.** Hits and misses go into `product_lookups`, a **separate table
  labelled ODbL** (hits for 30 days, misses for 7, as `barcode-scanning-design.md`
  already plans). It holds only codes somebody scanned, which keeps it far
  from a "substantial" extraction. **OFF rows never go into BLS tables.**
- **Confirming a product.** The scan sheet shows the product with name, brand,
  pack size and nutrition per 100 g, plus a **plausibility check**:
  - energy should match 4/4/9 × the macros within about 20 %;
  - the units must be sane;
  - missing macros are flagged, not zeroed.

  **"Übernehmen"** makes it a **household package food** with `source = off`
  and its GTIN. The person can correct the numbers, and the row then says
  `off+edited`.
- **The diary** snapshots the values and their provenance, as it does for
  everything. A handful of values from one product, in one person's record,
  is very likely not a "substantial" extraction (reasoning, not legal advice).
- **Search by name** stays local in stage 1: the household's own foods plus
  BLS. A button, "Bei Open Food Facts suchen", may send **one** query on
  request. Search-as-you-type against OFF is ruled out by its rate limit and
  its documentation.

**Stage 2: a German subset, if name search is wanted.**
- An opt-in **import** of OFF's nightly export (JSONL or Parquet), filtered to
  `countries_tags = en:germany` and trimmed to about 15 fields, into its own
  table `off_products`.
- **Benefits:** local search, offline use, and no barcode leaving the server
  per scan.
- **Costs:**
  - roughly 100–300 MB (an assumption; measure it first);
  - an importer job;
  - the table is an ODbL *Derivative Database*: the archive export must flag
    or leave out those rows, and the docs must say so.
- **Updates:** OFF publishes daily deltas for 14 days, which cannot express
  deletions, so a weekly full import is simpler.

**Later: contributing back.** A separate, per-action, opt-in "Mit Open Food
Facts teilen" for a product a person typed from its label. It is public and
cannot be revoked, and the person sees that before sending. It needs an OFF
account (the person's own is cleaner) and sends only label facts and their
own photos.

### 6.4 Before building

1. Email **reuse@openfoodfacts.org** with this design. Ask specifically about
   the lookup cache and the diary snapshot (bead `culina-v2-b3hl.18`).
2. Download the export once. Measure German completeness for energy, fat,
   carbohydrate and protein, and the size of a trimmed subset.
3. **Decision needed:** whether OFF is on or off by default in a new
   instance's setup.
   - Recommendation: offer it in setup with one honest sentence, and leave the
     choice to the admin.
   - Culina's "no outbound call unless someone switched it on" stance holds
     either way. The README sentence changes from "zero outbound calls" to
     that.

---

## 7. Principles, as testable rules

These are distilled from §2. Each can be checked in review or a test.

1. **Same destinations, same order, everywhere.** Only the chrome (header,
   dock, panes) changes with the width.
2. **Tabs navigate, the "+" adds.** No tab triggers an action, and no action
   needs a page change first.
3. **Every frequent job takes at most 2 taps from anywhere**, plus typing:
   add an item, plan a known recipe (3 is acceptable), new recipe, log a
   recent food, made it.
4. **One job, one component.** One plan sheet, one add-to-list sheet, one food
   picker, one Hinzufügen sheet.
5. **Frequent writes are optimistic, with undo.** Nothing frequent waits for
   the server or asks "are you sure?".
6. **Household and personal look different.** Personal opt-in features cost
   non-users zero pixels.
7. **Labels stay whole at 320 px in German, and never shrink.** Any change to
   the bar is measured.
8. **Wide screens get more, never different:** extra panes, never extra
   concepts.
9. **Every figure shows its source**, and no data source is used without
   someone switching it on.
10. **Feedback is quiet:** no red, no nagging, and the ShellDock for "now"
    state.
11. **A redesign must not add a tap to any of the top 5 jobs in §2.2.** This
    is the MyFitnessPal lesson.

---

## 8. Phasing

| Phase | What | Beads |
| --- | --- | --- |
| **0, no regret** | One plan sheet. One add-to-list sheet. Optimistic adds and removes with undo. A link from Einkauf to Woche. Autofocus on desktop. Today card shopping prompt. The stale comment in `plan/+page.svelte`. | `.3`–`.6` |
| **1** | The global Hinzufügen sheet and `N`. Search that acts. | `.7`, `.8` |
| **2** | A K vs W prototype, used on a phone, then the decision. | `.2` |
| **3** | Diary MVP in the chosen place (`culina-v2-b3hl`). | — |
| **4** | OFF stage 1: barcode lookup with two switches, provenance chips, the "Datenquellen" page. Needs the product memory and `ScanSheet` from local `main` on `origin/main`. | `.9` |
| **5** | Woche's list pane on wide screens. OFF stage 2, if wanted. Native shortcuts, widget, iOS share extension. | `.10`, later beads |

---

## 9. Decisions needed

1. **Structure:** K (recommended), W, or F. Proposed: prototype K and W, then
   decide by feel (`.2`).
2. **Global add:** adopt the one "+" with the Hinzufügen sheet, and `N` on
   desktop (§4). Recommended, whatever 1 becomes.
3. **Woche and Einkauf:** keep two places and join them (§5), unless 1
   becomes W.
4. **Open Food Facts:**
   - Stage 1 with two switches. Recommended.
   - Default in setup: offered, with the choice left to the admin.
   - Stage 2: only after measuring.
5. **README:** "zero outbound calls" becomes "no outbound call unless an admin
   and a person switched it on".
