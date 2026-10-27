# Culina's words, and the food log redesigned

| | |
| --- | --- |
| Status | Accepted 24 October 2026: Jakob took all four recommendations in §5 (`culina-v2-6dt0.1`). |
| Decides | (1) The words Culina uses for its places and actions. (2) How the personal food log (today "Tagebuch") looks and works, so it reads as designed rather than assembled. |
| Builds on | `diary-design.md` (what the log does, decided), `woche-design.md` (one place, one system), `app-structure-design.md` §7 (rules) |
| Beads | See §6 |

---

## 1. Words first

### 1.1 What is wrong today

The bar reads *Rezepte · Kochbücher · Woche · Tagebuch*, and Woche's two views
read *Plan | Liste*. Jakob's objection is the right one: a place should be
named for what it is.

- **"Liste"** is the shape of the thing, not the thing. Everything is a list.
- **"Tagebuch"** is a metaphor, and the wrong one. In German it means a
  private book of feelings: Apple's *Journal* is introduced in German press as
  "Apples Tagebuch-App". The log holds what was eaten, counted against one's
  own targets. It is a record, not a diary.
- **The pane** says "Einkauf" while its own page says "Einkaufsliste". That
  is one thing with two names, and it comes from the Woche build.

### 1.2 What the sources say

- **Apple HIG, Writing** (read 24 October 2026):
  - "Create a list of common terms, and reference that list to keep your
    language consistent."
  - Button labels: "it's almost always best to use a verb", and do not be
    "too cute or clever".
  - Possessive pronouns are "often unnecessary": "Favorites" over "Your
    Favorites".
- **Apple HIG, Tab views:** use nouns or short noun phrases for tab labels,
  and let a label describe the contents of its pane.
- **What platforms call the category:** Apple Health ("Ernährung"), Samsung
  Health ("Ernährungsprotokoll", dashboard tile "Essen") and Fitbit
  ("Nutrition" inside Health). The diet apps say Tagebuch or Diary: Yazio,
  MyFitnessPal, FDDB, Lifesum.
- **What German meal planners call their two views:** Cookidoo has "Meine
  Woche" and "Einkaufsliste", KptnCook "Wochenplaner" and "Einkaufsliste". No
  app checked uses a bare "Liste".

### 1.3 The glossary (proposal)

One name per thing, everywhere: tab, heading, segment, settings, toast and
screen-reader label.

| Thing | Deutsch | English | Instead of |
| --- | --- | --- | --- |
| The library of recipes | Rezepte | Recipes | — |
| Shelves of recipes | Kochbücher | Cookbooks | — |
| The week, as a place | Woche | Week | — |
| What the week cooks | **Essensplan** | **Meal plan** | "Plan" |
| What the household buys | **Einkaufsliste** | **Shopping list** | "Liste", "Einkauf" |
| What I ate, against my targets | **Ernährung** | **Nutrition** | "Tagebuch", "Ernährungstagebuch" |
| One thing eaten | Eintrag | Entry | — |
| My own number for a day | Richtwert | Target | — |
| Putting a meal in the plan | Einplanen | Plan | — |
| Putting things on the list | Auf die Einkaufsliste | Add to shopping list | "Auf die Liste" |
| Writing down what was eaten | Eintragen | Log | "Hinzufügen" |

- **Why "Ernährung":**
  - It is the platform word (Apple and Samsung) for exactly this category.
  - It is a noun that describes the contents.
  - It sits beside the other tabs as a peer: *Rezepte · Kochbücher · Woche ·
    Ernährung*.
  - It covers kcal, protein and meals without implying a diet.
  - The risk is a whiff of "advice page". The page itself answers that: it
    opens on today and on what was eaten, not on articles.
- **The two alternatives,** if "Ernährung" does not feel right:
  - **"Essen":** plainest and shortest, but ambiguous next to Woche, where
    meals are planned.
  - **"Protokoll":** precise, but it reads like the office.
- **Why "Essensplan":** "Plan" alone has the same problem as "Liste". A
  segment says what it holds. *Essensplan | Einkaufsliste* fits two segments
  at 320 px in German (10 and 13 characters; to be measured in the build, with
  "Liste · 11" becoming "Einkaufsliste · 11").
- **The page titles** follow the place:
  - The food log's large title is the day ("Heute", or the weekday) under the
    tab "Ernährung". This is how Apple Health titles a category's day.
  - The settings category becomes "Ernährung".
- **The code keeps its words** (`diary`, `shopping`). Only what people read
  changes.

---

## 2. Why the log looks "self-made" (a critique)

The screenshots were taken 24 October 2026 at 390×844 and 1280×900, with a
realistic day of six entries and targets of 2,200 kcal and 110 g.

| # | What a top designer sees | Principle it breaks |
| --- | --- | --- |
| L1 | **No date.** "Heute" is a small eyebrow, and the ‹ › arrows float in a row of their own, apart from any day. Elsewhere, which day is it? | Nielsen #1 (system status), Norman (conceptual model, mapping) |
| L2 | **The hero number is hedged.** "mind. 1.666 kcal gegessen" starts with a qualifier, and "ca." or "mind." sits on nearly every figure on the page. Honest, but it reads as doubt everywhere. | Rams #10 (as little design as possible), Maeda *The One* |
| L3 | **Three sentences under the number** ("unter deinem Richtwert…", "davon 650 kcal von dir eingetragen", the band) before any meal appears. | HIG Charts: "summarize the main message", data most prominent |
| L4 | **The band** is hatched, with a dashed tail and a tick. It looks like a loading bar, and its encoding needs a legend nobody has. | Tufte (data-ink), HIG Charts (familiar marks) |
| L5 | **Macros are a ruled table** of three rows, with no relation drawn between amount and target. | Maeda *Organize*, Tufte |
| L6 | **Two typefaces on one line of meaning.** Entry names are in the editorial serif, section labels in bold sans, numbers in regular sans. At list size the serif looks like a mistake, not a voice. | Vignelli/Müller-Brockmann (one discipline), Rams #8 (thorough) |
| L7 | **"+ Hinzufügen" four times,** the same weight as the content. Empty meals and full ones look alike. | Norman (signifiers, not noise), Hara (emptiness as invitation) |
| L8 | **"An 1 von 7 Tagen eingetragen"** is a count of days, read as a score. | Case (calm technology), the guilt research in `diary-design.md` §14 |
| L9 | **Desktop is one narrow column** with a wide empty margin. The summary scrolls away with the meals. | "Wide screens get more, never different" (`app-structure-design.md` §7) |
| L10 | **"1 Stück · ca. 0 g"** for a piece-counted food without grams. Real food entries carry grams, so this is a fixture artefact, but `amountText` should not print a zero it does not know. | Rams #6 (honest) |

What is already right, and stays:
- the four meals;
- a figure's precision kept with it;
- one's own figures marked as such;
- the plan's meals offered, but not counted;
- no red, no streak;
- the source credit for the food data.

---

## 3. Principles for this screen

These are distilled from the research of 24 October 2026. Each one can be
checked.

1. **One hero** (Rams #10, Maeda *The One*). Squint at the screen and one
   number dominates: what was eaten today, or what is left. Everything else is
   visibly secondary.
2. **Say the day, always** (Nielsen #1, Norman). The large title is the day,
   with its date beneath. Moving between days happens where the days are shown.
3. **Summarize in words, draw in one mark** (HIG Charts).
   - One sentence carries the main message.
   - One thin track, in one hue, is the only graphic.
   - The sentence is also the accessible description.
4. **Precision once, not on every figure** (Rams #6 and #10). Each figure
   keeps its precision, but as a light mark beside it (`≈`, `≥`), read aloud as
   words. The summary says it once in words: "teils geschätzt".
5. **One discipline** (Vignelli, Müller-Brockmann, Rams #8).
   - One UI typeface (the sans). The editorial serif is reserved for the large
     title, as everywhere in Culina.
   - At most four text sizes.
   - Every row has one left edge and one right-aligned column of tabular
     figures.
6. **Lists the way the platform draws them** (HIG layout, Apple Health and
   Settings). Meals are inset-grouped lists: a section header with its
   subtotal, rows, then the section's quiet add row. Empty meals are one row
   high.
7. **Planned meals are scheduled items** (Apple Medications).
   - A planned meal shows in its meal, with an empty circle. One tap logs it.
   - "Übersprungen" is a neutral answer, not an error.
   - It counts only once confirmed.
8. **Recognition over recall** (Nielsen #6, HIG Entering data: "offer choices
   instead of requiring text entry").
   - The add sheet opens on the meal the clock suggests, with recent and
     frequent foods first.
   - Re-logging one of them is one tap.
9. **Calm** (Amber Case, MacroFactor's adherence-neutral target).
   - No red, no praise, no count of days.
   - Going over the target extends the same track past its mark.
   - Without a target the screen is complete, showing plain numbers only.
10. **Feedback where it happens** (Nielsen #1, `woche-design.md` §4).
    - A logged row arrives tinted, and the totals change in place.
    - Every write has *Rückgängig*.
    - Nothing asks "are you sure?".
11. **Wide screens get more, never different.** From 64rem the summary stays
    beside the meals.

---

## 4. The redesign

### 4.1 Phone

```
 ┌──────────────────────────────────────────┐
 │ Heute                          (large    │  editorial serif, the day
 │ Samstag, 24. Oktober            title)   │
 │                                          │
 │  Mo  Di  Mi  Do  Fr  Sa  So              │  week strip: a dot under days
 │  ·   ·   ·       ·  (24)                 │  with entries, today filled
 │                                          │
 │  1.666                     von 2.200 kcal │  hero: tabular, large; the
 │  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━│━━━━━━━  │  target as a quiet mark on
 │  534 kcal bis zum Richtwert · teils       │  one thin track
 │  geschätzt                                │
 │                                          │
 │  Eiweiß        Kohlenhydrate     Fett     │  three columns, values
 │  60 / 110 g    220 g             53 g     │  tabular; protein has its
 │  ━━━━━━──────                             │  own track (it has a target)
 │                                          │
 │ FRÜHSTÜCK                       423 kcal │  inset-grouped section
 │ ┌──────────────────────────────────────┐ │
 │ │ Haferflocken               222       │ │  sans names, one right
 │ │ 60 g                                 │ │  column of tabular kcal
 │ │ Banane                    ≈105       │ │  ≈ = estimated (read
 │ │ 1 Stück · 118 g                      │ │  aloud "etwa")
 │ │ Joghurt 3,5 %               96       │ │
 │ │ ＋ Eintragen                          │ │  quiet accent add row
 │ └──────────────────────────────────────┘ │
 │ MITTAGESSEN                     650 kcal │
 │ ┌──────────────────────────────────────┐ │
 │ │ Café-Lunch                 650  ✎    │ │  ✎ = own figures
 │ │ ＋ Eintragen                          │ │
 │ └──────────────────────────────────────┘ │
 │ ABENDESSEN                               │
 │ ┌──────────────────────────────────────┐ │
 │ │ ○ Linsen-Dal mit Spinat   geplant    │ │  Medications pattern:
 │ │   2 Portionen · Übersprungen         │ │  circle = log one tap
 │ │ ＋ Eintragen                          │ │
 │ └──────────────────────────────────────┘ │
 │ SNACKS                                   │
 │ ┌──────────────────────────────────────┐ │
 │ │ ＋ Eintragen                          │ │  empty meal: one row
 │ └──────────────────────────────────────┘ │
 │ Nährwerte: BLS 4.0 (CC BY 4.0)           │
 └──────────────────────────────────────────┘
```

- **The large title is the day.**
  - "Heute", "Gestern", or the weekday for other days, with the date beneath
    (`dayName` and `dayDate` already exist in `weekDates.ts`).
  - The week strip replaces ‹ ›. Tapping a day opens it. Swiping the strip
    moves by a week. Days with entries get a small neutral dot, never a
    colour or a mark for "over".
- **The hero:**
  - The number is set large in tabular sans, with the unit small.
  - The target is shown as "von 2.200 kcal" on the right, only when one is
    set.
  - Tapping the number switches between eaten and remaining. That replaces the
    hidden settings choice as the daily gesture; the setting stays as the
    default.
- **One track:**
  - Solid, in the accent, on the sunken surface. The target is a 2 px mark.
  - Over the target, the fill runs past the mark in the same hue.
  - Own figures and estimates are not hatched. That said once in the summary
    sentence is enough.
- **The sentence** is one line: what is left (or what is over, as a plain
  number) and "teils geschätzt" when it is. It is the track's `aria-label`
  too.
- **Macros:**
  - Three columns, each a label, a tabular value, and "/ target" only where a
    target exists.
  - Only protein, which can have a target, gets a track.
  - No ruled table.
- **Meals:**
  - Each meal is an inset-grouped list, on the surface used for cards, with
    hairline separators between rows.
  - The section header is small caps (the `SectionLabel` style) with its
    subtotal on the right.
  - The add row is the section's last row: accent text, "＋ Eintragen".
- **Precision marks:** `≈` (estimated) and `≥` (at least) sit before a
  figure, in the muted colour. Each is read aloud as "etwa" or "mindestens".
  ✎ marks one's own figures.
- **Planned meals:**
  - An empty circle on the left; one tap logs a portion at the planned
    servings, with *Rückgängig*.
  - A secondary "Übersprungen" hides the suggestion for that day, also with
    *Rückgängig*.
- **Removed:**
  - "An 1 von 7 Tagen eingetragen" (the week strip's dots say it without a
    score);
  - the three-sentence stack;
  - the hatching.

### 4.2 Desktop (from 64rem)

```
 Heute · Samstag, 24. Oktober            │ FRÜHSTÜCK                  423 kcal
 Mo Di Mi Do Fr [Sa] So                  │ ┌───────────────────────────────┐
                                          │ │ Haferflocken             222  │
 1.666           von 2.200 kcal           │ │ …                             │
 ━━━━━━━━━━━━━━━━━━━━━━━━━│━━━━━━         │ └───────────────────────────────┘
 534 kcal bis zum Richtwert               │ MITTAGESSEN                650 kcal
                                          │ …
 Eiweiß 60/110 g · KH 220 g · Fett 53 g   │
 (sticky)                                 │
```

- The summary column is sticky on the left, and the meals scroll on the
  right. The week strip and the hero never leave the screen.
- This is the same move as Woche's pane: wide screens get more, never
  different.

### 4.3 Adding

- **One way in,** with a meal already chosen:
  - Each section's "＋ Eintragen" opens the sheet for that meal.
  - A single floating "＋" on the phone opens it for the meal the clock
    suggests (`mealByClock`).
- **The sheet's first view** is *Zuletzt* and *Häufig*. Each row re-logs in
  one tap, with the same amount as last time. Searching starts with typing.
  This meets the top-5 job's budget of one to two taps
  (`app-structure-design.md` §2.2).
- **"Gestern wiederholen"** is offered per meal when yesterday's meal had
  entries. The MyFitnessPal backlash was about losing exactly this kind of
  shortcut.

### 4.4 Design system

- **A `List` with `ListSection` and `ListRow`** (inset-grouped): header with
  trailing value, rows with leading and trailing slots, an add row.
  - It is used here first.
  - It is also the shape that settings and the shopping list's sections could
    share later, which is one system rather than a page-local look.
- **A `Meter`:** one thin track, an optional target mark, and over-target in
  the same hue, with the summary sentence as its label. It replaces `Band`,
  which was built for this page and is what makes it look assembled.
- **A `WeekStrip`:** seven days, today marked, a dot per day with entries.
  The Woche week could use it later for its own navigation.
- **Numbers:** `font-variant-numeric: tabular-nums` on every figure column, a
  rule `design-system.md` should state for all numbers in lists.

---

## 5. Decisions for Jakob

1. **The tab name:**
   - **Ernährung / Nutrition** (recommended).
   - Essen / Food.
   - Protokoll / Log.
2. **Woche's segments:**
   - **Essensplan | Einkaufsliste** (recommended).
   - Plan | Einkaufsliste.
3. **Precision marks:** `≈` and `≥` beside figures, with words for screen
   readers (recommended), or keep "ca." and "mind." written out.
4. **Planned meals as one-tap rows with "Übersprungen"** (recommended), or keep
   today's dashed suggestions.

---

## 6. Beads

Filed as the epic `culina-v2-6dt0`. The decision bead `.1` holds §5. The
children, `.2` to `.9` in this order, wait on it (`.8` also waits on `.4` and
`.5`):

1. **Glossary and renames:**
   - tab, settings, page and screen-reader names;
   - Woche's segments, the pane heading and the seam's verb;
   - `docs/glossary.md`.
2. **The day as the title, with a week strip** in place of ‹ ›.
3. **Summary:**
   - one hero with the eaten/remaining tap;
   - one Meter;
   - one sentence;
   - the macros in three columns.
4. **Meals as inset-grouped lists:**
   - DS `List`;
   - one typeface;
   - a tabular right column;
   - precision marks;
   - quiet add rows;
   - one-row empty meals.
5. **Planned meals as scheduled items:** one tap to log, "Übersprungen",
   *Rückgängig*.
6. **The add sheet opens on recent and frequent foods**, with one-tap re-log
   and "Gestern wiederholen".
7. **Desktop:** the summary sticky beside the meals from 64rem.
8. **`amountText`:** never "ca. 0 g".

---

## 7. Built, and where it differs (24 October 2026)

All of `culina-v2-6dt0` is built. A second look at the result, and Jakob's
"the hover effects are not right", changed four things from §4:

- **No add row per meal.** Five "＋ Eintragen" rows (one at the top, one per
  meal) repeated exactly what L7 criticised. Each meal's heading carries a
  quiet "＋" beside its subtotal instead. An empty meal is its heading alone,
  with no white box holding nothing (Hara: emptiness as an invitation, not a
  gap).
- **No floating "＋" on phones.** It covered the column of figures. "＋
  Eintragen" sits beside the day's title instead, where Apple Health puts its
  add action, and opens for the meal the clock suggests.
- **Hover:**
  - A row's button spans the row edge to edge, so the whole row lights up and
    is the target. The separator is drawn as an image so it still starts at
    the text.
  - The big number, which turns over between eaten and left, has a hover
    state, a press state and a small up-down mark. A control that does not
    look like one is a missing signifier (Norman).
- **Loading:** until the day arrives, the summary and the meals show
  skeletons in their own shape, not "0 kcal".

Not done, and worth a look on a real phone:
- swiping the week strip;
- the planned meal's energy in its row;
- dark mode (built only from tokens, but not looked at).

### 7.1 Desktop, second pass (27 October 2026)

Jakob found the desktop page "not so well designed". At 1440×900 it sat in a
64rem frame of its own, off the navbar's edge; "+ Eintragen" was squeezed
beside the title in the narrow column; the summary floated with nothing to hold
it; and an empty day was four loose headings. Changed:

- **The shell's frame.** From 64rem the page starts on the navbar's edge like
  Woche and Konto. The day's title runs across both columns with
  "+ Eintragen" at its end, and the meals column stops at 52rem.
- **One card for the day's numbers.** The week strip and the summary sit on
  one sticky raised card. A hairline parts the macros from the day's number.
- **Every meal is a card, its heading the first row.** This revises "an empty
  meal is its heading alone": the heading now sits inside the raised surface,
  so an empty meal is one row high (principle 6) and lines up with the full
  ones, not a white box holding nothing.
