# Accessibility and performance

A pass before the 1.0 tag, rather than a checkbox on every ticket. What was
checked, what it found, and — the part that matters more — what is now checked
on every change so the answers cannot quietly go back.

| | |
| --- | --- |
| Reviewed | 13 September 2026 |
| Target | WCAG 2.1 AA, and a first load small enough for a kitchen at the back of a house |

## What runs on every change

- **axe over twenty routes**, signed out and signed in, in light and dark, at
  `wcag2a`, `wcag2aa`, `wcag21a` and `wcag21aa`. Sign in, register, the recipe
  list, a recipe, cooking, the editor, the shopping list, the profile.
- **A keyboard walk**: the skip link is the first stop on every screen, every
  control on the recipe surface is reachable, everything focusable is a real
  control rather than a `div`, and cooking can be driven end to end without
  touching the screen.
- **Layout shift** on the list routes, held under 0.02 — Google calls 0.1
  "good"; Culina reserves space for everything that arrives late, so anything
  above a rounding error means a box was forgotten.
- **A weight budget**, printed on every run: first load, all JavaScript, all
  styles, gzipped.
- **The token contrast contract**, which proves every theme defines every token
  and that the pairs the UI stacks meet AA in both modes.

## What it found

**Cooking dimmed its own text to 2.7:1.** Non-current steps were faded to `0.45`
opacity and the header to `0.55`. The intent — quieter, not hidden — was right;
opacity was the wrong way to express it. It blends text toward the background by
an amount no palette review can see, and the theme's contrast test cannot reach
it, because every token involved is correct. They recede with `--text-muted`
now.

**"Pick it back up" was the accent colour on an accent-tinted surface**: 3.7:1
in light, 2.9:1 in dark. Two colours a few degrees apart is what makes the tint
work as a background and what makes it unreadable as text on top of itself.

**The editor had no heading and its step fields had no labels.** A screen reader
landing there was told nothing about where it was, and each step was an unnamed
box.

**The last step took focus with it.** Next and "I made it" were two controls
swapping places, so reaching the last step put a keyboard user back at the top
of the page at exactly the moment the only remaining action was finishing.

**The ingredient list said "No ingredients written down yet"** on a step that
names none — about a recipe with seven.

## Numbers

Measured on the built app, gzipped:

| | Measured | Budget |
| --- | --- | --- |
| First load (document, entry scripts, styles) | 55.8 kB | 80 kB |
| All JavaScript, every route | 97.4 kB | 140 kB |
| All styles | 12.0 kB | 24 kB |

Raise a budget deliberately, in a commit that says what was added and why it was
worth it — never to make a build pass.

## A known flake

`cooking can be driven without touching the screen` fails in roughly one full
suite run in five, only when the whole suite runs in parallel and never when the
file runs on its own. The assertion is that focus stays on the primary control
across the last step; when it fails, focus is on `body`, which is what a browser
does when the focused element becomes disabled. The control is disabled until
the cook session has loaded — deliberately, because a tap that silently does
nothing reads as a broken button — so something is briefly taking that session
away under load. It has not been found. It is recorded here rather than deleted
or retried away, because a test that is quietly marked flaky is a test that has
stopped meaning anything.

## What was not done, and why

**No screen-reader pass with VoiceOver or NVDA.** Nothing automated substitutes
for it: axe cannot tell whether a heading structure makes sense to somebody
listening, whether a live region interrupts at the wrong moment, or whether the
cooking transition is followable without sight. It is the largest remaining gap
in this document and it needs a person with a screen reader they use daily —
ideally not the person who wrote the app, who knows where everything is.

**No Lighthouse score.** The number is made of measurements that are checked
directly here — layout shift, transfer weight, and accessibility violations —
and a score is a worse way to hold a line than the thing it summarises. Running
it before a release is still worth doing on real hardware, where the numbers
mean something; a score from a laptop plugged into the wall does not.

**No throttled-connection budget for images.** Recipe photographs are the
largest thing on the page and they are served content-addressed, in three
widths, cached immutably, and behind an aspect-ratio box that reserves the
space. What is not measured is the largest contentful paint on a slow
connection, which needs a device lab rather than a CI runner.
