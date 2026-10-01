# Changelog

All notable changes to Culina are recorded here, in the format described by
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow
[semantic versioning](https://semver.org/spec/v2.0.0.html), where a major
version is reserved for a change that requires an operator to do something.

## [Unreleased]

Everything below is the first release, still being assembled.

### Added

- Recipes: written one line at a time, with the amounts in the step text
  derived from the ingredient list rather than repeated in it.
- Scaling that is honest: changing the servings changes the amounts inside the
  instructions, rounds the way a cook writes, and says so when the times and
  the tin stop being trustworthy.
- Cooking as a change of emphasis rather than a change of screen, with timers
  that survive the app being closed and a bar that brings you back to the step
  you left.
- A shopping list that merges amounts across recipes and groups them in the
  order a shop is walked, with per-household corrections that are remembered.
- Households: shared recipes and a shared list, with personal notes that stay
  personal. Invitations are made, copied and taken back from the app.
- Deleting a recipe, a cookbook or a household is recoverable for 30 days:
  an Undo on the spot, and a "Recently deleted" list in the household
  settings that anybody in the household can restore from. After that a
  daily job removes it for good, photographs included. An owner can delete a
  household from its settings, and restore it from there or from the welcome
  screen.
- A way back in for somebody who forgot their password, without email: ten
  recovery codes to save while you still know it, or a one-day code the
  administrator makes for you. Changing the password, or resetting it, signs
  out the other devices.
- Search that survives how people actually type. A typo (`Bolgnese`), a German
  word spelt the other two ways (`Bolognäse`, `Muesli`), a singular where the
  recipe says plural (`Tomate` for `Tomaten`) and a compound noun (`Hähnchen`
  for `Hähnchenbrustfilet`) all find what they meant, and step text is searched
  as well as titles and ingredients. Results are ordered by what was asked
  rather than by what was edited last: an exact title first, then the words of
  one, then a near miss, then a tag or an ingredient, then the method — an
  order that holds whatever the scoring inside it is later tuned to.
- German and English throughout, compiled at build time.
- An installable PWA that opens with no network, and keeps a recipe you have
  opened readable without one.
- One container image serving the API and the app from the same origin, for
  `linux/amd64` and `linux/arm64`, signed and with an SBOM.
- Errors in the browser reach the operator: the web app reports what nobody
  caught, failed renders, service-worker failures and policy violations to the
  server, which logs and exports them through the same OpenTelemetry pipeline
  as its own.
