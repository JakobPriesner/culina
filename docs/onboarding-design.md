# Onboarding: Olli's welcome, Olli in place, and a voice in the kitchen

Culina is one loop: **capture** a recipe, **plan** it into the week, **shop** for it, **cook** it, **remember** what
you would change. A new cook meets four tabs and an empty library; this is how the app explains itself without a
manual and without nagging.

## Principles

- **Show it working, then let them try.** Every scene of the welcome is a small working picture of the real
  thing (the step being written, the list filling up, a timer ringing), not a paragraph about it.
- **Olli is the guide.** Olli says one short thing per scene in a speech bubble and answers what the cook tries
  ("Exactly! Ingredients, steps and the shopping list always scale along."). The pose matches the moment:
  writing for steps, thinking for the week, on duty for cooking, celebrating at the end.
- **Short.** Six scenes, about a minute, skippable on every one; it ends on the first recipe, not a summary.
- **Where it is needed.** The rest of the guidance is Olli beside the thing it explains, the first time it is
  used, once.
- **Never in the way.** No tip blocks a control, none comes back after "Got it", and cook mode only gets one quiet
  tip, because somebody at a hob has wet hands.
- **Calm when asked.** With reduced motion (system) or Olli's motion off (Settings), every demo shows its end
  state at once and nothing plays by itself.
- **Undo is in Settings.** "Take the tour again" and "Show tips again" live under Account.

## The welcome (`features/onboarding/Welcome.svelte`)

A `Stage` (the design system's full-screen dialog: the whole phone, a large panel elsewhere). Swipe or the arrow
keys move between scenes, as in cook mode (`cooking/stepGestures`); the dots jump.

| Scene | Olli | Try it |
| --- | --- | --- |
| Welcome | hello | The loop as a ring: Collect, Plan, Shop, Cook, each naming its tab. A marker goes round by itself until a station is tapped; Olli then points at the bar where the tabs live. |
| Get recipes in | reading | Link, Photo, Just a name or From an app: a link is typed and read into a recipe card, a page is scanned, a name alone lands on Wednesday, or a collection is ticked over from another app into a cookbook named after it. The apps are named from `features/import/sourceApps.ts`, the one list the connect form, token links and hints also read, so a newly supported app appears here without touching the tour. |
| Steps that help while cooking | writing | A step types itself: `@Zwi` offers "Zwiebeln" and becomes the ingredient mark of the read view, "5 Minuten" becomes a timer. Then a pointer glides to the servings stepper and presses "+" twice: the amounts in the step and in the ingredient line pop as they change, and "×2" appears beside the stepper, before Olli hands over ("Your turn!"). With motion off, Olli asks the cook to press "+" instead. Three rules under it: one action per step, one to three sentences, waits in minutes. |
| Plan, then shop | thinking | Tap a day (or wait: Wednesday is planned for you) and the list writes itself; tick lines off. |
| Cook with your hands free | on duty | A phone in cook mode: the step, its timer (run fast), Read aloud (the device's own voice), Next, and "I made it". |
| Let's go | celebrating | The new-recipe page's doors (`?start=paste`, `?start=photo`, `?start=idea` where drafting is on, else the plain form, and importing from an app), or "Look around first". |

It opens by itself once, right after a new cook's first household: the step after signing up (`/welcome`), or
an invitation that lands in one. Everyone can take it again from the account menu or Settings. Cooks who were
here before it existed are not interrupted by it: an inline offer on the library was tried and dropped, because
it pushed the library's search and cards down for people who did not ask for anything.

The household step itself (`/welcome`) is Olli's first line: "Hi, I'm Olli, your kitchen helper."

## Olli in place (`features/onboarding/CoachTip.svelte`)

Each tip is Olli (small) with a speech bubble (`OlliSays`), in the page's flow.

| Where | Id | Olli | Tip |
| --- | --- | --- | --- |
| Editor, steps | `steps` | writing | The step-writing rules from the welcome's step scene. |
| Cook mode | `cook` | on duty | Swipe or tap the screen for the next step; start timers from the bar; turn on reading aloud. |
| Week | `plan` | thinking | Drag meals between days; tick a meal you cooked; the list fills from the week. |
| Shopping list | `shopping` | peeking | Tap a line to tick it; the meals behind the list are above it and can be taken off whole. |

Seen tips are remembered per person on the device (`localStorage`, `culina.tips.v1.<userId>`): it is a UI
preference, nothing private, and it is fine for a new phone to show the tips once more.

## Reading aloud (`features/cooking/readAloud.svelte.ts`)

A toggle in cook mode's tools reads the current step, and each next one, through the browser's speech
synthesis, in the recipe's language. Amounts are read as scaled ("200 Gramm Butter"), from the same scaling as
the screen, so what is heard matches what is shown. It stops when cooking ends or the page is left, and the choice
is remembered on the device. Where the browser has no speech synthesis, the toggle is absent.

## Not done (yet)

- Voice commands ("next"): media keys and a remote already advance steps (`cooking-remote`), and speech
  recognition is unevenly supported and privacy-sensitive (often sent to a vendor).
- Server-side tip state across devices: not worth a table for a handful of booleans.
