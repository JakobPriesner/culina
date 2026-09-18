# Culina user-flow review

Reviewed through the browser on 14–15 September 2026; reconciled with selected current source and issue status on 18 September.

Culina has several thoughtful individual interactions, but the complete journey still asks the user to reconnect information the app already knows. The largest opportunity is continuity between choosing, planning, shopping, cooking, and remembering. Quantity correctness and access to cooking information should come before additional features.

The principle behind the user's bedside-screen example applies well here: the application should adapt to the activity already underway. A useful detail removes a decision at the right moment, preserves context, and remains understandable and reversible.

## What was actually exercised

The walkthrough used the local app at `localhost:5173`, with desktop and 390 × 844 mobile browser viewports. It covered sign-in, household onboarding, an empty library, recipe creation by pasted text, editing, detail, scaling, shopping, planning, cooking, resuming after navigation and reload, completion, personal notes, and creating a cookbook from a recipe.

The sample recipe, **Review · Tomato pasta**, served four: 400 g pasta, 600 g tomatoes, 2 tbsp olive oil, and 2 cloves garlic, with three instructions. This allowed quantities and transitions to be checked against a known result.

On 18 September the supplied credentials were rejected. Consequently, this report distinguishes dated browser observations from source checks and reported fixes. It is not a claim that every finding still reproduces in today's running build. Cookbook detail, settings, invitations, offline use, multiple devices, accessibility with assistive technology, and background alarms were not fully tested.

## The journey and its handoffs

| Transition | Observed experience | Assessment |
|---|---|---|
| First visit → household → first recipe | The empty library provides a clear creation action. Pasting text previews four ingredients and three steps before creation. | Clear start, with useful confirmation before committing. |
| Recipe → adjusted quantities → shopping | Changing four to six servings correctly produces 600 g pasta and 900 g tomatoes in shopping. | Strong connection within this path. |
| Shopping → return to the recipe | Opening the recipe afresh from the library returns to its original four servings. The list still contains the six-serving purchase. | The user must reconstruct which meal those purchases were for. |
| Recipe → weekly plan | In the tested detail view there was no direct planning action. Planning required navigating to Plan and selecting the recipe again. The picker did not ask for servings. | A natural next action loses the current recipe context. |
| Weekly plan → shopping | The success message includes an Open list action. Repeating an unchanged single-meal addition did not duplicate quantities. Two separate occurrences of the same recipe, however, contributed only one batch in the tested flow. | Useful handoff, with a serious quantity problem. |
| Recipe → cook → leave → resume | Six servings and the active step survived leaving for Shopping, returning through the cooking bar, and reloading. | One of the best integrated flows. Preserve this behavior. |
| Cook → completion → history | Completion returned to the recipe, retained six servings, and produced a dated cooking record and an undo action. | Good foundation; a short reflection opportunity would close the loop more clearly. |
| Recipe → create cookbook → membership | Creating **Review · Weeknights** returned to the recipe's cookbook dialog with membership already selected and the scaled recipe intact. | Excellent contextual creation: the original task is completed without making the user repeat it. |

## Findings, in recommended order

### 1. Make planned shopping quantities trustworthy

**High impact; reproduced in the browser on 14 September.**

With the four-serving pasta recipe planned for both Monday and Tuesday, adding the week reported **Added 2 recipes**. The unchecked list contained 400 g pasta and 600 g tomatoes. Two separate cooking occasions required 800 g and 1,200 g. A previously checked six-serving batch was separate from these unchecked quantities.

The product needs to distinguish two intentions: synchronizing the same planned meal again, and cooking the same recipe on another occasion. Both must work without the user manually repairing the list. On 18 September, the plan still passed recipe ID and servings to shopping for each meal, without a plan-entry identifier; this supports investigating the handoff but does not establish the complete backend cause.

The list should also explain contributions on demand: **Tomatoes · 1,200 g — Monday dinner + Tuesday dinner**. A change to one planned meal should adjust only its contribution. Manually added groceries must remain intact. Planning leftovers should be an explicit choice, distinct from cooking another batch.

Tracked as **culina-v2-t1yw**, related to **culina-v2-erv.10** and **culina-v2-1l9f**.

### 2. Keep ingredients and personal knowledge available during cooking

**High impact; reproduced on 15 September; ingredient-selection behavior corroborated in source on 18 September.**

The pasted recipe entered cook mode, but the first instruction, “Boil the pasta for 10 minutes,” displayed no ingredients for the step. Its ingredients had not been linked to individual instructions. The cook view offered no full-list escape in the tested flow. The saved personal note, “use less garlic next time,” was also unavailable there.

Step-specific ingredients are valuable, but incomplete linking must not hide quantities. Provide **This step / All ingredients**, or an always-reachable full-list drawer. When links are absent, explain that and show the full list instead of implying that no ingredients are needed. An import should not appear complete and then require undisclosed editing before cook mode becomes useful.

Surface personal notes within cooking. Initially, a compact recipe-note drawer is enough. Later, notes explicitly attached to a step can appear at that step. Do not guess which instruction a general note belongs to.

Existing tracking: **culina-v2-erv.8** and **culina-v2-277**. The runtime fallback evidence has been added to the ingredient-linking issue.

### 3. Carry the intended meal and servings across features

**Medium impact; reproduced on 14–15 September.**

Scaling itself worked, and the direct recipe-to-cook link preserved the chosen yield. A fresh library link restored the authored yield, however. The detail view could simultaneously show metadata for four portions and a control set to six. Planning also required finding the recipe again without a servings choice in the tested picker.

Keep the authored recipe separate from a particular meal, but make the distinction visible: **Cooking for 6 · Original recipe serves 4**. Offer **Plan this** from recipe detail, opening a small date, meal, and servings chooser already filled from the current view. The planned meal should carry that yield into shopping and cooking.

A general recipe link may reasonably open at its original yield. The improvement is to retain the specific meal context where it exists, and offer an explicit **Use last cooked amount** when helpful. Silently changing every future recipe visit would create a different ambiguity.

Existing tracking: **culina-v2-55j** and **culina-v2-erv.10**. Edit was already present on 15 September; the older combined “no Edit or Add-to-plan” issue should be narrowed accordingly.

### 4. Let the current activity determine the mobile layout

**Medium impact; mobile observations from 14–15 September.**

On Shopping, the heading and expanded amount/unit/name form occupied roughly the first 590 pixels of an 844-pixel viewport. Only a couple of checklist items were visible before the bottom navigation. In the supermarket, checking items should dominate. Collapse manual entry behind a compact **Add item** action and expand it when requested.

Recipe action controls also occupied substantial mobile space; overlapping ingredient content is separately tracked as **culina-v2-hto**. Check that content remains reachable above sticky controls, including with longer translations and larger text.

During the cooking walkthrough, Next updated the progress indicator while the previous step remained in view. **The current source now implements scrolling to the active step**, including reduced-motion handling. This is an improvement awaiting browser verification, not an unchanged missing feature. Verify long instructions, rapid changes, keyboard focus, and the first resumed step. Existing tracking: **culina-v2-l4l**.

### 5. Finish the excellent “scale to what I have” interaction

**Medium impact; reproduced on 15 September and corroborated in source on 18 September.**

Selecting tomatoes and typing **300** left Apply disabled without an explanation. Typing **300 g** produced the correct two-serving preview. The example unit existed only in a placeholder, which vanished while typing.

Default a bare number to the selected ingredient's unit and show the interpretation, or immediately explain the missing unit. Retain explicit compatible units and explain incompatible ones. Keep the existing preview: it gives users confidence before the quantities change.

Tracked as **culina-v2-pr2y**.

### 6. Connect written timing and the next cooking attempt

**Integration opportunity; tested recipe on 15 September.**

The imported instruction included ten minutes, but offered no corresponding timer in the tested cook flow. This is a gap in getting a usable duration from creation into cooking, not evidence that the application has no timer system. The editor-duration gap is already tracked as **culina-v2-kxv**; the current source also contains timer work that was not reverified in the browser.

Offer a recognizable **10 min timer** next to an explicit duration, with a preview or correction when importing ambiguous text. A running timer should retain its step name when the user advances or visits Shopping.

After cooking, keep completion immediate and undoable, then offer one optional prompt: **Anything to remember next time?** Show the resulting note on the next cook. Photo and other details can remain secondary. This makes history improve the next experience instead of merely recording the previous one.

## The small details most worth integrating

| Real situation | Contextual response | Why it belongs |
|---|---|---|
| A dinner is planned for today | Show a compact meal card with servings and the next useful action: review shopping or start cooking. | Connects the existing plan, list, and cook session. No new dashboard is needed. |
| The user is checking groceries | Give most of the screen to the remaining items; show source meals on demand. | Reduces scrolling and explains quantities at the point of purchase. |
| A timer is running while another step is active | Keep a compact, named countdown visible, with a quiet visual state change on completion and a textual status. | The cooking equivalent of the user's ambient-screen example: useful information follows the activity. Color alone should not carry meaning. |
| A recipe has a personal note from last time | Make it available before cooking and at an explicitly linked step. | Turns recorded experience into help at the right moment. |
| A recipe is added to a new cookbook | Keep the existing create-and-add behavior and return context. | This already demonstrates the desired level of integration. |
| A user finishes shopping for a planned meal | Offer that meal as the route back to its recipe; retain the planned servings. | Closes the gap between buying and cooking without assuming that checked groceries prove pantry inventory. |

A new setting is unnecessary for most of these details. Use information supplied by the user's existing actions, show why an adjustment happened, and make it easy to override. Timer behavior while the browser or device is asleep needs separate verification before promising an alarm experience.

## Changes since the original walkthrough

- Search-state loss was observed in the earlier flow, but **culina-v2-qzk** is now closed with implementation and verification recorded. It is not listed as a current defect here.
- Recipe Edit was visible by 15 September.
- Active-step scrolling is now present in the 18 September source; runtime confirmation remains outstanding.
- Current ingredient presentation, cookbook, search, and timer work has evolved. This review does not treat the 14–15 September screenshots and behavior as a complete representation of all newer work.

The next validation should use one complete scenario: choose a recipe, plan it twice with different servings, generate shopping, buy ingredients, cook one planned occurrence, record a note, and return to the other occurrence. That single journey checks whether the features cooperate. Its quantities, context, and next actions should be understandable without reconstructing earlier choices.

## Evidence and handoff

This report is primarily based on direct browser interactions. Narrow source checks included the plan's shopping handoff, `RecipeSurface.svelte`, `ScaleToAmountSheet.svelte`, `yieldInUrl.ts`, and the cooking route. The prescribed codebase graph could not index this project; coverage requests returned no matching project. Direct source reads were used as a limited fallback, with no exhaustive architecture claim.

No application code was changed for this review. No new implementation tests were run for this document. The local review household, sample recipe, cookbook, note, shopping items, plan entries, and cooking record were left in place as reproducible review data. Existing working-tree changes were preserved. No commit or push was performed.
