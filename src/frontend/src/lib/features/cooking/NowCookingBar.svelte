<script lang="ts">
  import { resolve } from '$app/paths';
  import { page } from '$app/state';
  import { IconButton } from '$ds';

  import { session as account } from '$features/auth/session.svelte';
  import { m } from '$shell/i18n';
  import { kitchenTimers, kitchenWakeLock } from './kitchen.svelte';
  import { cooking } from './stores/cooking.svelte';
  import CookingRemoteToggle from './CookingRemoteToggle.svelte';

  /**
   * The way back into what you are cooking, from any page: a small floating pill, like a live activity,
   * with two actions (resume or end). Hidden on the cooking screen itself, on the recipe's own page (whose
   * cooking action says "Resume" instead), and for a session that was only opened and left: on its first
   * step, with no timer, started moments ago.
   */
  const session = $derived(cooking.session);

  /** How long a session on its first step, with no timer, counts as only looked at. */
  const glance = 2 * 60 * 1000;

  const onTheRecipe = $derived(
    session
      ? [`/recipes/${session.recipeId}`, `/recipes/${session.recipeId}/cook`].includes(
          page.url.pathname
        )
      : false
  );

  // Read again on every navigation, which is when a glance turns into cooking worth returning to.
  const underWay = $derived.by(() => {
    void page.url.pathname;

    return (
      session !== null &&
      (session.currentStepIndex > 0 ||
        kitchenTimers.timers.length > 0 ||
        Date.now() - Date.parse(session.startedAt) > glance)
    );
  });

  /** A session belongs to the person, so it can be from a household other than the one on screen; then say which. */
  const elsewhere = $derived(
    session?.householdId && session.householdId !== account.activeHouseholdId
      ? (account.households.find((h) => h.householdId === session.householdId) ?? null)
      : null
  );

  function resume() {
    if (elsewhere) {
      account.selectHousehold(elsewhere.householdId);
    }
  }
</script>

{#if session && underWay && !onTheRecipe}
  <aside class="pill" aria-label={m['cooking.nowCooking']({ title: session.recipeTitle })}>
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
      onclick={resume}
    >
      <span class="title">
        <span class="recipe">{m['cooking.nowCooking']({ title: session.recipeTitle })}</span>
        {#if elsewhere}
          <span class="household">{m['cooking.inHousehold']({ household: elsewhere.name })}</span>
        {/if}
      </span>

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
  /* A capsule floating above the bottom bar, not a band across the page: it says what is under way and gets out of the way. */
  .pill {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    width: fit-content;
    max-width: min(32rem, calc(100% - var(--layout-gutter) * 2));
    min-height: var(--control-sm);
    margin: 0 auto var(--space-3);
    padding: var(--space-1) var(--space-1) var(--space-1) var(--space-4);
    border: 1px solid var(--border);
    border-radius: var(--radius-full);
    background: var(--surface-overlay-glass);
    color: var(--text);
    box-shadow: var(--shadow-glass);
    backdrop-filter: blur(16px) saturate(140%);
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

  /* Only the recipe's name gives way: which household it is in must stay readable. */
  .title {
    display: flex;
    flex: 1;
    min-width: 0;
    font-size: var(--text-sm);
    white-space: nowrap;
  }

  .recipe {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  /* A flex item drops its leading space, so the gap is drawn here. */
  .household {
    flex: none;
    margin-inline-start: var(--space-1);
    color: var(--text-muted);
  }

  /* Weight, not colour: the pill's glass can sit over any part of the page. */
  .resume {
    flex: none;
    font-size: var(--text-sm);
    font-weight: var(--weight-semibold);
    text-decoration: underline;
    text-underline-offset: 0.2em;
  }
</style>
