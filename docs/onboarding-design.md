# Onboarding: a short tour, tips where they are needed, and a voice in the kitchen

Culina is one loop: **capture** a recipe, **plan** it into the week, **shop** for it, **cook** it, **remember** what
you would change. A new cook meets four tabs and an empty library; this is how the app explains itself without a
manual and without nagging.

## Principles

- **Short.** The tour is five cards, about a minute, skippable on every card. Nothing in it is required.
- **Where it is needed.** Most guidance lives next to the thing it explains and appears the first time that thing
  is used (writing steps, cooking, the week, the list), once.
- **Show, don't list.** Each card has Olli and a small picture of the real thing (the tabs, a step with an
  ingredient reference and a timer), so the words can be few.
- **Never in the way.** No tip blocks a control, none comes back after "Got it", and cook mode only gets one quiet
  line, because somebody at a hob has wet hands.
- **Undo is in Settings.** "Take the tour again" and "Show tips again" live under Account.

## The tour (`features/onboarding/KitchenTour.svelte`)

| Card | Olli | What it says |
| --- | --- | --- |
| Welcome to your kitchen | hello | The loop and the four tabs: Recipes (your collection), Cookbooks (shelves, also self-filling), Week (what is cooked when), Shopping (fills itself from the week). |
| Get recipes in | reading | Paste a link or text, photograph a page, or keep just a name; a name is enough to plan a dish and fill in later. |
| Steps that help while cooking | writing | One step is one thing your hands do without looking back; start a new one when they do something new or when you wait (give it a timer); one to three sentences, so it fits a screen in cook mode; type `@` to reference an ingredient so its amount follows the servings and cook mode shows it with the step. Example below. |
| Plan, then shop | thinking | Put recipes on days; the list fills itself with what the week needs, once, minus what you untick as already at home; a bigger batch can leave leftovers for another day. |
| Cook with your hands free | celebrating | Big steps, swipe or tap on, timers that ring, the screen stays awake, steps read aloud; afterwards "I made it" keeps notes and photos for next time. |

It opens by itself once, right after a new cook's first household: the step after signing up (`/welcome`), or
an invitation that lands in one. Everyone can take it again from the account menu or Settings. Cooks who were
here before it existed are not interrupted by it: an inline offer on the library was tried and dropped, because
it pushed the library's search and cards down for people who did not ask for anything.

## Tips in place (`features/onboarding/CoachTip.svelte`)

| Where | Id | Tip |
| --- | --- | --- |
| Editor, steps | `steps` | The step-writing rules from the tour card, with the same example. |
| Cook mode | `cook` | Swipe or tap the screen for the next step; start timers from the bar; turn on reading aloud. |
| Week | `plan` | Drag meals between days; tick a meal you cooked; the list fills from the week. |
| Shopping list | `shopping` | Tap a line to tick it; the meals behind the list are above it and can be taken off whole. |

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
