<script lang="ts">
  import { tick } from 'svelte';
  import NavIcon from '$shell/NavIcon.svelte';
  import { m } from '$shell/i18n';
  import { createPreviewLibrary } from './previewLibrary.svelte';
  import PreviewCollection from './PreviewCollection.svelte';
  import PreviewFeature from './PreviewFeature.svelte';
  import PreviewFilters from './PreviewFilters.svelte';
  import PreviewHeader from './PreviewHeader.svelte';
  import PreviewResume from './PreviewResume.svelte';
  import { sampleRecipes, type PreviewRecipe, type PreviewProgress } from './recipes';
  import RecipePreviewSurface from './RecipePreviewSurface.svelte';

  const recipes = sampleRecipes();
  const library = createPreviewLibrary(recipes);
  const featured = recipes[0]!;

  let selected = $state<PreviewRecipe | undefined>();
  let detail = $state<HTMLDivElement>();
  let libraryElement: HTMLDivElement;
  let opener: HTMLElement | undefined;
  let libraryScroll = 0;
  let progress = $state<Record<string, PreviewProgress>>({});
  const activeRecipes = $derived(recipes.filter((recipe) => progress[recipe.id]?.cooking));

  /** Opens a recipe, or with none comes back to the library where it was left. */
  async function open(recipe?: PreviewRecipe) {
    if (recipe) {
      opener = document.activeElement instanceof HTMLElement ? document.activeElement : undefined;
      libraryScroll = window.scrollY;
      progress[recipe.id] ??= { servings: 2, currentStep: 0, cooking: false, checked: {} };
      selected = recipe;
      await tick();
      detail
        ?.querySelector<HTMLElement>(progress[recipe.id]?.cooking ? '[aria-current="step"]' : 'h1')
        ?.focus();
    } else {
      selected = undefined;
      await tick();
      window.scrollTo({ top: libraryScroll, behavior: 'instant' });
      (opener?.isConnected ? opener : libraryElement.querySelector<HTMLElement>('h1'))?.focus({
        preventScroll: true
      });
    }
  }
</script>

<PreviewHeader />
<main>
  {#if selected}
    <div bind:this={detail}>
      <RecipePreviewSurface
        recipe={selected}
        progress={progress[selected.id]!}
        onprogress={(value) => {
          if (selected) progress[selected.id] = value;
        }}
        onback={() => void open()}
      />
    </div>
  {/if}
  <div class="library" bind:this={libraryElement} hidden={selected !== undefined}>
    <div class="page-title">
      <div>
        <p class="kitchen-label">
          <span aria-hidden="true"><NavIcon icon="recipes" current={false} /></span>{m[
            'preview.kitchen'
          ]()}
        </p>
        <h1 tabindex="-1">{m['preview.title']()}</h1>
        <p class="subtitle">{m['preview.subtitle']()}</p>
      </div>
    </div>
    {#each activeRecipes as recipe (recipe.id)}
      <PreviewResume {recipe} onresume={() => void open(recipe)} />
    {/each}
    <PreviewFilters bind:filter={library.filter} bind:search={library.search} />
    {#if !library.narrowed}
      <PreviewFeature recipe={featured} onopen={() => void open(featured)} />
    {/if}
    <PreviewCollection
      recipes={library.shown}
      isFavourite={library.isFavourite}
      onopen={(recipe) => void open(recipe)}
      onfavourite={(recipe) => library.toggleFavourite(recipe.id)}
      onreset={library.reset}
    />
    <footer class="footer">{m['preview.footer']()}</footer>
  </div>
</main>

<style>
  .library {
    max-width: var(--layout-wide);
    margin-inline: auto;
    padding: var(--layout-page-space) var(--layout-gutter-end) var(--layout-page-space)
      var(--layout-gutter-start);
  }

  .library[hidden] {
    display: none;
  }

  .kitchen-label {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    color: var(--accent);
    font-size: var(--text-xs);
    font-weight: var(--weight-semibold);
    letter-spacing: 0.12em;
    text-transform: uppercase;
  }

  .kitchen-label span {
    width: var(--space-4);
    height: var(--space-4);
  }

  h1 {
    font-family: var(--font-editorial);
    font-size: var(--text-display);
    font-weight: var(--weight-regular);
    letter-spacing: -0.04em;
    margin-top: var(--space-3);
  }

  .subtitle {
    color: var(--text-muted);
    margin-top: var(--space-3);
  }

  .footer {
    margin-top: var(--space-8);
    padding-block: var(--space-4);
    border-top: 1px solid var(--border);
    font-size: var(--text-xs);
    color: var(--text-muted);
  }

  @media (width < 64rem) {
    .library {
      padding: var(--space-6) var(--layout-gutter-end) var(--space-6) var(--layout-gutter-start);
    }
  }
</style>
