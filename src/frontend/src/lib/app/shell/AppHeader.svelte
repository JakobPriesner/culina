<script lang="ts">
  import { goto } from '$app/navigation';
  import { page } from '$app/state';
  import { resolve } from '$app/paths';

  import HouseholdSwitcher from '$features/auth/HouseholdSwitcher.svelte';
  import { session } from '$features/auth/session.svelte';

  import { connection } from '../connection.svelte';
  import { m } from '$shell/i18n';
  import { offersNewRecipe } from '../navigation';
  import Navigation from '../Navigation.svelte';
  import NewRecipeLink from '../NewRecipeLink.svelte';
  import HeaderBrand from './HeaderBrand.svelte';
  import NavigationProgress from './NavigationProgress.svelte';
  import SearchButton from './SearchButton.svelte';

  interface Props {
    /** The shell decides, because the search shortcut follows it. */
    searchable: boolean;
    height?: number;
  }

  let { searchable, height = $bindable() }: Props = $props();

  const creating = $derived(offersNewRecipe(page.url.pathname));

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

    <!-- Re-enables pointer events: the household menu opens inside and would inherit the header's "none". -->
    <div class="actions">
      {#if session.activeHousehold}
        <div class="tools">
          {#if searchable}<SearchButton />{/if}
          <HouseholdSwitcher onswitch={switched} />
        </div>
      {/if}
      {#if creating}
        <NewRecipeLink />
      {/if}
    </div>
    {#if !connection.online}
      <div class="status"><p class="offline">{m['connection.offline']()}</p></div>
    {/if}
  </div>
</header>

<style>
  .header {
    grid-area: header;
    position: sticky;
    top: 0;
    z-index: var(--z-sticky);
    pointer-events: none;
  }

  /* Fades the page out under the pills. A gradient, not a full-width backdrop-filter, which would blur the empty gutters every scroll frame. */
  .header::before {
    content: '';
    position: absolute;
    inset-block-start: 0;
    inset-inline: 0;
    height: calc(100% + var(--space-8));
    background: linear-gradient(to bottom, var(--surface) 35%, transparent);
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

  /* One glass capsule grouping search and household, which relate to each other. */
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

  /* Guided cooking has its own way back; compact screens need the space for the instructions. */
  @media (width < 64rem) {
    :global(.shell.focused-cooking) .header {
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

    .header::before {
      display: none;
    }
  }

  /* Same as short viewports when enlarged text crowds the screen: the header scrolls away; the bottom bars stay for navigation. */
  :global(.shell.crowded) .header {
    position: relative;
    top: auto;
  }

  :global(.shell.crowded) .header::before {
    display: none;
  }

  @media print {
    .header {
      display: none !important;
    }
  }
</style>
