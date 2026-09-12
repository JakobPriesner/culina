# Culina: product and quality plan, revision 3

**Review date: 12 September 2026. Status: proposed release baseline.**

Culina should make a personal recipe collection dependable in a real kitchen: find something worth cooking, adjust it accurately, cook without losing your place, and improve the recipe without losing anyone's work.

The original external plan remains historical context. This document reconciles that idea with the current repository and proposes a release standard. It is not a certification or a completed security audit. Implementation status and dependencies live in Beads.

## 1. Choose the product before expanding it

Working assumption: households first, with production-grade security, reliability and maintainability. Professional kitchens are a separate product decision: their permissions, recipe approval, yield/costing and operating procedures need research before being added. SSO, SCIM, organizational billing and multi-region deployment have not been accepted into scope.

Keep the strongest idea from the original brief: continuity across finding → reading → scaling → cooking → returning. Treat the supplied foldable-phone story as an illustration of that principle, not verified evidence about a particular device. The application must remain understandable with every animation disabled.

**Suggested v1:** accounts and household membership; recipe creation/editing, photos, search and favourites; trustworthy scaling; reading/cooking; local recovery of drafts and cooking progress; installable PWA with a bounded offline read contract; recoverable deletion, account recovery, operational backups and basic data portability.

**After the core release:** shopping, meal planning, cook history, personal result photos, URL import, social features and nutrition estimates. The current backlog already contains shopping and cook-history work. Preserve it, but explicitly decide its release milestone; do not accidentally ship a larger v1 because tickets exist. Offline authoring with automatic background merging is also a separate capability.

Before accepting this scope, answer: What task brings someone back weekly? Who owns a household when its creator leaves? Must a shared device retain private recipes offline? Does one user belong to several households? The current domain documentation already anticipates richer membership than the original one-household-per-user sketch.

## 2. Reconcile the sources of truth

The original plan and the repository now describe different applications:

| Concern | Original external plan | Current repository evidence | Recommendation |
| --- | --- | --- | --- |
| Project state | Empty, scaffold and initialize Git | Existing source, tests, open tasks and work in progress | Continue existing work; do not re-scaffold |
| Persistence/auth | EF Core and Identity | README and domain/deployment docs describe Npgsql + Dapper, SQL migrations and opaque sessions | Keep the implemented direction; require a security review of custom session/recovery behavior rather than rewriting for plan conformity |
| Scope | No shopping or cook log | README, design docs and Beads include both | Assign explicit milestones |
| Scaling | Frontend-only, free units, display matching | Domain/scaling docs describe canonical units and ingredient references | Establish one arithmetic specification and trace all callers before changing implementations |
| Conventions | Several empty skill folders | The referenced frontend/security convention files now contain instructions | Replace the historical blockers with specific remaining implementation gaps |
| UI | Header only, no rail | Existing desktop rail and mobile navigation | Keep current navigation while it serves real destinations; revisit if shopping is deferred |
| API/static output | `contracts/culina-api.json`, `200.html` | Frontend config uses `backend/openapi/Api.json`, `index.html` | Derive commands and CI checks from actual paths |

Package versions in prose are snapshots, not a dependency policy. Pin the SDK/runtime, package-manager version, lockfiles and image digests in the build configuration. Upgrade them together after compatibility checks. Avoid promises such as “30 lines” for transitions or “80 lines” for scaling; their acceptance criteria matter more than line counts.

## 3. Define behavior at the difficult transitions

### Finding → deciding

Start on the user's collection, not a dashboard of invented statistics. Put search, favourites and a useful time filter within reach. Recipe identity, total time, yield and source matter more than category badges. Search title, tags and ingredients; preserve query, filter, scroll and focus when returning from a recipe. Avoid an empty detail page during a background refresh.

A featured recipe is optional, useful only when there is a real reason to feature it. The new design preview uses one sample to demonstrate photography, not an algorithmic recommendation or a permanently required promotional block. A recipe without a photo receives the same readable title and metadata; do not manufacture an unrelated food picture.

### Writing → saved

A title-only draft is valid. Show an explicit state: **Saving**, **Saved**, **Saved on this device**, **Couldn't save**, or **Needs review**. “Saved” means acknowledged by the server. Preserve raw ingredient text alongside parsed fields so a parser never silently discards what someone wrote.

Journal edits locally before the network request, scoped to user and household. Serialize saves or discard obsolete responses by revision. Use optimistic concurrency to detect conflicts; keep both the local draft and latest server version and offer a comprehensible comparison. Never silently overwrite another person's changes or retry a stale write until it succeeds.

Test lost responses after successful writes, two tabs, photo uploads racing a save, navigation during debounce, session expiry and language changes. The current root layout recreates its component tree when the locale changes; editor and cooking state must outlive that tree before those workflows ship. Undo requires an actual recovery mechanism and retention period; a disappearing toast is not a recycle bin.

### Original amount → scaled amount

Keep authored quantities, exact arithmetic and display formatting separate. Factor 1 preserves authored intent. A fraction of an egg remains a fraction; it must not become one egg or a range as the canonical value. A small measured amount must not become “a pinch.” Unknown units remain intact. If display rounding is helpful, show an approximation and provide the exact amount.

When anchoring to 600 g, **600 g is the constraint**. Compute the exact factor from that amount; round only the displayed serving label, never feed it back into ingredient calculations. Guard zero, negative, missing and non-finite bases. Keep oven temperature and time unchanged and explain that cookware and doneness still require judgment.

The existing `docs/scaling-rules.md` recommendations to floor counts at one, replace tiny spoon quantities with a pinch, and recompute the factor from rounded yield need revision. They remain historical implemented/planned behavior until the linked arithmetic task updates specification, fixtures and all implementations together.

Use one versioned, language-neutral fixture corpus if arithmetic truly runs on both client and server. That is valuable compatibility testing in the current architecture, even though it was unnecessary in the original frontend-only plan. Include fractions, precision boundaries, nullable quantities, both locales and repeated scale/restore cycles.

### Reading → cooking → returning

Keep stable ingredient and step identity. A shared surface is a useful implementation tool; the acceptance test is that the cook recognizes the same recipe, portion count and current step. On a phone, make the current step and Next action reachable while keeping ingredients one explicit action away. Do not force the desktop two-column layout onto a small screen.

Use visible Previous/Next controls, at least 56 CSS pixels in cooking mode. Whole-screen tap zones must never steal a tap intended for a checkbox, timer or scroll. Keyboard shortcuts must ignore typing, dialogs and editable controls. Respect reduced motion and focus placement. Ingredient detection may assist navigation, but a heuristic match is never proof of completeness or a basis for silently changing instruction text.

Resume data needs recipe ID, recipe version, stable step ID, serving factor and timestamp. When the recipe changes, explain and reconcile instead of resuming at an unrelated array index.

Timers store an absolute deadline; a scheduled callback merely refreshes the display. Recompute after backgrounding, sleep or restart. Multiple timers remain individually labelled. An installed PWA cannot promise a reliable alarm while suspended or closed solely because a JavaScript timer exists; browser scheduling can be delayed. Document the supported notification behavior and provide an honest fallback. [MDN timer behavior](https://developer.mozilla.org/en-US/docs/Web/API/Window/setTimeout#reasons_for_delays_longer_than_specified).

### Connected → offline → connected

Commit to offline **reading and cooking of downloaded recipes**, with a visible “Available offline” state only after recipe data and required assets have been stored successfully. Define limits, eviction, version, timestamp and a remove-download action. A successful online page view alone is not proof of offline availability.

Scope caches by user and household; exclude authenticated API responses from generic URL-only service-worker caches. Clear private data on logout/account change and verify a second account cannot see the first account's content. Document the unavoidable limitation: a disconnected client cannot learn about a server-side revocation until it reconnects. Prefer explicit opt-in downloads on shared devices.

Preserve recoverable drafts locally, but do not claim server synchronization until it completes. Update the service worker at a safe point, with an option to defer during cooking or editing. Never reload an active workflow to install a new version.

## 4. Visual direction: contemporary, warm and practical

The first implementation is tidy but reads like a generic account form. The revision uses a less yellow neutral ramp, warm charcoal in dark mode, the existing terracotta accent, a consistent bowl wordmark, editorial display typography and crisp system typography for controls and quantities. Food photography and text remain separate so contrast is predictable.

Use serif display type sparingly for recipe identity and major headings; ingredients, instructions and controls prioritize readability. Avoid a rounded rectangle around every paragraph. Preserve 44-pixel controls, 56-pixel cooking controls, visible focus, meaningful pressed states, responsive wrapping, reserved image geometry and no-motion behavior. These are product targets; they should not be confused with the lower minimum target-size criterion in WCAG 2.2.

**Delivered in this revision:** shared theme/brand/authentication framing and a development-only recipe preview at `/design/recipes`. The preview demonstrates search, filters, favourites, empty results, exact simple scaling, ingredient check-off and read/cook emphasis. Sample data and temporary state are labelled. It is not connected to the recipe API and does not implement persistent cook sessions, timers, editing or offline downloads. `/design` remains the primitive gallery. Both are guarded out of a normal release.

The preview is a composition to review, not a second frontend architecture. Promote its useful components into the actual recipe flows as their existing tickets are implemented. Avoid coupling to the recipe-store work already in progress.

## 5. Production quality is evidence

Keep the modular monolith and one application image serving API and SPA, with PostgreSQL separately. Preserve explicit DI, validated configuration, operation boundaries and the generated client. Add Strategy only for an actual family of interchangeable behaviors. A five-project solution is not itself proof of maintainability; clear ownership and cheap, reliable change are.

| Release contract | Evidence required |
| --- | --- |
| Household isolation | Read/write/list/search/image/export tests across two households; membership removal; no existence leaks; roles enforced on server |
| Identity lifecycle | Review sign-in, verification/recovery, invitation expiry/reuse, session rotation/revocation, CSRF and brute-force controls; no secret or recipe content in logs |
| Write integrity | Concurrent edit, stale version, duplicate submission and lost-response tests; draft survives recovery; soft delete can be restored |
| Upload safety | Decode and validate content, bound dimensions and bytes, remove unwanted metadata, authorize image reads, handle storage failures |
| Recovery | Restore database, images and required key material into a clean environment, verify counts and authenticated image access; record actual recovery time and data loss |
| Release safety | Reproducible image, contract drift gate, migration tests against previous release, readiness after migration, graceful shutdown, documented rollback compatibility |
| Operability | Useful request IDs, bounded metric labels, actionable errors, disk/database capacity signals, named owner and incident/restore runbooks |
| Portability | Export personal/household data with images and a documented format; deletion/retention behavior; access-controlled downloads |

Use a scoped OWASP ASVS 5.0 Level 2 review as a security acceptance framework: record applicable requirements, evidence, exceptions and owners. A scanner or a checklist is not ASVS verification. [OWASP ASVS](https://github.com/OWASP/ASVS/tree/v5.0.0).

## 6. Set measurable gates before claiming quality

- **Accessibility:** target WCAG 2.2 AA; automated checks plus keyboard, screen reader, 200% text size, 320-CSS-pixel reflow, dark mode and reduced-motion review of real workflows. Test dialog focus and authentication usability. Passing token contrast tests alone is insufficient. [W3C quick reference](https://www.w3.org/WAI/WCAG22/quickref/).
- **User experience:** in moderated household trials, people can save a first recipe, find it by an ingredient, scale it and resume cooking without instruction. Proposed pilot gate: at least 4 of 5 participants complete each task without help, with every data-loss or misleading-quantity observation resolved. This small study is directional, not statistical proof.
- **Performance:** target LCP ≤2.5 s, INP ≤200 ms and CLS ≤0.1 at the 75th percentile of real visits, split by mobile/desktop. Use repeatable lab checks before sufficient field data exists; do not label a lab result a field pass. [Web Vitals](https://web.dev/articles/vitals).
- **Capacity:** proposed initial reference workload: 10,000 recipes in a household, 20 concurrent active users, bounded page sizes and realistic image uploads. Record hardware/data distribution; establish endpoint latency and resource budgets from a baseline before optimizing.
- **Operations:** proposed self-hosted baseline: daily verified backups, recovery-point target ≤24 hours and measured restore target ≤60 minutes. These are operating targets, not promises an arbitrary installation can automatically meet. A commercial service requires separately agreed availability and support commitments.

Release on completed contracts, not a count of closed tickets. Avoid postponing all accessibility, security and integration work to a final audit phase.

## 7. Deliver in vertical slices

1. **Find → read:** current recipe store, real collection/search, detail, states, focus restoration, generated client and household authorization. Review mobile and desktop with actual photos and missing-photo recipes.
2. **Write → recover:** create/edit, parser preservation, autosave, conflicts, upload failure, undo/restore and locale continuity. This proves data integrity before adding more workflows.
3. **Scale → cook → resume:** exact arithmetic, step identity, accessible controls, deadline timers and session/version reconciliation. Then add purpose-driven transitions.
4. **Disconnect → recover → upgrade:** scoped downloads, reconnect behavior, service-worker updates, account switching and revoked membership.
5. **Operate → restore → release:** real container, deployment rehearsal, restore and export, scoped security/accessibility review, performance baseline and a household pilot.

Beads contains the executable work. Continue the existing store task (`culina-v2-25k`) and UI tasks (`culina-v2-u9h`, `culina-v2-dqt`, `culina-v2-5vy`); do not create a competing implementation. Existing editor, offline, timer, deployment and quality tickets remain owners of their flows. The added quality tasks capture cross-cutting acceptance work and depend on those implementations where appropriate.

## 8. Beads handoff

Design and planning work: `culina-v2-3l5`. Proposed production-quality epic: `culina-v2-702`.

| Issue | Acceptance responsibility |
| --- | --- |
| `culina-v2-702.1` | Exact amounts, scaling and source intent |
| `culina-v2-702.2` | Autosave, conflicts, recoverable drafts and locale continuity |
| `culina-v2-702.3` | Household boundaries and identity lifecycle verification |
| `culina-v2-702.4` | Offline privacy, cache lifecycle and safe updates |
| `culina-v2-702.5` | Cooking resume, deadline timers and interruption behavior |
| `culina-v2-702.6` | Clean restore, upgrades and basic portability rehearsal |

Each ticket includes its context, behavior, affected areas, conventions and test evidence. Dependency edges point to the existing implementation tickets. Their status remains in Beads rather than in this table.
