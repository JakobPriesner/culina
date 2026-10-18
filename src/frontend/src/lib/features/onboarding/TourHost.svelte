<script lang="ts">
  import { page } from '$app/state';

  import { session } from '$features/auth/session.svelte';

  import { tips } from './tips.svelte';

  /**
   * Opens the tour when it is asked for: right after a new cook's first household (the step after signing up),
   * or from the account menu and Settings. Never over cook mode.
   */
  // Read before the first paint as well as on a change of person: a tip that appeared a moment later would shift the page.
  tips.own(session.user?.userId ?? null);

  $effect(() => {
    tips.own(session.user?.userId ?? null);
  });

  const cooking = $derived(page.route.id === '/(app)/recipes/[recipeId]/cook');

  const open = $derived(tips.touring && session.activeHouseholdId !== null && !cooking);
</script>

<!-- Downloaded only when it opens: every signed-in page carries this shell. -->
{#if open}
  {#await import('./KitchenTour.svelte') then { default: KitchenTour }}
    <KitchenTour open onclose={() => tips.endTour()} />
  {/await}
{/if}
