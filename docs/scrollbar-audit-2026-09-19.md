# Scrollbar audit — 2026-09-19

Tracked as `culina-v2-yxmc`.

Every scrolling container in the frontend was inventoried and checked against
three questions:

1. **Does the bar look like this app?** The warm-paper theme owns every other
   piece of chrome; the scrollbar was the one surface still rendering raw
   platform grey.
2. **Does content run underneath it?** An overlay scrollbar (macOS default,
   iPadOS, Android) is drawn *on top of* the content box. A container with no
   reserved gutter loses its last ~12px of text whenever it scrolls.
3. **Is it the only bar in its stack?** A scroller nested inside a scroller
   produces two vertical bars in one dialog and steals the wheel from the
   wrong element.

## Inventory

| # | Container | File | Axis | Findings |
|---|-----------|------|------|----------|
| 1 | `.results` (recipe picker list) | `lib/features/recipes/RecipePicker.svelte:209` | y | **A, B, C** |
| 2 | `.body` (every dialog and sheet) | `lib/design-system/overlay/Dialog.svelte:169` | y | **A, B** |
| 3 | `.panel` (popover) | `lib/design-system/overlay/Popover.svelte:168` | both | **A, B, D** |
| 4 | `.list` (autocomplete suggestions) | `lib/features/recipes/editor/SuggestionList.svelte:67` | y | **A, B, F** |
| 5 | `.picker` (per-step ingredient picker) | `lib/features/recipes/editor/StepIngredients.svelte:173` | y | **A, B** |
| 6 | `.list` (filter sheet tags) | `lib/features/recipes/filters/FilterSheet.svelte:181` | y | **A, B, F** |
| 7 | `.entries` (cookbook rule editor) | `lib/features/cookbooks/RuleEditor.svelte:178` | y | **A, B, F** |
| 8 | `.strip` (attempt strip) | `lib/features/cooking/AttemptStrip.svelte:175` | x | **A, E** |
| 9 | `.shelf` (similar recipes) | `lib/features/recipes/SimilarRecipes.svelte:83` | x | **A** |
| 10 | `.track.walkable` (suggestion deck) | `lib/features/recipes/SuggestionDeck.svelte:158` | x | bar hidden on purpose — no change |
| 11 | `.saved` (saved-search chips) | `lib/features/recipes/filters/LibraryToolbar.svelte:310` | x | bar hidden on purpose — no change |

## Findings

### A — No scrollbar is themed (all 9 affected containers)

The app defines tokens for every other piece of viewer chrome and sets
`color-scheme` so the browser tracks light/dark, but nothing ever set
`scrollbar-color`. The result is the platform default: a grey rail with a
blue-grey thumb on Windows and Linux, and the same on macOS for anyone with
*Show scroll bars: Always*, sitting directly on a cream `--surface`. This is
the bar in the reported screenshot.

**Fix:** one app-wide treatment in `app.css` — `scrollbar-width: thin` plus a
tokenised `scrollbar-color`, with a `::-webkit-scrollbar` block for Safari and
older Chrome, which do not implement the standard properties. Both branches
resolve from `--border-strong` / `--text-subtle`, so dark mode follows for
free. The two containers that deliberately hide their bar set
`scrollbar-width: none` on a class, which outranks the universal selector.

### B — Content runs under the bar (containers 1–7)

None of the vertical scrollers reserved space for the bar. In the reported
screenshot this is why `3 Portionen` is sliced down its right edge: `.meta` is
`white-space: nowrap`, so it cannot reflow out of the way, and the overlay
scrollbar is painted over the last glyphs.

**Fix:** two mechanisms, because they cover different machines and neither is
sufficient alone.

`scrollbar-gutter: stable` handles the machines where the bar takes layout
width — Windows, Linux, macOS set to *Show scroll bars: Always*. The gutter is
reserved whether or not the bar is currently drawn, so the rows also stop
shifting sideways the moment the list grows past its cap.

It does **nothing** where the bar is an overlay (macOS by default, iPadOS,
Android), which is the configuration in the reported screenshot. An overlay bar
occupies no layout space at all, so there is no width for the gutter to
reserve; the bar is simply painted on top of whatever is under it. Measured on
the running app, `offsetWidth - clientWidth` on the picker's list is `0`. So
each scroller also gets an explicit `padding-inline-end: var(--space-2)`, which
is what actually holds the ends of the lines out from under the bar. Verified
side by side against the real stylesheet: with the padding the meta column
clears the thumb, without it the thumb is drawn across `4 Portionen`.

`Dialog`'s body is the exception — it gets the gutter only, because its own
`var(--space-6)` inline padding already holds its content clear of an overlay
bar.

### C — Two scrollbars in one dialog (container 1)

`Dialog`'s `.body` already scrolls, and its panel is capped at `85dvh` (phone)
or `100dvh - var(--space-16)` (desktop). The recipe picker then capped its
results list at `50dvh` and scrolled that too — a second viewport-proportional
scroller holding essentially the whole dialog, inside the first. On a short
viewport (a phone in landscape, a half-height desktop window) both bars are
drawn at once, about ten pixels apart, and the wheel goes to whichever the
pointer happens to be over.

**Fix:** `Dialog` now recognises a body whose content manages its own height —
a child marked `data-fills-dialog` — and stops being a scroller itself,
becoming a flex column that hands its height to that child. The picker marks
its wrapper and drops the `50dvh` cap, so the list is the only scroller. That
also fixes something the cap was working around badly: the search field is now
genuinely pinned above the results instead of being the first thing to scroll
away from the list it filters.

Containers 6 and 7 are *not* this bug. Each is a small fixed-height list that
is one field among several in a form — the same shape as a multi-select — so a
bounded inner scroller there is correct. They were only missing the gutter and
the scroll containment below.

### F — Scrolling an inner list drags the sheet behind it (containers 4, 6, 7)

None of the three bounded lists set `overscroll-behavior`. Reaching the end of
the tags, the rules or the autocomplete suggestions chained the remaining
scroll to the sheet or page behind — so the field the list belongs to slid out
from under it mid-gesture.

**Fix:** `overscroll-behavior: contain` on each, matching what the dialog body
and the per-step ingredient picker already did.

### D — Popover could scroll sideways (container 3)

`overflow: auto` applies to both axes. The popover's height is capped in JS by
`place()`, which is correct, but the width is capped in CSS by `max-width`,
and any content wider than that produced a horizontal bar under the panel
instead of wrapping.

**Fix:** `overflow-x: clip` with `overflow-y: auto`, so only the axis that
`place()` actually manages can scroll.

### E — Attempt strip clips its own focus ring (container 8)

`.strip` has `padding: 0 0 var(--space-1)`. A scroll container clips at its
padding box, so the focus ring on the first and last attempt is cut off on the
inline axis. `SimilarRecipes` already worked around exactly this with
`padding: var(--space-1)` and a comment; the strip never got the same fix.

**Fix:** match `SimilarRecipes` — pad the inline axis too.

## Not changed

Containers 10 and 11 hide their scrollbar with `scrollbar-width: none` and a
`::-webkit-scrollbar { display: none }`, each with a comment giving the reason
(a single-panel deck should not show a rubber band; a one-line chip row should
not give up a line of height to a bar). Both reasons still hold, and both
rows are reachable by drag, wheel and keyboard. Left as they are.
