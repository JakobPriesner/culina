<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { badgeManager } from '$shell/badgeManager.svelte';
  import { cooking } from './stores/cooking.svelte';
  import { kitchenTimers, kitchenWakeLock } from './kitchen.svelte';
  import { kitchenLighting } from './lighting.svelte';
  import { readAloud } from './readAloud.svelte';
  import { editTimerState } from './timerState';
  import { cookingPip } from './pip.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';
  import { kitchenMediaSession } from './mediaSession.svelte';
  import { preferences } from '$shell/preferences.svelte';
  import { native } from '$api';
  import { syncNativeTimers } from './nativeTimers';

  const sessionId = $derived(cooking.session?.sessionId ?? null);

  onMount(() => {
    kitchenLighting.load();
    return () => {
      readAloud.stop();
      cookingPip.close();
      kitchenMediaSession.stop();
      kitchenTimers.clear();
      badgeManager.clear();
      delete document.documentElement.dataset['kitchenLighting'];
    };
  });

  $effect(() => {
    const id = sessionId;
    untrack(() => kitchenTimers.load());
    if (!id) return;
    const stopClock = untrack(() => kitchenTimers.tick());
    const stopHolding = untrack(() => kitchenWakeLock.engage());
    return () => {
      stopClock();
      stopHolding();
      kitchenTimers.clear();
    };
  });

  $effect(() => {
    // Track the common session and clock, never the remote's own writes.
    const snapshot = {
      session: cooking.session,
      detail: recipes.detail,
      locale: preferences.locale,
      measurements: preferences.measurementSystem
    };
    for (const timer of kitchenTimers.timers) kitchenTimers.remaining(timer);
    untrack(() => kitchenMediaSession.sync(snapshot.detail));
  });

  // The app hands the operating system every running timer, so each rings with the app asleep or closed.
  $effect(() => {
    if (!native) return;
    const id = sessionId;
    const timers = $state.snapshot(kitchenTimers.timers);
    const url = untrack(() =>
      cooking.session
        ? `/recipes/${encodeURIComponent(cooking.session.recipeId)}/cook?yield=${cooking.session.servings}`
        : '/'
    );
    void syncNativeTimers(id ? { sessionId: id, url, timers } : null);
  });

  $effect(() => {
    const id = sessionId;
    const pipSession = cookingPip.sessionId;
    if (pipSession && pipSession !== id) untrack(() => cookingPip.close());
    const detail = recipes.detail;
    if (detail) untrack(() => cookingPip.updateRecipe(detail));
  });

  $effect(() => {
    const active = cooking.session !== null;
    const mode = kitchenLighting.mode;
    if (active && mode !== 'normal') document.documentElement.dataset['kitchenLighting'] = mode;
    else delete document.documentElement.dataset['kitchenLighting'];
  });

  // Derived so the badge effect re-runs when a count changes, not on every
  // tick of the clock that `runningCount` is computed from.
  const runningCount = $derived(kitchenTimers.runningCount);
  const uncheckedCount = $derived(
    shopping.householdId === session.activeHouseholdId
      ? shopping.items.filter((item) => !item.isChecked).length
      : 0
  );

  $effect(() => {
    const id = sessionId ?? '';
    const count = uncheckedCount;
    badgeManager.update(runningCount, count);
    void editTimerState('__badge', (state) => ({ ...state, url: id, shoppingItems: count }));
  });
</script>
