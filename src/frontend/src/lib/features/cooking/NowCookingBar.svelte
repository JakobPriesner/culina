<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import { cooking } from './stores/cooking.svelte';

  /**
   * The way back into what you were cooking.
   *
   * It appears wherever you wandered off to — the shopping list, another
   * recipe, the settings — because the thing on the hob does not stop being on
   * the hob. It is deliberately one line and two actions: go back to it, or
   * say it is over. Anything more would be a second app competing with the one
   * you are looking at.
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
  <div class="bar">
    <span class="dot" aria-hidden="true"></span>

    <a
      class="what"
      href="{resolve('/(app)/recipes/[recipeId]/cook', {
        recipeId: session.recipeId
      })}?yield={session.servings}"
    >
      <span class="title">{m['cooking.nowCooking']({ title: session.recipeTitle })}</span>

      <span class="resume">{m['cooking.resume']()}</span>
    </a>

    <!-- Ending it, not hiding it. A bar that vanished while the session stayed
         open would come back on the next page load, and the cook would have no
         way to understand why. -->
    <IconButton label={m['cooking.abandon']()} size="sm" onclick={() => void cooking.end(false)}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M6 6l12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </div>
{/if}

<style>
  .bar {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    min-height: var(--control-sm);
    padding-block: var(--space-2);
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
    background: var(--surface-accent-subtle);
    color: var(--text);
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

  /* The link is everything except the dismiss control, so the easy target is
     still the one that leads back to the pan. */
  .what {
    display: flex;
    flex: 1;
    align-items: center;
    gap: var(--space-3);
    min-width: 0;
    padding-block: var(--space-2);
    color: var(--text);
    text-decoration: none;
  }

  .title {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: var(--text-sm);
  }

  /* Weight, not colour. The accent on an accent-tinted surface is 3.7:1 in
     light and 2.9:1 in dark — two colours a few degrees apart, which is what
     makes the tint work as a background and what makes it unreadable as text
     on top of itself. */
  .resume {
    flex: none;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }
</style>
