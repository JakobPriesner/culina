<script lang="ts">
  import { onMount, untrack } from 'svelte';
  import { session } from '$features/auth/session.svelte';
  import { shopping } from '$features/shopping/stores/shopping.svelte';
  import { badgeManager } from '$shell/badgeManager.svelte';
  import { cooking } from './stores/cooking.svelte';
  import { kitchenTimers, kitchenWakeLock } from './kitchen.svelte';
  import { kitchenLighting } from './lighting.svelte';
  import { editTimerState } from './timerState';

  const sessionId = $derived(cooking.session?.sessionId ?? null);

  onMount(() => {
    kitchenLighting.load();
    return () => {
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
    const active = cooking.session !== null;
    const mode = kitchenLighting.mode;
    if (active && mode !== 'normal') document.documentElement.dataset['kitchenLighting'] = mode;
    else delete document.documentElement.dataset['kitchenLighting'];
  });

  $effect(() => {
    const id = cooking.session?.sessionId ?? null;
    const count =
      shopping.householdId === session.activeHouseholdId
        ? shopping.items.filter((item) => !item.isChecked).length
        : 0;
    badgeManager.update(kitchenTimers.runningCount, count);
    void editTimerState('__badge', (state) => ({ ...state, url: id ?? '', shoppingItems: count }));
  });
</script>
