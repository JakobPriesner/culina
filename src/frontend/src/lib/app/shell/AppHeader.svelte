<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { resolve } from '$app/paths';

  import { session } from '$features/auth/session.svelte';
  import { libraryView } from '$features/recipes/stores/libraryView.svelte';
  import { recipes } from '$features/recipes/stores/recipes.svelte';

  import { connection } from '../connection.svelte';
  import { headerAccessory } from '../headerAccessory.svelte';
  import { m } from '$shell/i18n';
  import Navigation from '../Navigation.svelte';
  import AccountMenu from './AccountMenu.svelte';
  import HeaderBrand from './HeaderBrand.svelte';
  import NavigationProgress from './NavigationProgress.svelte';
  import SearchButton from './SearchButton.svelte';

  interface Props {
    /** The shell decides, because the search shortcut follows it. */
    searchable: boolean;
    height?: number;
  }

  let { searchable, height = $bindable() }: Props = $props();

  /** Nothing to search in an empty library; its own invitation is the way on. */
  const emptyLibrary = $derived(
    recipes.status === 'ready' && recipes.items.length === 0 && !libraryView.filtered
  );

  /** Lists stay put on a household change; a page for one thing (route with a param) belongs to the old household, so go to the library. */
  function switched() {
    if (page.route.id?.includes('[')) {
      void goto(resolve('/(app)'));
    }
  }
</script>

<header class="header" bind:clientHeight={height}>
  <NavigationProgress />

  <div class="header-inner">
    <HeaderBrand />

    <div class="wide-only"><Navigation placement="top" /></div>

    <!-- Re-enables pointer events: the account menu opens inside and would inherit the header's "none". -->
    <div class="actions">
      <!-- Without a household too: the account menu is the one way to settings. -->
      {#if session.user}
        <div class="tools">
          <!-- Nothing to search in an empty library; its invitation is the way on. -->
          {#if searchable && !emptyLibrary}<SearchButton />{/if}
          <AccountMenu onswitch={switched} />
        </div>
      {/if}
    </div>
    {#if !connection.online}
      <div class="status"><p class="offline">{m['connection.offline']()}</p></div>
    {/if}
    {#if headerAccessory.current}
      <div class="accessory">{@render headerAccessory.current()}</div>
    {/if}
  </div>
</header>
<div class="fade" aria-hidden="true"></div>

<style>
  .header {
    grid-area: header;
    position: sticky;
    top: 0;
    z-index: var(--z-shell);
    pointer-events: none;
  }

  /*
   * Hides the page under the pills. Solid for the header's whole height: fading from a third of the way down let a
   * heading or a line of text read through beside the pills and above a stuck section heading.
   */
  .header::before {
    content: '';
    position: absolute;
    inset: 0;
    background: var(--surface);
  }

  /*
   * Fades the page out below the header. Its own layer, under the page's sticky level: a heading stuck on the header's
   * line paints over it and stays crisp, while one its section pushes up still goes under the solid header.
   * A gradient, not a full-width backdrop-filter, which would blur the empty gutters every scroll frame.
   */
  .fade {
    position: fixed;
    top: var(--header-inset);
    inset-inline: 0;
    z-index: var(--z-sticky);
    height: var(--space-8);
    background: linear-gradient(to bottom, var(--surface), transparent);
    pointer-events: none;
  }

  /* Brand, destinations and tools share one row; the tools are on every page so the destinations stay centred. */
  .header-inner {
    /* Positioned so the pills paint above the scrim. */
    position: relative;
    display: grid;
    grid-template-columns: minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-4);
    max-width: var(--layout-wide);
    margin-inline: auto;
    min-height: 4.5rem;
    padding-block: var(--space-3);
    padding-inline: var(--layout-gutter-start) var(--layout-gutter-end);
  }

  .actions {
    grid-column: 2;
    display: flex;
    align-items: center;
    justify-content: flex-end;
    gap: var(--space-2);
    min-width: 0;
    pointer-events: auto;
  }

  /* One glass capsule grouping search and the account, the header's two tools. */
  .tools {
    display: flex;
    align-items: center;
    padding: var(--space-1);
    border-radius: var(--radius-full);
    background: var(--surface-nav-glass);
    backdrop-filter: blur(20px) saturate(180%);
    -webkit-backdrop-filter: blur(20px) saturate(180%);
    box-shadow: var(--shadow-glass);
  }

  .status {
    grid-column: 1 / -1;
    display: flex;
    justify-content: flex-end;
    min-width: 0;
  }

  .offline {
    pointer-events: auto;
    margin: 0;
    padding: var(--space-1) var(--space-3);
    border-radius: var(--radius-full);
    background: var(--warning-subtle);
    color: var(--text);
    font-size: var(--text-xs);
    text-align: end;
  }

  /* A page's own bar (see headerAccessory): a row of the header, so the scrim ends below it. */
  .accessory {
    grid-column: 1 / -1;
    min-width: 0;
    pointer-events: auto;
  }

  .wide-only {
    pointer-events: auto;
    grid-column: 2;
    display: none;
  }

  @media (width < 40rem) {
    .header-inner {
      gap: var(--space-2);
    }
  }

  /* Guided cooking is a focused mode with its own way back: no navigation, at any width, so nothing pulls the cook away from the step. */
  :global(.shell.focused-cooking) :is(.header, .fade) {
    display: none;
  }

  /* A recipe on a phone brings its own sticky bar (back, title) and needs the height for the photo; the bottom bar still navigates. */
  @media (width < 52rem) {
    :global(.shell.reading-recipe) :is(.header, .fade) {
      display: none;
    }
  }

  /* The first column never shrinks below the brand: just past 64rem it would get ~9rem and wrap the wordmark's dot. */
  @media (min-width: 64rem) {
    .header-inner {
      grid-template-columns: minmax(min-content, 1fr) auto minmax(0, 1fr);
    }

    .actions {
      grid-column: 3;
    }

    .wide-only {
      display: block;
    }
  }

  /* In landscape or with a keyboard open, give the content its height back. */
  @media screen and (max-height: 32rem) {
    .header {
      position: relative;
      top: auto;
    }

    .header::before,
    .fade {
      display: none;
    }
  }

  /* Same as short viewports when enlarged text crowds the screen: the header scrolls away; the bottom bars stay for navigation. */
  :global(.shell.crowded) .header {
    position: relative;
    top: auto;
  }

  :global(.shell.crowded) .header::before,
  :global(.shell.crowded) .fade {
    display: none;
  }

  @media print {
    .header,
    .fade {
      display: none !important;
    }
  }
</style>
