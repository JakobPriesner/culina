<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';

  import { m } from '$shell/i18n';
  import { cooking } from './stores/cooking.svelte';

  /**
   * The way back into what you were cooking.
   *
   * It appears wherever you wandered off to — the shopping list, another
   * recipe, the settings — because the thing on the hob does not stop being on
   * the hob. It is deliberately one line and one action: anything more would be
   * a second app competing with the one you are looking at.
   *
   * It hides itself on the cooking screen, where it would be pointing at the
   * page you are already on.
   */
  const session = $derived(cooking.session);

  const onTheCookingScreen = $derived(
    session ? page.url.pathname === `/recipes/${session.recipeId}/cook` : false
  );
</script>

{#if session && !onTheCookingScreen}
  <a
    class="bar"
    href="{resolve('/(app)/recipes/[recipeId]/cook', {
      recipeId: session.recipeId
    })}?yield={session.servings}"
  >
    <span class="dot" aria-hidden="true"></span>

    <span class="what">{m['cooking.nowCooking']({ title: session.recipeTitle })}</span>

    <span class="resume">{m['cooking.resume']()}</span>
  </a>
{/if}

<style>
  .bar {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    padding: var(--space-3) var(--layout-gutter);
    background: var(--surface-accent-subtle);
    color: var(--text);
    text-decoration: none;
  }

  /* A small steady mark rather than a pulsing one: something is on the hob, and
     a blinking light in the corner of the eye for twenty minutes is a nuisance. */
  .dot {
    flex: none;
    width: var(--space-2);
    height: var(--space-2);
    border-radius: var(--radius-full);
    background: var(--accent);
  }

  .what {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: var(--text-sm);
  }

  .resume {
    flex: none;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    color: var(--accent);
  }
</style>
