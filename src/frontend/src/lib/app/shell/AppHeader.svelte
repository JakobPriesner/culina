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
  <div class="edge" aria-hidden="true"><i></i><i></i><i></i><i></i></div>
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

<style>
  .header {
    grid-area: header;
    position: sticky;
    top: 0;
    z-index: var(--z-shell);
    pointer-events: none;
  }

  /*
   * Frosts the page under the pills like iOS's scroll edge effect: tinted and blurred at the top, easing
   * into the sharp page with no edge, so a photo runs on under the header. One blur faded out shows a
   * blurred copy over the sharp text halfway down; stacked blurs, each weaker and reaching further,
   * compound into a smooth ramp instead. The tint eases out too, so a dark card gets no pale band.
   */
  .edge {
    position: absolute;
    inset: 0 0 calc(-1 * var(--space-6));
    background: linear-gradient(
      to bottom,
      var(--surface-bar-glass) 0%,
      color-mix(in srgb, var(--surface-bar-glass) 75%, transparent) 25%,
      color-mix(in srgb, var(--surface-bar-glass) 40%, transparent) 50%,
      color-mix(in srgb, var(--surface-bar-glass) 15%, transparent) 75%,
      transparent 100%
    );
  }

  .edge i {
    position: absolute;
    inset: 0;
  }

  .edge i:nth-child(1) {
    backdrop-filter: blur(1px);
    -webkit-backdrop-filter: blur(1px);
    mask-image: linear-gradient(to bottom, black 60%, transparent 100%);
  }

  .edge i:nth-child(2) {
    backdrop-filter: blur(3px);
    -webkit-backdrop-filter: blur(3px);
    mask-image: linear-gradient(to bottom, black 40%, transparent 80%);
  }

  .edge i:nth-child(3) {
    backdrop-filter: blur(6px);
    -webkit-backdrop-filter: blur(6px);
    mask-image: linear-gradient(to bottom, black 20%, transparent 60%);
  }

  .edge i:nth-child(4) {
    backdrop-filter: blur(12px) saturate(180%);
    -webkit-backdrop-filter: blur(12px) saturate(180%);
    mask-image: linear-gradient(to bottom, black 0%, transparent 40%);
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
  :global(.shell.focused-cooking) .header {
    display: none;
  }

  /* A recipe on a phone brings its own sticky bar (back, title) and needs the height for the photo; the bottom bar still navigates. */
  @media (width < 52rem) {
    :global(.shell.reading-recipe) .header {
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

    .edge {
      display: none;
    }
  }

  /* Same as short viewports when enlarged text crowds the screen: the header scrolls away; the bottom bars stay for navigation. */
  :global(.shell.crowded) .header {
    position: relative;
    top: auto;
  }

  :global(.shell.crowded) .edge {
    display: none;
  }

  @media print {
    .header {
      display: none !important;
    }
  }
</style>
