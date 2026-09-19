<script lang="ts">
  import type { Snippet } from 'svelte';

  import { page } from '$app/state';
  import { resolve } from '$app/paths';

  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { session } from '$features/auth/session.svelte';

  /**
   * Settings, split into categories.
   *
   * One screen holding appearance, the household, invitations, the archive and
   * the way out was a scroll with no shape: nothing on it belonged next to
   * anything else, and finding the one control you came for meant reading past
   * four you did not. Each category is its own route instead, listed down the
   * side on a wide screen and across the top on a phone.
   *
   * Routes rather than tabs, because a settings page someone is talked through
   * on the phone has to be linkable, and because the back button out of the
   * archive should land on the list of categories rather than on the recipes.
   *
   * The heading belongs to this file rather than to the three pages, because
   * the category's name is the same word the rail is already showing: three
   * pages each writing their own is three places for it to stop matching.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  // The assistant configures the instance rather than the person looking at it
  // — one key, one bill — so it is in the rail only for the one account that
  // may change it. Absent rather than disabled, like every other thing in this
  // app somebody cannot do.
  const categories = $derived([
    { href: resolve('/(app)/me'), label: m['me.account'], lead: m['me.account.lead'] },
    {
      href: resolve('/(app)/me/appearance'),
      label: m['me.appearance'],
      lead: m['me.appearance.lead']
    },
    {
      href: resolve('/(app)/me/household'),
      label: m['me.household'],
      lead: m['me.household.lead']
    },
    ...(session.user?.isAdmin
      ? [{ href: resolve('/(app)/me/ai'), label: m['me.ai'], lead: m['me.ai.lead'] }]
      : [])
  ]);

  // Exact, not a prefix: no category has children of its own, and a prefix
  // would light Account up on every one of them.
  const current = $derived(page.url.pathname);
  const active = $derived(categories.find((category) => category.href === current));
</script>

<Page>
  <div class="settings">
    <nav class="rail" aria-label={m['me.title']()}>
      <!-- Not a heading: it names the rail, and a heading here would land
           between the page's own and the sections inside it. -->
      <p class="legend">{m['me.title']()}</p>

      <div class="categories">
        {#each categories as category (category.href)}
          {@const selected = category.href === current}
          <a
            class="category"
            class:selected
            href={category.href}
            aria-current={selected ? 'page' : undefined}
          >
            {category.label()}
          </a>
        {/each}
      </div>
    </nav>

    <div class="panel">
      {#if active}
        <header class="header">
          <h1>{active.label()}</h1>
          <p class="lead">{active.lead()}</p>
        </header>
      {/if}

      {@render children()}
    </div>
  </div>
</Page>

<style>
  .settings {
    display: grid;
    gap: var(--space-8);
    align-items: start;
  }

  .legend {
    /* Only worth the line on the desktop rail, where it captions a column.
       Above a wrapping row of pills it would caption nothing. */
    display: none;
  }

  /* On a phone: a row across the top, wrapping rather than scrolling sideways,
     because a category hidden past the edge of the screen is a category nobody
     knows exists. */
  .categories {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1);
  }

  /* The same pill as the navbar above it. An inner navigation that invented its
     own selected state would read as a different app's furniture. */
  .category {
    min-height: var(--control-sm);
    display: flex;
    align-items: center;
    padding: var(--space-2) var(--space-3);
    border-radius: var(--radius-full);
    color: var(--text-muted);
    font-size: var(--text-sm);
    font-weight: var(--weight-medium);
    text-decoration: none;
    transition:
      color var(--duration-fast) var(--ease-out),
      background-color var(--duration-fast) var(--ease-out);
  }

  .category:hover {
    background: var(--surface-hover);
    color: var(--text);
  }

  .category.selected {
    background: var(--surface-accent-subtle);
    color: var(--accent);
    font-weight: var(--weight-semibold);
  }

  /* Capped well short of the page, because a settings row is a label on the
     left and a control on the right, and across a 1400px monitor that is a
     centimetre of eye travel per setting with nothing in between. The rail
     takes the width the panel gives up. */
  .panel {
    display: flex;
    flex-direction: column;
    gap: var(--layout-section-gap);
    min-width: 0;
    max-width: 46rem;
  }

  .header {
    display: flex;
    flex-direction: column;
    gap: var(--space-2);
  }

  h1 {
    font-size: var(--text-2xl);
    font-weight: var(--weight-semibold);
    line-height: var(--leading-tight);
  }

  .lead {
    max-width: var(--measure);
    color: var(--text-muted);
    line-height: var(--leading-normal);
  }

  @media (min-width: 64rem) {
    .settings {
      grid-template-columns: 14rem minmax(0, 1fr);
      gap: var(--space-12);
    }

    /* Follows a long archive or invitation list down the page: the categories
       are how you leave, and a way out you have to scroll back up for is not
       one. */
    .rail {
      position: sticky;
      top: var(--space-8);
    }

    .legend {
      display: block;
      margin-bottom: var(--space-3);
      padding-inline: var(--space-4);
      color: var(--text-subtle);
      font-size: var(--text-xs);
      font-weight: var(--weight-semibold);
      letter-spacing: 0.08em;
      text-transform: uppercase;
    }

    .categories {
      flex-direction: column;
      flex-wrap: nowrap;
      gap: var(--space-1);
    }

    /* Stretched to the column, so the pill marks a row of the list rather than
       floating at whatever width its label happens to be. */
    .category {
      justify-content: flex-start;
      padding-inline: var(--space-4);
    }
  }
</style>
