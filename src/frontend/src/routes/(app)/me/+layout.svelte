<script lang="ts">
  import type { Snippet } from 'svelte';

  import { page } from '$app/state';
  import { resolve } from '$app/paths';

  import { m } from '$shell/i18n';
  import Page from '$shell/Page.svelte';
  import { session } from '$features/auth/session.svelte';

  import NotAllowed from './NotAllowed.svelte';

  /**
   * Settings categories as routes, not tabs, so each is linkable and Back from the archive lands on the category list.
   * The heading lives here so it always matches the rail.
   */
  interface Props {
    children: Snippet;
  }

  let { children }: Props = $props();

  const personal = [
    { href: resolve('/(app)/me'), label: m['me.account'], lead: m['me.account.lead'] },
    {
      href: resolve('/(app)/me/appearance'),
      label: m['me.appearance'],
      lead: m['me.appearance.lead']
    },
    {
      href: resolve('/(app)/me/security'),
      label: m['me.security'],
      lead: m['me.security.lead']
    },
    {
      href: resolve('/(app)/me/household'),
      label: m['me.household'],
      lead: m['me.household.lead']
    },
    {
      href: resolve('/(app)/me/ingredients'),
      label: m['me.ingredients'],
      lead: m['me.ingredients.lead']
    }
  ];

  // Assistant and server configure the instance, not the person: the administrator gets them as a group of their own
  // in the rail, and nobody else gets them at all (absent, not disabled).
  const administration = [
    { href: resolve('/(app)/me/ai'), label: m['me.ai'], lead: m['me.ai.lead'] },
    { href: resolve('/(app)/me/server'), label: m['me.server'], lead: m['me.server.lead'] }
  ];
  const admin = $derived(session.user?.isAdmin ? administration : []);
  const categories = $derived([...personal, ...admin]);

  // Exact match: a prefix would light Account up on every sub-route.
  const current = $derived(page.url.pathname);
  const active = $derived(categories.find((category) => category.href === current));

  /**
   * A non-administrator on an admin page: say "not yours" here rather than load settings the server refuses,
   * whose refusal would read as a failed load with a useless retry.
   */
  const refused = $derived(
    session.user?.isAdmin ? null : (administration.find((page) => page.href === current) ?? null)
  );
</script>

{#snippet link(category: { href: string; label: () => string })}
  {@const selected = category.href === current}
  <a
    class="category"
    class:selected
    href={category.href}
    aria-current={selected ? 'page' : undefined}
  >
    {category.label()}
  </a>
{/snippet}

<svelte:head>
  {#if refused}<title>{m['notAllowed.title']()}</title>{/if}
</svelte:head>

<Page>
  <div class="settings" class:crowded={admin.length > 0}>
    <nav class="rail" aria-label={m['me.title']()}>
      <!-- Not a heading: it would land between the page's own heading and its sections. -->
      <p class="legend">{m['me.title']()}</p>

      <div class="categories">
        {#each personal as category (category.href)}
          {@render link(category)}
        {/each}

        {#if admin.length > 0}
          <div class="administration" role="group" aria-labelledby="settings-administration">
            <p class="legend" id="settings-administration">{m['me.administration']()}</p>

            {#each admin as category (category.href)}
              {@render link(category)}
            {/each}
          </div>
        {/if}
      </div>
    </nav>

    <div class="panel">
      {#if active}
        <header class="header">
          <h1>{active.label()}</h1>
          <p class="lead">{active.lead()}</p>
        </header>
      {/if}

      {#if refused}
        <NotAllowed />
      {:else}
        {@render children()}
      {/if}
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
    /* Only on the desktop rail, where it captions a column. */
    display: none;
  }

  /* Phone: wraps instead of scrolling sideways, so no category hides past the edge. */
  .categories {
    display: flex;
    flex-wrap: wrap;
    gap: var(--space-1);
  }

  /* Phone and tablet: the administrator's pills follow the others in the same row, after a hairline. */
  .administration {
    display: contents;
  }

  .administration::before {
    content: '';
    align-self: center;
    width: 1px;
    height: 1.25rem;
    margin-inline: var(--space-1);
    background: var(--border);
  }

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
  }

  /* Capped: on a wide monitor a label-left, control-right row is a long eye trip. */
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
    font-size: var(--text-3xl);
    line-height: var(--leading-tight);
  }

  .lead {
    max-width: var(--measure);
    color: var(--text-muted);
    line-height: var(--leading-normal);
  }

  /*
   * Phone, and a tablet whose header has no room for an administrator's six categories: sticky under the header
   * on an opaque band bled to the gutter (the row wraps to two lines), above the header's layer so its scrim fades
   * the page, not the pills.
   */
  @media (max-width: 47.999rem) {
    .rail {
      position: sticky;
      z-index: var(--z-sticky);
      top: var(--header-inset);
      margin-inline: calc(-1 * var(--layout-gutter-start)) calc(-1 * var(--layout-gutter-end));
      padding: var(--space-2) var(--layout-gutter-end) var(--space-2) var(--layout-gutter-start);
      border-bottom: 1px solid var(--border);
      background: var(--surface);
    }
  }

  /*
   * Tablet: pills move up into the empty right half of the header, out of flow (height: 0) so the panel keeps the page top.
   * The offset is the measured header middle (the wordmark's font metrics set the line); the token is a placeholder until then.
   */
  @media (min-width: 48rem) and (max-width: 63.999rem) {
    .crowded > .rail {
      position: sticky;
      z-index: var(--z-sticky);
      top: var(--header-inset);
      margin-inline: calc(-1 * var(--layout-gutter-start)) calc(-1 * var(--layout-gutter-end));
      padding: var(--space-2) var(--layout-gutter-end) var(--space-2) var(--layout-gutter-start);
      border-bottom: 1px solid var(--border);
      background: var(--surface);
    }

    .settings:not(.crowded) {
      gap: 0;
    }

    .settings:not(.crowded) > .rail {
      position: sticky;
      /* The header's scrim is opaque where these pills land. */
      z-index: var(--z-sticky);
      top: calc(var(--header-inset) / 2);
      margin-block-start: calc(-1 * (var(--layout-page-space) + var(--header-inset) / 2));
      height: 0;
      display: flex;
      align-items: center;
      justify-content: flex-end;
    }

    /* The navbar's own container: the pills need a surface once the page scrolls under them. */
    .settings:not(.crowded) .categories {
      flex-wrap: nowrap;
      padding: var(--space-1);
      border: 1px solid var(--border);
      border-radius: var(--radius-full);
      background: var(--surface-nav-glass);
      backdrop-filter: blur(16px);
      box-shadow: var(--shadow-card);
    }
  }

  @media (min-width: 64rem) {
    .settings {
      grid-template-columns: 14rem minmax(0, 1fr);
      gap: var(--space-12);
    }

    /* Follows a long archive down the page, clear of the floating header. */
    .rail {
      position: sticky;
      top: var(--space-24);
    }

    .legend {
      display: block;
      margin-bottom: var(--space-3);
      padding-inline: var(--space-4);
      color: var(--text-subtle);
      font-size: var(--text-sm);
      font-weight: var(--weight-semibold);
    }

    .categories {
      flex-direction: column;
      flex-wrap: nowrap;
      gap: var(--space-1);
    }

    /*
     * Stretched so the pill marks a list row rather than floating at label width. List-row size and full text colour,
     * so a destination never reads like the small, faint caption above it.
     */
    .category {
      justify-content: flex-start;
      padding-inline: var(--space-4);
      color: var(--text);
      font-size: var(--text-base);
      font-weight: var(--weight-regular);
    }

    /* The rail captions the administrator's group the way it captions the whole column, below a hairline. */
    .administration {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
      margin-top: var(--space-4);
      padding-top: var(--space-4);
      border-top: 1px solid var(--border);
    }

    .administration::before {
      content: none;
    }
  }
</style>
