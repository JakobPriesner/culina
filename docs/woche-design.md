# Woche — the plan and the list as one place

| | |
| --- | --- |
| Status | Built 24 October 2026, all four phases of §7 (uncommitted in the `kcal-tracking` worktree). Structure W is chosen "for now" (`culina-v2-eawy.1`). §9 says where the build differs from this proposal. |
| Decides | How the week's plan and the household's shopping list become one surface, on desktop (side by side) and on phones (Plan \| Liste) |
| Builds on | `app-structure-design.md` §3 (W), §5 (the relationship), §7 (rules); the W prototype in the `kcal-tracking` worktree |
| Beads | See §8 |

**The rule from here on.** The planner and the shopping list are designed
together. A change to either one is checked in the other view, and in the
desktop layout where both are on screen: its grid, its add flows, its badges,
its empty and loading states, and what it does to the other.

---

## 1. Why this needs a design of its own

W puts the plan and the list under one tab. Today that is a seam, not a
whole: two pages a switch flips between, and on a wide screen the list page
placed in a column beside the week. Each half still behaves as if the other
were somewhere else. The week's button that fills the list sits at the bottom
of the week, the list's toast offers to open the list that is already on
screen, and nothing on either side shows which meal a line is for.

The desktop layout is the part Jakob likes. It is also the part where no
competitor has anything (§3), so it is the one worth making excellent.

## 2. What the page does today

Seen at 1440×900, 1280×800, 1100×800 and 390×844, with a realistic week (six
meals, three on the list, one cooked) and a 13-line list. The screenshots came
from a throwaway Playwright spec against the W prototype.

| # | Finding | Where |
| --- | --- | --- |
| W1 | **On a laptop the week is below the fold.** At 1280×800 the header, the Today card and the coach tip fill the first screen; only the top of Monday to Wednesday shows. The three-column card grid makes a 7-day week three rows tall, Sunday alone in the last. | desktop |
| W2 | **The list's heading outranks the page's.** The pane's "Einkaufsliste" (an `h2`) is drawn larger than the page's "Diese Woche" (the `h1`), and the two headings do not share a baseline. | desktop |
| W3 | **The action that joins them is at the wrong end.** "3 Mahlzeiten auf die Einkaufsliste" sits under Sunday, about 1,500 px down, far from the pane it fills. | desktop |
| W4 | **Feedback points elsewhere.** After that button, the toast offers "Liste öffnen" (`openListAction`) even when the list is beside it. Unplanning a listed meal asks in a toast at the screen edge, not where the lines are. | desktop |
| W5 | **No provenance on screen.** A card says "Auf der Einkaufsliste" in small text; the list says nothing about meals unless the disclosure "Mahlzeiten und Rezepte auf dieser Liste: 3 · 3 schon vergangen" is opened, and that repeats the week that is already on the left. The data is there: every line carries its `sources` (meal, day, share). | both |
| W6 | **Past and future read backwards.** Past days are outlined cards with no fill, days ahead have the sunken grey fill, so the week ahead looks disabled and the days gone look like the live ones. | both |
| W7 | **Cards spend a row on two icons.** In the grid each card gives a full row to ✓ (made it) and × (remove). | desktop |
| W8 | **The Today card repeats today's day** when the week is on screen beside it. On a phone it earns its place (first thing on landing); on a desktop it pushes the week down (W1). | desktop |
| W9 | **The phone switch carries no state.** "Plan \| Liste" says nothing about the list, so "is there anything to buy?" means switching to look. | phone |
| W10 | **A line in a section the client does not know vanishes.** `groupsToBuy` only draws known sections, but "N fehlen noch" counts every line. The contract is an enum today, so this needs a newer server and an older cached app, but then lines go missing silently. | both |

What is already right and stays: the list in walking order, one-tap ticks with
undo, bought lines kept, the planned week added in one request that never
doubles a line, servings changes that offer to update the list, unplanning that
*offers* withdrawal instead of forcing it, manual lines that no plan touches,
and the remembered Plan | Liste view.

## 3. What others do (research, 24 October 2026)

A web survey of Samsung Food, Plan to Eat, Paprika 3, Mealime, AnyList, Eat
This Much, Cooklist, KptnCook, Bring!, Apple Reminders and Fantastical, from
vendor help pages; only what those pages state is listed. The main sources:
- [Plan to Eat: list date range](https://learn.plantoeat.com/help/change-your-shopping-list-date-range)
- [Paprika help](https://paprikaapp.com/help/windows/)
- [AnyList: add from the meal plan](https://help.anylist.com/articles/meal-planning-calendar-add-recipe-ingredients)
- [Mealime: the grocery list](https://support.mealime.com/article/75-how-the-grocery-list-works)
- [Eat This Much: the grocery list](https://help.eatthismuch.com/help/how-does-the-grocery-list-work)
- [Apple Reminders: grocery lists](https://support.apple.com/en-ae/105086)

No genuine user threads about the plan-to-list connection were found.

- **Nobody verifiably shows the plan and the list on one screen.** The
  nearest are Paprika's separate desktop panes and Fantastical's task sidebar
  beside the calendar (drag a task onto a day). Culina's pane is ahead of the
  field.
- **Nobody verifiably links a line back to its meal.** Paprika can show
  recipe names under items (off by default); AnyList can remove one recipe's
  share of a merged item. Everything beyond that is open.
- **What happens on the list when a meal goes is the weakest area.** No
  source documents automatic withdrawal. Plan to Eat's list follows a date
  range; AnyList and Paprika copy once and go stale.
- **Shopping mode is universal:** aisle sections, check-off, a pending count
  (Mealime's badge on the list icon), bought lines moved aside.
- **Anti-patterns:**
  - copy-and-forget lists that go stale;
  - regenerating the list over manual lines (Mealime, Plan to Eat's reset);
  - a hand-edited line that silently stops following its meal (Eat This Much);
  - a pantry that must be redone every week;
  - the source meal hidden behind a setting.

Culina already avoids all five, except that its provenance sits behind a
disclosure (W5).

## 4. Principles for Woche

1. **One week, two views of it.** The plan is what the week eats, the list is
   what the week needs. Each view shows the other's state where it matters,
   never a copy of it.
2. **The seam is where the action is.** What joins them (fill the list, take a
   meal's lines off) happens at the boundary: on desktop, at the top of the
   pane; on a phone, at the top of the list and at the end of the plan.
3. **Show the change where you are looking.** When the pane is on screen, a
   change to the list is shown in the pane, not in a toast. When it is not, a
   toast says so, as now.
4. **Provenance by default, never in the way.** Which meal a line is for is
   one hover, focus or tap away, in both directions. It never adds rows to the
   shop view on a phone.
5. **The rules still hold** (`app-structure-design.md` §7). In particular:
   - no tap is added to a top-5 job (adding a line, ticking in the shop,
     seeing today, planning a recipe);
   - the list works without a plan;
   - a plan change never touches a manual line;
   - wide screens get more, never different.

## 5. Proposals

### 5.1 One header for Woche

- **Desktop:**
  - The page header spans both panes: "Diese Woche · 19.–25. Oktober" with
    ‹ › on the left.
  - The pane's heading becomes "Einkauf" at the size of a section heading
    (the `h2` it is), on the same baseline, with "11 fehlen noch" and the
    progress bar under it. This fixes W2.
- **Phone:**
  - The segment carries the list's state: "Plan" | "Liste · 11", counting the
    lines still to find. A count of nought shows nothing. This fixes W9 at no
    tap cost.

### 5.2 The seam: what the week still needs

- **Desktop:**
  - The shop bar leaves the bottom of the week and becomes the first line of
    the pane: "Diese Woche: 3 Mahlzeiten noch nicht auf der Liste ·
    Hinzufügen". It follows the week on screen ("Nächste Woche: …" after ›),
    because the list is one household list and the week is what is being
    planned.
  - It disappears when nothing is missing.
  - The bottom-of-week bar goes on desktop. This fixes W3.
- **Phone:**
  - The same line opens the Liste view (this is `eawy.6`'s strip).
  - The plan keeps its bar at the end of the week, where planning ends.
- **After adding**, the new lines arrive in the pane tinted for about two
  seconds, with no toast while the pane is visible (W4). A phone keeps the
  toast with "Liste öffnen".

### 5.3 Provenance both ways

- **Desktop, hover or focus:**
  - Hovering or focusing a meal card tints its lines in the pane. A merged
    line shows that meal's share ("Zwiebeln 3 — 1 für Linsen-Dal").
  - Hovering or focusing a line outlines the cards of the meals it is for.
  - This is pure highlighting: it holds no state and has keyboard parity
    through focus.
- **"Auf der Liste" on a card becomes a link:**
  - On desktop it scrolls the pane to the meal's first line and pulses its
    lines.
  - On a phone it opens Liste with those lines marked (`?mahlzeit=<entryId>`);
    Back returns to the plan where it was.
- **A line's ⋯ menu names what it is for:** "Für Linsen-Dal · Di 20.", one
  entry per meal, each opening the plan at that day. This is the way back from
  the list, which no competitor has.
- **The disclosure "Mahlzeiten und Rezepte auf dieser Liste":**
  - On desktop it goes from the pane, where the week beside it already says
    it. What it adds is the leftovers of past meals, which become a one-line
    notice: "3 Zeilen von vergangenen Mahlzeiten · Ansehen".
  - On a phone it stays, as the way to withdraw a meal from the list.

### 5.4 Plan changes reach the list where you look

- **Unplanning a meal that is on the list:**
  - Desktop: the pane shows a note at its lines, "Ofengemüse ist nicht mehr
    geplant · 5 Zeilen entfernen · Behalten", instead of the toast. Ticked
    lines stay either way, as now (they are in the cupboard).
  - Phone: the toast, as now.
- **Moving a listed meal** changes nothing on the list (the same shopping),
  but its provenance follows at once.
- **Changing servings** keeps today's question. When the list follows, the
  changed amounts tint in the pane.
- **Dragging a meal onto the pane** puts its shopping on the list. Drag is a
  shortcut, never the only way: the card's menu has "Auf die Liste" too.

### 5.5 The desktop week as an agenda beside the list

From 80rem, where the pane is shown, the week becomes seven rows instead of a
three-column grid:

```
 Diese Woche · 19.–25. Oktober            ‹  ›  │ Einkauf
                                                  │ 11 fehlen noch  ▬▬▭▭▭▭▭▭
 Mo 19  [Linsen-Dal · Abend ✓ gekocht]        +   │ Diese Woche: 3 Mahlzeiten
 Di 20  [Overnight Oats · Früh] [Ofengemüse…] +   │ noch nicht auf der Liste · Hinzufügen
 Mi 21  [Spaghetti Carbonara · Abend]          +  │ [ z. B. 200 g Butter        ] (+)
 Do 22  [Hähnchen-Curry · Abend]               +  │ Obst & Gemüse 4
 Fr 23                                         +  │ ☐ Paprika                 3
▌Sa 24  [Shakshuka · Mittag   Kochen ›]       +   │ ☐ Zucchini                2
 So 25  [Linsen-Dal · Abend]                   +  │ …
```

- **Rows:** each day is a row: the day and date in a fixed first column,
  meals as compact cards that wrap within the row, and the day's "+" at its
  end. The day's energy line sits small at the right.
- **Fits one laptop screen:** about 560 px for a typical week, so the whole
  week and the top of the list fit at 1280×800 (W1). Drag between rows stays
  as it is.
- **Today:**
  - Today's row carries the accent fill.
  - On desktop the Today card folds into today's meal card ("Kochen ›" on the
    card) and is not shown above the week (W8).
  - On phones the Today card stays.
- **Past and future (also on phones):**
  - Days gone get muted text and no fill.
  - Days ahead are plain.
  - Today gets the accent. This fixes W6.
- **Card actions:** ✓ and × show on hover and focus with a fine pointer, and
  always with a coarse one. They are not a row of their own (W7).
- **Below 80rem nothing changes.** The day cards and the switch stay as they
  are.
- **Measured (24 October 2026, `g5ie.10`): the pane stays from 80rem.** I
  measured with the breakpoint lowered to 64rem, a six-meal week, German, and
  the bottom edge of Sunday's row:

  | Viewport | Mouse | Touch |
  | --- | --- | --- |
  | 1024×768 | 860 | 1064 |
  | 1100×800 | 838 | 851 |
  | 1280×800 | 740 | 810 |
  | 1440×900 | 741 | 811 |

  - No word broke at any width.
  - At 1024 and 1100 px the week no longer fits one screen.
  - On touch, where ✓ and × stay in the row, titles, meta and "Auf der
    Einkaufsliste" all wrap at 1024 px. That is a worse week than the full
    width the switch gives it.

### 5.6 Empty and loading, together

- **Empty week and empty list:**
  - One state for the place, not two Ollis: "Plane ein Essen — die Zutaten
    kommen auf die Liste", with "Rezept planen".
  - The pane shows only its add field, because the list is often used without
    a plan.
- **Empty week, list in use:** an inline line in the week ("Noch nichts
  geplant · Rezept planen"), not the large empty state.
- **Loading:** the skeleton mirrors the two panes (this is the open part of
  `eawy.10`).
- **The list's coach tip** stays under the list. The plan's tip moves under
  the week on desktop, so neither pushes its content down.

### 5.7 On a phone, one place

- **Remember each view's scroll position:** returning from Liste to Plan
  lands where it was, not at the top. To verify: today the switch is a
  navigation (`goto`), and restoring is unverified.
- **No cross-fade after all** (decided while building `g5ie.12`). The app
  switches pages instantly by design (`motion.spec.ts`: no dissolve, no view
  transition). The switch keeps to that rule, and the remembered scroll is
  what makes the two views read as one place.
- **No swipe between views.** It would compete with dragging meals and with
  the system back gesture.

### 5.8 Robustness

- **Unknown sections:** a line whose section this client does not know is
  drawn under "Sonstiges", so the count and the lines agree (W10).

## 6. Not now

- **"Schon zu Hause"** memory per ingredient (Paprika's pantry, done right):
  a staple marked once stays unticked from future meals. This needs its own
  design; it touches the server's sum.
- **"Ich gehe einkaufen"** (Bring!): one tap tells the household. It needs
  push to other members.
- **A recipe sidebar on desktop** to drag recipes onto days (Paprika). This
  belongs with the global add (`eawy.7`).
- **Several shops or lists.** One household, one list stays.

## 7. Phasing

| Phase | What | Why first |
| --- | --- | --- |
| **0** | Make W the structure: drop the prototype switch, K and "wie jetzt"; keep the `diaryPlacement()` seam; release builds get W. | Everything below assumes W. Today release builds still show the old bar. |
| **1, the seam** | §5.1 one header and the count in the segment; §5.2 the seam line and no stray toast; §5.3 the "Auf der Liste" link and the line's "Für …" entries; §5.8. W6's past/future treatment. | Small, visible, and it joins the two halves without moving the layout. |
| **2, the desktop layout** | §5.5 agenda rows and the Today card folded in; §5.6 empty and loading; §5.3 hover provenance; the 64rem measurement. | The largest change, and the one that makes the desktop view whole. |
| **3, the feel** | §5.4 plan changes shown in the pane, drag onto the pane; §5.7 scroll memory and the view transition. | Polish on a layout that has settled. |

## 8. Beads

Epic `culina-v2-g5ie`.

| Phase | Beads |
| --- | --- |
| 0 | `.1` make W the structure |
| 1 | `.2` header and count · `.3` the seam · `.4` provenance links · `.5` past/future days · `.6` unknown sections |
| 2 | `.7` agenda rows · `.8` empty and loading · `.9` hover provenance · `.10` the 64rem measurement |
| 3 | `.11` plan changes in the pane · `.12` scroll memory and the view transition |

Dependencies:
- `.2`, `.3` and `.7` wait for `.1`.
- `.8` to `.11` wait for `.7`.

Older beads:
- `eawy.6`: its parts (1) and (2) continue in `.3` and `.4`. Its Today-card
  prompt, desktop autofocus and stale comment stay with `eawy.6`.
- `eawy.10`: the pane is built. Its skeleton criterion continues in `.8`.
- `eawy.2` is closed with the choice.

## 9. What was built, and where it differs (24 October 2026)

- **The bar for everyone:** Rezepte, Kochbücher, Woche, Tagebuch (`navigation.ts` `barDestinations`).
  - The diary keeps its tab while it is off. Its page offers the switch,
    with the consent sentence, so the bar never changes shape.
  - The prototype switch, K and the old bar are gone. `woche.svelte.ts`
    remembers the view, and `WocheSwitch.svelte` is the switch.
- **The seam:** `WeekToList.svelte` is one sentence with its verb, "Auf die
  Liste", at the end. It was planned as a band with a button, but on a 320 px
  phone that pushed the first line of the list below the fold.
- **The card:** in `PlannedCard` the title is the link, stretched over the
  card. Meta and status share a line, so "Auf der Einkaufsliste" (a link to
  the lines) and "+ Auf die Liste" are controls of their own.
- **The pane as a drop target:** dropping a meal on the list beside the week
  used to move the meal to the last day the pointer crossed. It now puts the
  meal's shopping on the list (`Landing.list`).
- **Measured:** the pane stays from 80rem (§5.5).
- **No cross-fade:** the switch has none, by the app's rule (§5.7).
- **Not built:** "Schon zu Hause", "Ich gehe einkaufen" and the recipe sidebar,
  as §6 says.

## 10. Days and weeks: the plan redesigned (24 October 2026)

Jakob's review of the built page: the hairline between week and list had a
rounded end; the page did not look like a calm, professional tool; on a phone
the header's fade lay over the Plan | Liste switch; nothing said what should
stay in view on a phone or jump to today; and on a Saturday the week still
opened with Monday. A web survey of Apple's HIG, Apple Calendar, Google
Calendar, Outlook, Fantastical and the meal planners in §3 informed the answers
(sources in the session's research brief; the key ones are below).

### 10.1 Two views, and the month as a way to get somewhere

| View | Shows | Arrows step | Default |
| --- | --- | --- | --- |
| **Tage** | today and the three days after it (`daysShown`), from any day | one day | below 80rem |
| **Woche** | Monday to Sunday, the shopping week | one week | beside the list |

- The choice is remembered per device (`culina.plan.view`, `planView.svelte.ts`).
- **No day view.** One day is too little to plan with; the first row of Tage
  already is today.
- **No 3-day columns.** Apple shows at most two day columns on a phone, and
  meal cards need the full width for their words.
- **No month to plan in.** A month of meals brings recurrence and a second
  shopping list. The month is a picker instead (`MonthPicker`). The range under
  the title opens it, and a day in it shows the plan from that day. This is
  Fantastical's and AnyList's use of the month.
- **Four days, stepped a day at a time,** is Google Calendar's "4 days" and the
  rolling list that the simpler planners moved to (Crouton, Samsung Food).
  Today is never the last day shown.
- **Woche stays** because the list fills from Monday weeks (the server reads,
  writes and shops in weeks).

### 10.2 What is always in view on a phone

- One opaque bar inside the floating header (`headerAccessory`) holds
  Plan | Liste and, from 30rem (a phone on its side, a tablet), ‹ Heute ›. On a
  phone upright (under 30rem) ‹ Heute › cannot share the bar, and a second
  pinned row would take a quarter of the screen, so it sits in the page head
  beside the dates and scrolls away with it. The head there is two rows: the
  title with Tage | Woche, then ‹ Heute › and the dates (`culina-v2-y6ti.10`). Heute rests (disabled) while today is already
  shown, so it never moves.
- The header's scrim covers the bar and fades out below it. It used to fade
  over the switch, which was a sticky element of the page under the shell's
  scrim. Apple: one scroll-edge effect, and never over controls.
- Tage | Woche and the dates sit in the page head. They are chosen rarely and
  are allowed to scroll away (NN/g: keep sticky headers small).
- No swipe between days. It would compete with dragging meals and with the
  system back gesture (§5.7), and the arrows are the visible way.

### 10.3 Today first, the days gone folded

- Whenever today is on screen, it is the first day shown.
- In this week's Woche, the days before today fold into one line,
  "Früher diese Woche · Mo., 19. – Fr., 23. Okt. · 2 Mahlzeiten", which opens
  them in place.
- A day a list line leads to is never folded.
- The Today card is gone from the plan. Today's meals carry "Kochen" on their
  own line, so today is said once. The library's home keeps the card.
- Near days are named by how near they are, "Heute", "Morgen", "Gestern", with
  the date beside them.

### 10.4 The calmer surface

- **Days:** an agenda, one surface parted by hairlines, not seven boxes. Today
  alone has the accent fill.
- **Day row:** the "+" ends the day's own line instead of taking a row.
- **Rows from 48rem:** the name sits in a column, the meals side by side, and
  the energy line under them, no longer squeezed under the name.
- **Cards:** a raised list cell with a hairline edge. The picture always stays
  beside the words, which hold whole words (no "Pizza-teig") clamped to two
  lines. "Kochen" is tonal, not filled, so a day of three meals is not three
  loud buttons.
- **Desktop ✓ and ×:** they appear on hover. Hidden, they are no longer there
  at all; an invisible layer used to swallow clicks on "Auf der Einkaufsliste".
- **The parting line** between the week and the list is straight, with no
  radius.

### 10.5 Across two weeks

- `mealPlan` holds every week it has read, keyed by Monday. Its `days` are the
  run on screen, so Saturday to Tuesday is two reads.
- Every write still returns its whole week. A move across Sunday night reads
  the week the meal left again.
- The seam beside the list says one line per week on screen that still misses
  meals ("Diese Woche: …", "Nächste Woche: …").
