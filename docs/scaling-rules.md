# Culina — scaling rules

Changing the number of portions is the feature every recipe app has and almost
none gets right. This document is the specification; the implementation is
`src/frontend/src/lib/features/recipes/scaling.ts` and its test suite.

## Where scaling lives, and why in exactly one place

Scaling must be **instant** on the client — no network round trip when someone
taps `+` — and **exact** on the server, which merges amounts into a shopping
list. Implementing it twice is how two implementations quietly disagree.

It splits cleanly instead:

| | Responsibility |
| --- | --- |
| **Server** | Exact decimal arithmetic only. Shopping-list quantities are stored **unrounded**, so merging never compounds rounding error. Knows nothing about how a number is shown. |
| **Client** | Human rounding, which is **presentation**. One pure module, no I/O, exhaustively unit-tested. |

The only genuinely shared thing is the unit vocabulary, and that is a backend
enum surfaced through OpenAPI — one definition, both sides
(`frontend-api-client`).

## The factor

```
factor = targetYield / baseYield
```

A `decimal`/`number` that is never rounded itself. Every displayed amount is
computed from the **base** amount and the factor, never from a previously
scaled amount — scaling twice from a rounded value drifts.

`targetYield` is chosen by the servings control, or derived from
"I have 600 g of flour" (below). It is clamped to `(0, 1000]`.

## Rounding: the rules

The goal is an amount a person can act on. `133.333 g` is arithmetic;
`1⅓ eggs` is honest and useless. Culina prints what a cook would write.

### 1. Mass and volume — round to a usable step

| Raw amount | Step | Example |
| --- | --- | --- |
| `< 10` | `0.5` | `7.3 g` → `7.5 g` |
| `10 – 100` | `5` | `133.3 g` → `135 g` |
| `100 – 1000` | `10` | `433.3 g` → `430 g` |
| `≥ 1000` | `50` | `1333 g` → `1350 g` |

Round half away from zero. Re-express upward when it reads better:
`1500 g` → `1.5 kg`, `2000 ml` → `2 l`. Never downward — `0.5 kg` is shown as
`500 g`, because a scale shows grams.

### 2. Countable things — an honest range, never a fraction

Anything with a count unit (`piece`, `clove`, `slice`, `can`, `pack`, `bunch`)
or no unit at all.

```
3 cloves × 1.5 = 4.5   →  "4–5 cloves"
2 eggs    × 0.67 = 1.33 →  "1–2 eggs"
4 pieces  × 1.5 = 6.0   →  "6 pieces"      exact, no range
1 onion   × 0.4 = 0.4   →  "1 onion"       never round a count to zero
```

- Within `0.15` of an integer, snap to it: `3.9` → `4`, not `3–4`.
- Otherwise show `floor–ceil`.
- **A count never rounds to 0.** The minimum is 1: a recipe that needs an onion
  still needs an onion at half scale.

### 3. Spoons — halves, then thirds

`tsp` and `tbsp` round to the nearest `0.5`, and `⅓`/`⅔` are allowed because
measuring spoons come in those sizes. `1.7 tbsp` → `1½ tbsp`. Below `0.25`,
show `a pinch of` for `tsp`.

### 4. Things that never scale

`pinch`, and any ingredient with no quantity at all ("salt", "pepper to
taste"). They are reproduced unchanged. Salting to taste does not double.

### 5. Mark what was approximated

Any amount the rounding changed by more than 2 % renders with a preceding `~`
and carries `isApproximate: true`. An approximation must never be mistaken for
a measurement — that is the difference between a scaling feature you can trust
and one you have to double-check.

### 6. What the app must admit it cannot scale

Beyond `factor ≥ 1.75` or `≤ 0.6`, a quiet single line appears beside the time,
never a modal:

> Times are for 4 portions — check earlier.

And in the recipe's metadata, permanently:

> Pan size and oven temperature are for the original {n} portions.

Baking time scales with the *thickness* of what is in the tin, not with its
mass; oven temperature does not scale at all; a doubled cake in the same tin is
a raw cake. Culina says so rather than silently lying, and this honesty is
worth more than a cleverer formula.

## Scale to an amount you have

Tap any ingredient's amount → "I have …" → the whole recipe reshapes around it.

```
factor       = availableAmount / baseAmountOfThatIngredient   (converted to a common unit)
targetYield  = baseYield × factor
```

The yield that results is displayed rounded to a sensible value
(`3.7 portions` → `3.5`), and the factor is then recomputed from that rounded
yield, so what the user sees and what the numbers do agree.

This reuses the machinery that already exists and introduces no new concept —
it is simply the same scaling driven from the other end. It is also the real
human moment: a leftover 600 g of flour, an odd package size.

## Display formatting

- Decimals are trimmed: `1.0` → `1`, `1.50` → `1.5`.
- Common fractions render as glyphs where the unit makes them natural
  (spoons, cups): `½`, `⅓`, `⅔`, `¼`, `¾`.
- Locale-aware separators via `Intl.NumberFormat` — `1,5 kg` in German,
  `1.5 kg` in English.
- Ranges use an en dash with no spaces: `4–5`.
- Amount and unit are one non-breaking group so `250` never wraps away from `g`.

## Interaction

- The servings control is a stepper with a tappable value that opens direct
  entry. Steps of 1 for `servings`; for `pieces`, steps that suit the base
  (12 muffins steps by 6, not by 1).
- **Numbers morph in place.** Changed amounts get a ~400 ms highlight so the eye
  can see *what* changed. No reload, no spinner, no layout shift.
- Under `prefers-reduced-motion`, the highlight becomes an instant tint change
  with no transition.
- The chosen scaling is remembered **per recipe, per user** (server-side, in the
  cook session and in user preferences) — you always cook this one for two, so
  it opens at two.
- Scaling **never** mutates the recipe. It is a view, and it is reflected in the
  URL (`?yield=6`) so a scaled recipe can be shared or reloaded intact
  (`sveltekit-state-and-optimistic-ui`).

## Step text scales too

This is the payoff for the ingredient↔step links in `domain-model.md`. Because
a step stores `[[ingredient:…]]` rather than the literal text "200 g butter",
the step renders with the *scaled* amount inline:

> Melt **180 g butter** in the pan.

Getting this wrong — ingredient list scaled, step text not — is the single most
common bug in recipe apps, and the model makes it impossible here.

## Test vectors

`scaling.spec.ts` covers, at minimum:

- Every row of the step table in §1, at the boundaries (`9.9`, `10`, `100`,
  `1000`).
- `kg`/`l` re-expression up, and the absence of re-expression down.
- Counts: snapping, ranges, and the floor of 1.
- Spoons: halves and thirds; the `pinch` threshold.
- Non-scaling ingredients passed through byte-identical.
- `isApproximate` set exactly when the delta exceeds 2 %.
- Scale-to-amount round-trips: choosing 600 g of flour yields a factor that
  reproduces ~600 g of flour.
- Factor `1` is an identity: every amount renders exactly as authored.
- German and English formatting of the same value.
