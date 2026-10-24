# Culina — a private food diary (design)

| | |
| --- | --- |
| Status | Accepted 22 October 2026, with the decisions in §0. MVP built 22 October 2026 for structure W, with its placement kept switchable. §15 says what was built and where it differs. |
| Written | 21 October 2026 |
| Decides | Whether and how a person tracks what they eat (kcal and macros) in Culina, without breaking the nutrition stance |
| Bead | `culina-v2-b3hl` (epic) and its children `.1`–`.20` |

The ask: *"I want to be able to use this app to track my kcal for fitness and
healthy reasons."*

The constraint, from `README.md`: "No hidden calorie estimates (a wrong number
is worse than none)." `nutrition-design.md` turned that into a lookup with a
citation. Every figure says how much it covers, and unknown looks unknown.

This proposal keeps that promise in the form it was meant: no **hidden**
estimates. A diary in Culina is **a page of what you ate**, filled mostly by
confirming what Culina already knows you cooked, and otherwise by a food and
an amount, as in Yazio. Every figure on it is looked up, estimated from a
cited typical portion, or typed by you, and the page always says which. It is
private, opt-in, and invisible to anybody who does not switch it on.

Labels, as in the other design docs:

- **Fact**: read from this repository or a primary source.
- **Reasoning**: an argument from those.
- **Assumption**: not verified; stated so it can be checked.
- **Decision needed**: Jakob's call, with a recommendation.

Two expert reviews shaped this: a visual/app-design critique and a UX/research
critique, both written for this proposal. Where they disagreed, §11 says which
way this doc went and why.

---

## 0. Decisions (Jakob, 22 October 2026)

| # | Question | Decided |
| --- | --- | --- |
| 3 | A Richtwert below the floor | A calm sentence plus a link to support. It does not block and does not lecture. |
| 5 | MCP agents | No agent access to diary data. It is sensitive, and a person might share it through an agent without meaning to. Every diary operation is withheld. |
| 6 | Snapshot or live | Each entry stores its numbers when logged (§4.2). |
| 7 | Goals | An optional kcal Richtwert and an optional protein target. Nothing more in the first release. |
| 8 | TDEE helper, weight log | Neither in the first release (`.13`, `.14`). |
| 9 | Snacks | Yes, a fourth meal in the diary. The plan keeps its three slots. |
| 10 | *Made it* → diary | A toast action after one-tap *made it*, and a portion stepper in `CookedWrapUp` (§6.1). |
| 11 | Values shown | kcal, protein, carbs and fat. Saturated fat, sugars and salt stay on detail and recipe pages. |
| 13 | Offline | Reading works offline. Saving needs a connection in the first release. |
| 1 | Estimates | For tracking, a realistic estimate beats no number. There are three trust levels: looked up, estimated (≈, from a cited typical portion) and typed by you (§4.4, §14.1). A food is logged with an amount in g, ml, Stück, Scheibe or Portion, as in Yazio. The README keeps "no **hidden** estimates". |
| 2 | Where it lives | A **Tagebuch** tab that appears at the end of the bar only for a person who switched the diary on, plus the Today-card line as a shortcut (§7, §14.2). Plan and Shopping stay separate. Precondition: a five-tab check at 320 px in German and English. If "Kochbücher" clips, the fallback is merging Rezepte and Kochbücher, never smaller labels. |
| 4 | Opt-in text | No explainer and no warnings. The switch carries one consent sentence. Switching off asks whether to delete the entries too (§8, §14.3). |
| 12 | Motivation | No breakable streak, but a logged-days count that never resets. No badges, no rings, no red. "Remaining" is an optional headline, with "eaten" as the default. Reminders are opt-in, at times the person picks, at most two a day, in neutral wording. A weekly summary with no pass/fail (§8, §14.4). |

Where an earlier section and §14 disagreed, the sections below have been
brought in line with these decisions.

---

## 1. What Culina already has

| Need of a diary | What exists | Gap |
| --- | --- | --- |
| Figures for a recipe portion | Per-serving or per-piece nutrition for the 7 EU-label values, BLS 4.0 lookup, with `atLeast`, `estimated` and `counted/lines` (`Domain/Nutrition/NutritionCalculator.cs`, `GET /recipes/{id}/nutrition`) | None. The figure is computed on read and never stored. |
| Figures for a plain food | `GET /foods?q=` (BLS search, 7,140 foods, per 100 g); household package foods typed from a label (`household_foods`, 0041) | No "I ate 150 g of X" anywhere. |
| What was cooked | Per-**person** cook log (`cook_log_entries`): recipe, `made_at`, servings made, note, photo. One-tap *made it* with undo from the recipe page, Today card and week, plus `CookedWrapUp` after cook mode. | Cooking ≠ eating. `servings` is the batch, not my portion. |
| What is planned | Household week: date + slot (breakfast/lunch/dinner), servings, leftovers. A quiet line under each day, "mind. 1.850 kcal pro Person" (`planning/PlanDayNutrition.svelte`, `nutrition/dayFigure.ts`). | A plan is an agreement, not a record. There is no snack slot. |
| Packages | Household product memory (GTIN → name/ingredient/pack size, migration 0048) and `ScanSheet` (camera or typed digits). **Fact**: these sit on local `main` and are not yet on `origin/main`. | A product carries no nutrition, by design. |
| A person | `User` (email, display name). `UserPreferences` (language, theme, motion, metric/imperial). | No goals, no body data, no consent record. |
| Ownership | Two axes (`domain-model.md`): household-owned (recipes, plan, shopping) and person-owned (notes, cook log, sessions, preferences). Person-owned data is never shown to other members. | — |
| Charts | `ProgressBar` only. No chart library, no ring. | A small viz primitive. |

**Reasoning.** Most of what a diary needs is already there. What is missing is
one person-owned table of *eaten* entries, a goal, and a page to read them on.
The domain model already uses the word: a plan "only its author could see would
be a diary" (`domain-model.md`, Planning). This is that diary, deliberately
separate from the plan.

---

## 2. Personas

1. **Lena, 29, cutting for a fitness goal.** She wants to stay in a deficit
   without weighing everything. She leaves if logging takes more than a minute
   a day, if her café lunch can't be entered honestly, or if over-days are
   scolded.
2. **Marco, 34, building muscle.** He wants about 150 g protein a day, and the
   protein in a portion before he cooks. He leaves if protein is an afterthought
   or his protein bar doesn't match its label.
3. **Anja, 47, wants awareness.** No target, just "am I eating enough and
   balanced?" She leaves if she is forced to set a goal or prompted to weigh
   herself. She is why **no goal** is a complete mode.
4. **Tobias, 41, cooks for the family and does not track.** His partner does.
   He leaves (or resents the app) if kcal creeps into his plan or cook mode, or
   if the household can see his partner's diary. He is why the feature is
   **invisible when off**.
5. **Renate, 62, type 2 diabetes, guided by her doctor.** She needs carbs per
   meal that she can trust. She is why carbohydrates are first-class and why
   the lower-bound marking matters most. She is also why Culina says plainly
   that it gives no medical advice, and never doses anything.

**Not a target user.** Anyone with a history of disordered eating. The design
must never become their trigger (§8).

---

## 3. Use cases, by frequency × value

1. *"I ate the dinner we cooked, my portion was 1½."* One tap from *made it*.
2. *"Same breakfast as yesterday."* Repeat a meal or a whole day.
3. *"My usual yoghurt / apple / protein bar."* Recent foods, one tap.
4. *"Where am I today?"* A glance at kcal and protein.
5. *"Scan this package."* Barcode to a household package food, then a portion.
6. *"Café lunch."* A number I type, marked as mine.
7. *"How was my week?"* An average, not a daily verdict.
8. Set or change a target. Pause or switch the diary off.
9. Export or delete everything.

---

## 4. The diary entry

### 4.1 Model

`diary_entries`, person-owned like `cook_log_entries` (`user_id` with
`ON DELETE CASCADE`):

| Field | Meaning |
| --- | --- |
| `id` | UUIDv7 |
| `user_id` | the eater. Never a household. |
| `on_date` | a **date**, not a timestamp. The plan's reasoning holds: "Thursday" has no time zone, and travelling must not move a breakfast. |
| `meal` | `breakfast`, `lunch`, `dinner`, `snack`. The plan has no snack slot, because a slot there must change what you buy. Eating is not buying. |
| `source` | `recipe`, `food` (BLS), `householdFood`, `typed` |
| `recipe_id?`, `household_id?` | for `recipe`. A soft link: a deleted recipe keeps its entry. |
| `food_code?` / `household_food_id?` | for `food` / `householdFood` |
| `cook_log_entry_id?`, `meal_plan_entry_id?` | where the entry came from, if anywhere (for "already logged" and undo) |
| `amount`, `unit` | for a recipe, portions in ¼ steps. For a food, an amount in `g`, `ml`, `piece`, `slice` or `portion`. |
| `grams?` | the weight the figures were computed from. For a unit other than `g`, it comes from a household weight, a cited typical portion or a density. |
| `label` | the name **as it was** (recipe title, food name, or typed text) |
| `kcal`, `protein_g`, `carbs_g`, `fat_g`, `sat_fat_g`, `sugars_g`, `salt_g` | a **snapshot**. Nullable means unknown, never zero. |
| `at_least`, `estimated` | carried from the lookup. `estimated` is also set when a non-gram unit was turned into grams by a typical portion. |
| `basis?` | for an estimate, what it rests on, e.g. "Döner Kebab (BLS) × 350 g, typische Portion" |
| `nutrition_version` | `NutritionData.Version` at the time |
| `created_at`, `version` | as everywhere |

### 4.2 Why a snapshot, when recipe nutrition is computed on read

**Reasoning.** A recipe page shows the recipe as it is now. A diary shows what
you ate then. If Monday's curry was edited on Wednesday, or moved to the bin,
Monday must not change. Snapshotting also keeps a month view to one indexed
range read (`user_id, on_date`) instead of recomputing nutrition for every
entry.

The cost is that a correction made later (a household weight, a better food
match) does not reach old entries. That is the honest behaviour: the entry
says what Culina knew when you logged it. *Repeat* and *recent* always take a
**fresh** lookup, so a correction reaches every new entry at once.

### 4.3 Why not a column on the cook log

`CookLogEntry.Servings` is the batch made. One cook can feed four eaters,
across two days. Tobias's cook log must stay what it is. An eaten entry may
point at a cook-log entry, and that is all.

### 4.4 Three trust levels

Decided 22 October 2026 (§0 #1, research in §14.1). For tracking, a realistic
estimate beats no number. What the README rejects is an estimate that hides.

| Level | Source | Drawn as |
| --- | --- | --- |
| **Nachgeschlagen** | A BLS food or dish in grams or millilitres. A household package food in grams. A fully counted recipe portion. | The plain number. The source is in the detail view. |
| **Geschätzt** | A BLS food or dish in Stück, Scheibe or Portion, via a cited typical portion ("1 Döner ≈ 350 g"). A household unit weight. A recipe portion with typical weights in it. | "≈ 620 kcal". The detail names the basis. |
| **Von dir** | Quick add: kcal, with optional macros | A pencil glyph and "von dir eingetragen". Never the BLS attribution. |

- **A lower bound stays a lower bound.** A recipe portion that is only partly
  counted shows `mind.`, rounds down, and opens the existing
  `NutritionBreakdown` under "Was fehlt?". Nothing invents the missing lines.
- **Typical portions** for about 100 common dishes and foods (Döner, Brezel,
  Croissant, a slice of bread, an apple, a pizza) are added to the existing
  cited typical-weights table. They come from USDA FNDDS portions (CC0) or
  printed pack sizes, and each row names its source. BLS already holds about
  2,000 composite dishes, so the values per 100 g are there.
- **Unknown macros** on a typed entry stay `–`, never 0.
- **Day total.** The sum of what is known. It is marked `≈` if any entry is an
  estimate, and `mind.` if any is a lower bound. If any part is typed, the day
  says so: "≈ 1.640 kcal, davon 400 von dir eingetragen".
- **No AI photo guesses, no restaurant-chain tables, and no crowd database.**
- **Plausibility, not a block.** A typed value over 5,000 kcal, or a portion
  over 10, asks "Stimmt das?" once.
- **A plan is not a record.** Planned meals appear as suggestions (§6.2) and
  count towards nothing until confirmed.

---

## 5. Goals

- **A manual Richtwert**, optional: "Mein Richtwert: ___ kcal". There is an
  optional protein target in grams. Carbs and fat targets come later, if anyone
  asks.
- **No computed TDEE in the first release.** Mifflin–St Jeor plus an activity
  factor is a ±20 % guess (**Assumption**, commonly cited, check before v2), and
  it needs age, sex, height and weight: the most sensitive data Culina would
  ever hold. A later "Schätzen helfen" may calculate a suggestion that the
  person copies into the field. It must name the formula and say it is an
  estimate, and it stores no body data.
- **No weight log in the first release.** Weight is the highest-risk data point
  and the one most tied to harmful behaviour. Adding it needs its own decision
  (bead below).
- **Floor.** A Richtwert below 1,200 kcal shows a calm sentence and a link to
  support. It does not block, and it does not lecture (§0 #3). Verify the link
  before launch (the BZgA eating-disorder line is the obvious candidate).
- **Over is not red.** A day over the Richtwert looks like a day under it. The
  wording is neutral ("etwas über dem Richtwert"), never "zu viel", "Defizit"
  or "Budget". The week's average is where balance is read.
- **"Eaten" by default, "remaining" on request.** The headline shows what was
  eaten. A setting switches it to *noch übrig* for people who plan their day
  by it, which Yazio users expect. Over the Richtwert, *noch übrig* reads "0",
  not a negative red number.
- **No goal at all** is a complete mode: totals and macros, no band end-mark.

---

## 6. Logging in under 30 seconds

Culina's advantage over MyFitnessPal or Yazio is that it already knows what
this household cooked. A day should be logged mostly by **confirming**, not
searching.

### 6.1 *Made it* becomes *ate it*

These changes apply only when the diary is on:

- One-tap *made it* (recipe page, Today card, week): the existing toast gains
  an action, **"Als Portion eintragen"**. Tapping it logs one portion at once
  and shows a second toast with *Ändern* and *Rückgängig*. The one-tap path
  stays one tap for Tobias; the diary adds a second tap only for the person
  who tracks.
- `CookedWrapUp` (end of cook mode): one optional line, "Deine Portion: [− 1 +]",
  defaulting to 1. It is pre-filled and never required.
- The entry points at the cook-log entry, so undoing *made it* asks whether the
  eaten entry should go too.

### 6.2 The plan as suggestions

Today's planned meals that are not yet logged appear in the diary as
**suggestion rows**: dashed, "aus dem Wochenplan", with one tap to "gegessen".
Unconfirmed suggestions never count. Yesterday's leftovers (`leftover_of`)
appear as suggestions on the day they were planned for.

### 6.3 Repeat and recent

- "Wie gestern" per meal, and "Gestern übernehmen" for a whole day.
- **Zuletzt**: the person's frequent entries over the last 30 days, ranked by
  how often they were used, never by kcal. One tap logs the same amount as last
  time, with a fresh lookup.

### 6.4 Search, scan, type

- **Search** across this household's recipes, BLS foods and dishes, and
  household package foods. A food takes an **amount and a unit**: g, ml,
  Stück, Scheibe or Portion. The units offered are the ones a weight is known
  for: the household's own first, then cited typical portions. Any unit other
  than g or ml makes the entry `≈`. This is the path for an ingredient, a Döner
  or a bakery item.
- **Scan.** The existing `ScanSheet` with a new purpose, `diary`. A product
  whose ingredient resolves to a household package food or a BLS food logs
  with a portion step. A product without nutrition offers "Von der Packung
  eintragen", which opens the existing `PackageFoodForm` and saves the values
  as a household package food, so the next scan is one tap. Open Food Facts
  remains an admin opt-in, as `barcode-scanning-design.md` §5.2 decides.
- **Type.** "Nur Kalorien eintragen" is the last row of the add sheet, never
  the default.

### 6.5 Budget

| Action | Taps | Time |
| --- | --- | --- |
| Planned dinner (suggestion or toast) | 1 | about 3 s |
| Repeat a meal | 2 | about 5 s |
| Recent food | 1 | about 3 s |
| Scan a known package | 3 | about 10 s |
| Café lunch, typed | 4 | about 20 s |

---

## 7. Where it lives

Decided 22 October 2026 (§0 #2; options, competitors and guidance in §14.2).

**A Tagebuch tab for the people who keep one.** When a person switches the
diary on, a fifth destination, **Tagebuch**, appears at the **end** of the bar,
after Einkauf. It has the same position on desktop and mobile. Nobody else's
bar changes, and no existing tab moves. This is the IA decision that
`navigation-research.md` asks for before a fifth destination. It holds the
five-destination ceiling, and that document is updated to record it.

**Why not merge Plan and Shopping.** Shopping is used in the shop,
one-handed, often by another member, and for ad-hoc items. A segment would
hide it behind a tap for every household, to serve a feature most of them
never switch on. No competitor that was checked merges them. Plan to Eat, the
most plan-centred app, keeps Plan and Shop as peers.

**Not inside Plan.** Plan is household-owned, and the diary is person-owned.

When the diary is **on**:

1. **The Tagebuch tab** opens `/diary`, the day page.
2. **The Today card** (`TodayMeal`, on `/` and `/plan`) gains a compact day
   line, "mind. 1.240 kcal · 64 g Eiweiß", with a thin band. It is a shortcut
   to the diary from where the meal is cooked.
3. **`/me/diary`** in Settings holds the switch, the Richtwert, the protein
   target, *eaten / remaining*, reminders, export and delete.
4. **Plan** stays as it is. Its day line is for planning and must not turn
   into a goal comparison. Tracking looks back; planning looks forward.

When it is **off**, none of this exists: no tab, no empty card, no teaser, and
no "try tracking" nudge.

**Precondition: measured, and it fails.** Before the tab ships, the five-tab
bar was to be measured at 320 px in German and English. On 22 October 2026
(`navigation-research.md`, "A fifth destination for the food diary:
measured"):

- "Kochbücher" overflows at 320, 360, 375, 414 and 430 px, and "Cookbooks" at
  most of those widths.
- Five tabs fit only between about 386 and 399 px.

The fallback, merging Rezepte and Kochbücher into one library tab, fits but
was **rejected** by Jakob on 22 October, because it changes navigation for
everyone (bead `.20`, closed).

**Where the diary lives is now part of the app-wide structure decision**
(`app-structure-design.md` §3, bead `culina-v2-eawy.1`). The options are:

- **K** (recommended there): a Tagebuch button in the header tools, plus the
  Today-card line.
- **W:** Woche as a plan-and-list hub, with a Tagebuch tab in the freed slot.
- **F:** a full-width five-tab bar.

K and W are to be prototyped and chosen by feel. Labels are never shrunk.

---

## 8. Safety, privacy, ethics

- **Opt-in with one sentence, no explainer** (§0 #4, §14.3). The switch
  **Ernährungstagebuch** in `/me/diary`, off by default, carries one line:
  *"Deine Einträge sind Gesundheitsdaten und nur für dich sichtbar. Mit dem
  Einschalten willigst du ein, dass Culina sie speichert. Keine medizinische
  Beratung."*
  - Switching it on records `consented_at` and `consent_version` on the
    person's diary settings.
  - There are no warnings about children, pregnancy or eating disorders. The
    support link lives only in the floor sentence (§5).
  - The help text is honest that "nur für dich sichtbar" means in the app:
    whoever runs the database can read it.
- **Off asks about the entries.** Switching off withdraws consent, so it asks
  **"Einträge auch löschen?"** with *Löschen* as the primary button and
  *Behalten* as the other. Kept entries stay hidden until the diary is switched
  on again. Deleting everything from settings takes a typed confirmation, the
  one justified exception to toasts-with-undo. Single entries use toast with
  undo.
- **No breakable streak**, no "perfect days", no badges, and no celebrations
  for numbers (§0 #12, §14.4). Instead, a count that never resets: "an 5 von 7
  Tagen eingetragen". A day with nothing logged is a gap, never a zero, and the
  app says nothing about it.
- **Reminders are opt-in**, off by default.
  - The person picks the times, at most two a day.
  - The wording is neutral ("Mittagessen eintragen?"). Never "you forgot" or
    "streak at risk".
  - Delivery is Web Push on a server-side schedule, using the existing VAPID
    setup. The native apps use local notifications.
- **Olli** gets one tip when the diary is first switched on, about how to use
  it ("Was du kochst, trägst du mit einem Tipp ein."). Olli never comments on
  numbers.
- **No feedback into the kitchen.** Diary data never ranks, filters, suggests
  or labels recipes. `search-design.md` already forbids nutrition-dependent
  ranking, and the diary adds nothing to it.
- **Household privacy.** The diary is person-owned. It is not visible to other
  members or to owners. It is not in the household archive's shared part (only
  the asker's own, per the archive rule), and an admin cannot read it in the
  UI.
- **GDPR Art. 9.** Eating data can be health data. That means explicit consent,
  purpose, minimisation, export and erasure. The instance operator is the
  controller, so `docs/operations.md` gets a short note for operators.
  **Assumption**: this is a UX reading of GDPR, not legal advice.
- **MCP agents.** All new diary operations are withheld in
  `Api/Agents/AgentOperations.cs`, with the reason "personal health data: a
  person could share it through an agent without meaning to" (§0 #5). There is
  no later read scope. Reading BLS foods stays allowed, as it is.
- **Offline cache.** Diary reads may join the private, owner-stamped offline
  cache. Writes are online-only in the first release, and the add sheet says
  so when offline.

---

## 9. Visual design

### 9.1 The day as a page

The diary looks like a **ruled page with a running total in the margin**, not
a fitness dashboard. The hero is a number and one sentence:

```
Heute                                  Do., 21. Okt.  ‹ ›
1.240 kcal                         ← sans digits, --text-xl+ (no serif digits beside counts)
etwas unter deinem Richtwert       ← --text-muted, descriptive, never evaluative
[████████████░░░░░░░┆░░░]          ← Band; tick = Richtwert
Eiweiß         64 g  ────●──
Kohlenhydrate 140 g  ───●───
Fett           38 g  ─────●─
─────────────────────────────────────
Frühstück                                     340
  Haferbrei mit Beeren · 1 Portion            340
Mittagessen                              mind. 620
  Linsencurry · ½ Portion                ≈    480
  Apfel · 1 Stück                             140
Abendessen
  ┄ Ofengemüse · aus dem Wochenplan ┄   [gegessen]
Snacks
  + Hinzufügen
─────────────────────────────────────
Nährwerte: Max Rubner-Institut, BLS 4.0 (CC BY 4.0)
```

- Rows use the existing **hairline-cell pattern**, not cards. A recipe name may
  be serif; quantities and figures are sans.
- Macros are **rows with a hairline track**, not a stacked P/C/F band: grams of
  three macros do not sum to the day. Colour comes from one ink family; labels
  and positions differ, never colour alone.
- **Desktop:** a `Page` with the diary rows on the left two thirds and a sticky,
  flat summary on the right.

### 9.2 Three textures, no status colours

| Meaning | Drawn as |
| --- | --- |
| Exact lookup | solid ink |
| From a cited typical weight | hatched ink, `≈` on the row |
| Lower bound (something not counted) | open, dashed end on the band, `mind.` on the number |
| Typed by you | pencil glyph, no attribution |

`--danger`, `--warning` and `--success` are never used in this feature.
`--accent` stays on the one primary action (*Eintragen*).

### 9.3 Design-system additions

- **`Band`** (`lib/design-system/display/`): an 8 px capsule track with solid,
  hatched or open-dashed fill and an optional tick. It is `role="img"` with a
  full `aria-label` ("1.240 Kilokalorien, mindestens, Richtwert etwa 2.100").
  The visible number is the truth; the band is redundant to it.
- **Rejected: a ring.** It is the "close your rings" mechanic, it cannot draw an
  open lower bound or an overshoot honestly, and it is harder to read at 320 px.
- **Later: `WeekStrip`** (seven vertical bands, gaps drawn as empty tracks, not
  as zero) and **`TrendLine`** (one moving-average polyline, values in a
  `Disclosure` table).
- Tokens: `--viz-ink`, `--viz-track`, `--viz-tick`, `--viz-hatch` in
  `semantic.css` and both themes, with 3:1 non-text contrast enforced in
  `themes.spec.ts`. Plain SVG and CSS, no chart library.

### 9.4 Sheets

- **Add-food sheet** (`Sheet`): a search field with a scan icon, and a
  `SegmentedControl` with *Zuletzt · Rezepte · Lebensmittel · Eigenes*.
  *Zuletzt* is the default. Rows show kcal per portion trailing, with `≈` where
  estimated.
- **Portion sheet** (recipe): the title is the recipe name, then "Rezept für 4 ·
  1 Portion = 480 kcal", a `Stepper` in ¼ steps, and chips for ½, 1 and 1½.
  Below that: "Das sind 240 kcal · 12 g Eiweiß", `mind. · 7 von 9 Zutaten` when
  partial, and **Eintragen**.
- **Food sheet**: an amount and a unit (g, ml, Stück, Scheibe, Portion). The
  units offered are those a weight is known for, as chips ("1 Stück ≈ 140 g").
  A non-gram unit shows its basis under the total, with `≈`.
- **Day hero**: *gegessen* by default, *noch übrig* when the person chose it.
  Under the week strip (v2) and on the day page, the count "an 5 von 7 Tagen
  eingetragen" never resets and never says "lost".

### 9.5 Motion

Only these, each an instant cut under reduced motion:

- The band fill moves when an entry is added or removed (200 ms).
- The portion sheet's live total uses the existing servings morph (400 ms).
- A suggestion row settles into a real row (200 ms).

No count-up on the hero, no celebration at the Richtwert, no haptic flourish.

### 9.6 German at 320 px

- Macro rows are vertical (label left, value right), never a 4-up grid.
- "Kohlenhydrate" is written out and allowed to wrap; no unexplained "KH".
- The tab label and page title are "Tagebuch". The settings row says
  "Ernährungstagebuch".
- The five-tab bar is measured at 320 px in German and English before the tab
  ships (§7).
- The Today line shows kcal and protein only.
- Test at 320 px with 200 % text in German.

---

## 10. API sketch

These follow `rest-api-design`: resources under `/users/me`.

| Method | Path | Notes |
| --- | --- | --- |
| `GET` | `/users/me/diary?from=&to=` | entries and day totals for a date range (at most 31 days). ETag from the max `version`. |
| `POST` | `/users/me/diary/entries` | body: date, meal, and one of {recipe + portions + householdId, food code + amount + unit, household food + amount + unit, typed figures}, plus an optional cook-log or plan link. The server resolves the unit to grams, does the lookup and the snapshot, and sets the trust level. **The client never sends figures for a lookup.** |
| `PATCH` | `/users/me/diary/entries/{id}` | amount/unit (re-snapshot), meal, date. `If-Match`. |
| `GET` | `/foods/{code}/units?householdId=` | the units a weight is known for (household first, then typical portions), with grams and source |
| `DELETE` | `/users/me/diary/entries/{id}` | `If-Match` |
| `GET` | `/users/me/diary/recent` | the 30-day frequency list |
| `GET`, `PUT` | `/users/me/settings/diary` | enabled, consentedAt, consentVersion, kcalTarget?, proteinTarget?, headline (`eaten` or `remaining`), reminders (up to two local times) |
| `DELETE` | `/users/me/diary` | erase all: after a typed confirmation in settings, or from "Einträge auch löschen?" when switching off |
| `GET` | `/users/me/diary` with `Accept: text/csv` | export |

Errors follow the existing `<Domain>Errors` pattern: `diary.disabled` (403
when the switch is off), `diary.recipeNotVisible`, `diary.foodUnknown`,
`diary.implausible` (only for values outside hard bounds).

Migrations take the next free numbers at build time. **Fact**: `origin/main`
ends at 0053 and local `main` holds an unpushed 0048, so the numbers must be
reconciled when they merge.

---

## 11. Where the two reviews disagreed

| Topic | Visual designer | UX designer | This doc |
| --- | --- | --- | --- |
| *Made it* → log | a toast action | an inline stepper in the made-it flow | A toast action for the one-tap path, so it stays one tap for non-trackers. An inline stepper in `CookedWrapUp`, where the person is already in a sheet. |
| Progress graphic | a `Band` primitive | a plain `ProgressBar` (fewer components) | `Band`. A `ProgressBar` cannot draw an open lower bound or overshoot, and that open end is the honesty this product is known for. |
| Diary route | `/me/diary` or `/diary` | `/me/diary` | The page is `/diary`; the settings are `/me/diary`. `/me` is Settings, and a daily-use page does not belong in the settings rail. |
| Barcode | secondary, an icon in the field | ranked 4th, first-class | An icon in the search field. It is fast once a household has entered the label, and useless before. |

Where they agreed: no fifth tab, no rings, streaks or red, no weight and no
TDEE in the first release, Olli silent on numbers, snack as a fourth meal,
gaps instead of zeros, and diary data never shaping recipes.

The decisions of 22 October changed two of these: a fifth tab now appears for
people who switch the diary on (§7), and "remaining" is an optional headline
(§5).

---

## 12. Phases

**MVP** (the children of the epic):

- Settings: the switch with its consent sentence, "Einträge auch löschen?" on off, the Richtwert, the protein target, eaten or remaining, export and delete.
- `diary_entries` with snapshots and three trust levels, and the API in §10.
- About 100 cited typical portions for common dishes and foods.
- The `Band` primitive and its tokens.
- `/diary` day view: totals with the `≈` and lower-bound rules, rows, suggestions from the plan, the logged-days count, attribution, skeleton, empty state.
- Add-food sheet, food sheet (amount + unit) and portion sheet: recent, recipes, BLS, household foods, typed.
- The conditional Tagebuch tab, after the 320 px check.
- *Made it* toast action, `CookedWrapUp` portion line, and the Today day line.
- Agent withholding, docs (`README`, `domain-model`, `api`, `design-system`, `navigation-research`, `operations`).

**v2:** the week view (`WeekStrip`, weekly average), repeat a whole day,
scanning from the diary, opt-in reminders, and fibre as an eighth value (BLS
has it; `Nutrients` does not).

**Later, each its own decision:** a TDEE helper, a weight log, Apple Health and
Health Connect through the native shells, carbs per meal for diabetes (never
insulin advice), a `TrendLine`, and Open Food Facts values for a scanned
product (only after asking OFF how ODbL treats a diary snapshot).

**Non-goals:** weight-loss coaching, a red or negative "remaining", breakable
streaks, badges, social sharing, AI photo recognition, restaurant-chain
databases, a crowd database, exercise tracking or "earned" calories,
micronutrients, meal scores or "healthy" labels, diary data in search or
suggestions, MCP agent access to diary data, and a pantry.

---

## 13. What to test with people first

1. Does *made it → Als Portion eintragen* feel helpful, or like surveillance of
   cooking? Ask Tobias as well as Lena.
2. Do people confirm plan suggestions, and do they trust totals that ignore
   unconfirmed ones?
3. Are "≈ 1.640 kcal" and "mind. 1.640 kcal" read as honest or as broken?
   Which wording for typed entries lands?
4. Is a manual Richtwert enough for Lena and Marco?
5. Does a goal-free day feel empty to Anja?
6. Does Renate need carbs per meal in the first release?
7. Do people who switch the diary on notice the new tab, and does anyone in
   the household find a bar that differs per person confusing?
8. How do the floor sentence, the remaining option and reminders read to
   someone who has had an eating disorder? Review them with an ED-informed
   clinician, not only general users.

**Success measures.** Logging a day takes under 30 seconds with a plan. More
than 60 % of entries come from suggestions, repeat or recent; typed entries
stay under 15 %. People who never switch the diary on see no change in plan,
*made it* or cook-mode completion. **Not** measured as a goal: calories logged
or "engagement". Guardrails: the share who switch it off within a week, and
the number of Richtwerte near the floor.

---

## 14. Research for the four open questions (22 October 2026)

Desk research by four research passes, with key claims spot-checked. Where a
claim was not checked against its source it says so. None of this is legal
advice. Jakob accepted all four recommendations below on 22 October 2026 (§0).

### 14.1 #1, estimates and Yazio-like logging

**What the trackers do.**
- **Yazio, MyFitnessPal, Lose It!**: large, mostly crowd-sourced databases,
  with a "verified" tick on some entries and many duplicates.
  - Generic dishes come with default portions ("1 Stück", "1 Portion").
  - Quick add takes calories, optionally with macros.
  - Barcode lookups are filled from the same database.
  - A single number is shown, with no sign of how uncertain it is.
  - (Recalled, not checked against the vendors' pages.)
- **Cronometer** names the source of each entry: lab databases (USDA, NCCDB)
  apart from crowd entries. That is the closest model to Culina.

**How accurate any of this is.**
- Crowd databases miss lab values by about 17 % on average for common foods.
  This comes from Tosi et al. 2022, quoted second-hand.
- Restaurant meals ran about 18 % above their stated energy (Urban et al. 2010).
- EU label tolerances are of the order of ±20 %, per the 2012 Commission
  guidance. The exact table was not checked.

**Reasoning.** A cited typical portion is not less accurate than what Yazio
shows. It is only more honest if it is marked as an estimate.

**What Culina already has.**
- **Fact** (`bls.tsv`, checked): BLS 4.0 holds about 2,000 composite dishes,
  coded X and Y. Among them:
  - Döner Kebab (177 kcal/100 g, veal and beef);
  - Currywurst mit Pommes (217);
  - Laugenbrezel mit Butter (403);
  - Lasagne al forno, Kartoffelsalat (14 kinds), 48 kinds of Schnitzel.
- **Gaps:** burgers, a plain pizza Margherita, and branded products.
- **No portion weights.** BLS has none, but Culina's cited typical-weight
  mechanism (`TypicalWeights.cs`, marked "≈") is exactly the missing piece.

**Proposal: three trust levels.** These replace "lookup or typed" in §4.4.

| Level | Source | Shown |
| --- | --- | --- |
| **Nachgeschlagen** | A BLS food or dish in grams. A household package food in grams. A fully counted recipe portion. | The plain number, with the source in the detail view |
| **Geschätzt** | A BLS food or dish × a cited typical portion ("1 Döner ≈ 350 g"). A household unit weight. A partly counted recipe portion. | "≈ 620 kcal". The detail names the basis: "Döner Kebab (BLS) × 350 g, typische Portion". |
| **Von dir** | Quick add, with or without macros | A pencil mark |

- **Ingredient plus amount** is the main path. Search BLS, then enter the
  amount in g, ml, Stück, Scheibe or Portion. Any unit other than grams turns
  the entry into an estimate, as recipes already do.
- **Curated typical portions** for about 100 common dishes and foods (Döner,
  Brezel, Croissant, a slice of bread, an apple). The weights come from cited
  sources (USDA FNDDS portions under CC0, or printed pack sizes), in the
  existing typical-weights table.
- **Day total:** "≈ 1.840 kcal" when any entry is an estimate. Lower bounds
  stay "mind.".
- **README:** "no **hidden** calorie estimates" stays true. Estimates are
  allowed, and they are always marked and always cited.
- **Barcode:** the household names a product once from the label, so the
  next scan is one tap.
  - Open Food Facts stays an admin opt-in. ODbL share-alike applies to a
    stored derived database. A runtime lookup that is shown and not stored is
    the least risky use, but it still makes an outbound call.
  - **Sub-decision:** whether a diary snapshot of an OFF value counts as
    storing. Ask reuse@openfoodfacts.org before building on it.
- **Not doing:** AI photo estimation (weak evidence), restaurant-chain
  databases, and a crowd database.

### 14.2 #2, where the diary lives

**What competitors do** (verified where marked):
- **Plan to Eat** has Cook, Plan and Shop as three peer tabs. Shop is built
  from the plan's dates and is still its own tab (verified).
- **Paprika** keeps Meals and Groceries apart (verified).
- **Cronometer** moved Trends out of its bar so the Diary stays the anchor
  (verified).
- **MacroFactor** puts a quick-add "+" on every main page (verified).
- **Lifesum** shows a Recipes tab only to Premium users. That is the one
  precedent found for a conditional tab (partly verified).
- No verified app merges plan and shopping into one tab.

**Guidance.**
- Material 3: three to five destinations of equal importance (verified).
- NN/g: hidden navigation is the least discoverable (verified).
- Apple's HIG discourages hiding tabs when content is unavailable. This is
  recalled; the page could not be fetched.
- Research on adaptive menus: unexpected changes cost performance, while a
  structure that stays stable and grows is tolerated.

**The options:**

| Option | For | Against |
| --- | --- | --- |
| **a** A fifth tab for everyone | Simple, stable, one tap | A dead tab for most households. Five tabs at 320 px are 64 px each, and "Kochbücher" may clip. |
| **b** Merge Plan + Shopping into "Woche" | Matches "plan, then shop" as one weekly job | Shopping is used in the shop, one-handed, by other members, with ad-hoc items. A segment hides it behind a tap. No competitor does it. It costs every household something to serve an opt-in feature. |
| **c** Merge Rezepte + Kochbücher into "Bibliothek" | The same kind of content, and cookbooks are the least-used tab | Reverses a navigation decision made weeks ago. Changes the bar for everyone. |
| **d** A Tagebuch tab that appears only when the diary is on, at the end of the bar | Non-users see exactly today's four tabs. Users get one tap, and positions never move. | The bar differs per person. Five-tab 320 px check needed. Apple's guidance leans against tabs that come and go, but this one changes only by the person's own choice. |
| **e** A "Heute" hub as the new home | Day-centred, like the trackers | Empty for non-users, or displaces the library |
| **f** Inside Plan | The Today card is already there | Mixes personal and household data. A many-times-a-day task under a weekly view. |
| **g** Today card + account menu only (the first proposal) | No chrome cost | A daily task two taps deep. A snack in the afternoon has no obvious door. |
| **h** A global "+" | Logging from anywhere | Needs a home for the day, and competes with "new recipe" |

**Recommendation: d, plus the Today card line as a shortcut.** Runner-up: c,
if the diary is expected to become a core feature. b last among the real
options.

**Competitors, rechecked 22 October 2026** (after the five-tab bar failed, §7):

- **Yazio** (help centre, checked): its main sections are Diary, Recipes,
  Fasting, Analysis, Profile and Settings. The Diary is "the heart of the app".
  It has no meal plan and no shopping list.
- **MyFitnessPal** (help centre, checked): the Diary became the home tab,
  *Today*, in April 2026.
  - A blue "+" in the bottom bar opens the logging tools.
  - The More menu moved behind a profile icon at the top right.
  - Reported bar: Today, Progress, Plan, +, More (secondary source).
  - The Premium+ meal planner comes "complete with a grocery list", which
    suggests plan and groceries share one Plan tab. That is not checked in the
    app.
- **Samsung Food**: recipes, planner and shopping lists are separate areas.
  Nutrition is per recipe, and tracking is a paid add-on or Samsung Health,
  which is a separate app. Tab names are not checked.
- **Plan to Eat** (Cook, Plan, Shop) and **Paprika**: no diary at all.
- **Where the shopping list sits.**
  - It is its own entry in most apps:
    - Plan to Eat (Shop);
    - Cookidoo, whose 2025 menu reads For You, Explore, My Recipes, My Week,
      Shopping List (checked on cookidoo.international);
    - Mealime, which has a grocery-list icon beside the meal plan (support
      docs, checked; the tab names are not given);
    - Eat This Much, which has a Groceries section (review).
  - Only MyFitnessPal folds it into Plan. The planner (Premium+) "turns your
    plan into a grocery list" on the Plan screen (help centre, checked). A
    review reports Meal Planner and Groceries as two sub-tabs.
- **Where the diary sits.** Trackers make it the home tab: Yazio's Diary,
  MyFitnessPal's Today, Lifesum's Diary. Kitchen apps have none. No app was
  found that puts the diary inside another page. The plan reaches it through
  an action on a meal instead. MyFitnessPal's plan has "Log to Diary" on each
  meal, which is what *Als Portion eintragen* and the suggestion rows (§6) do.

**Pattern.** Trackers make the diary the home and fold everything else
behind it. Kitchen apps keep recipes, plan and shopping as tabs and leave
tracking to another app or tier. No app that was checked has recipes,
collections, plan, shopping and a diary as five peers.

**Precondition:** measure the five-tab bar at 320 px in German and English
before deciding. If "Kochbücher" clips, the fallback is c, not smaller
labels.

### 14.3 #4, is an opt-in text needed?

**Facts.**
- CJEU C-21/23 *Lindenapotheke* (4 October 2024, checked): order data that
  only allow health to be *inferred* are health data under Art. 9.
- The 2015 WP29 annex on lifestyle apps reads raw intake data the same way
  (recalled).
- **Reasoning:** treat a kcal diary as health data.

**Who must comply.**
- A person hosting for their own family is very likely under the household
  exemption (Art. 2(2)(c)). Then the GDPR does not apply to them.
- An operator hosting for a club or friends is a controller, and needs
  explicit consent (Art. 9(2)(a)).
- Culina cannot tell which case it is in, so it should make the strict case
  easy.

**What "explicit" takes.** The EDPB consent guidelines (05/2020, summary
checked) ask for an express statement, which may be written. A switch that is
off by default and carries one consent sentence is an express statement. A
modal is not required.

**Medical device.** No disclaimer is legally required. What makes software a
medical device under the MDR is its intended purpose: claims and features
such as computed targets or treating a condition. "Keine medizinische
Beratung" helps keep that purpose clean, but it is not a shield.

**Proposal.** Drop the explainer and the children / pregnancy /
eating-disorder warnings. Keep:

- The switch **"Ernährungstagebuch"**. Under it, one sentence: *"Deine
  Einträge sind Gesundheitsdaten und nur für dich sichtbar. Mit dem Einschalten
  willigst du ein, dass Culina sie speichert. Keine medizinische Beratung."*
- Switching off asks **"Einträge auch löschen?"** with *Löschen* as the
  primary button and *Behalten* as the other. Keeping data without consent is
  the weaker position.
- `consented_at` and `consent_version` on the diary settings row. No log
  table.
- **Honesty:** "nur für dich sichtbar" means in the app. Someone with access
  to the database can read it, and the help text says so.

### 14.4 #12, motivation: streaks, rings, colours, reminders

**Facts.**
- Logging works through **frequency**. In Harvey et al. 2019 (checked), people
  who lost ≥ 10 % logged about 2.7 times a day, against 1.7. Time spent per day
  made no difference.
- The best-supported retention lever is **less friction**, not rewards.
- **Harm** is real and concentrated in a vulnerable minority. Levinson et al.
  2017, *Eating Behaviors* 27 (checked): 75 % of 105 ED patients used
  MyFitnessPal, and 73 % of those said it contributed to their disorder.
  - That was a clinical sample, without a control group.
  - Most other evidence is correlational. The one RCT recalled found no broad
    harm in students; its abstract was not checked.
  - **Reasoning:** design defensively, without treating every user as a
    patient.
- **MacroFactor** markets itself as "adherence-neutral", with no warnings, red
  numbers or shaming when over target (checked, from its own store text).
- **Mass-market apps** (MyFitnessPal, Lose It!, Yazio, Lifesum) use
  "remaining", red-over and streaks (recalled).
- **Streaks:** evidence for them in diet apps is thin. The plausible risk is
  the what-the-hell effect after a break (Polivy and Herman).

**Proposal.** This softens §8's "none of it".

| Element | Proposal |
| --- | --- |
| Breakable streak | **No.** Instead a count that never resets: "an 5 von 7 Tagen eingetragen". |
| Badges | **No** in the first release. Never for quantities (kcal, weight, deficits). |
| Rings | **No.** The Band shows the same information without the "close it" pull. |
| Red over the Richtwert | **No.** It looks the same as under, as in MacroFactor. |
| "Remaining" | **Optional.** The default shows *gegessen*. A setting switches the headline to *noch übrig*, for people who plan the day by it (Yazio users expect it). |
| Reminders | **Opt-in, off by default.** The person picks the times, at most two a day, in neutral wording ("Mittagessen eintragen?"). Never "you forgot" or "streak at risk". Culina already sends Web Push (VAPID), so this needs a server-side schedule. In the native apps it would be local notifications. |
| Weekly summary | **Yes:** averages and protein pattern, no pass/fail. Already planned in `.10`. |
| Celebrations | **Not for numbers.** At most Olli's one tip at the first entry. |

---

## 15. What was built (22 October 2026)

The MVP, for structure W (Jakob's preference). Where the diary appears follows
from the structure in one place, so a change from W to K later does not touch
the diary.

**Backend**
- Migration `0055_diary.sql`: `diary_settings` and `diary_entries`.
- `Domain/Diary`: `DiaryEntry`, `DiarySettings`, `DiaryFigures` and
  `DiaryErrors`.
- `Application/Users/DiaryLookup`: adding and changing an amount share one
  lookup.
- Seven endpoints under `/users/me/diary` and `/users/me/settings/diary`
  (`api.md`, "Food diary"). All seven are withheld from MCP agents.
- `GET /foods` gains each food's cited typical `units`.
- Tests:
  - domain: 16 unit tests;
  - integration: 9 tests (consent, food, typical unit, recipe snapshot, typed,
    move and re-lookup, privacy, switching off, range);
  - the agent allow-list check.

**Frontend**
- `features/diary`:
  - stores for the settings and the days;
  - `dayWith`, which adds a day up as the server does, so a change shows at
    once;
  - the add sheet, with *Zuletzt*, *Lebensmittel* (the table and the
    household's own), *Eigenes*, and a recipe through the shared
    `RecipePicker`;
  - the amount sheet, which also changes an entry or removes it;
  - the summary, using `Band` from the design system;
  - the Today-card line;
  - `logging.ts`, so every write gets a toast with *Rückgängig*.
- **The diary is the fourth tab, for everyone** (24 October 2026, with W
  chosen; `navigation.ts` `barDestinations`):
  - It is a tab even while the diary is off. Nobody trying the prototype found
    the switch in the settings.
  - While it is off, `/diary` offers "Tagebuch einschalten" with the same
    consent sentence.
  - The bar never changes shape when someone switches it on.
  - The Today-card line and the portion offer still appear only while it is
    on.
- `/diary` shows a day with ‹ ›. Today's planned meals appear as dashed
  suggestions, and an "an N von 7 Tagen eingetragen" count is shown.
- `/me/diary` holds:
  - the switch with its one consent sentence;
  - the Richtwert, with the floor sentence and a help link below 1,200 kcal;
  - the protein target;
  - eaten or remaining;
  - "Einträge auch löschen?" when switching off;
  - a typed-word confirmation for deleting everything.
- After "made it", from cook mode or one tap, someone who keeps a diary gets
  one more toast: *Als Portion eintragen* (`offerPortion` in
  `finishCooking`).
- Tests: diary unit specs, and `tests/e2e/diary.spec.ts` running against a
  mocked diary server (off, on, the settings).

**Where it differs from the plan above**
- **Recent foods are derived in the client** from the last 14 days read. There
  is no `GET /users/me/diary/recent`.
- **A food's units come with `GET /foods`**, not from a separate
  `/foods/{code}/units`.
- **No If-Match on entry writes.** It is one person's own diary, like their
  preferences.
- **No CSV export yet.** Deleting everything and the account cascade are
  built.
- **`Band` uses existing tokens** (`--text-muted`, `--border-strong`,
  `--surface-sunken`), not new `--viz-*` tokens. That keeps it in the token
  lint without a theme change.
- **No portion stepper in `CookedWrapUp`.** The *Als Portion eintragen* toast
  comes from `finishCooking`, so cook mode and one-tap "made it" behave alike.
  The amount sheet changes the portion afterwards.
- **Structure W is the app's structure** since 24 October 2026
  (`culina-v2-eawy.1`, `culina-v2-g5ie.1`). The prototype switch and the K
  header tool are gone.
- **Typical portions for dishes (`.16`) are not added yet.** Units are those
  the existing cited table already has (Zwiebel, Ei, Knoblauchzehe…).
- **Undoing *made it* does not ask; it takes the portion off with its own
  *Rückgängig*** (`takeBackPortion`), and withdraws a portion offer still on
  screen. A question in the middle of a toast's undo would be a dialog for one
  tap; the undo keeps the choice.
- **Migration numbering:** `0055`, after `main`'s `0054_household_products.sql`.
