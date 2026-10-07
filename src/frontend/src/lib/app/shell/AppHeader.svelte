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

  /** The floating row at the top: brand, destinations on a wide screen, and the tools. */
  interface Props {
    /** Whether search is on offer here; the shell decides, because the shortcut follows it. */
    searchable: boolean;
    /** How tall the header is once measured; left unset until then. */
    height?: number;
  }

  let { searchable, height = $bindable() }: Props = $props();

  /** See `offersNewRecipe`: only where a new recipe would belong to what is on screen. */
  const creating = $derived(offersNewRecipe(page.url.pathname));

  /**
   * Where a change of household leaves the page.
   *
   * A list — the library, the plan, the shopping — stays put and shows the
   * other household's. A page about one thing, whose route names it, goes back
   * to the library instead: that recipe or that cookbook belongs to the
   * household just left, and staying on it would be showing one kitchen's
   * thing under another kitchen's name.
   */
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

    <!-- Takes pointer events back from the header for the household menu's
         sake: its panel opens inside this, and would otherwise inherit the
         header's "none" and pass every click through to the page beneath. -->
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

  /* The page fading out under the pills instead of being cut in half at the
     top edge. A gradient and not a blur: what passes under here is a centred
     column on a flat background, so a full-width backdrop-filter would spend
     every scroll frame blurring the gutters on either side of it. */
  .header::before {
    content: '';
    position: absolute;
    inset-block-start: 0;
    inset-inline: 0;
    height: calc(100% + var(--space-8));
    background: linear-gradient(to bottom, var(--surface) 35%, transparent);
  }

  /* Three floating groups share one row: the brand, the destinations, and
     the header's tools — the household and search in one capsule, and on the
     library the way to write a new recipe beside it. The tools are there on
     every page, so the destinations stay centred rather than sliding across as
     the page changes. */
  .header-inner {
    /* Positioned, so the pills paint above the scrim rather than under it. */
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

  /*
   * One pane of glass with two controls in it, after the grouped toolbar
   * buttons of Apple's Liquid Glass.
   *
   * Search and the household are both about what is on screen — find
   * something in this kitchen, or look at another one — and two lone circles
   * at opposite ends of the header said they had nothing to do with each
   * other. The capsule is the glass; the buttons inside are only a lit circle
   * under the pointer, so the pair reads as one thing with two parts.
   */
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

  /* Connection feedback gets its own small badge without moving the controls. */
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

  /* The narrow header keeps the brand and recipe creation. Expanded navigation
     takes the middle slot between them on desktop. */
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

  /* Guided cooking has its own way back to the recipe. On compact screens
     the instructions need the space used by the app's header and navigation. */
  @media (width < 64rem) {
    :global(.shell.focused-cooking) .header {
      display: none;
    }
  }

  /* Expanded navigation needs room for the brand, labels and recipe creation.
     The first column is never narrower than the brand: just past 64rem the
     labelled destinations leave each side about 9rem, less than the brand
     pill, which squeezed the wordmark's dot onto a line of its own. Wider than
     the brand, both sides are equal and the destinations sit centred; at the
     narrowest they give way by the few pixels the brand needs. */
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

    /* Not sticky here, so nothing ever passes under it. */
    .header::before {
      display: none;
    }
  }

  /* And the same answer when it is text rather than the window that has taken
     the room: the header scrolls away with the page instead of floating over
     what is left of it. The bars at the bottom stay — they are how somebody
     gets anywhere — and giving back the header's share is enough to read and
     type in what remains. */
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
