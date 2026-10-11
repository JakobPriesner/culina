<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { IconButton } from '$ds';

  import { m } from '$shell/i18n';
  import { kitchenWakeLock } from './kitchen.svelte';
  import { cooking } from './stores/cooking.svelte';
  import CookingRemoteToggle from './CookingRemoteToggle.svelte';

  /** The way back into what you are cooking, from any page: one line, two actions (resume or end). Hidden on the cooking screen itself. */
  const session = $derived(cooking.session);

  const onTheCookingScreen = $derived(
    session ? page.url.pathname === `/recipes/${session.recipeId}/cook` : false
  );
</script>

{#if session && !onTheCookingScreen}
  <aside class="bar" aria-label={m['cooking.nowCooking']({ title: session.recipeTitle })}>
    <span
      class="dot"
      class:held={kitchenWakeLock.held}
      role="img"
      aria-label={kitchenWakeLock.held ? m['kitchen.awake']() : m['kitchen.canSleep']()}
      title={kitchenWakeLock.held ? m['kitchen.awake']() : m['kitchen.canSleep']()}
    ></span>

    <a
      class="what"
      href="{resolve('/(app)/recipes/[recipeId]/cook', {
        recipeId: session.recipeId
      })}?yield={session.servings}"
    >
      <span class="title">{m['cooking.nowCooking']({ title: session.recipeTitle })}</span>

      <span class="resume">{m['cooking.resume']()}</span>
    </a>

    <CookingRemoteToggle />

    <!-- Ends the session rather than hiding it: a hidden bar would return on the next page load, unexplained. -->
    <IconButton label={m['cooking.abandon']()} size="sm" onclick={() => void cooking.end(false)}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M6 6l12 12M18 6 6 18" stroke-linecap="round" />
      </svg>
    </IconButton>
  </aside>
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

  /* A steady mark, not a pulse: a blinking light in the corner of the eye for twenty minutes is a nuisance. */
  .dot {
    flex: none;
    width: var(--space-2);
    height: var(--space-2);
    border-radius: var(--radius-full);
    border: 1px solid var(--text-muted);
    background: transparent;
  }

  .dot.held {
    background: var(--success);
    border-color: var(--success);
    box-shadow: 0 0 0 var(--space-1) var(--success-subtle);
  }

  /* The link is everything but the dismiss control, so the easy target leads back to the pan. */
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

  /* Weight, not colour: accent text on an accent tint is 3.7:1 in light and 2.9:1 in dark. */
  .resume {
    flex: none;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }
</style>
