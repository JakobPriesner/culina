# Culina UI quality-of-life review — 28 October 2026

Epic: **culina-v2-y6ti**. **19 new findings**, **six existing issues linked with fresh review evidence**, and a separate validation follow-up (**culina-v2-y6ti.16**). The original review task is **culina-v2-y6ti.1** and the interaction-polish follow-up is **culina-v2-y6ti.19**. Remediation and design choices remain open for discussion.

The warm paper/olive palette, editorial headings, system-font controls, compact recipe rows, recent mobile sheets, and desktop plan/list pairing provide a strong foundation. The clearest opportunity is to make the amount and placement of interface controls adapt better to the available space. Broad recoloring would offer less benefit than fixing action collisions, narrow-screen density, and inconsistent form grouping.

## Design framework read

- [Jakob Nielsen: 10 usability heuristics](https://www.nngroup.com/articles/ten-usability-heuristics/) — consistency, visible status, error prevention, recognition, and focused interfaces.
- [Bruce Tognazzini: First Principles of Interaction Design](https://asktog.com/atc/principles-of-interaction-design/) — discoverability, predictable behavior, readable controls, and testing aesthetic decisions against usability.
- [Luke Wroblewski: Web Application Form Design](https://www.lukew.com/ff/1502/web-application-form-design) — related field grouping, label alignment, and primary/secondary action hierarchy.
- [Dieter Rams: ten principles through Vitsœ](https://www.vitsoe.com/us/about/good-design) — unobtrusive, understandable design and care in details. This is supporting product-design context, not web-specific research.
- Apple Human Interface Guidelines: [Layout](https://developer.apple.com/design/human-interface-guidelines/layout), [Color](https://developer.apple.com/design/human-interface-guidelines/color), [Typography](https://developer.apple.com/design/human-interface-guidelines/typography), and [UI design tips](https://developer.apple.com/design/tips/). Applied principles include adaptable hierarchy, restraint, legibility, and comfortable targets. Native point sizes are a design benchmark here; the measured web rectangles are CSS pixels, and are not a WCAG audit.
- Culina’s own [design system](/Users/jakob/WebstormProjects/culina-v2/docs/design-system.md) was also read: warm neutrals, two typographic voices, semantic tokens, and selective primary emphasis.

## New findings

P1 = unintended-action risk; P2 = material usability/layout improvement; P3 = polish or a design choice requiring discussion.

| Issue | Priority | Finding and direction |
| --- | --- | --- |
| culina-v2-y6ti.15 | P1 | Separate overlapping remove-from-cookbook and calorie-info targets; the overlap currently resolves to remove. |
| culina-v2-y6ti.3 | P2 | Replace three wrapped rows of mobile settings destinations with a compact, discoverable structure. |
| culina-v2-y6ti.4 | P2 | Remove repeated row/control labels while preserving accessible labeling. |
| culina-v2-y6ti.6 | P2 | Prevent the cookbook overflow button from sitting alone on a second toolbar row. |
| culina-v2-y6ti.7 | P2 | Keep a meaningful recipe name in the editor rail; shorten the steady autosave wording. |
| culina-v2-y6ti.8 | P2 | Increase the diary date targets from about 26px wide at 320px. |
| culina-v2-y6ti.9 | P2 | Prevent Kohlenhydrate from breaking into Kohlenhydrat / e. |
| culina-v2-y6ti.10 | P2 | Bring the first planned day upward by consolidating the phone week controls. |
| culina-v2-y6ti.11 | P2 | Reduce the height of the reading-page action dock above bottom navigation. |
| culina-v2-y6ti.13 | P2 | Stop a delayed create result from replacing a subsequently selected import flow. |
| culina-v2-y6ti.17 | P2 | Stack actions earlier so long recipe titles are not squeezed into a skinny tablet column. |
| culina-v2-y6ti.2 | P3 | Decide shared field surfaces; compare subtle cream fields on white surfaces while preserving distinction inside cream groups. |
| culina-v2-y6ti.5 | P3 | Align privacy/supporting text with the inset of the card it belongs to. |
| culina-v2-y6ti.12 | P3 | Make the intentional empty-draft option explicit or require the promised title. |
| culina-v2-y6ti.14 | P3 | Reduce repeated filled-olive merge actions and restore a clear primary-action hierarchy. |

Each Beads issue contains reproduction/context, impact, a proposed direction, acceptance criteria, and an evidence path. Issue .2 is explicitly a design exploration rather than a claim that white inputs are broken. Issue .13 records one observed navigation race; it still needs a delayed-response interaction test during implementation.

**Follow-up findings**

| Issue | Priority | Finding and direction |
| --- | --- | --- |
| culina-v2-y6ti.18 | P2 | Registration validation skips the first invalid name field and focuses email. |
| culina-v2-y6ti.20 | P2 | Keep diary search, selection and amount entry in one reversible sheet flow. |
| culina-v2-y6ti.21 | P3 | Prototype a connected recipe-card/photo to detail transition, with a fast reduced-motion fallback. |
| culina-v2-y6ti.22 | P2 | Preserve familiar content and geometry on repeat visits while refreshing honestly. |

## Seamless interaction follow-up

The user clarified that the mobile profile popup's clean, fluid feel is a benchmark for the rest of Culina. This follow-up distinguishes existing strengths, observable friction and proposed enhancements.

The shared sheet treatment already works well for profile, ingredient scaling and nutrition. Profile → Settings dismisses the sheet and reaches the destination; a transient snapshot during the exit must not be mistaken for a stuck overlay. Culina already documents press feedback, moving selection indicators, steady navigation, page dissolves, matching overlay entrances/exits and reduced-motion behavior in [its motion design](/Users/jakob/WebstormProjects/culina-v2/docs/motion-and-native-design.md). Preserve that foundation.

The next improvements should make whole journeys coherent:

- **One reversible surface for one task (.20).** Food search → amount currently changes from a tall search sheet to a short amount sheet. There is no Back action in the amount stage. Close and reopening entry retains the search, but takes extra actions. Add a clear return within the same journey, retain query/results position, and make any height/content transition keyboard-safe.
- **A clear connection between a selected recipe and its detail (.21).** Review a subtle shared-photo/title prototype. This is a proposed enhancement, not an assertion that the current dissolve is broken. Preserve rapid navigation, Back restoration and the no-photo/reduced-motion fallback.
- **Fewer visual resets (.22).** Distinguish first loading from revisiting and refreshing. Retain valid same-context content and final layout geometry when safe, with honest local status instead of replaying a whole-screen skeleton. This is not a diagnosis of backend latency.
- **Calm controls before added animation.** The existing dock, week/header and tablet-heading findings remain important: motion cannot compensate for controls obscuring content. Feedback belongs near the action, should not move the next target, and should leave the user free to act again immediately.

This direction follows [Apple HIG Motion](https://developer.apple.com/design/human-interface-guidelines/motion) and [NN/G's role of motion in UX](https://www.nngroup.com/articles/animation-purpose-ux/): use short, purposeful, optional feedback and continuity. No blanket slide animation, bounce, number rolling, extra Olli loop or interaction-blocking transition is proposed. Timing and frame pacing still need implementation-time/device validation.

The motion review itself is tracked in **culina-v2-y6ti.19**. The three new motion items are review/prototype proposals; no UI code was changed.

## Registration follow-up

After the user enabled registration, the actual form was reviewed at **320×568, 390×844, 844×390 and 1440×900**. The light layout fits without horizontal overflow, uses readable 17px inputs, supports scrolling to the actions in landscape, and presents clear associated field errors. Empty submission focuses email while the first invalid field is name; **culina-v2-y6ti.18** records this separately.

No password was entered, no account was created and no successful registration/email-verification flow is claimed. See [phone form](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/registration-phone.jpg), [validation](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/registration-validation.jpg) and [desktop form](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/registration-desktop.jpg).

## Existing findings retained without duplicates

The epic tracks these issues without moving them out of the earlier audit epic.

| Existing issue | Evidence from this review |
| --- | --- |
| culina-v2-kurc | The first library row remains below the phone’s initial screen, after the header, toolbar, suggestion and pager. |
| culina-v2-tkib | At 320px the count and sort wrap onto multiple lines in the toolbar. The old automated height threshold was not rerun. |
| culina-v2-gezn | The cooking dock measures 272px at 320×568 (about 48% of viewport height) when a timer suggestion is present; landscape auto-scroll leaves part of the active step behind the dock. |
| culina-v2-mucd | The filter sheet still says “Fertig” without a result count. Tag display-name/slug casing was not independently re-proven in this household. |
| culina-v2-wpyv.11 | Ingredient settings are 10,266px high at 1024px width; nutrition starts around y=9,883, after the catalogue. |
| culina-v2-wpyv.10 | The live suggestions label Lasagneblatt → Penne as the same food. No merge was executed. |

## Recommended design direction

Preserve the current warm palette and the improved mobile sheet pattern. Make controls more compact and contextual on short screens, give content priority over repeated navigation/status, and keep the stronger olive fill for a small number of meaningful actions.

For fields, prototype a very light cream fill on white cards and sheets. Keep white or another clearly distinct surface inside cream editing groups. The field should remain identifiable, with the same border, focus, error, and disabled language everywhere. Reusing the navbar’s translucent material verbatim is not necessary. Compare light and dark examples before choosing.

For discussion, I would address overlapping cookbook actions first, then the cooking dock and tablet title/action layout, then mobile week/settings navigation. Agree on the surface treatment before applying it to forms.

## Browser coverage

This was a manual review of the current local working tree at http://localhost:5173, in the in-app browser, German/light appearance. It was not a full cross-browser certification.

| Area | What was used |
| --- | --- |
| Recipes | Library, search query/results/reset context, filters, recipe detail, portions/ingredient affordances, editor and inline ingredient editing, new-recipe form, pasted-recipe preview/cancel, missing/deleted-recipe recovery. |
| Cooking | Start/resume, next step, notes panel, portrait and landscape controls, timer suggestion. No timer or speech playback was started; no “Hab ich gekocht”/completion action was submitted. |
| Cookbooks | Collection list/detail, create dialog, automatic-rule fields, add-recipe picker, action placement. No existing collection content was changed. |
| Week and shopping | Phone Plan/List switch, day/week view, add-meal picker, desktop paired layout, shopping input/autocomplete and recipe picker. The current household’s plan/list were empty; a populated plan or shopping list was not manufactured. |
| Diary | Empty and existing populated days, date selection, food search, amount preview, manual-entry form, recent-entry presentation. No diary entry was saved or changed. |
| Settings | Account, appearance, security, household, ingredients/search, nutrition, assistant, mail and server. Sensitive actions and settings saves were not submitted. |
| Tour | All six guided-tour panels were stepped through; the tour was closed. |
| Entry/gated pages | Sign-in, recovery form, initially disabled and subsequently enabled registration form with empty-field validation, invalid/missing email-link and confirmation states, invalid agent-authorization state, and connect/setup/welcome redirects. Successful token-dependent states were not available. |

Viewport samples, used on representative pages rather than every possible page/state combination:

- Phones: **320×568**, **390×844**, **844×390**.
- Foldable-sized rectangles: **540×720**, **720×540**.
- Tablets: **768×1024**, **1024×768**, **1024×1366**.
- Desktop and ultrawide: **1440×900**, **1920×1080**, **2560×1080**, **3440×1440**.

The wide library stayed contained and used an appropriate grid. The desktop week/list pairing was a positive reference. Browser viewport resizing does not reproduce physical hinges, safe-area hardware, mobile keyboard occlusion, touch ergonomics, Safari/Firefox behavior, or OS text scaling. Those checks, dark/increased-contrast appearance, and successful invite/shared-link/authorization/setup/import-job states remain in **culina-v2-y6ti.16**. No valid share/invitation/active-import token was created merely to enter those pages. Development gallery routes were inventoried, not counted as product pages.

The code graph’s coverage generation was 2026-10-18; route inventory was cross-checked against current filesystem paths and the live browser. Material cook-stop behavior was checked against current source where graph freshness had changed. Findings above are browser observations; no unverified implementation root cause is asserted.

## Evidence

**Cookbook: an orphaned overflow row and overlapping row actions**

![Cookbook: an orphaned overflow row and overlapping row actions](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/cookbook-phone.jpg)

**Cooking: 272px dock in a 568px viewport with a timer suggestion**

![Cooking: 272px dock in a 568px viewport with a timer suggestion](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/cooking-320.jpg)

**Diary: narrow date targets and the broken macro label**

![Diary: narrow date targets and the broken macro label](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/diary-320.jpg)

**Tablet recipe: the title competes with the horizontal action group**

![Tablet recipe: the title competes with the horizontal action group](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/recipe-tablet.jpg)

**Settings: repeated labels and the footnote inset**

![Settings: repeated labels and the footnote inset](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/diary-settings-tablet.jpg)

**Settings: three rows of category navigation**

![Settings: three rows of category navigation](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/settings-phone.jpg)

Additional screenshots are in [this audit folder](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design), including the wide library, editor, week and cleanup result.

## Cleanup and handoff

The empty-title check created one persisted draft, **01a18177-2b54-74a8-b913-c5b243f7f80d**. With the user’s explicit approval, it was moved to Culina’s normal 30-day trash; the browser displayed the deletion confirmation and the recipe then showed the missing-recipe recovery page. [Cleanup confirmation](/Users/jakob/WebstormProjects/culina-v2/.beads/audits/2026-10-28-ui-design/review-entry-trashed.jpg).

Review cooking sessions were stopped through the session control without recording a cooked meal. No timer was started. Unsaved form input was canceled or left without submission, and temporary viewport overrides were reset.

No application source was edited by this review. Its deliverables are Beads records/comments and this audit evidence folder. Other application changes were present in the shared checkout at handoff and were left untouched. No git commit, push, or Dolt remote sync was run. No application tests/build were run because this review changed no application code; validation consisted of the browser walkthrough, screenshots, and rendered DOM measurements.

