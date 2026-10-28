# Culina UI review, second pass — 28 October 2026

Epic: **culina-v2-6ilt**, 18 findings. This pass ran after the fixes from epic culina-v2-y6ti landed, so it looks at the current state. Nothing already tracked was refiled. Comments were added to culina-v2-mucd, culina-v2-y6ti.10 and culina-v2-ozzs.

## References

- [Vercel Web Interface Guidelines](https://vercel.com/design/guidelines): hit targets, links vs buttons, consistent text actions, loading/error states.
- [Rauno Freiberg, Invisible details of interaction design](https://rauno.me/craft/interaction-design): responsiveness, Fitts's law, origin of motion.
- Refactoring UI (Wathan/Schoger), via summaries: hierarchy through size, weight and spacing before colour; tinted greys.
- Apple HIG: [Layout](https://developer.apple.com/design/human-interface-guidelines/layout) (size classes, keep function constant across sizes), [Color](https://developer.apple.com/design/human-interface-guidelines/color) (one colour, one meaning; colour sparingly on controls), [Typography](https://developer.apple.com/design/human-interface-guidelines/typography), [Text fields](https://developer.apple.com/design/human-interface-guidelines/text-fields).

## Coverage

These pages were checked in the in-app browser, German, light theme: library, recipe detail, cooking, editor, new recipe, import, cookbooks, Woche plan and list, Ernährung diary, and every settings page. Sizes: 320x568, 390x844, 844x390, 540x720, 673x841, 744x1133, 1024x1366, 1180x820, 1280x800, 1440x900, 1920x1080, 2560x1080, 3440x1440. An iframe sweep over 11 pages × 11 sizes found **no horizontal overflow**, and wide screens stay contained. Real devices, dark mode and text scaling remain open in culina-v2-y6ti.16.

## Findings

| Issue | P | Finding |
| --- | --- | --- |
| 6ilt.1 | P2 | Header, dock and resume pill stack up on landscape phones and foldables |
| 6ilt.2 | P2 | 'Kochen beenden' sits under the desktop cooking dock, partly off screen |
| 6ilt.3 | P2 | Recipe actions are four bare icons on phones (labelled on desktop) |
| 6ilt.4 | P2 | Decision: icon-only bottom dock (committed in 1dad6a9b) vs labels |
| 6ilt.5 | P2 | Settings pages use three different layout patterns |
| 6ilt.6 | P2 | Settings mix instant-save and explicit Speichern, even within one page |
| 6ilt.9 | P2 | The 'nothing logged today' diary line repeats on Rezepte and Woche |
| 6ilt.11 | P2 | Standalone controls smaller than 24px (step ingredient chips, '+ Notiz', editor links) |
| 6ilt.7 | P3 | 'Verwaltung' rail heading centred on desktop (CSS leak, me/+layout.svelte:176) |
| 6ilt.8 | P3 | Import cookbooks carry ISO dates in their names |
| 6ilt.10 | P3 | Five different text-link looks |
| 6ilt.12 | P3 | Decision: field surface (see below) |
| 6ilt.13 | P3 | Decision: one place for each page's primary action |
| 6ilt.14 | P3 | Several filled olive buttons per settings page |
| 6ilt.15 | P3 | 'Gruppe ohne Namen' vs '+ Zutat zu Gruppe 1 hinzufügen' |
| 6ilt.16 | P3 | A kcal ⓘ button on every recipe card |
| 6ilt.17 | P3 | Desktop recipe detail: ingredients start below the fold, and the dock covers the steps |
| 6ilt.18 | P3 | Settings page repeats a per-section error when everything failed |

## Field colour (the cream question)

The navbar colour is `--c-sand-150` (#ece9e1), which is exactly `--field-surface-disabled`. Applied to fields on the cream page, enabled inputs read as disabled. Inside white grouped cards a cream fill reads well, like an inset well. Recommendation: white fields on the page, a subtle cream fill only inside white groups, and a disabled state with its own cue. See `fields-*.png`.

## Side effects

Opening /cook started a cooking session; it was ended through 'Kochen beenden' without recording a meal. A temporary dev server on 5177 was started and stopped, and `.claude/launch.json` was restored. No application code was changed.
