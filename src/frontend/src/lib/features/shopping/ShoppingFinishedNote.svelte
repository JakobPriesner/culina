<script lang="ts">
  import { resolve } from '$app/paths';
  import { Chevron } from '$ds';

  import { upcoming } from '$features/planning/upcoming.svelte';
  import { hrefAtYield } from '$features/recipes/surface/yieldInUrl';
  import { m } from '$shell/i18n';
  import Olli from '$shell/olli/Olli.svelte';

  interface Props {
    /** Whether the shop finished while somebody was here to see it. */
    celebrate: boolean;
  }

  let { celebrate }: Props = $props();

  /** What the shop was for, next: today's meal still to cook, at the servings planned. */
  const next = $derived(upcoming.mealNow);
</script>

<!-- The shop is done. Said out loud, because a page whose only content is
     a struck-through list looks like a page that has lost something. -->
<div class="finished">
  {#if celebrate}<Olli pose="celebrating" size="sm" />{/if}
  <p>{m['shopping.allBought']()}</p>
  {#if next}
    <!-- eslint-disable svelte/no-navigation-without-resolve -- The route is resolved; only the yield is appended. -->
    <a
      class="next"
      href={hrefAtYield(
        resolve('/(app)/recipes/[recipeId]', { recipeId: next.recipeId }),
        next.servings
      )}
    >
      <!-- eslint-enable svelte/no-navigation-without-resolve -->
      {m['shopping.allBought.tonight']({ title: next.title })}
      <Chevron />
    </a>
  {/if}
</div>

<style>
  .finished {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: var(--space-3);
    color: var(--text-muted);
    font-size: var(--text-sm);
    margin-bottom: var(--space-8);
  }

  .next {
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    min-height: var(--control-sm);
    color: var(--accent);
    font-weight: var(--weight-medium);
    text-decoration: none;
  }

  @media (hover: hover) {
    .next:hover {
      text-decoration: underline;
    }
  }
</style>
